<?php
 defined('MOODLE_INTERNAL') || die();
$functions = ['local_edumonitor_get_access' => [
 'classname' => 'local_edumonitor\external\get_access', 'methodname' => 'execute',
 'description' => 'Read effective teacher course permissions.', 'type' => 'read', 'ajax' => false
], 'local_edumonitor_get_states' => [
 'classname' => 'local_edumonitor\external\get_states', 'methodname' => 'execute',
 'description' => 'Read authorized assignment submission states.', 'type' => 'read', 'ajax' => false
]];
$services = ['EduMonitor' => ['functions' => ['local_edumonitor_get_access',
 'core_webservice_get_site_info', 'core_enrol_get_enrolled_users', 'core_course_get_contents',
 'gradereport_user_get_grade_items', 'local_edumonitor_get_states'], 'restrictedusers' => 0, 'enabled' => 1, 'shortname' => 'edumonitor']];
