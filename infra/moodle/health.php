<?php
// Readiness must load Moodle as the web-server user and reach its database.
define('NO_DEBUG_DISPLAY', true);
define('NO_MOODLE_COOKIES', true);
try {
    @require(__DIR__.'/config.php');
    if (!$DB->record_exists('external_services', ['shortname'=>'edumonitor', 'enabled'=>1])) {
        throw new RuntimeException('Service not ready');
    }
    header('Content-Type: text/plain');
    echo "Healthy\n";
} catch (Throwable $error) {
    http_response_code(503);
    header('Content-Type: text/plain');
    echo "Unhealthy\n";
}
