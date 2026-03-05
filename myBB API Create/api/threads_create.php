<?php
/**
 * MyBB Thread Creation API - Vereinfachte stabile Version
 */

// ======================
// BASISKONFIGURATION
// ======================
define('MYBB_ROOT', dirname(__DIR__) . '/');
define('DEBUG_LOG', MYBB_ROOT.'api/logs/threads_create_debug.log');
define('ERROR_LOG', MYBB_ROOT.'api/logs/threads_create_errors.log');

// Error Handling
ini_set('display_errors', 1);
ini_set('log_errors', 1);
ini_set('error_log', ERROR_LOG);
error_reporting(E_ALL);

file_put_contents(DEBUG_LOG, "[".date('Y-m-d H:i:s')."] === START ===\n", FILE_APPEND);

// ======================
// HEADERS
// ======================
header("Access-Control-Allow-Origin: *");
header("Access-Control-Allow-Methods: POST, OPTIONS");
header("Access-Control-Allow-Headers: Content-Type");
header('Content-Type: application/json; charset=utf-8');

// ======================
// KONFIGURATION LADEN
// ======================
if (!file_exists(MYBB_ROOT.'inc/config.php')) {
    http_response_code(500);
    die(json_encode(['success' => false, 'error' => 'Config file not found']));
}

require_once MYBB_ROOT.'inc/config.php';

// ======================
// DATENBANKVERBINDUNG
// ======================
$db = new mysqli(
    $config['database']['hostname'],
    $config['database']['username'],
    $config['database']['password'],
    $config['database']['database'],
    $config['database']['port'] ?? 3306
);

if ($db->connect_error) {
    http_response_code(500);
    die(json_encode([
        'success' => false,
        'error' => 'Database connection failed',
        'details' => $db->connect_error
    ]));
}

$db->set_charset('utf8mb4');
define('TABLE_PREFIX', $config['database']['table_prefix']);



// ======================
// ERWEITERTER MYCODE PARSER MIT HIDE-TAG UND HTML
// ======================
// ======================
// VOLLSTÄNDIGER MYCODE PARSER MIT ALLEN TAGS
// ======================
function parse_mycode($message, $allow_mycode = true, $allow_html = false, $user_uid = 0) {
    // HTML erlauben oder escapen
    if (!$allow_html) {
        $message = htmlspecialchars($message);
    }
    
    if (!$allow_mycode) {
        return $message;
    }
    
    // Grundlegende MyCode-Tags
    $replacements = [
        '/\[b\](.*?)\[\/b\]/is' => '<strong>$1</strong>',
        '/\[i\](.*?)\[\/i\]/is' => '<em>$1</em>',
        '/\[u\](.*?)\[\/u\]/is' => '<u>$1</u>',
        '/\[center\](.*?)\[\/center\]/is' => '<div style="text-align:center;">$1</div>',
        '/\[align=center\](.*?)\[\/align\]/is' => '<div style="text-align:center;">$1</div>',
        '/\[url=(.*?)\](.*?)\[\/url\]/is' => '<a href="$1">$2</a>',
        '/\[url\](.*?)\[\/url\]/is' => '<a href="$1">$1</a>',
        '/\[size=(\d+)\](.*?)\[\/size\]/is' => '<span style="font-size:$1px">$2</span>',
        '/\[color=([^"]+)\](.*?)\[\/color\]/is' => '<span style="color:$1">$2</span>',
        '/\[img\](.*?)\[\/img\]/is' => '<img src="$1" style="max-width:100%;height:auto;">'
    ];
    
    // MyCode ersetzen
    $message = preg_replace(array_keys($replacements), array_values($replacements), $message);
    
    return $message;
}

// ======================
// JSON INPUT HANDLING
// ======================
$json_input = file_get_contents('php://input');
file_put_contents(DEBUG_LOG, "RAW Input:\n".$json_input."\n", FILE_APPEND);

$data = json_decode($json_input, true);
if (json_last_error() !== JSON_ERROR_NONE) {
    http_response_code(400);
    die(json_encode([
        'success' => false,
        'error' => 'Invalid JSON',
        'details' => json_last_error_msg()
    ]));
}

// ======================
// VALIDIERUNG
// ======================
$required = [
    'name' => 'string',
    'api_key' => 'string',
    'fid' => 'integer',
    'subject' => 'string',
    'message' => 'string'
];

$errors = [];
foreach ($required as $field => $type) {
    if (!isset($data[$field])) {
        $errors[] = "Missing field: $field";
        continue;
    }
    
    switch ($type) {
        case 'string':
            if (!is_string($data[$field]) || trim($data[$field]) === '') {
                $errors[] = "$field must be a non-empty string";
            }
            break;
        case 'integer':
            if (!is_numeric($data[$field]) || (int)$data[$field] <= 0) {
                $errors[] = "$field must be a positive integer";
            }
            break;
    }
}

if (!empty($errors)) {
    http_response_code(400);
    die(json_encode([
        'success' => false,
        'error' => 'Validation failed',
        'details' => $errors
    ]));
}

// ======================
// BENUTZERPRÜFUNG
// ======================
$name = $db->real_escape_string($data['name']);
$api_key = $db->real_escape_string($data['api_key']);

$user_query = $db->query("
    SELECT uid, username 
    FROM ".TABLE_PREFIX."users 
    WHERE username = '$name' 
    AND api_key = '$api_key'
    LIMIT 1
");

if (!$user_query || $user_query->num_rows === 0) {
    http_response_code(403);
    die(json_encode(['success' => false, 'error' => 'Invalid credentials']));
}

$user = $user_query->fetch_assoc();

// ======================
// AVATAR-USER PRÜFEN
// ======================
$avatar_uid = isset($data['avatar_uid']) ? (int)$data['avatar_uid'] : $user['uid'];
if ($avatar_uid != $user['uid']) {
    $avatar_user_query = $db->query("
        SELECT uid, username 
        FROM ".TABLE_PREFIX."users 
        WHERE uid = $avatar_uid
        LIMIT 1
    ");
    
    if (!$avatar_user_query || $avatar_user_query->num_rows === 0) {
        http_response_code(404);
        die(json_encode(['success' => false, 'error' => 'Avatar user not found']));
    }
    
    $avatar_user = $avatar_user_query->fetch_assoc();
} else {
    $avatar_user = $user;
}

// ======================
// FORUM PRÜFEN
// ======================
$fid = (int)$data['fid'];
$forum_query = $db->query("
    SELECT fid 
    FROM ".TABLE_PREFIX."forums 
    WHERE fid = $fid
    LIMIT 1
");

if (!$forum_query || $forum_query->num_rows === 0) {
    http_response_code(404);
    die(json_encode(['success' => false, 'error' => 'Forum not found']));
}

// ======================
// THREAD ERSTELLEN
// ======================
$subject = $db->real_escape_string($data['subject']);
$allow_mycode = isset($data['allow_mycode']) ? (bool)$data['allow_mycode'] : true;
$time = time();
$ip = ''; // Setzt die IP als leeren String

// ======================
// MESSAGE PARSING MIT HTML OPTION
// ======================
// [Anpassung in der Message-Verarbeitung]
$allow_mycode = isset($data['allow_mycode']) ? (bool)$data['allow_mycode'] : true;
$allow_html = isset($data['allow_html']) ? (bool)$data['allow_html'] : true;

$parsed_message = parse_mycode($data['message'], $allow_mycode, $allow_html, $user['uid']);
$parsed_message = $db->real_escape_string($parsed_message);
// Transaktion starten
$db->autocommit(false);

try {
    // 1. Thread erstellen
    $thread_query = $db->query("
        INSERT INTO ".TABLE_PREFIX."threads 
        (fid, subject, uid, username, dateline, firstpost, lastpost, lastposter, lastposteruid, visible, notes) 
        VALUES (
            $fid, 
            '$subject', 
            {$user['uid']}, 
            '{$user['username']}', 
            $time, 
            0, 
            $time, 
            '{$avatar_user['username']}',
            {$avatar_user['uid']},
            1,
            ''
        )
    ");
    
    if (!$thread_query) {
        throw new Exception("Thread creation failed: ".$db->error);
    }
    
    $tid = $db->insert_id;

    // 2. Post erstellen
$post_query = $db->query("
    INSERT INTO ".TABLE_PREFIX."posts 
    (tid, fid, subject, uid, username, message, ipaddress, dateline, visible) 
    VALUES (
        $tid, 
        $fid, 
        '$subject', 
        {$user['uid']}, 
        '{$user['username']}', 
        '$parsed_message', 
        '$ip',
        $time, 
        1
    )
");
    
    if (!$post_query) {
        throw new Exception("Post creation failed: ".$db->error);
    }
    
    $pid = $db->insert_id;

    // 3. Thread aktualisieren
    $update_thread = $db->query("
        UPDATE ".TABLE_PREFIX."threads 
        SET firstpost = $pid 
        WHERE tid = $tid
    ");
    
    if (!$update_thread) {
        throw new Exception("Thread update failed: ".$db->error);
    }

// 4. Forum aktualisieren (erweitert)
$update_forum = $db->query("
    UPDATE ".TABLE_PREFIX."forums 
    SET 
        threads = threads + 1, 
        posts = posts + 1, 
        lastpost = $time, 
        lastposter = '{$avatar_user['username']}', 
        lastposteruid = {$avatar_user['uid']},
        lastposttid = $tid,
        lastpostsubject = '$subject'
    WHERE fid = $fid
");
    
    if (!$update_forum) {
        throw new Exception("Forum update failed: ".$db->error);
    }
// 5. Thread komplett aktualisieren
$update_thread_complete = $db->query("
    UPDATE ".TABLE_PREFIX."threads 
    SET 
        lastpost = $time,
        lastposter = '{$avatar_user['username']}',
        lastposteruid = {$avatar_user['uid']},
        subject = '$subject'
    WHERE tid = $tid
");
// 6. MyBB Cache aktualisieren
if (file_exists(MYBB_ROOT."inc/functions_cache.php")) {
    require_once MYBB_ROOT."inc/functions_cache.php";
    update_forum_counters($fid);
    rebuild_forum_cache($fid);
}
    // Transaktion bestätigen
    $db->commit();
    
    // Erfolgsmeldung
    http_response_code(201);
    echo json_encode([
        'success' => true,
        'thread_id' => $tid,
        'post_id' => $pid,
        'forum_id' => $fid,
        'mycode_status' => $allow_mycode,
        'avatar_used' => [
            'uid' => $avatar_user['uid'],
            'username' => $avatar_user['username']
        ]
    ]);

} catch (Exception $e) {
    $db->rollback();
    file_put_contents(DEBUG_LOG, "TRANSACTION ERROR: ".$e->getMessage()."\n", FILE_APPEND);
    file_put_contents(DEBUG_LOG, "MySQL Error: ".$db->error."\n", FILE_APPEND);
    
    http_response_code(500);
    echo json_encode([
        'success' => false,
        'error' => 'Thread creation failed',
        'details' => $e->getMessage(),
        'db_error' => $db->error
    ]);
}

$db->close();
file_put_contents(DEBUG_LOG, "[".date('Y-m-d H:i:s')."] === END ===\n\n", FILE_APPEND);
?>