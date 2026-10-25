<?php
/**
 * FG Forum API - Routen
 *
 * Alle Antworten sind JSON:
 *   Erfolg: {"ok": true,  "data": {...}}
 *   Fehler: {"ok": false, "error": {"code": "...", "message": "...", "details": [...]}}
 *
 * Anmeldung:
 *   Header "X-FG-Token: fgs_..."  (Token aus route=auth/login)
 *   Header "X-FG-Key: fgk_..."    (API-Key aus dem Benutzer-CP)
 *   oder   "Authorization: Bearer <token oder key>"
 *
 * Autor: FGSeRaser - https://github.com/FGSeRaser
 */

if(!defined('IN_MYBB') || !defined('FGAPI_REQUEST'))
{
	die('Direct access to this file is not allowed.');
}

class FGApi
{
	const API_VERSION = 2;

	/** @var array Input from JSON body, POST and GET */
	private $input = array();

	/** @var array|null Token row of the current request */
	private $token = null;

	/** @var postParser */
	private $parser = null;

	private $routes = array(
		// route                  => array(method, handler, needs login)
		'info'                    => array('GET',  'route_info',            false),
		'auth/login'              => array('POST', 'route_login',           false),
		'auth/logout'             => array('POST', 'route_logout',          true),
		'me'                      => array('GET',  'route_me',              true),
		'forums'                  => array('GET',  'route_forums',          true),
		'threads'                 => array('GET',  'route_threads',         true),
		'thread'                  => array('GET',  'route_thread',          true),
		'latest'                  => array('GET',  'route_latest',          true),
		'search'                  => array('GET',  'route_search',          true),
		'online'                  => array('GET',  'route_online',          true),
		'user'                    => array('GET',  'route_user',            true),
		'preview'                 => array('POST', 'route_preview',         true),
		'thread/create'           => array('POST', 'route_thread_create',   true),
		'thread/moderate'         => array('POST', 'route_thread_moderate', true),
		'post/reply'              => array('POST', 'route_post_reply',      true),
		'post/get'                => array('GET',  'route_post_get',        true),
		'post/edit'               => array('POST', 'route_post_edit',       true),
		'post/delete'             => array('POST', 'route_post_delete',     true),
		'attachment'              => array('GET',  'route_attachment',      true),
		'attachment/upload'       => array('POST', 'route_attachment_upload', true),
		'attachment/delete'       => array('POST', 'route_attachment_delete', true),
		'pms'                     => array('GET',  'route_pms',             true),
		'pm'                      => array('GET',  'route_pm',              true),
		'pm/send'                 => array('POST', 'route_pm_send',         true),
		'pm/delete'               => array('POST', 'route_pm_delete',       true),
		'keys'                    => array('GET',  'route_keys',            true),
		'keys/create'             => array('POST', 'route_keys_create',     true),
		'keys/revoke'             => array('POST', 'route_keys_revoke',     true),
	);

	// =================================================================
	//  Request handling
	// =================================================================

	public function run()
	{
		global $mybb, $cache;

		header('Cache-Control: no-store');
		header('X-Content-Type-Options: nosniff');

		if($_SERVER['REQUEST_METHOD'] == 'OPTIONS')
		{
			$this->send(array('allow' => 'GET, POST'));
		}

		$plugin_list = $cache->read('plugins');
		if(empty($plugin_list['active']['fgapi']) || !function_exists('fgapi_create_token'))
		{
			$this->fail('plugin_inactive', 'Das Plugin "FG Forum API" ist nicht aktiviert.', 503);
		}
		if(empty($mybb->settings['fgapi_enabled']))
		{
			$this->fail('api_disabled', 'Die API ist im Moment abgeschaltet.', 503);
		}
		if(!empty($mybb->settings['fgapi_require_https']) && !$this->is_https())
		{
			$this->fail('https_required', 'Dieses Forum erlaubt die API nur über HTTPS.', 403);
		}

		$this->read_input();
		$this->setup_environment();

		$route = trim((string)$this->in('route'), '/');
		if($route === '' && !empty($_SERVER['PATH_INFO']))
		{
			$route = trim($_SERVER['PATH_INFO'], '/');
		}
		if($route === '')
		{
			$route = 'info';
		}
		if(!isset($this->routes[$route]))
		{
			$this->fail('unknown_route', 'Unbekannte Route: '.$route, 404);
		}

		list($method, $handler, $needs_login) = $this->routes[$route];
		if($method == 'POST' && $_SERVER['REQUEST_METHOD'] != 'POST')
		{
			$this->fail('method_not_allowed', 'Diese Route braucht eine POST-Anfrage.', 405);
		}

		if($needs_login)
		{
			$this->authenticate();
			$this->check_board_open();
		}

		$this->$handler();
	}

	private function read_input()
	{
		$json = array();
		$type = isset($_SERVER['CONTENT_TYPE']) ? strtolower($_SERVER['CONTENT_TYPE']) : '';
		if(strpos($type, 'application/json') !== false)
		{
			$raw = file_get_contents('php://input');
			if($raw !== '' && $raw !== false)
			{
				$json = json_decode($raw, true);
				if(!is_array($json))
				{
					$this->fail('invalid_json', 'Die Anfrage enthält kein gültiges JSON.', 400);
				}
			}
		}
		// JSON body wins over form fields, form fields over the query string
		$this->input = array_merge($_GET, $_POST, $json);
	}

	/** Reads an input value. */
	private function in($key, $type = 'string', $default = null)
	{
		if(!isset($this->input[$key]))
		{
			return $default === null ? ($type == 'int' ? 0 : ($type == 'bool' ? false : '')) : $default;
		}
		$value = $this->input[$key];
		switch($type)
		{
			case 'int':
				return (int)$value;
			case 'bool':
				return is_bool($value) ? $value : in_array(strtolower((string)$value), array('1', 'true', 'yes', 'on'), true);
			case 'array':
				return is_array($value) ? $value : array();
			default:
				return is_scalar($value) ? (string)$value : '';
		}
	}

	private function is_https()
	{
		if(!empty($_SERVER['HTTPS']) && strtolower($_SERVER['HTTPS']) != 'off')
		{
			return true;
		}
		if(isset($_SERVER['HTTP_X_FORWARDED_PROTO']) && strtolower($_SERVER['HTTP_X_FORWARDED_PROTO']) == 'https')
		{
			return true;
		}
		return isset($_SERVER['SERVER_PORT']) && $_SERVER['SERVER_PORT'] == 443;
	}

	/** Theme, language and session object, like xmlhttp.php does it. */
	private function setup_environment()
	{
		global $mybb, $cache, $lang, $theme, $session, $db;

		require_once MYBB_ROOT.'inc/class_session.php';
		$session = new session;
		$session->ipaddress = get_ip();
		$session->packedip = my_inet_pton($session->ipaddress);
		$session->useragent = isset($_SERVER['HTTP_USER_AGENT']) ? my_substr($_SERVER['HTTP_USER_AGENT'], 0, 200) : 'FG Forum Client';

		$theme = $cache->read('default_theme');
		if(!is_array($theme) || empty($theme['tid']))
		{
			$query = $db->simple_select('themes', 'name, tid, properties', '', array('order_by' => 'tid', 'limit' => 1));
			$theme = $db->fetch_array($query);
		}
		$properties = my_unserialize($theme['properties']);
		if(is_array($properties))
		{
			$theme = array_merge($theme, $properties);
		}
		if(empty($theme['imgdir']))
		{
			$theme['imgdir'] = 'images';
		}
		$theme['imglangdir'] = $theme['imgdir'];

		$this->set_language($mybb->settings['bblanguage']);
	}

	private function set_language($language)
	{
		global $lang;
		if(!$lang->language_exists($language))
		{
			$language = 'english';
		}
		$lang->set_language($language);
		$lang->load('global');
		$lang->load('messages');
		if(function_exists('fgapi_load_lang'))
		{
			fgapi_load_lang();
		}
	}

	// =================================================================
	//  Output
	// =================================================================

	private function clean_buffers()
	{
		while(ob_get_level() > 0)
		{
			ob_end_clean();
		}
	}

	private function send($data, $status = 200)
	{
		$this->clean_buffers();
		http_response_code($status);
		header('Content-Type: application/json; charset=utf-8');
		echo json_encode(array('ok' => true, 'data' => $data), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
		$this->finish();
	}

	private function fail($code, $message, $status = 400, $details = null)
	{
		$this->clean_buffers();
		http_response_code($status);
		header('Content-Type: application/json; charset=utf-8');
		$error = array('code' => $code, 'message' => $message);
		if($details !== null)
		{
			$error['details'] = array_values((array)$details);
		}
		echo json_encode(array('ok' => false, 'error' => $error), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
		$this->finish();
	}

	private function finish()
	{
		// Shutdown queries (e.g. lastactive) still have to run
		if(function_exists('run_shutdown'))
		{
			run_shutdown();
		}
		exit;
	}

	// =================================================================
	//  Authentication
	// =================================================================

	private function header_value($name)
	{
		$key = 'HTTP_'.strtoupper(str_replace('-', '_', $name));
		if(!empty($_SERVER[$key]))
		{
			return trim($_SERVER[$key]);
		}
		if($name == 'Authorization' && !empty($_SERVER['REDIRECT_HTTP_AUTHORIZATION']))
		{
			return trim($_SERVER['REDIRECT_HTTP_AUTHORIZATION']);
		}
		return '';
	}

	private function authenticate()
	{
		global $db, $mybb;

		$secret = $this->header_value('X-FG-Token');
		if($secret === '')
		{
			$secret = $this->header_value('X-FG-Key');
		}
		if($secret === '')
		{
			$auth = $this->header_value('Authorization');
			if(stripos($auth, 'Bearer ') === 0)
			{
				$secret = trim(substr($auth, 7));
			}
		}
		if($secret === '')
		{
			$this->fail('auth_required', 'Bitte melde dich an (Token oder API-Key fehlt).', 401);
		}
		if(!preg_match('#^fg[ks]_[A-Za-z0-9]{20,80}$#', $secret))
		{
			$this->fail('auth_invalid', 'Der Token oder API-Key ist ungültig.', 401);
		}

		$hash = hash('sha256', $secret);
		$query = $db->simple_select('fgapi_tokens', '*', "tokenhash='".$db->escape_string($hash)."'", array('limit' => 1));
		$token = $db->fetch_array($query);
		if(!$token)
		{
			$this->fail('auth_invalid', 'Der Token oder API-Key ist ungültig oder wurde widerrufen.', 401);
		}
		if($token['kind'] == 'session' && (int)$token['expires'] > 0 && (int)$token['expires'] < TIME_NOW)
		{
			$db->delete_query('fgapi_tokens', "id='".(int)$token['id']."'");
			$this->fail('auth_expired', 'Deine Anmeldung ist abgelaufen. Bitte melde dich neu an.', 401);
		}

		$user = get_user((int)$token['uid']);
		if(!$user)
		{
			$db->delete_query('fgapi_tokens', "id='".(int)$token['id']."'");
			$this->fail('auth_invalid', 'Das Konto zu diesem Zugang existiert nicht mehr.', 401);
		}

		$this->load_user($user);
		$this->token = $token;

		// Remember usage (and keep sessions alive)
		$update = array('lastused' => TIME_NOW, 'ip' => $db->escape_string(get_ip()));
		if($token['kind'] == 'session')
		{
			$days = (int)$mybb->settings['fgapi_token_days'];
			$update['expires'] = $days > 0 ? TIME_NOW + $days * 86400 : 0;
		}
		if(TIME_NOW - (int)$token['lastused'] > 60)
		{
			$db->update_query('fgapi_tokens', $update, "id='".(int)$token['id']."'");
		}
	}

	/** Loads the user like a normal page visit: permissions, bans, language, online list. */
	private function load_user($user)
	{
		global $mybb, $db, $session, $cache;

		$query = $db->simple_select('sessions', 'sid', "uid='".(int)$user['uid']."'", array('limit' => 1));
		$sid = $db->fetch_field($query, 'sid');
		$session->sid = $sid ? $sid : 0;

		if(!$session->load_user((int)$user['uid'], $user['loginkey']))
		{
			$this->fail('auth_invalid', 'Das Konto konnte nicht geladen werden.', 401);
		}

		$usergroups = $cache->read('usergroups');
		$group = isset($usergroups[$mybb->user['usergroup']]) ? $usergroups[$mybb->user['usergroup']] : array();
		if(!empty($group['isbannedgroup']))
		{
			$reason = !empty($mybb->user['banreason']) ? $mybb->user['banreason'] : '';
			$this->fail('banned', 'Dein Konto ist gesperrt.'.($reason !== '' ? ' Grund: '.$reason : ''), 403);
		}
		if((int)$mybb->user['usergroup'] == 5)
		{
			$this->fail('not_activated', 'Dein Konto ist noch nicht aktiviert.', 403);
		}
		if(!fgapi_in_groups($mybb->user, $mybb->settings['fgapi_api_groups']))
		{
			$this->fail('no_api_access', 'Deine Benutzergruppe darf die API nicht benutzen.', 403);
		}

		$this->set_language($mybb->settings['bblanguage']);
	}

	private function check_board_open()
	{
		global $mybb;
		if(!empty($mybb->settings['boardclosed']) && empty($mybb->usergroup['canviewboardclosed']))
		{
			$reason = strip_tags((string)$mybb->settings['boardclosed_reason']);
			$this->fail('board_closed', 'Das Forum ist gerade geschlossen.'.($reason !== '' ? ' '.$reason : ''), 503);
		}
		if(empty($mybb->usergroup['canview']))
		{
			$this->fail('no_permission', 'Du darfst dieses Forum nicht ansehen.', 403);
		}
	}

	private function too_many_attempts()
	{
		global $db, $mybb;
		$limit = (int)$mybb->settings['fgapi_login_limit'];
		if($limit <= 0)
		{
			return false;
		}
		$db->delete_query('fgapi_attempts', 'dateline < '.(TIME_NOW - 900));
		$ip = $db->escape_string(get_ip());
		$query = $db->simple_select('fgapi_attempts', 'COUNT(*) AS c', "ip='{$ip}'");
		return (int)$db->fetch_field($query, 'c') >= $limit;
	}

	private function add_attempt()
	{
		global $db;
		$db->insert_query('fgapi_attempts', array('ip' => $db->escape_string(get_ip()), 'dateline' => TIME_NOW));
	}

	// =================================================================
	//  Helpers: permissions and formatting
	// =================================================================

	private function require_input($fields)
	{
		$missing = array();
		foreach($fields as $field)
		{
			if(trim((string)$this->in($field)) === '')
			{
				$missing[] = $field;
			}
		}
		if($missing)
		{
			$this->fail('missing_fields', 'Es fehlen Angaben: '.implode(', ', $missing), 400, $missing);
		}
	}

	/** Forum + permissions or an error (404 / 403). */
	private function forum_access($fid, $need_threads = true)
	{
		global $mybb;

		$forum = get_forum((int)$fid);
		if(!$forum || $this->is_inactive($forum['fid']))
		{
			$this->fail('forum_not_found', 'Dieses Forum gibt es nicht.', 404);
		}
		$permissions = forum_permissions($forum['fid']);
		if(empty($permissions['canview']) || ($need_threads && empty($permissions['canviewthreads'])))
		{
			$this->fail('no_permission', 'Du darfst dieses Forum nicht ansehen.', 403);
		}
		if($this->is_unviewable($forum['fid']))
		{
			$this->fail('forum_password', 'Dieses Forum ist mit einem Passwort geschützt und im Client nicht verfügbar.', 403);
		}
		return array($forum, $permissions);
	}

	private $unviewable = null;
	private $inactive = null;

	private function unviewable_list($only_readable = true)
	{
		if($this->unviewable === null)
		{
			$list = get_unviewable_forums($only_readable);
			$this->unviewable = $list === '' ? array() : array_map('intval', explode(',', $list));
		}
		return $this->unviewable;
	}

	private function inactive_list()
	{
		if($this->inactive === null)
		{
			$list = get_inactive_forums();
			$this->inactive = $list === '' ? array() : array_map('intval', explode(',', $list));
		}
		return $this->inactive;
	}

	private function is_unviewable($fid)
	{
		return in_array((int)$fid, $this->unviewable_list());
	}

	private function is_inactive($fid)
	{
		return in_array((int)$fid, $this->inactive_list());
	}

	/** SQL condition that hides forums the user may not read ("p." or "t." column prefix). */
	private function forum_filter_sql($column)
	{
		global $mybb;
		$hidden = array_unique(array_merge($this->unviewable_list(), $this->inactive_list()));
		$sql = '';
		if($hidden)
		{
			$sql .= " AND {$column} NOT IN (".implode(',', $hidden).")";
		}
		// Forums where the user may only see his own threads
		$own_only = array();
		$permissions = forum_permissions();
		if(is_array($permissions))
		{
			foreach($permissions as $fid => $perm)
			{
				if(!empty($perm['canonlyviewownthreads']))
				{
					$own_only[] = (int)$fid;
				}
			}
		}
		if($own_only)
		{
			$sql .= " AND ({$column} NOT IN (".implode(',', $own_only).") OR t.uid='".(int)$mybb->user['uid']."')";
		}
		return $sql;
	}

	/** Which post states the user can see in a forum. */
	private function visible_states($fid)
	{
		$states = array(1);
		if(is_moderator($fid, 'canviewunapprove'))
		{
			$states[] = 0;
		}
		if(is_moderator($fid, 'canviewdeleted'))
		{
			$states[] = -1;
		}
		return $states;
	}

	private function thread_access($tid)
	{
		global $mybb;

		$thread = get_thread((int)$tid);
		if(!$thread || substr((string)$thread['closed'], 0, 6) == 'moved|')
		{
			$this->fail('thread_not_found', 'Dieses Thema gibt es nicht.', 404);
		}
		list($forum, $permissions) = $this->forum_access($thread['fid']);

		$visible = in_array((int)$thread['visible'], $this->visible_states($thread['fid']));
		$own_unapproved = (int)$thread['visible'] == 0 && !empty($mybb->settings['showownunapproved']) && $thread['uid'] == $mybb->user['uid'];
		if(!$visible && !$own_unapproved)
		{
			$this->fail('thread_not_found', 'Dieses Thema gibt es nicht.', 404);
		}
		if(!empty($permissions['canonlyviewownthreads']) && $thread['uid'] != $mybb->user['uid'])
		{
			$this->fail('no_permission', 'Du darfst in diesem Forum nur deine eigenen Themen sehen.', 403);
		}
		return array($thread, $forum, $permissions);
	}

	private function abs_url($url)
	{
		global $mybb;
		$url = (string)$url;
		if($url === '' || preg_match('#^(https?:)?//#i', $url) || strpos($url, 'data:') === 0)
		{
			return $url;
		}
		$url = preg_replace('#^\./#', '', $url);
		return rtrim($mybb->settings['bburl'], '/').'/'.ltrim($url, '/');
	}

	private function user_brief($user)
	{
		$uid = isset($user['uid']) ? (int)$user['uid'] : 0;
		$avatar = '';
		if($uid > 0 && function_exists('format_avatar'))
		{
			$formatted = format_avatar(isset($user['avatar']) ? $user['avatar'] : '', isset($user['avatardimensions']) ? $user['avatardimensions'] : '');
			$avatar = $this->abs_url($formatted['image']);
		}
		$usergroup = isset($user['usergroup']) ? (int)$user['usergroup'] : 1;
		$displaygroup = isset($user['displaygroup']) ? (int)$user['displaygroup'] : 0;
		$username = isset($user['username']) ? $user['username'] : '';
		return array(
			'uid'           => $uid,
			'username'      => $username,
			'username_html' => $uid > 0 ? format_name(htmlspecialchars_uni($username), $usergroup, $displaygroup) : htmlspecialchars_uni($username),
			'avatar'        => $avatar,
			'usergroup'     => $usergroup,
			'usertitle'     => isset($user['usertitle']) ? $user['usertitle'] : '',
			'postnum'       => isset($user['postnum']) ? (int)$user['postnum'] : 0,
		);
	}

	private function parser()
	{
		if($this->parser === null)
		{
			require_once MYBB_ROOT.'inc/class_parser.php';
			$this->parser = new postParser;
		}
		return $this->parser;
	}

	private function forum_parser_options($forum, $username = '', $smilies_off = false)
	{
		global $mybb;
		return array(
			'allow_html'      => (int)$forum['allowhtml'],
			'allow_mycode'    => (int)$forum['allowmycode'],
			'allow_smilies'   => $smilies_off ? 0 : (int)$forum['allowsmilies'],
			'allow_imgcode'   => (int)$forum['allowimgcode'],
			'allow_videocode' => (int)$forum['allowvideocode'],
			'filter_badwords' => 1,
			'me_username'     => $username,
		);
	}

	/** Parses MyCode to HTML with absolute links (the client has no base URL). */
	private function to_html($message, $options)
	{
		$html = $this->parser()->parse_message($message, $options);
		$self = $this;
		$html = preg_replace_callback('#\s(src|href)="([^"]*)"#i', function($m) use ($self) {
			return ' '.$m[1].'="'.$self->abs_url_public($m[2]).'"';
		}, $html);
		// Inline attachments are rendered by the client
		$html = preg_replace('#\[attachment=(\d+)\]#i', '<span class="fg-attachment" data-aid="$1"></span>', $html);
		return $html;
	}

	public function abs_url_public($url)
	{
		if(strpos($url, '#') === 0 || stripos($url, 'mailto:') === 0 || stripos($url, 'javascript:') === 0)
		{
			return $url;
		}
		return $this->abs_url(html_entity_decode($url));
	}

	private function plain_excerpt($message, $length = 220)
	{
		$text = $this->parser()->text_parse_message($message, array('filter_badwords' => 1));
		$text = preg_replace('#\s+#u', ' ', trim(strip_tags($text)));
		$text = preg_replace('#\[attachment=\d+\]#i', '', $text);
		if(my_strlen($text) > $length)
		{
			$text = my_substr($text, 0, $length - 1).'…';
		}
		return $text;
	}

	private function prefix_text($prefix_id)
	{
		if(!$prefix_id)
		{
			return '';
		}
		$prefix = build_prefixes((int)$prefix_id);
		return !empty($prefix['prefix']) ? $prefix['prefix'] : '';
	}

	private function page_args($default_per_page, $max_per_page)
	{
		$per_page = $this->in('perpage', 'int');
		if($per_page <= 0)
		{
			$per_page = $default_per_page;
		}
		$per_page = min($per_page, $max_per_page);
		$page = max(1, $this->in('page', 'int'));
		return array($page, $per_page, ($page - 1) * $per_page);
	}

	private function new_posthash()
	{
		global $mybb;
		return md5($mybb->user['uid'].random_str());
	}

	private function load_attachments($pids, $fid)
	{
		global $db;
		$result = array();
		$pids = array_filter(array_map('intval', (array)$pids));
		if(!$pids)
		{
			return $result;
		}
		$where = 'pid IN ('.implode(',', $pids).')';
		if(!is_moderator($fid, 'canviewunapprove'))
		{
			$where .= " AND visible='1'";
		}
		$query = $db->simple_select('attachments', '*', $where, array('order_by' => 'aid'));
		while($attachment = $db->fetch_array($query))
		{
			$result[(int)$attachment['pid']][] = $this->attachment_info($attachment);
		}
		return $result;
	}

	private function attachment_info($attachment)
	{
		$ext = my_strtolower(get_extension($attachment['filename']));
		return array(
			'aid'       => (int)$attachment['aid'],
			'pid'       => (int)$attachment['pid'],
			'filename'  => $attachment['filename'],
			'filetype'  => $attachment['filetype'],
			'filesize'  => (int)$attachment['filesize'],
			'downloads' => (int)$attachment['downloads'],
			'is_image'  => in_array($ext, array('png', 'jpg', 'jpeg', 'jpe', 'gif', 'bmp', 'webp')),
			'thumbnail' => !empty($attachment['thumbnail']) && $attachment['thumbnail'] != 'SMALL',
			'visible'   => (int)$attachment['visible'],
		);
	}

	/** Same rules as editpost.php: may the user edit this post? */
	private function can_edit_post($post, $thread, $forum, $permissions)
	{
		global $mybb;
		if(is_moderator($forum['fid'], 'caneditposts'))
		{
			return true;
		}
		if(empty($forum['open']) || !empty($mybb->user['suspendposting']) || $thread['closed'] == 1)
		{
			return false;
		}
		if(empty($permissions['caneditposts']) || $mybb->user['uid'] != $post['uid'])
		{
			return false;
		}
		if(!empty($mybb->usergroup['edittimelimit']) && $post['dateline'] < (TIME_NOW - $mybb->usergroup['edittimelimit'] * 60))
		{
			return false;
		}
		return (int)$post['visible'] == 1 || ((int)$post['visible'] == 0 && !empty($mybb->settings['showownunapproved']));
	}

	/** Same rules as editpost.php: may the user delete this post (or thread if it is the first post)? */
	private function can_delete_post($post, $thread, $forum, $permissions)
	{
		global $mybb;
		$first = $post['pid'] == $thread['firstpost'];
		if($first && (is_moderator($forum['fid'], 'candeletethreads') || is_moderator($forum['fid'], 'cansoftdeletethreads')))
		{
			return true;
		}
		if(!$first && (is_moderator($forum['fid'], 'candeleteposts') || is_moderator($forum['fid'], 'cansoftdeleteposts')))
		{
			return true;
		}
		if($thread['closed'] == 1 || !empty($mybb->user['suspendposting']) || $mybb->user['uid'] != $post['uid'])
		{
			return false;
		}
		if($first ? empty($permissions['candeletethreads']) : empty($permissions['candeleteposts']))
		{
			return false;
		}
		return (int)$post['visible'] == 1 || ((int)$post['visible'] == 0 && !empty($mybb->settings['showownunapproved']));
	}

	private function post_options()
	{
		return array(
			'signature'          => $this->in('signature', 'bool', true) ? 1 : 0,
			'subscriptionmethod' => 0,
			'disablesmilies'     => $this->in('disablesmilies', 'bool') ? 1 : 0,
		);
	}

	private function datahandler_errors($handler)
	{
		$errors = array();
		foreach((array)$handler->get_friendly_errors() as $error)
		{
			$errors[] = html_entity_decode(strip_tags($error), ENT_QUOTES, 'UTF-8');
		}
		return $errors;
	}

	private function check_can_post()
	{
		global $mybb, $lang;
		if(!empty($mybb->user['suspendposting']))
		{
			$this->fail('suspended', 'Du bist im Moment vom Schreiben ausgeschlossen.', 403);
		}
	}

	// =================================================================
	//  Routes: info and login
	// =================================================================

	private function route_info()
	{
		global $mybb, $cache;
		$stats = $cache->read('stats');
		$this->send(array(
			'api'            => 'FG Forum API',
			'api_version'    => self::API_VERSION,
			'plugin_version' => FGAPI_VERSION,
			'mybb_version'   => $mybb->version,
			'board'          => array(
				'name' => $mybb->settings['bbname'],
				'url'  => $mybb->settings['bburl'],
			),
			'login' => array(
				'password' => !empty($mybb->settings['fgapi_password_login']),
				'api_key'  => true,
				'method'   => (int)$mybb->settings['username_method'],
			),
			'features' => array(
				'attachments' => !empty($mybb->settings['enableattachments']),
				'pms'         => !empty($mybb->settings['enablepms']),
			),
			'stats' => array(
				'users'   => isset($stats['numusers']) ? (int)$stats['numusers'] : 0,
				'threads' => isset($stats['numthreads']) ? (int)$stats['numthreads'] : 0,
				'posts'   => isset($stats['numposts']) ? (int)$stats['numposts'] : 0,
			),
			'author' => array('name' => 'FGSeRaser', 'url' => 'https://github.com/FGSeRaser'),
		));
	}

	private function route_login()
	{
		global $mybb, $db;

		if(empty($mybb->settings['fgapi_password_login']))
		{
			$this->fail('password_login_disabled', 'In diesem Forum ist nur der Login mit API-Key erlaubt.', 403);
		}
		$this->require_input(array('username', 'password'));
		if($this->too_many_attempts())
		{
			$this->fail('too_many_attempts', 'Zu viele Fehlversuche. Bitte warte 15 Minuten.', 429);
		}

		require_once MYBB_ROOT.'inc/functions_user.php';
		$user = get_user_by_username($this->in('username'), array(
			'fields'          => '*',
			'username_method' => (int)$mybb->settings['username_method'],
		));

		if(!$user)
		{
			$this->add_attempt();
			$this->fail('login_failed', 'Benutzername oder Passwort ist falsch.', 401);
		}
		if(login_attempt_check((int)$user['uid'], false) === false)
		{
			$this->fail('account_locked', 'Zu viele Fehlversuche für dieses Konto. Bitte versuche es später noch einmal.', 429);
		}
		if(!validate_password_from_uid((int)$user['uid'], $this->in('password'), $user))
		{
			$this->add_attempt();
			$db->update_query('users', array('loginattempts' => 'loginattempts+1'), "uid='".(int)$user['uid']."'", 1, true);
			$this->fail('login_failed', 'Benutzername oder Passwort ist falsch.', 401);
		}

		$db->update_query('users', array('loginattempts' => 1), "uid='".(int)$user['uid']."'");
		$user = get_user((int)$user['uid']);
		$this->load_user($user);
		$this->check_board_open();

		$days = (int)$mybb->settings['fgapi_token_days'];
		$expires = $days > 0 ? TIME_NOW + $days * 86400 : 0;
		$client = trim($this->in('client'));
		if($client === '')
		{
			$client = 'FG Forum Client';
		}
		$token = fgapi_create_token($mybb->user['uid'], 'session', $client, $expires);

		$this->send(array(
			'token'   => $token,
			'expires' => $expires,
			'user'    => $this->me_data(),
		));
	}

	private function route_logout()
	{
		global $db;
		if($this->token['kind'] == 'session')
		{
			$db->delete_query('fgapi_tokens', "id='".(int)$this->token['id']."'");
		}
		$this->send(array('logged_out' => true));
	}

	private function me_data()
	{
		global $mybb;
		$data = $this->user_brief($mybb->user);
		$data['email'] = $mybb->user['email'];
		$data['regdate'] = (int)$mybb->user['regdate'];
		$data['threadnum'] = (int)$mybb->user['threadnum'];
		$data['pms'] = array(
			'unread' => (int)$mybb->user['pms_unread'],
			'total'  => (int)$mybb->user['pms_total'],
		);
		$data['permissions'] = array(
			'use_pms'          => !empty($mybb->settings['enablepms']) && !empty($mybb->usergroup['canusepms']),
			'send_pms'         => !empty($mybb->settings['enablepms']) && !empty($mybb->usergroup['cansendpms']),
			'create_keys'      => fgapi_in_groups($mybb->user, $mybb->settings['fgapi_key_groups']),
			'admin'            => !empty($mybb->usergroup['cancp']),
			'moderator'        => !empty($mybb->usergroup['issupermod']) || is_moderator(),
			'suspended'        => !empty($mybb->user['suspendposting']),
		);
		$data['attachments'] = array(
			'enabled'   => !empty($mybb->settings['enableattachments']),
			'max_files' => (int)$mybb->settings['maxattachments'],
			'quota_kb'  => (int)$mybb->usergroup['attachquota'],
			'types'     => $this->allowed_attachment_types(),
		);
		$data['auth'] = $this->token ? array('kind' => $this->token['kind'], 'label' => $this->token['label']) : array('kind' => 'session', 'label' => '');
		return $data;
	}

	private function allowed_attachment_types()
	{
		global $cache;
		$types = array();
		foreach((array)$cache->read('attachtypes') as $ext => $type)
		{
			if(!is_member($type['groups']))
			{
				continue;
			}
			$types[] = array('ext' => $ext, 'max_kb' => (int)$type['maxsize'], 'mime' => $type['mimetype']);
		}
		return $types;
	}

	private function route_me()
	{
		$this->send($this->me_data());
	}

	// =================================================================
	//  Routes: reading
	// =================================================================

	private function route_forums()
	{
		global $db, $mybb, $forum_cache;

		cache_forums();
		$hidden = array_merge($this->inactive_list());
		$unviewable = get_unviewable_forums(false);
		$unviewable = $unviewable === '' ? array() : array_map('intval', explode(',', $unviewable));

		// Counters are not part of the forum cache
		$counters = array();
		$query = $db->simple_select('forums', 'fid, threads, posts, lastpost, lastposter, lastposteruid, lastposttid, lastpostsubject');
		while($row = $db->fetch_array($query))
		{
			$counters[(int)$row['fid']] = $row;
		}

		$children = array();
		foreach($forum_cache as $forum)
		{
			$children[(int)$forum['pid']][] = $forum;
		}
		foreach($children as $pid => $list)
		{
			usort($children[$pid], function($a, $b) {
				return (int)$a['disporder'] - (int)$b['disporder'];
			});
		}

		$result = array();
		$walk = function($pid, $depth) use (&$walk, &$result, $children, $hidden, $unviewable, $counters, $mybb) {
			if(empty($children[$pid]))
			{
				return;
			}
			foreach($children[$pid] as $forum)
			{
				$fid = (int)$forum['fid'];
				if(in_array($fid, $hidden) || in_array($fid, $unviewable))
				{
					continue;
				}
				$permissions = forum_permissions($fid);
				$count = isset($counters[$fid]) ? $counters[$fid] : array();
				$show_lastpost = !empty($permissions['canviewthreads']) && empty($permissions['canonlyviewownthreads']) && !empty($count['lastpost']);
				$result[] = array(
					'fid'         => $fid,
					'pid'         => (int)$forum['pid'],
					'depth'       => $depth,
					'type'        => $forum['type'],
					'name'        => $forum['name'],
					'description' => strip_tags($forum['description']),
					'linkto'      => $forum['linkto'],
					'open'        => (int)$forum['open'] == 1,
					'threads'     => isset($count['threads']) ? (int)$count['threads'] : 0,
					'posts'       => isset($count['posts']) ? (int)$count['posts'] : 0,
					'lastpost'    => $show_lastpost ? array(
						'dateline' => (int)$count['lastpost'],
						'tid'      => (int)$count['lastposttid'],
						'subject'  => $count['lastpostsubject'],
						'uid'      => (int)$count['lastposteruid'],
						'username' => $count['lastposter'],
					) : null,
					'can' => array(
						'view_threads'   => !empty($permissions['canviewthreads']),
						'post_threads'   => $forum['type'] == 'f' && (int)$forum['open'] == 1 && !empty($permissions['canpostthreads']),
						'post_replies'   => !empty($permissions['canpostreplys']),
						'attach'         => !empty($mybb->settings['enableattachments']) && !empty($permissions['canpostattachments']),
						'moderate'       => is_moderator($fid),
					),
				);
				$walk($fid, $depth + 1);
			}
		};
		$walk(0, 0);

		$this->send(array('forums' => $result));
	}

	private function route_threads()
	{
		global $db, $mybb;

		list($forum, $permissions) = $this->forum_access($this->in('fid', 'int'));
		if($forum['type'] != 'f')
		{
			$this->send(array('forum' => $this->forum_brief($forum, $permissions), 'threads' => array(), 'page' => 1, 'pages' => 1, 'total' => 0));
		}
		list($page, $per_page, $offset) = $this->page_args(20, 100);

		$states = $this->visible_states($forum['fid']);
		$where = "t.fid='".(int)$forum['fid']."' AND t.visible IN (".implode(',', $states).") AND t.closed NOT LIKE 'moved|%'";
		if(!empty($mybb->settings['showownunapproved']) && !in_array(0, $states))
		{
			$where = "t.fid='".(int)$forum['fid']."' AND (t.visible IN (".implode(',', $states).") OR (t.visible='0' AND t.uid='".(int)$mybb->user['uid']."')) AND t.closed NOT LIKE 'moved|%'";
		}
		if(!empty($permissions['canonlyviewownthreads']))
		{
			$where .= " AND t.uid='".(int)$mybb->user['uid']."'";
		}

		$query = $db->simple_select('threads t', 'COUNT(t.tid) AS c', $where);
		$total = (int)$db->fetch_field($query, 'c');

		$threads = array();
		$query = $db->query("
			SELECT t.*, u.avatar, u.avatardimensions, u.usergroup, u.displaygroup
			FROM ".TABLE_PREFIX."threads t
			LEFT JOIN ".TABLE_PREFIX."users u ON (u.uid=t.uid)
			WHERE {$where}
			ORDER BY t.sticky DESC, t.lastpost DESC
			LIMIT {$offset}, {$per_page}
		");
		while($thread = $db->fetch_array($query))
		{
			$threads[] = $this->thread_brief($thread);
		}

		$this->send(array(
			'forum'   => $this->forum_brief($forum, $permissions),
			'threads' => $threads,
			'page'    => $page,
			'pages'   => max(1, (int)ceil($total / $per_page)),
			'total'   => $total,
		));
	}

	private function forum_brief($forum, $permissions)
	{
		global $mybb;
		return array(
			'fid'  => (int)$forum['fid'],
			'name' => $forum['name'],
			'type' => $forum['type'],
			'can'  => array(
				'post_threads' => $forum['type'] == 'f' && (int)$forum['open'] == 1 && !empty($permissions['canpostthreads']) && empty($mybb->user['suspendposting']),
				'post_replies' => !empty($permissions['canpostreplys']) && empty($mybb->user['suspendposting']),
				'attach'       => !empty($mybb->settings['enableattachments']) && !empty($permissions['canpostattachments']),
				'moderate'     => is_moderator($forum['fid']),
			),
		);
	}

	private function thread_brief($thread)
	{
		return array(
			'tid'           => (int)$thread['tid'],
			'fid'           => (int)$thread['fid'],
			'subject'       => $this->parser()->parse_badwords($thread['subject']),
			'prefix'        => $this->prefix_text($thread['prefix']),
			'uid'           => (int)$thread['uid'],
			'username'      => $thread['username'],
			'dateline'      => (int)$thread['dateline'],
			'lastpost'      => (int)$thread['lastpost'],
			'lastposter'    => $thread['lastposter'],
			'lastposteruid' => (int)$thread['lastposteruid'],
			'replies'       => (int)$thread['replies'],
			'views'         => (int)$thread['views'],
			'sticky'        => (int)$thread['sticky'] == 1,
			'closed'        => (string)$thread['closed'] === '1',
			'visible'       => (int)$thread['visible'],
			'attachments'   => isset($thread['attachmentcount']) ? (int)$thread['attachmentcount'] : 0,
		);
	}

	private function route_thread()
	{
		global $db, $mybb;

		list($thread, $forum, $permissions) = $this->thread_access($this->in('tid', 'int'));
		list($page, $per_page, $offset) = $this->page_args(20, 50);

		$states = $this->visible_states($forum['fid']);
		$where = "p.tid='".(int)$thread['tid']."' AND (p.visible IN (".implode(',', $states).")";
		if(!empty($mybb->settings['showownunapproved']))
		{
			$where .= " OR (p.visible='0' AND p.uid='".(int)$mybb->user['uid']."')";
		}
		$where .= ')';

		$query = $db->simple_select('posts p', 'COUNT(p.pid) AS c', $where);
		$total = (int)$db->fetch_field($query, 'c');
		$pages = max(1, (int)ceil($total / $per_page));
		if($this->in('page') === 'last')
		{
			$page = $pages;
			$offset = ($page - 1) * $per_page;
		}

		// Jump to the page that contains a certain post
		$focus_pid = $this->in('pid', 'int');
		if($focus_pid > 0)
		{
			$focus = get_post($focus_pid);
			if($focus && (int)$focus['tid'] == (int)$thread['tid'])
			{
				$dateline = (int)$focus['dateline'];
				$query = $db->simple_select('posts p', 'COUNT(p.pid) AS c', $where." AND (p.dateline < '{$dateline}' OR (p.dateline = '{$dateline}' AND p.pid <= '{$focus_pid}'))");
				$position = max(1, (int)$db->fetch_field($query, 'c'));
				$page = min($pages, max(1, (int)ceil($position / $per_page)));
				$offset = ($page - 1) * $per_page;
			}
			else
			{
				$focus_pid = 0;
			}
		}

		$rows = array();
		$query = $db->query("
			SELECT p.*, u.username AS user_name, u.avatar, u.avatardimensions, u.usergroup, u.displaygroup, u.usertitle, u.postnum, u.signature
			FROM ".TABLE_PREFIX."posts p
			LEFT JOIN ".TABLE_PREFIX."users u ON (u.uid=p.uid)
			WHERE {$where}
			ORDER BY p.dateline ASC, p.pid ASC
			LIMIT {$offset}, {$per_page}
		");
		while($row = $db->fetch_array($query))
		{
			$rows[] = $row;
		}

		$attachments = $this->load_attachments(array_map(function($row) { return $row['pid']; }, $rows), $forum['fid']);
		$posts = array();
		foreach($rows as $index => $row)
		{
			$posts[] = $this->post_full($row, $thread, $forum, $permissions, $attachments, $offset + $index + 1);
		}

		// Count the view and mark the thread as read
		$db->write_query("UPDATE ".TABLE_PREFIX."threads SET views=views+1 WHERE tid='".(int)$thread['tid']."'");
		require_once MYBB_ROOT.'inc/functions_indicators.php';
		mark_thread_read($thread['tid'], $forum['fid']);

		$thread_data = $this->thread_brief($thread);
		$thread_data['forum'] = $this->forum_brief($forum, $permissions);
		$thread_data['can'] = array(
			'reply'    => $this->can_reply($thread, $forum, $permissions),
			'moderate' => is_moderator($forum['fid']),
			'close'    => is_moderator($forum['fid'], 'canopenclosethreads'),
			'stick'    => is_moderator($forum['fid'], 'canstickunstickthreads'),
		);

		$this->send(array(
			'thread' => $thread_data,
			'focus_pid' => $focus_pid,
			'posts'  => $posts,
			'page'   => $page,
			'pages'  => $pages,
			'total'  => $total,
		));
	}

	private function can_reply($thread, $forum, $permissions)
	{
		global $mybb;
		if(empty($permissions['canpostreplys']) || !empty($mybb->user['suspendposting']) || empty($forum['open']))
		{
			return false;
		}
		if(!empty($permissions['canonlyreplyownthreads']) && $thread['uid'] != $mybb->user['uid'])
		{
			return false;
		}
		if($thread['closed'] == 1 && !is_moderator($forum['fid'], 'canpostclosedthreads'))
		{
			return false;
		}
		return true;
	}

	private function post_full($row, $thread, $forum, $permissions, $attachments, $number)
	{
		global $mybb;

		$author = $row;
		if(!empty($row['user_name']))
		{
			$author['username'] = $row['user_name'];
		}
		$options = $this->forum_parser_options($forum, $author['username'], !empty($row['smilieoff']));

		$signature = '';
		if(!empty($row['includesig']) && !empty($row['signature']) && !empty($mybb->settings['sigmycode']) !== null)
		{
			$signature = $this->to_html($row['signature'], array(
				'allow_html'      => (int)$mybb->settings['sightml'],
				'allow_mycode'    => (int)$mybb->settings['sigmycode'],
				'allow_smilies'   => (int)$mybb->settings['sigsmilies'],
				'allow_imgcode'   => (int)$mybb->settings['sigimgcode'],
				'filter_badwords' => 1,
			));
		}

		$editor = '';
		if(!empty($row['edituid']))
		{
			$edit_user = get_user((int)$row['edituid']);
			$editor = $edit_user ? $edit_user['username'] : '';
		}

		return array(
			'pid'          => (int)$row['pid'],
			'tid'          => (int)$row['tid'],
			'number'       => $number,
			'subject'      => $this->parser()->parse_badwords($row['subject']),
			'author'       => $this->user_brief($author),
			'dateline'     => (int)$row['dateline'],
			'edittime'     => (int)$row['edittime'],
			'editor'       => $editor,
			'editreason'   => isset($row['editreason']) ? $row['editreason'] : '',
			'message'      => $row['message'],
			'html'         => $this->to_html($row['message'], $options),
			'signature'    => $signature,
			'visible'      => (int)$row['visible'],
			'is_first'     => (int)$row['pid'] == (int)$thread['firstpost'],
			'attachments'  => isset($attachments[(int)$row['pid']]) ? $attachments[(int)$row['pid']] : array(),
			'can'          => array(
				'edit'   => $this->can_edit_post($row, $thread, $forum, $permissions),
				'delete' => $this->can_delete_post($row, $thread, $forum, $permissions),
				'quote'  => $this->can_reply($thread, $forum, $permissions),
			),
		);
	}

	private function route_post_get()
	{
		global $db;
		$post = get_post($this->in('pid', 'int'));
		if(!$post)
		{
			$this->fail('post_not_found', 'Diesen Beitrag gibt es nicht.', 404);
		}
		list($thread, $forum, $permissions) = $this->thread_access($post['tid']);
		if(!in_array((int)$post['visible'], $this->visible_states($forum['fid'])) && !$this->can_edit_post($post, $thread, $forum, $permissions))
		{
			$this->fail('post_not_found', 'Diesen Beitrag gibt es nicht.', 404);
		}
		$user = get_user((int)$post['uid']);
		$row = array_merge($post, $user ? array(
			'user_name' => $user['username'], 'avatar' => $user['avatar'], 'avatardimensions' => $user['avatardimensions'],
			'usergroup' => $user['usergroup'], 'displaygroup' => $user['displaygroup'], 'usertitle' => $user['usertitle'],
			'postnum' => $user['postnum'], 'signature' => $user['signature'],
		) : array());
		$attachments = $this->load_attachments(array($post['pid']), $forum['fid']);
		$data = $this->post_full($row, $thread, $forum, $permissions, $attachments, 0);
		$data['thread'] = $this->thread_brief($thread);
		$this->send($data);
	}

	private function route_latest()
	{
		global $db, $mybb;

		$max = max(1, (int)$mybb->settings['fgapi_latest_max']);
		$limit = $this->in('limit', 'int');
		$limit = $limit <= 0 ? 10 : min($limit, $max);
		$filter = $this->forum_filter_sql('p.fid');
		$type = $this->in('type') == 'threads' ? 'threads' : 'posts';

		$items = array();
		if($type == 'threads')
		{
			$query = $db->query("
				SELECT t.*, p.message, f.name AS forumname
				FROM ".TABLE_PREFIX."threads t
				INNER JOIN ".TABLE_PREFIX."posts p ON (p.pid=t.firstpost)
				INNER JOIN ".TABLE_PREFIX."forums f ON (f.fid=t.fid)
				WHERE t.visible='1' AND p.visible='1' AND t.closed NOT LIKE 'moved|%' {$filter}
				ORDER BY t.dateline DESC, t.tid DESC
				LIMIT {$limit}
			");
			while($row = $db->fetch_array($query))
			{
				$item = $this->thread_brief($row);
				$item['forumname'] = $row['forumname'];
				$item['excerpt'] = $this->plain_excerpt($row['message']);
				$items[] = $item;
			}
		}
		else
		{
			$query = $db->query("
				SELECT p.pid, p.tid, p.fid, p.subject, p.uid, p.username, p.dateline, p.message,
					t.subject AS threadsubject, t.prefix, t.replies, t.firstpost, f.name AS forumname,
					u.avatar, u.avatardimensions, u.usergroup, u.displaygroup, u.username AS user_name
				FROM ".TABLE_PREFIX."posts p
				INNER JOIN ".TABLE_PREFIX."threads t ON (t.tid=p.tid)
				INNER JOIN ".TABLE_PREFIX."forums f ON (f.fid=p.fid)
				LEFT JOIN ".TABLE_PREFIX."users u ON (u.uid=p.uid)
				WHERE p.visible='1' AND t.visible='1' AND t.closed NOT LIKE 'moved|%' {$filter}
				ORDER BY p.dateline DESC, p.pid DESC
				LIMIT {$limit}
			");
			while($row = $db->fetch_array($query))
			{
				$items[] = $this->post_list_item($row);
			}
		}

		$this->send(array('type' => $type, 'items' => $items));
	}

	private function post_list_item($row)
	{
		$author = $row;
		if(!empty($row['user_name']))
		{
			$author['username'] = $row['user_name'];
		}
		return array(
			'pid'           => (int)$row['pid'],
			'tid'           => (int)$row['tid'],
			'fid'           => (int)$row['fid'],
			'subject'       => $this->parser()->parse_badwords($row['subject']),
			'threadsubject' => $this->parser()->parse_badwords($row['threadsubject']),
			'prefix'        => $this->prefix_text($row['prefix']),
			'forumname'     => $row['forumname'],
			'author'        => $this->user_brief($author),
			'dateline'      => (int)$row['dateline'],
			'is_first'      => (int)$row['pid'] == (int)$row['firstpost'],
			'replies'       => (int)$row['replies'],
			'excerpt'       => $this->plain_excerpt($row['message']),
		);
	}

	private function route_search()
	{
		global $db, $mybb;

		if(empty($mybb->usergroup['cansearch']))
		{
			$this->fail('no_permission', 'Deine Benutzergruppe darf nicht suchen.', 403);
		}
		$words = trim($this->in('q'));
		$min = max(2, (int)$mybb->settings['minsearchword']);
		if(my_strlen($words) < $min)
		{
			$this->fail('query_too_short', 'Der Suchbegriff muss mindestens '.$min.' Zeichen lang sein.', 400);
		}
		list($page, $per_page, $offset) = $this->page_args(25, 50);

		$like = $db->escape_string_like(my_strtolower($words));
		$where_text = "LOWER(t.subject) LIKE '%{$like}%'";
		if($this->in('titles_only', 'bool') == false)
		{
			$where_text = "(LOWER(t.subject) LIKE '%{$like}%' OR LOWER(p.message) LIKE '%{$like}%')";
		}
		$where = "p.visible='1' AND t.visible='1' AND t.closed NOT LIKE 'moved|%' AND {$where_text}".$this->forum_filter_sql('p.fid');
		$fid = $this->in('fid', 'int');
		if($fid > 0)
		{
			$where .= " AND p.fid='{$fid}'";
		}
		if($this->in('author') !== '')
		{
			$where .= " AND LOWER(p.username)='".$db->escape_string(my_strtolower($this->in('author')))."'";
		}

		$query = $db->query("
			SELECT COUNT(p.pid) AS c
			FROM ".TABLE_PREFIX."posts p
			INNER JOIN ".TABLE_PREFIX."threads t ON (t.tid=p.tid)
			WHERE {$where}
		");
		$total = (int)$db->fetch_field($query, 'c');

		$items = array();
		$query = $db->query("
			SELECT p.pid, p.tid, p.fid, p.subject, p.uid, p.username, p.dateline, p.message,
				t.subject AS threadsubject, t.prefix, t.replies, t.firstpost, f.name AS forumname,
				u.avatar, u.avatardimensions, u.usergroup, u.displaygroup, u.username AS user_name
			FROM ".TABLE_PREFIX."posts p
			INNER JOIN ".TABLE_PREFIX."threads t ON (t.tid=p.tid)
			INNER JOIN ".TABLE_PREFIX."forums f ON (f.fid=p.fid)
			LEFT JOIN ".TABLE_PREFIX."users u ON (u.uid=p.uid)
			WHERE {$where}
			ORDER BY p.dateline DESC, p.pid DESC
			LIMIT {$offset}, {$per_page}
		");
		while($row = $db->fetch_array($query))
		{
			$items[] = $this->post_list_item($row);
		}

		$this->send(array(
			'query' => $words,
			'items' => $items,
			'page'  => $page,
			'pages' => max(1, (int)ceil($total / $per_page)),
			'total' => $total,
		));
	}

	private function route_online()
	{
		global $db, $mybb, $cache;

		$cutoff = TIME_NOW - (int)$mybb->settings['wolcutoff'];
		$can_see_invisible = !empty($mybb->usergroup['canviewwolinvis']);
		$users = array();
		$seen = array();
		$guests = 0;
		$query = $db->query("
			SELECT s.uid, s.time, u.username, u.invisible, u.avatar, u.avatardimensions, u.usergroup, u.displaygroup, u.usertitle, u.postnum
			FROM ".TABLE_PREFIX."sessions s
			LEFT JOIN ".TABLE_PREFIX."users u ON (u.uid=s.uid)
			WHERE s.time > {$cutoff}
			ORDER BY s.time DESC
		");
		while($row = $db->fetch_array($query))
		{
			if((int)$row['uid'] == 0)
			{
				$guests++;
				continue;
			}
			if(isset($seen[$row['uid']]))
			{
				continue;
			}
			$seen[$row['uid']] = true;
			if(!empty($row['invisible']) && !$can_see_invisible && $row['uid'] != $mybb->user['uid'])
			{
				continue;
			}
			$item = $this->user_brief($row);
			$item['invisible'] = !empty($row['invisible']);
			$item['time'] = (int)$row['time'];
			$users[] = $item;
		}

		$stats = $cache->read('stats');
		$this->send(array(
			'users'  => $users,
			'guests' => $guests,
			'stats'  => array(
				'users'        => isset($stats['numusers']) ? (int)$stats['numusers'] : 0,
				'threads'      => isset($stats['numthreads']) ? (int)$stats['numthreads'] : 0,
				'posts'        => isset($stats['numposts']) ? (int)$stats['numposts'] : 0,
				'newest_user'  => isset($stats['lastusername']) ? $stats['lastusername'] : '',
				'newest_uid'   => isset($stats['lastuid']) ? (int)$stats['lastuid'] : 0,
			),
		));
	}

	private function route_user()
	{
		global $mybb, $db;

		if(empty($mybb->usergroup['canviewprofiles']))
		{
			$this->fail('no_permission', 'Du darfst keine Profile ansehen.', 403);
		}
		$uid = $this->in('uid', 'int');
		$user = $uid > 0 ? get_user($uid) : false;
		if(!$user && $this->in('username') !== '')
		{
			$user = get_user_by_username($this->in('username'), array('fields' => '*'));
		}
		if(!$user)
		{
			$this->fail('user_not_found', 'Diesen Benutzer gibt es nicht.', 404);
		}

		$data = $this->user_brief($user);
		$data['regdate'] = (int)$user['regdate'];
		$data['threadnum'] = (int)$user['threadnum'];
		$data['reputation'] = (int)$user['reputation'];
		$hide_activity = !empty($user['invisible']) && empty($mybb->usergroup['canviewwolinvis']) && $user['uid'] != $mybb->user['uid'];
		$data['lastactive'] = $hide_activity ? 0 : (int)$user['lastactive'];
		$data['website'] = $user['website'];
		$data['signature'] = '';
		if(!empty($user['signature']))
		{
			$data['signature'] = $this->to_html($user['signature'], array(
				'allow_html'      => (int)$mybb->settings['sightml'],
				'allow_mycode'    => (int)$mybb->settings['sigmycode'],
				'allow_smilies'   => (int)$mybb->settings['sigsmilies'],
				'allow_imgcode'   => (int)$mybb->settings['sigimgcode'],
				'filter_badwords' => 1,
			));
		}
		$this->send($data);
	}

	private function route_preview()
	{
		global $mybb;
		$message = $this->in('message');
		if($this->in('pm', 'bool'))
		{
			$options = array(
				'allow_html'      => (int)$mybb->settings['pmsallowhtml'],
				'allow_mycode'    => (int)$mybb->settings['pmsallowmycode'],
				'allow_smilies'   => (int)$mybb->settings['pmsallowsmilies'],
				'allow_imgcode'   => (int)$mybb->settings['pmsallowimgcode'],
				'allow_videocode' => (int)$mybb->settings['pmsallowvideocode'],
				'filter_badwords' => 1,
				'me_username'     => $mybb->user['username'],
			);
		}
		else
		{
			$fid = $this->in('fid', 'int');
			if($fid <= 0 && $this->in('tid', 'int') > 0)
			{
				$thread = get_thread($this->in('tid', 'int'));
				$fid = $thread ? (int)$thread['fid'] : 0;
			}
			list($forum, $permissions) = $this->forum_access($fid, false);
			$options = $this->forum_parser_options($forum, $mybb->user['username']);
		}
		$this->send(array('html' => $this->to_html($message, $options)));
	}

	// =================================================================
	//  Routes: writing
	// =================================================================

	private function route_thread_create()
	{
		global $mybb, $session, $db;

		$this->check_can_post();
		$this->require_input(array('fid', 'subject', 'message'));
		list($forum, $permissions) = $this->forum_access($this->in('fid', 'int'));
		if((int)$forum['open'] == 0 || $forum['type'] != 'f' || $forum['linkto'] != '')
		{
			$this->fail('forum_closed', 'In diesem Forum können keine Themen erstellt werden.', 403);
		}
		if(empty($permissions['canpostthreads']))
		{
			$this->fail('no_permission', 'Du darfst in diesem Forum keine Themen erstellen.', 403);
		}

		// Same duplicate check as newthread.php
		$subject = $this->in('subject');
		$message = $this->in('message');
		$query = $db->simple_select('posts', 'pid', "uid='".(int)$mybb->user['uid']."' AND fid='".(int)$forum['fid']."' AND subject='".$db->escape_string($subject)."' AND message='".$db->escape_string($message)."' AND dateline>".(TIME_NOW - 600), array('limit' => 1));
		if($db->num_rows($query) > 0)
		{
			$this->fail('duplicate', 'Dieses Thema wurde gerade schon erstellt.', 409);
		}

		require_once MYBB_ROOT.'inc/functions_post.php';
		require_once MYBB_ROOT.'inc/datahandlers/post.php';
		$handler = new PostDataHandler('insert');
		$handler->action = 'thread';
		$data = array(
			'fid'       => (int)$forum['fid'],
			'subject'   => $subject,
			'prefix'    => $this->in('prefix', 'int'),
			'icon'      => 0,
			'uid'       => (int)$mybb->user['uid'],
			'username'  => $mybb->user['username'],
			'message'   => $message,
			'ipaddress' => $session->packedip,
			'posthash'  => $this->in('posthash'),
			'savedraft' => 0,
			'options'   => $this->post_options(),
		);
		$data['modoptions'] = $this->mod_options($forum['fid']);
		$handler->set_data($data);
		if(!$handler->validate_thread())
		{
			$this->fail('validation_failed', 'Das Thema konnte nicht erstellt werden.', 422, $this->datahandler_errors($handler));
		}
		$info = $handler->insert_thread();

		$this->send(array(
			'tid'     => (int)$info['tid'],
			'pid'     => (int)$info['pid'],
			'visible' => (int)$info['visible'],
			'message' => (int)$info['visible'] == 1 ? 'Thema erstellt.' : 'Thema erstellt. Es wird erst nach Freigabe durch einen Moderator sichtbar.',
		), 201);
	}

	/** Moderators can close or stick right while posting. */
	private function mod_options($fid)
	{
		$options = array();
		if($this->in('close', 'bool') && is_moderator($fid, 'canopenclosethreads'))
		{
			$options['closethread'] = 1;
		}
		if($this->in('stick', 'bool') && is_moderator($fid, 'canstickunstickthreads'))
		{
			$options['stickthread'] = 1;
		}
		return $options;
	}

	private function route_post_reply()
	{
		global $mybb, $session, $db;

		$this->check_can_post();
		$this->require_input(array('tid', 'message'));
		list($thread, $forum, $permissions) = $this->thread_access($this->in('tid', 'int'));
		if(!$this->can_reply($thread, $forum, $permissions))
		{
			$this->fail('no_permission', $thread['closed'] == 1 ? 'Dieses Thema ist geschlossen.' : 'Du darfst hier nicht antworten.', 403);
		}

		$message = $this->in('message');
		$query = $db->simple_select('posts', 'pid', "uid='".(int)$mybb->user['uid']."' AND tid='".(int)$thread['tid']."' AND message='".$db->escape_string($message)."' AND dateline>".(TIME_NOW - 600), array('limit' => 1));
		if($db->num_rows($query) > 0)
		{
			$this->fail('duplicate', 'Diese Antwort wurde gerade schon gesendet.', 409);
		}

		require_once MYBB_ROOT.'inc/functions_post.php';
		require_once MYBB_ROOT.'inc/datahandlers/post.php';
		$handler = new PostDataHandler('insert');
		$handler->action = 'post';
		$subject = $this->in('subject');
		if(trim($subject) === '')
		{
			$subject = 'RE: '.$thread['subject'];
		}
		$data = array(
			'tid'       => (int)$thread['tid'],
			'replyto'   => $this->in('replyto', 'int'),
			'fid'       => (int)$forum['fid'],
			'subject'   => $subject,
			'icon'      => 0,
			'uid'       => (int)$mybb->user['uid'],
			'username'  => $mybb->user['username'],
			'message'   => $message,
			'ipaddress' => $session->packedip,
			'posthash'  => $this->in('posthash'),
			'savedraft' => 0,
			'options'   => $this->post_options(),
		);
		$data['modoptions'] = $this->mod_options($forum['fid']);
		$handler->set_data($data);
		if(!$handler->validate_post())
		{
			$this->fail('validation_failed', 'Die Antwort konnte nicht gesendet werden.', 422, $this->datahandler_errors($handler));
		}
		$info = $handler->insert_post();
		$merged = !empty($info['merge']);
		if((int)$info['visible'] != 1)
		{
			$text = 'Antwort gesendet. Sie wird erst nach Freigabe durch einen Moderator sichtbar.';
		}
		else
		{
			$text = $merged ? 'Antwort an deinen letzten Beitrag angehängt (das Forum fasst kurz aufeinanderfolgende Beiträge zusammen).' : 'Antwort gesendet.';
		}

		$this->send(array(
			'tid'     => (int)$thread['tid'],
			'pid'     => (int)$info['pid'],
			'visible' => (int)$info['visible'],
			'merged'  => $merged,
			'message' => $text,
		), 201);
	}

	private function route_post_edit()
	{
		global $mybb;

		$this->require_input(array('pid', 'message'));
		$post = get_post($this->in('pid', 'int'));
		if(!$post)
		{
			$this->fail('post_not_found', 'Diesen Beitrag gibt es nicht.', 404);
		}
		list($thread, $forum, $permissions) = $this->thread_access($post['tid']);
		if(!$this->can_edit_post($post, $thread, $forum, $permissions))
		{
			$this->fail('no_permission', 'Du darfst diesen Beitrag nicht (mehr) bearbeiten.', 403);
		}

		require_once MYBB_ROOT.'inc/functions_post.php';
		require_once MYBB_ROOT.'inc/datahandlers/post.php';
		$handler = new PostDataHandler('update');
		$handler->action = 'post';
		$subject = $this->in('subject');
		if(trim($subject) === '')
		{
			$subject = $post['subject'];
		}
		$prefix = isset($this->input['prefix']) ? $this->in('prefix', 'int') : (int)$thread['prefix'];
		$data = array(
			'pid'        => (int)$post['pid'],
			'prefix'     => $prefix,
			'subject'    => $subject,
			'icon'       => (int)$post['icon'],
			'uid'        => (int)$post['uid'],
			'username'   => $post['username'],
			'edit_uid'   => (int)$mybb->user['uid'],
			'message'    => $this->in('message'),
			'editreason' => $this->in('editreason'),
			'options'    => array(
				'signature'          => (int)$post['includesig'],
				'subscriptionmethod' => 0,
				'disablesmilies'     => (int)$post['smilieoff'],
			),
		);
		$handler->set_data($data);
		if(!$handler->validate_post())
		{
			$this->fail('validation_failed', 'Der Beitrag konnte nicht gespeichert werden.', 422, $this->datahandler_errors($handler));
		}
		$info = $handler->update_post();

		$this->send(array(
			'pid'     => (int)$post['pid'],
			'tid'     => (int)$post['tid'],
			'visible' => (int)$info['visible'],
			'message' => 'Beitrag gespeichert.',
		));
	}

	private function route_post_delete()
	{
		global $mybb, $lang;

		$post = get_post($this->in('pid', 'int'));
		if(!$post)
		{
			$this->fail('post_not_found', 'Diesen Beitrag gibt es nicht.', 404);
		}
		list($thread, $forum, $permissions) = $this->thread_access($post['tid']);
		if(!$this->can_delete_post($post, $thread, $forum, $permissions))
		{
			$this->fail('no_permission', 'Du darfst diesen Beitrag nicht löschen.', 403);
		}
		if((int)$post['visible'] == -1 && !empty($mybb->settings['soft_delete']))
		{
			$this->fail('already_deleted', 'Dieser Beitrag ist schon gelöscht.', 409);
		}

		require_once MYBB_ROOT.'inc/class_moderation.php';
		require_once MYBB_ROOT.'inc/functions_upload.php';
		$moderation = new Moderation;
		$fid = (int)$forum['fid'];
		$log = array('fid' => $fid, 'tid' => (int)$thread['tid']);
		$lang->load('editpost');

		if($post['pid'] == $thread['firstpost'])
		{
			if(!empty($mybb->settings['soft_delete']) || is_moderator($fid, 'cansoftdeletethreads'))
			{
				$moderation->soft_delete_threads(array((int)$thread['tid']));
				$log['pid'] = (int)$post['pid'];
				log_moderator_action($log, $lang->thread_soft_deleted);
				$result = 'thread_soft_deleted';
			}
			else
			{
				$moderation->delete_thread((int)$thread['tid']);
				log_moderator_action($log, $lang->thread_deleted);
				$result = 'thread_deleted';
			}
		}
		else
		{
			if(!empty($mybb->settings['soft_delete']) || is_moderator($fid, 'cansoftdeleteposts'))
			{
				$moderation->soft_delete_posts(array((int)$post['pid']));
				$log['pid'] = (int)$post['pid'];
				log_moderator_action($log, $lang->post_soft_deleted);
				$result = 'post_soft_deleted';
			}
			else
			{
				$moderation->delete_post((int)$post['pid']);
				log_moderator_action($log, $lang->post_deleted);
				$result = 'post_deleted';
			}
		}

		$this->send(array(
			'result'         => $result,
			'thread_deleted' => strpos($result, 'thread') === 0,
			'tid'            => (int)$thread['tid'],
			'fid'            => $fid,
		));
	}

	private function route_thread_moderate()
	{
		global $lang;

		list($thread, $forum, $permissions) = $this->thread_access($this->in('tid', 'int'));
		$action = $this->in('action');
		$fid = (int)$forum['fid'];
		$tid = (int)$thread['tid'];

		$rights = array(
			'close'   => 'canopenclosethreads',
			'open'    => 'canopenclosethreads',
			'stick'   => 'canstickunstickthreads',
			'unstick' => 'canstickunstickthreads',
		);
		if(!isset($rights[$action]))
		{
			$this->fail('unknown_action', 'Unbekannte Aktion. Erlaubt: close, open, stick, unstick.', 400);
		}
		if(!is_moderator($fid, $rights[$action]))
		{
			$this->fail('no_permission', 'Dafür brauchst du Moderatorrechte in diesem Forum.', 403);
		}

		require_once MYBB_ROOT.'inc/class_moderation.php';
		$moderation = new Moderation;
		$lang->load('moderation');
		$log = array('fid' => $fid, 'tid' => $tid);
		switch($action)
		{
			case 'close':
				$moderation->close_threads(array($tid));
				log_moderator_action($log, $lang->mod_process_close);
				break;
			case 'open':
				$moderation->open_threads(array($tid));
				log_moderator_action($log, $lang->mod_process_open);
				break;
			case 'stick':
				$moderation->stick_threads(array($tid));
				log_moderator_action($log, $lang->mod_process_stick);
				break;
			case 'unstick':
				$moderation->unstick_threads(array($tid));
				log_moderator_action($log, $lang->mod_process_unstick);
				break;
		}
		$thread = get_thread($tid, true);
		$this->send(array('thread' => $this->thread_brief($thread), 'action' => $action));
	}

	// =================================================================
	//  Routes: attachments
	// =================================================================

	private function route_attachment_upload()
	{
		global $mybb, $db, $forum, $pid, $tid, $lang;

		if(empty($mybb->settings['enableattachments']))
		{
			$this->fail('attachments_disabled', 'Anhänge sind in diesem Forum abgeschaltet.', 403);
		}
		$this->check_can_post();

		$file = isset($_FILES['file']) ? $_FILES['file'] : (isset($_FILES['attachment']) ? $_FILES['attachment'] : null);
		if(!$file || !is_array($file) || is_array($file['name']))
		{
			$this->fail('no_file', 'Es wurde keine Datei mitgeschickt (Feldname "file").', 400);
		}

		require_once MYBB_ROOT.'inc/functions_upload.php';
		$lang->load('newthread');

		// Either for a new post (posthash) or an existing post (pid)
		$pid = $this->in('pid', 'int');
		$tid = 0;
		if($pid > 0)
		{
			$post = get_post($pid);
			if(!$post)
			{
				$this->fail('post_not_found', 'Diesen Beitrag gibt es nicht.', 404);
			}
			list($thread, $forum, $permissions) = $this->thread_access($post['tid']);
			if(!$this->can_edit_post($post, $thread, $forum, $permissions))
			{
				$this->fail('no_permission', 'Du darfst diesen Beitrag nicht bearbeiten.', 403);
			}
			$tid = (int)$thread['tid'];
			$mybb->input['posthash'] = '';
		}
		else
		{
			$posthash = preg_replace('#[^a-f0-9]#i', '', $this->in('posthash'));
			if(strlen($posthash) < 16)
			{
				$this->fail('missing_fields', 'Für neue Beiträge wird ein "posthash" gebraucht (32 Zeichen, a-f und 0-9).', 400, array('posthash'));
			}
			$fid = $this->in('fid', 'int');
			if($fid <= 0 && $this->in('tid', 'int') > 0)
			{
				$thread = get_thread($this->in('tid', 'int'));
				$fid = $thread ? (int)$thread['fid'] : 0;
			}
			list($forum, $permissions) = $this->forum_access($fid);
			$mybb->input['posthash'] = $posthash;
		}

		if(empty($permissions['canpostattachments']))
		{
			$this->fail('no_permission', 'Du darfst in diesem Forum keine Dateien anhängen.', 403);
		}
		if($error = check_parse_php_upload_err($file))
		{
			$this->fail('upload_failed', html_entity_decode(strip_tags($error), ENT_QUOTES, 'UTF-8'), 400);
		}

		$result = upload_attachment($file, false);
		if(!empty($result['error']))
		{
			$this->fail('upload_failed', html_entity_decode(strip_tags($result['error']), ENT_QUOTES, 'UTF-8'), 400);
		}

		$query = $db->simple_select('attachments', '*', "aid='".(int)$result['aid']."'");
		$attachment = $db->fetch_array($query);
		$info = $this->attachment_info($attachment);
		$info['mycode'] = '[attachment='.(int)$attachment['aid'].']';
		$this->send($info, 201);
	}

	private function route_attachment_delete()
	{
		global $mybb, $db;

		$query = $db->simple_select('attachments', '*', "aid='".$this->in('aid', 'int')."'");
		$attachment = $db->fetch_array($query);
		if(!$attachment)
		{
			$this->fail('attachment_not_found', 'Diesen Anhang gibt es nicht.', 404);
		}

		$allowed = false;
		if((int)$attachment['pid'] == 0)
		{
			$allowed = (int)$attachment['uid'] == (int)$mybb->user['uid'];
		}
		else
		{
			$post = get_post((int)$attachment['pid']);
			if($post)
			{
				list($thread, $forum, $permissions) = $this->thread_access($post['tid']);
				$allowed = $this->can_edit_post($post, $thread, $forum, $permissions)
					&& ((int)$attachment['uid'] == (int)$mybb->user['uid'] || is_moderator($forum['fid'], 'caneditposts'));
			}
		}
		if(!$allowed)
		{
			$this->fail('no_permission', 'Du darfst diesen Anhang nicht löschen.', 403);
		}

		require_once MYBB_ROOT.'inc/functions_upload.php';
		remove_attachment((int)$attachment['pid'], $attachment['posthash'], (int)$attachment['aid']);
		$this->send(array('deleted' => (int)$attachment['aid']));
	}

	private function route_attachment()
	{
		global $mybb, $db, $cache;

		if(empty($mybb->settings['enableattachments']))
		{
			$this->fail('attachments_disabled', 'Anhänge sind in diesem Forum abgeschaltet.', 403);
		}
		$query = $db->simple_select('attachments', '*', "aid='".$this->in('aid', 'int')."'");
		$attachment = $db->fetch_array($query);
		if(!$attachment)
		{
			$this->fail('attachment_not_found', 'Diesen Anhang gibt es nicht.', 404);
		}
		$thumb = $this->in('thumb', 'bool');

		$attachtypes = (array)$cache->read('attachtypes');
		$ext = get_extension($attachment['filename']);
		if(empty($attachtypes[$ext]))
		{
			$this->fail('attachment_not_found', 'Dieser Dateityp ist nicht (mehr) erlaubt.', 404);
		}

		if((int)$attachment['pid'] > 0)
		{
			$post = get_post((int)$attachment['pid']);
			if(!$post)
			{
				$this->fail('attachment_not_found', 'Diesen Anhang gibt es nicht.', 404);
			}
			list($thread, $forum, $permissions) = $this->thread_access($post['tid']);
			if(empty($permissions['candlattachments']) && !$thumb)
			{
				$this->fail('no_permission', 'Du darfst in diesem Forum keine Anhänge herunterladen.', 403);
			}
			if(!is_moderator($forum['fid'], 'canviewunapprove') && ((int)$attachment['visible'] != 1 || (int)$post['visible'] != 1))
			{
				$this->fail('attachment_not_found', 'Dieser Anhang ist noch nicht freigegeben.', 404);
			}
			if($attachtypes[$ext]['forums'] != -1 && strpos(','.$attachtypes[$ext]['forums'].',', ','.$forum['fid'].',') === false)
			{
				$this->fail('no_permission', 'Dieser Dateityp ist in diesem Forum nicht erlaubt.', 403);
			}
		}
		elseif((int)$attachment['uid'] != (int)$mybb->user['uid'])
		{
			$this->fail('no_permission', 'Dieser Anhang gehört zu einem Beitrag, der noch nicht gesendet wurde.', 403);
		}
		if(!$thumb && !is_member($attachtypes[$ext]['groups']))
		{
			$this->fail('no_permission', 'Deine Benutzergruppe darf diesen Dateityp nicht herunterladen.', 403);
		}

		$base = mk_path_abs($mybb->settings['uploadspath']);
		if($thumb && !empty($attachment['thumbnail']) && $attachment['thumbnail'] != 'SMALL')
		{
			$path = $base.'/'.$attachment['thumbnail'];
		}
		else
		{
			$path = $base.'/'.$attachment['attachname'];
			if(!$thumb)
			{
				$db->update_query('attachments', array('downloads' => (int)$attachment['downloads'] + 1), "aid='".(int)$attachment['aid']."'");
			}
		}
		if(!is_file($path))
		{
			$this->fail('attachment_missing', 'Die Datei fehlt auf dem Server.', 404);
		}

		$this->clean_buffers();
		$filename = str_replace(array("\r", "\n", "\0", '"'), '', ltrim(basename(' '.$attachment['filename'])));
		header('Content-Type: '.($attachment['filetype'] ? $attachment['filetype'] : 'application/octet-stream'));
		header('Content-Disposition: attachment; filename="'.$filename.'"; filename*=UTF-8\'\''.rawurlencode($filename));
		header('Content-Length: '.filesize($path));
		header("Content-Security-Policy: default-src 'none'");
		readfile($path);
		$this->finish();
	}

	// =================================================================
	//  Routes: private messages
	// =================================================================

	private function require_pms($send = false)
	{
		global $mybb;
		if(empty($mybb->settings['enablepms']) || empty($mybb->usergroup['canusepms']))
		{
			$this->fail('no_permission', 'Private Nachrichten sind für dich nicht verfügbar.', 403);
		}
		if($send && empty($mybb->usergroup['cansendpms']))
		{
			$this->fail('no_permission', 'Du darfst keine privaten Nachrichten senden.', 403);
		}
	}

	private function pm_parser_options()
	{
		global $mybb;
		return array(
			'allow_html'      => (int)$mybb->settings['pmsallowhtml'],
			'allow_mycode'    => (int)$mybb->settings['pmsallowmycode'],
			'allow_smilies'   => (int)$mybb->settings['pmsallowsmilies'],
			'allow_imgcode'   => (int)$mybb->settings['pmsallowimgcode'],
			'allow_videocode' => (int)$mybb->settings['pmsallowvideocode'],
			'filter_badwords' => 1,
		);
	}

	private function route_pms()
	{
		global $mybb, $db;
		$this->require_pms();

		$folder = $this->in('folder', 'int');
		if($folder <= 0)
		{
			$folder = 1;
		}
		list($page, $per_page, $offset) = $this->page_args(25, 100);
		$uid = (int)$mybb->user['uid'];

		$query = $db->simple_select('privatemessages', 'COUNT(*) AS c', "uid='{$uid}' AND folder='{$folder}'");
		$total = (int)$db->fetch_field($query, 'c');

		$items = array();
		$query = $db->query("
			SELECT pm.pmid, pm.toid, pm.fromid, pm.subject, pm.status, pm.dateline, pm.recipients,
				f.username AS fromname, f.avatar, f.avatardimensions, f.usergroup, f.displaygroup,
				t.username AS toname
			FROM ".TABLE_PREFIX."privatemessages pm
			LEFT JOIN ".TABLE_PREFIX."users f ON (f.uid=pm.fromid)
			LEFT JOIN ".TABLE_PREFIX."users t ON (t.uid=pm.toid)
			WHERE pm.uid='{$uid}' AND pm.folder='{$folder}'
			ORDER BY pm.dateline DESC, pm.pmid DESC
			LIMIT {$offset}, {$per_page}
		");
		while($row = $db->fetch_array($query))
		{
			$from = $row;
			$from['uid'] = (int)$row['fromid'];
			$from['username'] = (int)$row['fromid'] == 0 ? 'MyBB Engine' : $row['fromname'];
			$items[] = array(
				'pmid'     => (int)$row['pmid'],
				'subject'  => $this->parser()->parse_badwords($row['subject']),
				'from'     => $this->user_brief($from),
				'to'       => array('uid' => (int)$row['toid'], 'username' => $row['toname']),
				'unread'   => (int)$row['status'] == 0,
				'dateline' => (int)$row['dateline'],
			);
		}

		// 1-4 are fixed in MyBB, own folders have higher ids
		$folders = array(
			array('id' => 1, 'name' => 'Posteingang'),
			array('id' => 2, 'name' => 'Gesendet'),
			array('id' => 3, 'name' => 'Entwürfe'),
			array('id' => 4, 'name' => 'Papierkorb'),
		);
		foreach(explode('$%%$', (string)$mybb->user['pmfolders']) as $entry)
		{
			$parts = explode('**', $entry, 2);
			if(count($parts) == 2 && (int)$parts[0] > 4 && trim($parts[1]) !== '')
			{
				$folders[] = array('id' => (int)$parts[0], 'name' => $parts[1]);
			}
		}

		$this->send(array(
			'folder'  => $folder,
			'folders' => $folders,
			'items'   => $items,
			'page'    => $page,
			'pages'   => max(1, (int)ceil($total / $per_page)),
			'total'   => $total,
			'unread'  => (int)$mybb->user['pms_unread'],
		));
	}

	private function route_pm()
	{
		global $mybb, $db;
		$this->require_pms();

		$pmid = $this->in('pmid', 'int');
		$query = $db->simple_select('privatemessages', '*', "pmid='{$pmid}' AND uid='".(int)$mybb->user['uid']."'", array('limit' => 1));
		$pm = $db->fetch_array($query);
		if(!$pm)
		{
			$this->fail('pm_not_found', 'Diese Nachricht gibt es nicht.', 404);
		}

		if((int)$pm['status'] == 0)
		{
			$db->update_query('privatemessages', array('status' => 1, 'readtime' => TIME_NOW), "pmid='{$pmid}'");
			require_once MYBB_ROOT.'inc/functions_user.php';
			update_pm_count();
		}

		$from = (int)$pm['fromid'] > 0 ? get_user((int)$pm['fromid']) : array('uid' => 0, 'username' => 'MyBB Engine');
		$to = get_user((int)$pm['toid']);
		$options = $this->pm_parser_options();
		$options['me_username'] = $from ? $from['username'] : '';
		if(!empty($pm['smilieoff']))
		{
			$options['allow_smilies'] = 0;
		}
		$this->send(array(
			'pmid'     => (int)$pm['pmid'],
			'subject'  => $this->parser()->parse_badwords($pm['subject']),
			'from'     => $this->user_brief($from ? $from : array('uid' => 0, 'username' => '?')),
			'to'       => $to ? $this->user_brief($to) : null,
			'dateline' => (int)$pm['dateline'],
			'folder'   => (int)$pm['folder'],
			'message'  => $pm['message'],
			'html'     => $this->to_html($pm['message'], $options),
		));
	}

	private function route_pm_send()
	{
		global $mybb, $db, $session;
		$this->require_pms(true);
		$this->require_input(array('to', 'subject', 'message'));

		$to = $this->input['to'];
		$recipients = is_array($to) ? $to : explode(',', (string)$to);
		$recipients = array_values(array_unique(array_filter(array_map('trim', array_map('strval', $recipients)), 'strlen')));

		require_once MYBB_ROOT.'inc/datahandlers/pm.php';
		$handler = new PMDataHandler();
		$pm = array(
			'subject'   => $this->in('subject'),
			'message'   => $this->in('message'),
			'icon'      => 0,
			'fromid'    => (int)$mybb->user['uid'],
			'do'        => $this->in('replyto', 'int') > 0 ? 'reply' : '',
			'pmid'      => $this->in('replyto', 'int'),
			'ipaddress' => $session->packedip,
			'to'        => $recipients,
			'options'   => array(
				'signature'      => $this->in('signature', 'bool', true) ? 1 : 0,
				'disablesmilies' => 0,
				'savecopy'       => $this->in('savecopy', 'bool', true) ? 1 : 0,
				'readreceipt'    => 0,
			),
		);
		$handler->set_data($pm);
		if(!$handler->validate_pm())
		{
			$this->fail('validation_failed', 'Die Nachricht konnte nicht gesendet werden.', 422, $this->datahandler_errors($handler));
		}
		$info = $handler->insert_pm();
		$this->send(array('sent' => true, 'message' => 'Nachricht gesendet.'), 201);
	}

	private function route_pm_delete()
	{
		global $mybb, $db;
		$this->require_pms();
		$pmid = $this->in('pmid', 'int');
		$uid = (int)$mybb->user['uid'];
		$query = $db->simple_select('privatemessages', 'pmid, folder', "pmid='{$pmid}' AND uid='{$uid}'");
		$pm = $db->fetch_array($query);
		if(!$pm)
		{
			$this->fail('pm_not_found', 'Diese Nachricht gibt es nicht.', 404);
		}
		if((int)$pm['folder'] == 4)
		{
			$db->delete_query('privatemessages', "pmid='{$pmid}' AND uid='{$uid}'");
		}
		else
		{
			$db->update_query('privatemessages', array('folder' => 4, 'deletetime' => TIME_NOW), "pmid='{$pmid}' AND uid='{$uid}'");
		}
		require_once MYBB_ROOT.'inc/functions_user.php';
		update_pm_count();
		$this->send(array('deleted' => $pmid, 'trash' => (int)$pm['folder'] != 4));
	}

	// =================================================================
	//  Routes: own keys and sessions
	// =================================================================

	private function route_keys()
	{
		global $mybb, $db;
		$items = array();
		$query = $db->simple_select('fgapi_tokens', '*', "uid='".(int)$mybb->user['uid']."'", array('order_by' => 'created', 'order_dir' => 'DESC'));
		while($row = $db->fetch_array($query))
		{
			$items[] = array(
				'id'       => (int)$row['id'],
				'kind'     => $row['kind'],
				'label'    => $row['label'],
				'hint'     => $row['hint'],
				'ip'       => $row['ip'],
				'created'  => (int)$row['created'],
				'lastused' => (int)$row['lastused'],
				'expires'  => (int)$row['expires'],
				'current'  => (int)$row['id'] == (int)$this->token['id'],
			);
		}
		$this->send(array('items' => $items, 'can_create' => fgapi_in_groups($mybb->user, $mybb->settings['fgapi_key_groups'])));
	}

	private function route_keys_create()
	{
		global $mybb, $db;
		if(!fgapi_in_groups($mybb->user, $mybb->settings['fgapi_key_groups']))
		{
			$this->fail('no_permission', 'Deine Benutzergruppe darf keine API-Keys erstellen.', 403);
		}
		if($this->token['kind'] != 'session')
		{
			$this->fail('no_permission', 'Neue Keys können nur nach einem Login mit Passwort erstellt werden.', 403);
		}
		$query = $db->simple_select('fgapi_tokens', 'COUNT(*) AS c', "uid='".(int)$mybb->user['uid']."' AND kind='key'");
		if((int)$db->fetch_field($query, 'c') >= max(1, (int)$mybb->settings['fgapi_max_keys']))
		{
			$this->fail('max_keys', 'Du hast bereits die maximale Anzahl an API-Keys.', 409);
		}
		$label = trim($this->in('label'));
		$key = fgapi_create_token($mybb->user['uid'], 'key', $label === '' ? 'API-Key' : $label, 0);
		$this->send(array('key' => $key, 'message' => 'Kopiere den Key jetzt, er wird nur einmal angezeigt.'), 201);
	}

	private function route_keys_revoke()
	{
		global $mybb, $db;
		$id = $this->in('id', 'int');
		$uid = (int)$mybb->user['uid'];
		if($this->in('all', 'bool'))
		{
			$db->delete_query('fgapi_tokens', "uid='{$uid}' AND id<>'".(int)$this->token['id']."'");
			$this->send(array('revoked' => 'all_other'));
		}
		$query = $db->simple_select('fgapi_tokens', 'id', "id='{$id}' AND uid='{$uid}'");
		if(!$db->fetch_array($query))
		{
			$this->fail('not_found', 'Diesen Zugang gibt es nicht.', 404);
		}
		$db->delete_query('fgapi_tokens', "id='{$id}' AND uid='{$uid}'");
		$this->send(array('revoked' => $id));
	}
}
