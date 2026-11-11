import { useEffect, useState } from 'react';
import { Link, useOutletContext, useParams } from 'react-router';
import { AcademicProfile, AlertCards, Statistics, stateLabels } from './AcademicPanel';
import { GradeComparison, GradeDonut, formatGrade, gradeFromStats } from './Charts';

export const courseTitle = name => ({ PROGRAMACION:'Programación', 'BASES-DATOS':'Bases de Datos', MATEMATICA:'Matemática', REDES:'Redes', 'INGENIERIA-SOFTWARE':'Ingeniería de Software' }[name] ?? name);
const courseUrl = id => `/courses/${id}/summary`;

export function useCourseReports(courses, syncByCourse, request) {
  const [reports, setReports] = useState({});
  const version = courses.map(c => `${c.id}:${syncByCourse[String(c.id)]?.snapshot?.syncedAt ?? ''}`).join('|');
  useEffect(() => {
    const controller = new AbortController();
    setReports({});
    for (const course of courses) {
      if (!syncByCourse[String(course.id)]?.snapshot) continue;
      request(`/academic/api/courses/${course.id}/analysis`, { signal:controller.signal })
        .then(data => { if (!controller.signal.aborted) setReports(current => ({ ...current, [course.id]:{ data } })); })
        .catch(error => { if (!controller.signal.aborted) setReports(current => ({ ...current, [course.id]:{ error:error.message } })); });
    }
    return () => controller.abort();
  }, [version, courses, request]);
  return reports;
}

function CourseCards({ courses, syncByCourse, reports }) {
  return <div className="overview-courses">{courses.map(course => {
    const info = syncByCourse[String(course.id)];
    const report = reports[course.id]?.data;
    const score = gradeFromStats(report?.statistics);
    return <article className={`overview-course ${score != null && score < 7 ? 'needs-review' : ''}`} key={course.id}>
      <div className="overview-course-top"><span className="course-symbol" aria-hidden="true">{courseTitle(course.name).charAt(0)}</span><span className={`course-condition ${score == null ? '' : score < 7 ? 'attention' : 'positive'}`}>{score == null ? 'Pendiente' : score < 7 ? 'Requiere revisión' : 'Buen rendimiento'}</span></div>
      <h3><Link to={courseUrl(course.id)}>{courseTitle(course.name)}</Link></h3>
      <div className="course-score">{formatGrade(score)}{score != null && <small> / 10</small>}</div>
      <p>{report ? `${report.students} estudiantes · ${report.activityResults.length} actividades evaluables` : info?.snapshot ? 'Consultando indicadores…' : 'Sincroniza para consultar indicadores.'}</p>
      {reports[course.id]?.error && <p role="alert">{reports[course.id].error}</p>}
      <div className="course-card-footer"><span>{report?.alerts.length ?? '—'} alertas</span><Link to={courseUrl(course.id)}>Explorar curso →</Link></div>
    </article>;
  })}</div>;
}

export function OverviewPage() {
  const { courses, syncByCourse, reports, syncAll, busy } = useOutletContext();
  const available = courses.map(c => reports[c.id]?.data).filter(Boolean);
  const observations = available.reduce((sum, r) => sum + r.statistics.observations, 0);
  const mean = observations ? available.reduce((sum, r) => sum + (gradeFromStats(r.statistics) ?? 0) * r.statistics.observations, 0) / observations : null;
  const distribution = available.reduce((sum, r) => { for (const key of Object.keys(sum)) sum[key] += r.statistics.distribution[key] ?? 0; return sum; }, { below7:0, from7To89:0, from9To10:0 });
  const studentIds = new Set(courses.flatMap(c => syncByCourse[String(c.id)]?.snapshot?.students.map(s => s.id) ?? []));
  const loadedSnapshots = courses.filter(c => syncByCourse[String(c.id)]?.snapshot).length;
  return <div className="overview-page">
    <section className="overview-welcome"><div><span className="eyebrow">UNA MIRADA A TUS CURSOS</span><h2>Tu panorama académico</h2><p>Encuentra lo que marcha bien y dónde conviene acompañar de cerca.</p></div><button className="button button-primary" disabled={busy.size > 0} onClick={syncAll}>{busy.size > 0 ? 'Actualizando…' : '↻ Sincronizar ahora'}</button></section>
    <div className="overview-kpis"><article><span>Cursos autorizados</span><strong>{courses.length}</strong><small>{available.length} con indicadores disponibles</small></article><article><span>Estudiantes únicos</span><strong>{studentIds.size}</strong><small>En los cursos sincronizados</small></article><article><span>Promedio de actividades</span><strong>{formatGrade(mean)}<small>{mean != null && ' / 10'}</small></strong><small>Sobre {observations} notas válidas</small></article><article><span>Situaciones para revisar</span><strong>{available.reduce((sum,r) => sum + r.alerts.length, 0)}</strong><Link to="/support">Ir a acompañamiento →</Link></article></div>
    <p className="overview-coverage" role="status">Cobertura: {available.length} de {courses.length} cursos con indicadores. {loadedSnapshots < courses.length && 'Sincroniza los cursos pendientes para completar el panorama.'} Cada indicador conserva la última actualización de su curso.</p>
    <div className="overview-charts"><div className="visual-card"><GradeComparison title="Rendimiento por curso" points={courses.map(c => ({ label:courseTitle(c.name), value:gradeFromStats(reports[c.id]?.data?.statistics), href:courseUrl(c.id) }))} /></div><div className="visual-card"><GradeDonut distribution={distribution} observations={observations} /></div></div>
    <div className="section-heading"><div><span className="eyebrow">EXPLORA Y DA EL SIGUIENTE PASO</span><h2>Tus cursos</h2></div><Link className="text-button" to="/courses">Ver todos →</Link></div>
    <CourseCards courses={courses} syncByCourse={syncByCourse} reports={reports} />
    <section className="visual-card activity-shortcuts"><h3>Actividades que requieren revisión</h3><p>Abre una actividad para ver sus resultados, entregas y estudiantes.</p><div className="activity-shortcut-grid">{available.flatMap(r => r.activityResults.filter(a => a.statistics.observations > 0 && gradeFromStats(a.statistics) < 7).map(a => ({ ...a, courseId:r.courseId, courseName:r.courseName }))).slice(0, 8).map(a => <Link key={`${a.courseId}-${a.itemId}`} to={`/courses/${a.courseId}/activities/${a.itemId}`}><span>{courseTitle(a.courseName)}</span><strong>{a.name}</strong><b>{formatGrade(gradeFromStats(a.statistics))} / 10 →</b></Link>)}</div>{!available.some(r => r.activityResults.some(a => a.statistics.observations > 0 && gradeFromStats(a.statistics) < 7)) && <p>No hay actividades con promedio inferior a 7 en los datos disponibles.</p>}</section>
    <p className="table-note">La media general pondera cada observación válida, sin contar dos veces los totales. No es la calificación oficial ponderada de Moodle. Los datos ausentes nunca se convierten en cero.</p>
  </div>;
}

export function CourseDirectory() {
  const { courses, syncByCourse, reports } = useOutletContext();
  const [search, setSearch] = useState('');
  const visible = courses.filter(c => courseTitle(c.name).toLocaleLowerCase('es').includes(search.toLocaleLowerCase('es')));
  return <section><div className="directory-heading"><div><h2>Explora tus cursos</h2><p>Consulta actividades, resultados y contenidos que necesitan refuerzo.</p></div><label className="select-label">Buscar curso<input className="workspace-input" type="search" value={search} onChange={e => setSearch(e.target.value)} placeholder="Nombre del curso" /></label></div><CourseCards courses={visible} syncByCourse={syncByCourse} reports={reports} />{!visible.length && <p>No hay cursos que coincidan con tu búsqueda.</p>}</section>;
}

export function SupportPage() {
  const { courses, syncByCourse, reports, request } = useOutletContext();
  const [courseId, setCourseId] = useState('all');
  const [search, setSearch] = useState('');
  const [onlyAlerts, setOnlyAlerts] = useState(true);
  const rows = courses.filter(c => courseId === 'all' || String(c.id) === courseId).flatMap(course => {
    const snapshot = syncByCourse[String(course.id)]?.snapshot;
    const report = reports[course.id]?.data;
    return (snapshot?.students ?? []).map(student => ({ course, student, snapshot, report, alerts:report?.alerts.filter(a => a.studentId === student.id) ?? [] }));
  }).filter(row => row.student.name.toLocaleLowerCase('es').includes(search.trim().toLocaleLowerCase('es')) && (!onlyAlerts || row.alerts.length > 0));
  return <section className="support-page"><div className="directory-heading"><div><h2>Acompaña a cada estudiante</h2><p>Revisa evidencias por curso, sin mezclar calificaciones de asignaturas distintas.</p></div></div><div className="academic-filters"><label className="select-label">Curso<select value={courseId} onChange={e => setCourseId(e.target.value)}><option value="all">Todos mis cursos</option>{courses.map(c => <option key={c.id} value={c.id}>{courseTitle(c.name)}</option>)}</select></label><label className="select-label">Buscar estudiante<input type="search" value={search} onChange={e => setSearch(e.target.value)} placeholder="Nombre del estudiante" /></label><label className="checkbox-label"><input type="checkbox" checked={onlyAlerts} onChange={e => setOnlyAlerts(e.target.checked)} />Solo con alertas</label><span role="status">{rows.length} {rows.length === 1 ? 'ficha por curso' : 'fichas por curso'}</span></div>
    <div className="support-students">{rows.map(row => <SupportStudentCard key={`${row.course.id}-${row.student.id}`} row={row} request={request} />)}</div>
    {!rows.length && <div className="alerts-empty"><p>No hay fichas que coincidan. Comprueba los filtros y sincroniza los cursos pendientes.</p></div>}
  </section>;
}

function SupportStudentCard({ row, request }) {
  const [open, setOpen] = useState(false);
  return <details className="support-student" onToggle={event => setOpen(event.currentTarget.open)}><summary><span className="alert-avatar" aria-hidden="true">{row.student.name.charAt(0)}</span><span><strong>{row.student.name}</strong><small>{courseTitle(row.course.name)}</small></span><span className={`alert-badge ${row.alerts.length ? 'low' : ''}`}>{row.report ? row.alerts.length ? `${row.alerts.length} ${row.alerts.length === 1 ? 'situación' : 'situaciones'} para revisar` : 'Sin alertas' : 'Indicadores no disponibles'}</span><span aria-hidden="true">⌄</span></summary><div className="support-profile">{open && <AcademicProfile course={row.course} studentId={row.student.id} snapshot={row.snapshot} request={request} />}</div></details>;
}
export function ActivityPage() {
  const { courseId, itemId } = useParams();
  const { courses, syncByCourse, reports } = useOutletContext();
  const course = courses.find(c => String(c.id) === courseId);
  const snapshot = course && syncByCourse[courseId]?.snapshot;
  const report = reports[courseId]?.data;
  if (!course) return <p role="alert">No tienes acceso a este curso.</p>;
  if (!snapshot) return <p>Sincroniza el curso para consultar esta actividad. <Link to={courseUrl(courseId)}>Volver al curso</Link></p>;
  if (!report) return <p role={reports[courseId]?.error ? 'alert' : 'status'}>{reports[courseId]?.error ?? 'Consultando la actividad…'}</p>;
  const activity = report.activityResults.find(a => String(a.itemId) === itemId);
  if (!activity) return <p role="alert">Esta actividad no está disponible en el curso seleccionado.</p>;
  const grades = snapshot.grades.filter(g => String(g.itemId) === itemId);
  const studentName = id => snapshot.students.find(s => s.id === id)?.name ?? `Estudiante ${id}`;
  return <section className="data-card activity-detail"><Link className="text-button" to={courseUrl(course.id)}>← Volver a {courseTitle(course.name)}</Link><span className="eyebrow">DETALLE DE ACTIVIDAD</span><h2>{activity.name}</h2><p>Fuente: {report.source} · Actualizado: {new Date(report.syncedAt).toLocaleString('es-EC')}</p><Statistics stats={activity.statistics} /><h3>Resultados de estudiantes</h3><div className="table-wrap"><table><thead><tr><th>Estudiante</th><th>Nota / 10</th><th>Estado</th><th>Seguimiento</th></tr></thead><tbody>{grades.map(g => <tr key={g.studentId}><td>{studentName(g.studentId)}</td><td>{formatGrade(g.value == null || g.scaleId || g.maximum <= g.minimum ? null : (g.value-g.minimum) * 10 / (g.maximum-g.minimum))}</td><td>{stateLabels[g.state] ?? stateLabels.unknown}</td><td><Link className="text-button" to={`/courses/${course.id}/grades/${g.studentId}`}>Ver perfil →</Link></td></tr>)}</tbody></table></div><h3>Situaciones para revisar</h3><AlertCards alerts={report.alerts.filter(a => String(a.itemId) === itemId)} studentName={studentName} courseId={course.id} /></section>;
}
