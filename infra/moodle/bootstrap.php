<?php
// Laboratory only. Re-running keeps identities and refreshes the managed synthetic grades.
define('CLI_SCRIPT', true);
require('/var/www/html/config.php');
require_once($CFG->dirroot.'/user/lib.php');
\core\session\manager::set_user(get_admin());
require_once($CFG->dirroot.'/course/lib.php');
require_once($CFG->libdir.'/gradelib.php');
require_once($CFG->libdir.'/externallib.php');
require_once($CFG->dirroot.'/webservice/lib.php');
require_once('/opt/sprint2-fixtures.php');
set_config('enablewebservices', 1);
set_config('webserviceprotocols', 'rest');
set_config('enablecompletion', 1);
set_config('authloginviaemail', 1);
assign_capability('webservice/rest:use', CAP_ALLOW, $CFG->defaultuserroleid, context_system::instance()->id, true);
$teacherrole = $DB->get_record('role', ['shortname'=>'editingteacher'], '*', MUST_EXIST);
$studentrole = $DB->get_record('role', ['shortname'=>'student'], '*', MUST_EXIST);
$manual = enrol_get_plugin('manual');
function labuser($username, $firstname, $demopassword = null) {
 global $DB;
 $user = $DB->get_record('user', ['username'=>$username, 'deleted'=>0]);
 if (!$user) {
  $user = (object)['username'=>$username,'password'=>getenv('MOODLE_LAB_PASSWORD'),
   'firstname'=>$firstname,'lastname'=>'Laboratorio','email'=>$username.'@example.invalid',
   'auth'=>'manual','confirmed'=>1,'mnethostid'=>1];
  $user->id = user_create_user($user);
 }
 // Only the two explicit demo accounts have managed, reproducible passwords.
 if ($demopassword !== null && !validate_internal_user_password($user, $demopassword)) {
  update_internal_user_password($user, $demopassword);
 }
 return $user;
}
$teacher=labuser('docente', 'Docente', getenv('MOODLE_TEACHER_PASSWORD') ?: 'Docente123!');
$other=labuser('otrodocente', 'Otro docente');
$students=[];
for($i=1;$i<=12;$i++) $students[]=labuser('estudiante'.str_pad($i,2,'0',STR_PAD_LEFT),'Estudiante '.$i, $i === 1 ? (getenv('MOODLE_STUDENT_PASSWORD') ?: 'Estudiante123!') : null);
// Token creation is granted only to the explicitly seeded laboratory users.
$systemcontext = context_system::instance();
$tokenrole = $DB->get_record('role', ['shortname'=>'edumonitorlab']);
$tokenroleid = $tokenrole ? $tokenrole->id : create_role('EduMonitor laboratory', 'edumonitorlab', 'Local test token creation only');
assign_capability('moodle/webservice:createtoken', CAP_ALLOW, $tokenroleid, $systemcontext->id, true);
foreach (array_merge([$teacher, $other], $students) as $labuser) {
 role_assign($tokenroleid, $labuser->id, $systemcontext->id);
}
$labcourses = ['PROGRAMACION', 'BASES-DATOS', 'MATEMATICA', 'REDES', 'INGENIERIA-SOFTWARE', 'CURSO-RESTRINGIDO'];
foreach($labcourses as $index=>$shortname) {
 $course=$DB->get_record('course',['shortname'=>$shortname]);
 if(!$course) $course=create_course((object)['fullname'=>$shortname,'shortname'=>$shortname,'category'=>1,'format'=>'topics','numsections'=>4,'enablecompletion'=>1]);
 $context=context_course::instance($course->id);
 $instance=$DB->get_record('enrol',['courseid'=>$course->id,'enrol'=>'manual']);
 if(!$instance) { $id=$manual->add_instance($course); $instance=$DB->get_record('enrol',['id'=>$id]); }
 $manual->enrol_user($instance,$shortname==='CURSO-RESTRINGIDO'?$other->id:$teacher->id,$teacherrole->id);
 foreach($students as $student) $manual->enrol_user($instance,$student->id,$studentrole->id);
 // Two partials, four units; explicit 40/30/30 weighted categories in each partial.
 $total=grade_category::fetch_course_category($course->id);
 $total->aggregation=GRADE_AGGREGATE_WEIGHTED_MEAN;
 $total->update();
 // Keep the course's final mark on the same 0-10 scale as its activities.
 $courseitem=grade_item::fetch_course_item($course->id);
 $courseitem->grademin=0;
 $courseitem->grademax=10;
 $courseitem->update();
 $totalitem=$total->get_grade_item();
 $totalitem->grademin=0;
 $totalitem->grademax=10;
 $totalitem->update();
 for($partial=1;$partial<=2;$partial++) {
  $parent=grade_category::fetch(['courseid'=>$course->id,'fullname'=>'Parcial '.$partial]);
  if(!$parent) { $parent=new grade_category(['courseid'=>$course->id,'fullname'=>'Parcial '.$partial,'aggregation'=>GRADE_AGGREGATE_WEIGHTED_MEAN],false); $parent->insert(); $parent->set_parent($total->id); }
  $parent->aggregation=GRADE_AGGREGATE_WEIGHTED_MEAN;
  $parent->update();
  $parent->set_parent($total->id);
  $partialitem=$parent->get_grade_item();
  $partialitem->grademin=0;
  $partialitem->grademax=10;
  $partialitem->aggregationcoef=0.5;
  $partialitem->update();
  foreach(['Examenes'=>0.4,'Autonomo'=>0.3,'APEs'=>0.3] as $kind=>$weight) {
   $category=grade_category::fetch(['courseid'=>$course->id,'fullname'=>'P'.$partial.' '.$kind]);
   if(!$category) { $category=new grade_category(['courseid'=>$course->id,'fullname'=>'P'.$partial.' '.$kind,'aggregation'=>GRADE_AGGREGATE_MEAN],false); $category->insert(); $category->set_parent($parent->id); }
   $category->aggregation=GRADE_AGGREGATE_MEAN;
   $category->update();
   $category->set_parent($parent->id);
   $categoryitem=$category->get_grade_item();
   $categoryitem->grademin=0;
   $categoryitem->grademax=10;
   $categoryitem->aggregationcoef=$weight;
   $categoryitem->update();
   for($unit=($partial-1)*2+1;$unit<=$partial*2;$unit++) {
    $idnumber='EDU-U'.$unit.'-'.$kind;
    $item=grade_item::fetch(['courseid'=>$course->id,'idnumber'=>$idnumber]);
    if(!$item) {
     // Actual assignment modules, visible in course contents and gradebook.
     require_once($CFG->dirroot.'/course/modlib.php');
     $module=$DB->get_record('modules',['name'=>'assign'],'*',MUST_EXIST);
     $info=(object)['course'=>$course->id,'module'=>$module->id,'modulename'=>'assign','name'=>'Unidad '.$unit.' '.$kind,
      'section'=>$unit,'cmidnumber'=>$idnumber,'visible'=>1,'intro'=>'Actividad ficticia de laboratorio','introformat'=>FORMAT_HTML,
      'grade'=>10,'gradecat'=>$category->id,'assignsubmission_onlinetext_enabled'=>1,
      'assignsubmission_file_enabled'=>0,'assignfeedback_comments_enabled'=>1,'submissiondrafts'=>0,
      'requiresubmissionstatement'=>0,'sendnotifications'=>0,'sendlatenotifications'=>0,
      'sendstudentnotifications'=>0,'duedate'=>0,'cutoffdate'=>0,'gradingduedate'=>0,
      'allowsubmissionsfromdate'=>0,'teamsubmission'=>0,'requireallteammemberssubmit'=>0,
      'blindmarking'=>0,'markingworkflow'=>0,'markingallocation'=>0];
     $created=add_moduleinfo($info,$course);
     $item=grade_item::fetch(['courseid'=>$course->id,'itemtype'=>'mod','itemmodule'=>'assign','iteminstance'=>$created->instance]);
     $item->idnumber=$idnumber; $item->update();
    }
    // Refresh existing fixtures too: a restart must not retain the old mixed scales/results.
    $item->grademin=0;
    $item->grademax=10;
    $item->update();
    if ($item->itemmodule === 'assign') {
     $DB->set_field('assign', 'grade', 10, ['id'=>$item->iteminstance, 'course'=>$course->id]);
    }
    foreach($students as $n=>$student) {
     $value = $shortname === 'PROGRAMACION'
      ? ($n===11 ? null : ($n===0 ? 0 : 1 + (($n*3+$unit*5)%45)/10))
      : 7.2 + (($n*7+$unit*3+$index*5)%28)/10;
     $item->update_final_grade($student->id,$value,'edumonitor-lab');
    }
   }
  }
 }
 seed_sprint2_fixtures($course, $students);
 grade_regrade_final_grades($course->id);
}
$service=$DB->get_record('external_services',['shortname'=>'edumonitor'],'*',MUST_EXIST);
// No tokens printed. Obtain user token through Moodle login/token.php (service=edumonitor).
echo "Laboratorio preparado: 6 cursos (5 para docente), 2 docentes, 12 estudiantes; escala 0-10, pesos 40/30/30. Solo PROGRAMACION presenta rendimiento bajo.\n";
