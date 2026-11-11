<?php
namespace local_edumonitor\external;
use core_external\external_api;
use core_external\external_function_parameters;
use core_external\external_single_structure;
use core_external\external_multiple_structure;
use core_external\external_value;
class get_access extends external_api {
 public static function execute_parameters() { return new external_function_parameters([]); }
 public static function execute() {
  global $USER;
  self::validate_context(\context_user::instance($USER->id));
  $courses = [];
  foreach (enrol_get_users_courses($USER->id, true) as $course) {
   $context = \context_course::instance($course->id);
   if (has_capability('moodle/grade:viewall', $context) &&
       has_capability('moodle/course:viewparticipants', $context)) {
    $courses[] = ['id' => $course->id, 'name' => $course->fullname];
   }
  }
  return ['userId' => $USER->id, 'name' => fullname($USER), 'courses' => $courses];
 }
 public static function execute_returns() {
  return new external_single_structure(['userId' => new external_value(PARAM_INT, 'User'),
   'name' => new external_value(PARAM_TEXT, 'Name'),
   'courses' => new external_multiple_structure(new external_single_structure([
    'id' => new external_value(PARAM_INT, 'Course'), 'name' => new external_value(PARAM_TEXT, 'Name')]))]);
 }
}
