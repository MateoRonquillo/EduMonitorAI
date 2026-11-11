<?php
// Synthetic laboratory data only. Never loaded by production request handlers.
function seed_sprint2_fixtures($course, $students) {
 global $DB, $CFG;
 if ($course->shortname !== 'PROGRAMACION') return;
 require_once($CFG->dirroot.'/course/modlib.php');
 course_create_sections_if_missing($course, 5);
 $DB->set_field('course_sections', 'name', 'Fundamentos verificables Sprint 2', ['course'=>$course->id, 'section'=>5]);
 $module = $DB->get_record('modules', ['name'=>'assign'], '*', MUST_EXIST);
 foreach (['graded'=>'Nota cero y resultados', 'not_submitted'=>'Sin entrega vencida',
           'pending'=>'Entrega pendiente de calificar', 'unknown'=>'Trabajo grupal sin evidencia individual'] as $state=>$name) {
  $idnumber = 'EDU-S2-'.$state;
  $cm = $DB->get_record('course_modules', ['course'=>$course->id, 'idnumber'=>$idnumber]);
  if (!$cm) {
   $info = (object)['course'=>$course->id, 'module'=>$module->id, 'modulename'=>'assign',
    'name'=>'S2 '.$name, 'section'=>5, 'cmidnumber'=>$idnumber, 'visible'=>1,
    'intro'=>'Caso ficticio de aceptación PB 06 a PB 12', 'introformat'=>FORMAT_HTML,
    'grade'=>10, 'assignsubmission_onlinetext_enabled'=>1, 'assignsubmission_file_enabled'=>0,
    'assignfeedback_comments_enabled'=>1, 'submissiondrafts'=>0, 'requiresubmissionstatement'=>0,
    'sendnotifications'=>0, 'sendlatenotifications'=>0, 'sendstudentnotifications'=>0,
    'duedate'=>time()-86400, 'cutoffdate'=>0, 'gradingduedate'=>0, 'allowsubmissionsfromdate'=>0,
    'teamsubmission'=>($state === 'unknown' ? 1 : 0), 'requireallteammemberssubmit'=>0,
    'blindmarking'=>0, 'markingworkflow'=>0, 'markingallocation'=>0];
   $created = add_moduleinfo($info, $course);
   $instance = $created->instance;
   if ($state === 'graded') {
    $item = grade_item::fetch(['courseid'=>$course->id, 'itemtype'=>'mod', 'itemmodule'=>'assign', 'iteminstance'=>$instance]);
    foreach ($students as $n=>$student) $item->update_final_grade($student->id,
     $n === 0 ? 0 : ($n < 8 ? ($n % 4)+1 : 8), 'edumonitor-sprint2-lab');
   }
   if ($state === 'pending') {
    $now = time();
    foreach ($students as $student) $DB->insert_record('assign_submission', (object)[
     'assignment'=>$instance, 'userid'=>$student->id, 'timecreated'=>$now, 'timemodified'=>$now,
     'status'=>'submitted', 'groupid'=>0, 'attemptnumber'=>0, 'latest'=>1]);
   }
  }
 }
 rebuild_course_cache($course->id, true);
}
