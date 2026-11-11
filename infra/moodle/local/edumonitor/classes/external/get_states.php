<?php
namespace local_edumonitor\external;
use core_external\external_api;
use core_external\external_function_parameters;
use core_external\external_single_structure;
use core_external\external_multiple_structure;
use core_external\external_value;

// Only structured submission evidence; never exports submission text or files.
class get_states extends external_api {
 public static function execute_parameters() {
  return new external_function_parameters(['courseid' => new external_value(PARAM_INT, 'Course')]);
 }
 public static function execute($courseid) {
  global $DB;
  $params = self::validate_parameters(self::execute_parameters(), ['courseid'=>$courseid]);
  $context = \context_course::instance($params['courseid']);
  self::validate_context($context);
  require_capability('moodle/grade:viewall', $context);
  require_capability('moodle/course:viewparticipants', $context);
  $result = [];
  $students = get_enrolled_users($context, 'moodle/grade:view', 0, 'u.id', null, 0, 0, true);
  $modinfo = get_fast_modinfo($params['courseid']);
  foreach ($DB->get_records('assign', ['course'=>$params['courseid']]) as $assignment) {
   $cm = $modinfo->instances['assign'][$assignment->id] ?? null;
   // Group submissions and blind marking need separate identity resolution.
   if (!$cm || !$cm->uservisible || $assignment->teamsubmission || $assignment->blindmarking) continue;
   foreach ($students as $student) {
    $submission = $DB->get_record('assign_submission', ['assignment'=>$assignment->id, 'userid'=>$student->id, 'latest'=>1]);
    $state = 'unknown';
    if ($submission && $submission->status === 'submitted') $state = 'pending';
    else if (!$submission || in_array($submission->status, ['new','draft','reopened'], true)) $state = 'not_submitted';
    $override = $DB->get_record('assign_user_flags', ['assignment'=>$assignment->id, 'userid'=>$student->id]);
    // Dates can have group/user overrides: don't export a misleading deadline.
    $hasoverrides = $DB->record_exists('assign_overrides', ['assignid'=>$assignment->id]);
    $due = $hasoverrides ? 0 : max((int)$assignment->duedate, (int)($override->extensionduedate ?? 0));
    $result[] = ['studentId'=>$student->id, 'instance'=>$assignment->id, 'state'=>$state,
     'dueAt'=>$due, 'submittedAt'=>($state === 'pending' ? $submission->timemodified : 0)];
   }
  }
  return $result;
 }
 public static function execute_returns() {
  return new external_multiple_structure(new external_single_structure([
   'studentId'=>new external_value(PARAM_INT, 'Student'), 'instance'=>new external_value(PARAM_INT, 'Assignment'),
   'state'=>new external_value(PARAM_ALPHAEXT, 'Verified submission state'),
   'dueAt'=>new external_value(PARAM_INT, 'Deadline or zero when unavailable'),
   'submittedAt'=>new external_value(PARAM_INT, 'Submission timestamp or zero')]));
 }
}
