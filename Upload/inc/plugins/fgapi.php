<?php
/**
 * FG Forum API for MyBB 1.8
 *
 * REST-API für den FG Forum Client: Login mit Benutzername/Passwort oder API-Key,
 * Foren, Themen, Beiträge, Anhänge, private Nachrichten, Suche und Moderation.
 *
 * Autor:  FGSeRaser - https://github.com/FGSeRaser
 * Lizenz: MIT
 */

if(!defined('IN_MYBB'))
{
	die('Direct access to this file is not allowed.');
}

define('FGAPI_VERSION', '2.0.0');

$plugins->add_hook('usercp_menu', 'fgapi_usercp_menu', 40);
$plugins->add_hook('usercp_start', 'fgapi_usercp');

function fgapi_info()
{
	return array(
		'name'          => 'FG Forum API',
		'description'   => 'REST-API für den FG Forum Client: Login per Passwort oder API-Key, Themen, Beiträge, Anhänge, PNs, Suche und Moderation. Keys und Sitzungen verwaltet jeder Benutzer im Benutzer-CP.',
		'website'       => 'https://github.com/FGSeRaser/REST-API-MY-BB',
		'author'        => 'FGSeRaser',
		'authorsite'    => 'https://github.com/FGSeRaser',
		'version'       => FGAPI_VERSION,
		'compatibility' => '18*',
		'codename'      => 'fgapi',
	);
}

// ---------------------------------------------------------------------
//  Install / uninstall
// ---------------------------------------------------------------------

function fgapi_install()
{
	global $db;

	fgapi_uninstall_tables();
	$collation = $db->build_create_table_collation();

	switch($db->type)
	{
		case 'pgsql':
		case 'pgsql_pdo':
			$db->write_query("CREATE TABLE ".TABLE_PREFIX."fgapi_tokens (
				id serial,
				uid int NOT NULL default '0',
				kind varchar(10) NOT NULL default 'session',
				label varchar(100) NOT NULL default '',
				tokenhash varchar(64) NOT NULL default '',
				hint varchar(12) NOT NULL default '',
				ip varchar(50) NOT NULL default '',
				created int NOT NULL default '0',
				lastused int NOT NULL default '0',
				expires int NOT NULL default '0',
				PRIMARY KEY (id),
				UNIQUE (tokenhash)
			);");
			$db->write_query("CREATE INDEX ".TABLE_PREFIX."fgapi_tokens_uid ON ".TABLE_PREFIX."fgapi_tokens (uid);");
			$db->write_query("CREATE TABLE ".TABLE_PREFIX."fgapi_attempts (
				id serial,
				ip varchar(50) NOT NULL default '',
				dateline int NOT NULL default '0',
				PRIMARY KEY (id)
			);");
			break;
		case 'sqlite':
			$db->write_query("CREATE TABLE ".TABLE_PREFIX."fgapi_tokens (
				id INTEGER PRIMARY KEY,
				uid int NOT NULL default '0',
				kind varchar(10) NOT NULL default 'session',
				label varchar(100) NOT NULL default '',
				tokenhash varchar(64) NOT NULL default '' UNIQUE,
				hint varchar(12) NOT NULL default '',
				ip varchar(50) NOT NULL default '',
				created int NOT NULL default '0',
				lastused int NOT NULL default '0',
				expires int NOT NULL default '0'
			);");
			$db->write_query("CREATE TABLE ".TABLE_PREFIX."fgapi_attempts (
				id INTEGER PRIMARY KEY,
				ip varchar(50) NOT NULL default '',
				dateline int NOT NULL default '0'
			);");
			break;
		default:
			$db->write_query("CREATE TABLE ".TABLE_PREFIX."fgapi_tokens (
				id int unsigned NOT NULL auto_increment,
				uid int unsigned NOT NULL default '0',
				kind varchar(10) NOT NULL default 'session',
				label varchar(100) NOT NULL default '',
				tokenhash varchar(64) NOT NULL default '',
				hint varchar(12) NOT NULL default '',
				ip varchar(50) NOT NULL default '',
				created int unsigned NOT NULL default '0',
				lastused int unsigned NOT NULL default '0',
				expires int unsigned NOT NULL default '0',
				PRIMARY KEY (id),
				UNIQUE KEY tokenhash (tokenhash),
				KEY uid (uid)
			) ENGINE=InnoDB{$collation};");
			$db->write_query("CREATE TABLE ".TABLE_PREFIX."fgapi_attempts (
				id int unsigned NOT NULL auto_increment,
				ip varchar(50) NOT NULL default '',
				dateline int unsigned NOT NULL default '0',
				PRIMARY KEY (id),
				KEY ip (ip)
			) ENGINE=InnoDB{$collation};");
	}

	// Settings
	$query = $db->simple_select('settinggroups', 'COUNT(*) AS c');
	$order = (int)$db->fetch_field($query, 'c') + 1;
	$gid = $db->insert_query('settinggroups', array(
		'name'        => 'fgapi',
		'title'       => $db->escape_string('FG Forum API'),
		'description' => $db->escape_string('Einstellungen für die REST-API und den FG Forum Client.'),
		'disporder'   => $order,
		'isdefault'   => 0,
	));

	$settings = array(
		'fgapi_enabled'        => array('API aktiv', 'Schaltet die komplette API ein oder aus.', 'yesno', '1'),
		'fgapi_password_login' => array('Login mit Benutzername und Passwort', 'Erlaubt den Login im Client mit den normalen Forendaten. Bei "Nein" geht es nur mit API-Key.', 'yesno', '1'),
		'fgapi_api_groups'     => array('Gruppen mit API-Zugang', 'Welche Benutzergruppen die API (und damit den Client) benutzen dürfen.', 'groupselect', '-1'),
		'fgapi_key_groups'     => array('Gruppen, die API-Keys erstellen dürfen', 'Diese Gruppen sehen im Benutzer-CP die Seite "Forum-Client & API" und können Keys erzeugen.', 'groupselect', '2,3,4,6'),
		'fgapi_max_keys'       => array('Maximale Keys pro Benutzer', 'Wie viele API-Keys ein Benutzer gleichzeitig haben darf.', 'numeric', '5'),
		'fgapi_token_days'     => array('Gültigkeit einer Anmeldung (Tage)', 'Nach so vielen Tagen ohne Nutzung läuft eine Client-Anmeldung ab. 0 = nie.', 'numeric', '30'),
		'fgapi_login_limit'    => array('Fehlversuche pro IP (15 Minuten)', 'Nach so vielen falschen Logins wird die IP für 15 Minuten gesperrt. Zusätzlich gilt die normale MyBB-Sperre pro Konto.', 'numeric', '10'),
		'fgapi_require_https'  => array('Nur über HTTPS', 'Lehnt Anfragen ohne HTTPS ab. Sehr empfohlen, sobald dein Forum ein Zertifikat hat.', 'yesno', '0'),
		'fgapi_latest_max'     => array('Maximale Anzahl "Neueste Beiträge"', 'Obergrenze für die Liste der letzten Beiträge im Client.', 'numeric', '50'),
	);
	$disporder = 1;
	foreach($settings as $name => $setting)
	{
		$db->insert_query('settings', array(
			'name'        => $name,
			'title'       => $db->escape_string($setting[0]),
			'description' => $db->escape_string($setting[1]),
			'optionscode' => $setting[2],
			'value'       => $db->escape_string($setting[3]),
			'disporder'   => $disporder++,
			'gid'         => (int)$gid,
		));
	}
	rebuild_settings();
}

function fgapi_is_installed()
{
	global $db;
	return $db->table_exists('fgapi_tokens');
}

function fgapi_uninstall()
{
	global $db;

	fgapi_uninstall_tables();
	$db->delete_query('settings', "name LIKE 'fgapi_%'");
	$db->delete_query('settinggroups', "name='fgapi'");
	rebuild_settings();
}

function fgapi_uninstall_tables()
{
	global $db;
	if($db->table_exists('fgapi_tokens'))
	{
		$db->drop_table('fgapi_tokens');
	}
	if($db->table_exists('fgapi_attempts'))
	{
		$db->drop_table('fgapi_attempts');
	}
}

// ---------------------------------------------------------------------
//  Activate / deactivate (templates)
// ---------------------------------------------------------------------

function fgapi_activate()
{
	global $db;

	fgapi_deactivate();
	foreach(fgapi_templates() as $title => $template)
	{
		$db->insert_query('templates', array(
			'title'    => $db->escape_string($title),
			'template' => $db->escape_string($template),
			'sid'      => -2,
			'version'  => '1800',
			'dateline' => TIME_NOW,
		));
	}
}

function fgapi_deactivate()
{
	global $db;
	$db->delete_query('templates', "title IN ('fgapi_usercp','fgapi_usercp_nav','fgapi_usercp_row','fgapi_usercp_newkey','fgapi_usercp_empty','fgapi_usercp_createform')");
}

function fgapi_templates()
{
	return array(
		'fgapi_usercp_nav' => '<tr>
	<td class="tcat tcat_menu">
		<div><span class="smalltext"><strong>{$lang->fgapi_nav_title}</strong></span></div>
	</td>
</tr>
<tr><td class="trow1 smalltext"><a href="usercp.php?action=fgapi" class="usercp_nav_item usercp_nav_options">{$lang->fgapi_nav_link}</a></td></tr>',

		'fgapi_usercp' => '<html>
<head>
<title>{$mybb->settings[\'bbname\']} - {$lang->fgapi_title}</title>
{$headerinclude}
</head>
<body>
{$header}
<table width="100%" border="0" align="center">
<tr>
	{$usercpnav}
	<td valign="top">
		{$fgapi_message}
		<table border="0" cellspacing="{$theme[\'borderwidth\']}" cellpadding="{$theme[\'tablespace\']}" class="tborder">
			<tr><td class="thead" colspan="2"><strong>{$lang->fgapi_title}</strong></td></tr>
			<tr><td class="trow1" colspan="2">{$lang->fgapi_intro}<br /><br />
				<strong>{$lang->fgapi_endpoint}:</strong> <code>{$fgapi_endpoint}</code><br />
				<span class="smalltext">{$lang->fgapi_client_hint}</span>
			</td></tr>
		</table>
		<br />
		{$fgapi_createform}
		<table border="0" cellspacing="{$theme[\'borderwidth\']}" cellpadding="{$theme[\'tablespace\']}" class="tborder">
			<tr><td class="thead" colspan="5"><strong>{$lang->fgapi_active}</strong></td></tr>
			<tr>
				<td class="tcat"><span class="smalltext"><strong>{$lang->fgapi_col_name}</strong></span></td>
				<td class="tcat"><span class="smalltext"><strong>{$lang->fgapi_col_type}</strong></span></td>
				<td class="tcat"><span class="smalltext"><strong>{$lang->fgapi_col_created}</strong></span></td>
				<td class="tcat"><span class="smalltext"><strong>{$lang->fgapi_col_lastused}</strong></span></td>
				<td class="tcat" align="center"><span class="smalltext"><strong>{$lang->fgapi_col_action}</strong></span></td>
			</tr>
			{$fgapi_rows}
		</table>
		<br />
		<form action="usercp.php" method="post">
			<input type="hidden" name="my_post_key" value="{$mybb->post_code}" />
			<input type="hidden" name="action" value="fgapi" />
			<input type="hidden" name="do" value="revokeall" />
			<div align="center"><input type="submit" class="button" value="{$lang->fgapi_revoke_all}" /></div>
		</form>
		<br />
		<div class="smalltext" align="center">FG Forum API {$fgapi_version} &middot; <a href="https://github.com/FGSeRaser" target="_blank" rel="noopener">FGSeRaser</a></div>
	</td>
</tr>
</table>
{$footer}
</body>
</html>',

		'fgapi_usercp_createform' => '<form action="usercp.php" method="post">
<input type="hidden" name="my_post_key" value="{$mybb->post_code}" />
<input type="hidden" name="action" value="fgapi" />
<input type="hidden" name="do" value="createkey" />
<table border="0" cellspacing="{$theme[\'borderwidth\']}" cellpadding="{$theme[\'tablespace\']}" class="tborder">
	<tr><td class="thead" colspan="2"><strong>{$lang->fgapi_newkey}</strong></td></tr>
	<tr>
		<td class="trow1" width="40%"><strong>{$lang->fgapi_newkey_label}</strong><br /><span class="smalltext">{$lang->fgapi_newkey_desc}</span></td>
		<td class="trow1"><input type="text" class="textbox" name="label" size="35" maxlength="100" placeholder="z. B. PC Zuhause" /> <input type="submit" class="button" value="{$lang->fgapi_newkey_button}" /></td>
	</tr>
</table>
</form>
<br />',

		'fgapi_usercp_newkey' => '<div class="pm_alert" style="background:#e9f7ec;border:1px solid #6cbf7f;padding:10px;margin-bottom:10px">
	<strong>{$lang->fgapi_newkey_created}</strong><br />
	<input type="text" class="textbox" style="width:98%;font-family:monospace;font-size:14px;margin-top:6px" readonly="readonly" value="{$fgapi_newkey}" onclick="this.select()" /><br />
	<span class="smalltext">{$lang->fgapi_newkey_once}</span>
</div>',

		'fgapi_usercp_row' => '<tr>
	<td class="{$altbg}">{$row_label}<br /><span class="smalltext">&hellip;{$row_hint}</span></td>
	<td class="{$altbg}">{$row_type}</td>
	<td class="{$altbg}"><span class="smalltext">{$row_created}</span></td>
	<td class="{$altbg}"><span class="smalltext">{$row_lastused}<br />{$row_ip}</span></td>
	<td class="{$altbg}" align="center">
		<form action="usercp.php" method="post" style="margin:0">
			<input type="hidden" name="my_post_key" value="{$mybb->post_code}" />
			<input type="hidden" name="action" value="fgapi" />
			<input type="hidden" name="do" value="revoke" />
			<input type="hidden" name="id" value="{$row_id}" />
			<input type="submit" class="button" value="{$lang->fgapi_revoke}" />
		</form>
	</td>
</tr>',

		'fgapi_usercp_empty' => '<tr><td class="trow1" colspan="5" align="center">{$lang->fgapi_none}</td></tr>',
	);
}

// ---------------------------------------------------------------------
//  Helpers shared with the API
// ---------------------------------------------------------------------

/** Loads the language file without dying if a translation is missing. */
function fgapi_load_lang()
{
	global $lang;
	if(isset($lang->fgapi_title))
	{
		return;
	}
	$lang->load('fgapi', false, true);
	if(!isset($lang->fgapi_title))
	{
		// Fallback if the board language has no translation
		$defaults = array(
			'fgapi_nav_title' => 'Forum-Client',
			'fgapi_nav_link' => 'Forum-Client & API',
			'fgapi_title' => 'Forum-Client & API',
			'fgapi_intro' => 'Hier verwaltest du deine API-Keys und alle Geräte, auf denen du im FG Forum Client angemeldet bist.',
			'fgapi_endpoint' => 'API-Adresse',
			'fgapi_client_hint' => 'Im Client reicht die Adresse deines Forums. Du kannst dich mit Benutzername und Passwort oder mit einem API-Key anmelden.',
			'fgapi_active' => 'Aktive Keys und Anmeldungen',
			'fgapi_col_name' => 'Name',
			'fgapi_col_type' => 'Art',
			'fgapi_col_created' => 'Erstellt',
			'fgapi_col_lastused' => 'Zuletzt benutzt',
			'fgapi_col_action' => 'Aktion',
			'fgapi_type_key' => 'API-Key',
			'fgapi_type_session' => 'Client-Anmeldung',
			'fgapi_revoke' => 'Widerrufen',
			'fgapi_revoke_all' => 'Alle Keys und Anmeldungen widerrufen',
			'fgapi_none' => 'Noch keine Keys oder Anmeldungen.',
			'fgapi_never' => 'noch nie',
			'fgapi_newkey' => 'Neuen API-Key erstellen',
			'fgapi_newkey_label' => 'Name für den Key',
			'fgapi_newkey_desc' => 'Damit du später weißt, wo der Key benutzt wird.',
			'fgapi_newkey_button' => 'Key erstellen',
			'fgapi_newkey_created' => 'Dein neuer API-Key:',
			'fgapi_newkey_once' => 'Kopiere den Key jetzt. Aus Sicherheitsgründen wird er nur dieses eine Mal angezeigt.',
			'fgapi_error_maxkeys' => 'Du hast bereits die maximale Anzahl an API-Keys. Widerrufe zuerst einen alten Key.',
			'fgapi_revoked' => 'Der Zugang wurde widerrufen.',
			'fgapi_revoked_all' => 'Alle Keys und Anmeldungen wurden widerrufen.',
			'fgapi_no_permission' => 'Deine Benutzergruppe darf keine API-Keys erstellen.',
			'fgapi_disabled' => 'Die API ist im Moment abgeschaltet.',
		);
		foreach($defaults as $key => $value)
		{
			$lang->$key = $value;
		}
	}
}

/** Is the user in one of the groups of a groupselect setting? */
function fgapi_in_groups($user, $setting)
{
	$value = trim((string)$setting);
	if($value === '-1')
	{
		return true;
	}
	if($value === '')
	{
		return false;
	}
	$allowed = array_map('intval', explode(',', $value));
	$groups = array((int)$user['usergroup']);
	if(!empty($user['additionalgroups']))
	{
		$groups = array_merge($groups, array_map('intval', explode(',', $user['additionalgroups'])));
	}
	return count(array_intersect($allowed, $groups)) > 0;
}

/** Creates a new random secret, stores only its hash and returns the secret. */
function fgapi_create_token($uid, $kind, $label, $expires = 0)
{
	global $db;

	$prefix = $kind == 'key' ? 'fgk_' : 'fgs_';
	$secret = $prefix.random_str(44, false);
	$db->insert_query('fgapi_tokens', array(
		'uid'       => (int)$uid,
		'kind'      => $kind == 'key' ? 'key' : 'session',
		'label'     => $db->escape_string(my_substr(trim($label), 0, 100)),
		'tokenhash' => hash('sha256', $secret),
		'hint'      => $db->escape_string(substr($secret, -4)),
		'ip'        => $db->escape_string(get_ip()),
		'created'   => TIME_NOW,
		'lastused'  => 0,
		'expires'   => (int)$expires,
	));
	return $secret;
}

// ---------------------------------------------------------------------
//  User CP
// ---------------------------------------------------------------------

function fgapi_usercp_menu()
{
	global $mybb, $templates, $lang, $usercpmenu;

	if(empty($mybb->settings['fgapi_enabled']) || !fgapi_in_groups($mybb->user, $mybb->settings['fgapi_api_groups']))
	{
		return;
	}
	fgapi_load_lang();
	eval("\$usercpmenu .= \"".$templates->get('fgapi_usercp_nav')."\";");
}

function fgapi_usercp()
{
	global $mybb, $db, $lang, $templates, $theme, $headerinclude, $header, $footer, $usercpnav;

	if($mybb->get_input('action') != 'fgapi')
	{
		return;
	}

	fgapi_load_lang();
	add_breadcrumb($lang->fgapi_title, 'usercp.php?action=fgapi');

	if(empty($mybb->settings['fgapi_enabled']))
	{
		error($lang->fgapi_disabled);
	}
	if(!fgapi_in_groups($mybb->user, $mybb->settings['fgapi_api_groups']))
	{
		error_no_permission();
	}

	$uid = (int)$mybb->user['uid'];
	$can_create = fgapi_in_groups($mybb->user, $mybb->settings['fgapi_key_groups']);
	$fgapi_message = '';
	$fgapi_newkey = '';

	if($mybb->request_method == 'post')
	{
		verify_post_check($mybb->get_input('my_post_key'));
		$do = $mybb->get_input('do');

		if($do == 'createkey')
		{
			if(!$can_create)
			{
				error($lang->fgapi_no_permission);
			}
			$query = $db->simple_select('fgapi_tokens', 'COUNT(*) AS c', "uid='{$uid}' AND kind='key'");
			$count = (int)$db->fetch_field($query, 'c');
			$max = max(1, (int)$mybb->settings['fgapi_max_keys']);
			if($count >= $max)
			{
				$fgapi_message = inline_error(array($lang->fgapi_error_maxkeys));
			}
			else
			{
				$label = trim($mybb->get_input('label'));
				if($label === '')
				{
					$label = 'API-Key';
				}
				$fgapi_newkey = htmlspecialchars_uni(fgapi_create_token($uid, 'key', $label, 0));
				eval("\$fgapi_message = \"".$templates->get('fgapi_usercp_newkey')."\";");
			}
		}
		elseif($do == 'revoke')
		{
			$id = $mybb->get_input('id', MyBB::INPUT_INT);
			$db->delete_query('fgapi_tokens', "id='{$id}' AND uid='{$uid}'");
			$fgapi_message = '<div class="pm_alert">'.$lang->fgapi_revoked.'</div>';
		}
		elseif($do == 'revokeall')
		{
			$db->delete_query('fgapi_tokens', "uid='{$uid}'");
			$fgapi_message = '<div class="pm_alert">'.$lang->fgapi_revoked_all.'</div>';
		}
	}

	$fgapi_rows = '';
	$query = $db->simple_select('fgapi_tokens', '*', "uid='{$uid}'", array('order_by' => 'created', 'order_dir' => 'DESC'));
	while($row = $db->fetch_array($query))
	{
		$altbg = alt_trow();
		$row_id = (int)$row['id'];
		$row_label = htmlspecialchars_uni($row['label']);
		$row_hint = htmlspecialchars_uni($row['hint']);
		$row_type = $row['kind'] == 'key' ? $lang->fgapi_type_key : $lang->fgapi_type_session;
		$row_created = my_date('relative', $row['created']);
		$row_lastused = $row['lastused'] ? my_date('relative', $row['lastused']) : $lang->fgapi_never;
		$row_ip = htmlspecialchars_uni($row['ip']);
		eval("\$fgapi_rows .= \"".$templates->get('fgapi_usercp_row')."\";");
	}
	if($fgapi_rows === '')
	{
		eval("\$fgapi_rows = \"".$templates->get('fgapi_usercp_empty')."\";");
	}

	$fgapi_createform = '';
	if($can_create)
	{
		eval("\$fgapi_createform = \"".$templates->get('fgapi_usercp_createform')."\";");
	}

	$fgapi_endpoint = htmlspecialchars_uni($mybb->settings['bburl'].'/api/');
	$fgapi_version = FGAPI_VERSION;

	eval("\$page = \"".$templates->get('fgapi_usercp')."\";");
	output_page($page);
	exit;
}
