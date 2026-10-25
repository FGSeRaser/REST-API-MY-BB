<?php
/**
 * FG Forum API - Endpunkt
 *
 * Aufruf: https://dein-forum.de/api/?route=forums
 * Doku:   https://github.com/FGSeRaser/REST-API-MY-BB
 *
 * Autor:  FGSeRaser - https://github.com/FGSeRaser
 */

define('IN_MYBB', 1);
define('THIS_SCRIPT', 'api/index.php');
define('FGAPI_REQUEST', 1);

// Everything that might be printed by PHP warnings is thrown away before the JSON answer
ob_start();

$working_dir = dirname(__DIR__);
chdir($working_dir);
require_once $working_dir.'/inc/init.php';

$error_handler->force_display_errors = false;

require_once MYBB_ROOT.'inc/plugins/fgapi/api.php';

$api = new FGApi();
$api->run();
