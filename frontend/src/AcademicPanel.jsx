import { GradeDonut, GradeComparison, formatGrade, gradeFromStats } from './Charts';
import { useEffect, useState } from 'react';
import { Link } from 'react-router';

export const stateLabels = { graded: 'Calificado', pending: 'Entregado, pendiente de calificar', not_submitted: 'Sin entrega confirmada', unknown: 'Sin dato verificable' };
const number = (v) => v === null || v === undefined ? 'Sin datos' : new Intl.NumberFormat('es-EC', { maximumFractionDigits: 2 }).format(v);
const date = (v) => v ? new Date(v).toLocaleString('es-EC') : 'No disponible';

function useAcademic(path, version, request) {
  const [result, setResult] = useState({ loading: true });
  useEffect(() => {
    let active = true;
    const controller = new AbortController();
    setResult({ loading: true });
    request(path, { signal: controller.signal }).then(data => { if (active) setResult({ data }); })
      .catch(error => { if (active) setResult({ error: error.message }); });
    return () => { active = false; controller.abort(); };
  }, [path, version, request]);
  return result;
}

export function Statistics({ stats }) {
  return <>
    <div className="analysis-metrics">
      <article><span>Promedio de actividades</span><strong>{formatGrade(gradeFromStats(stats))}{stats.observations > 0 && <small> / 10</small>}</strong><small>Umbral mínimo: 7 / 10</small></article>
      <article><span>Notas válidas</span><strong>{stats.observations}</strong><small>Incluye la nota cero</small></article>
      <article><span>Registros sin promediar</span><strong>{stats.excluded}</strong><small>Sin nota o escala no comparable</small></article>
      <article><span>Nota mínima / máxima</span><strong>{formatGrade(stats.minimumGrade ?? (stats.minimumPercent == null ? null : stats.minimumPercent / 10))} / {formatGrade(stats.maximumGrade ?? (stats.maximumPercent == null ? null : stats.maximumPercent / 10))}</strong><small>Escala de 0 a 10</small></article>
    </div>
    <GradeDonut distribution={stats.distribution} observations={stats.observations} />
  </>;
}
export function AlertCards({ alerts, studentName, courseId, showProfile = true }) {
  const grouped = new Map();
  for (const alert of alerts) {
    if (!grouped.has(alert.studentId)) grouped.set(alert.studentId, []);
    grouped.get(alert.studentId).push(alert);
  }
  const groups = [...grouped].sort((a, b) => studentName(a[0]).localeCompare(studentName(b[0]), 'es', { numeric: true }));
  if (!groups.length) return <div className="alerts-empty"><span aria-hidden="true">✓</span><div><strong>No hay alertas en esta selección</strong><p>Puedes revisar otro filtro o continuar con el seguimiento del curso.</p></div></div>;
  return <ul className="student-alerts">{groups.map(([id, items]) => {
    const overdue = items.filter(a => a.rule === 'overdue_submission').length;
    const low = items.length - overdue;
    const ordered = [...items].sort((a, b) => Number(b.rule === 'overdue_submission') - Number(a.rule === 'overdue_submission') || a.activity.localeCompare(b.activity, 'es', { numeric: true }));
    return <li key={id}><details className="student-alert-card" open={groups.length === 1}>
      <summary><span className="alert-avatar" aria-hidden="true">{studentName(id).trim().charAt(0)}</span><span className="alert-student"><strong>{studentName(id)}</strong><span>{items.length} {items.length === 1 ? 'situación para revisar' : 'situaciones para revisar'}</span></span><span className="alert-badges">{overdue > 0 && <span className="alert-badge overdue">{overdue} {overdue === 1 ? 'entrega vencida' : 'entregas vencidas'}</span>}{low > 0 && <span className="alert-badge low">{low} {low === 1 ? 'nota baja' : 'notas bajas'}</span>}</span><span className="alert-expand" aria-hidden="true">⌄</span></summary>
      <div className="alert-card-body"><ul className="alert-items">{ordered.map(a => <li key={`${a.rule}-${a.itemId}`}>
        <span className={`alert-kind ${a.rule === 'overdue_submission' ? 'overdue' : 'low'}`} aria-hidden="true">{a.rule === 'overdue_submission' ? '◷' : '↘'}</span>
        <div><span className="alert-item-label">{a.rule === 'overdue_submission' ? 'Entrega vencida' : 'Calificación para revisar'}</span><strong>{a.activity}</strong>
          <p>{a.rule === 'overdue_submission' ? 'No se registra una entrega confirmada al actualizar el curso.' : <>Nota registrada: <b>{number(a.value)} / {number(a.maximum)}</b> · Por debajo del umbral de {number(a.thresholdPercent / 10)} / 10.</>}</p>
          <small>{a.dueAt && <>Fecha límite: {date(a.dueAt)} · </>}Verificado: {date(a.evidenceAt)}</small>
          <details className="alert-evidence"><summary>Consultar evidencia y regla</summary><p>{a.evidence}</p><small>Regla: {a.rule}{a.rule === 'low_grade' && ` · Escala original: ${number(a.minimum)} a ${number(a.maximum)}`}</small></details>
        </div></li>)}</ul>
        {showProfile && <Link className="alert-profile-link" to={`/courses/${courseId}/grades/${id}`}>Ver perfil académico <span aria-hidden="true">→</span></Link>}
      </div>
    </details></li>;
  })}</ul>;
}

export function AcademicPanel({ course, snapshot, request }) {
  const { data, error, loading } = useAcademic(`/academic/api/courses/${course.id}/analysis`, snapshot?.syncedAt, request);
  const [alertFilter, setAlertFilter] = useState('all');
  const [studentSearch, setStudentSearch] = useState('');
  if (loading) return <p role="status">Consultando indicadores autorizados…</p>;
  if (error) return <p role="alert">{error}</p>;
  const studentName = id => snapshot.students.find(s => s.id === id)?.name ?? `Estudiante ${id}`;
  const visibleAlerts = data.alerts.filter(a => (alertFilter === 'all' || a.rule === alertFilter) && studentName(a.studentId).toLocaleLowerCase('es').includes(studentSearch.trim().toLocaleLowerCase('es')));
  const studentsWithAlerts = new Set(visibleAlerts.map(a => a.studentId)).size;
  return <div className="academic-panel">
    <div className="section-heading"><div><span className="eyebrow">SEGUIMIENTO DEL CURSO</span><h3>{data.courseName}</h3></div><span>{data.students} estudiantes · {data.activities} actividades</span></div>
    <p>Fuente: {data.source} · Actualización: {date(data.syncedAt)}</p>
    <Statistics stats={data.statistics} />
    <p className="table-note">{data.method} Denominador: {data.statistics.observations} notas válidas. Los estados ausentes nunca se sustituyen por cero.</p>
    <div className="state-summary">{Object.entries(data.states).map(([key, count]) => <span className="soft-label" key={key}>{stateLabels[key]}: {count}</span>)}</div>
    <GradeComparison title="Resultados por actividad" points={data.activityResults.map(a => ({ label:a.name, value:gradeFromStats(a.statistics), href:`/courses/${course.id}/activities/${a.itemId}` }))} /><h3>Detalle de las actividades</h3>
    <div className="table-wrap"><table><thead><tr><th>Actividad</th><th>Promedio / 10</th><th>Observaciones</th><th>Excluidos</th></tr></thead><tbody>{data.activityResults.map(a => <tr key={a.itemId}><td><Link className="text-button" to={`/courses/${course.id}/activities/${a.itemId}`}>{a.name} →</Link></td><td>{formatGrade(gradeFromStats(a.statistics))}</td><td>{a.statistics.observations}</td><td>{a.statistics.excluded}</td></tr>)}</tbody></table></div>
    {!data.activityResults.length && <p>No hay resultados de actividades disponibles.</p>}
    <section className="alerts-section" aria-labelledby="course-alerts-title"><div className="alerts-heading"><div><span className="eyebrow">ACOMPAÑAMIENTO ACADÉMICO</span><h3 id="course-alerts-title">Estudiantes que necesitan una revisión</h3><p>Abre una ficha para revisar sus actividades y decidir el siguiente paso.</p></div><span className="alerts-total">{data.alerts.length} alertas</span></div>
    <div className="academic-filters"><label className="select-label">Tipo de alerta<select value={alertFilter} onChange={e => setAlertFilter(e.target.value)}><option value="all">Todas las situaciones</option><option value="low_grade">Calificaciones bajas</option><option value="overdue_submission">Entregas vencidas</option></select></label><label className="select-label">Buscar estudiante<input type="search" value={studentSearch} onChange={e => setStudentSearch(e.target.value)} placeholder="Nombre del estudiante" /></label><span role="status">{studentsWithAlerts} {studentsWithAlerts === 1 ? 'estudiante' : 'estudiantes'} · {visibleAlerts.length} de {data.alerts.length} alertas</span></div>
    <AlertCards alerts={visibleAlerts} studentName={studentName} courseId={course.id} />
    <p className="alerts-footnote">Se señalan notas menores a {data.rules.lowGradePercent / 10} / 10 y entregas con fecha vencida y ausencia confirmada. Las actividades entregadas y pendientes de calificar no se consideran sin entrega. Las alertas orientan tu revisión; no son diagnósticos.</p></section>
    <h3>Contenidos que requieren revisión</h3><p>Se requiere un mínimo de {data.rules.minimumObservations} estudiantes evaluados y al menos {data.rules.reinforcementPercent} % por debajo de {data.rules.lowGradePercent / 10} / 10 en el contenido.</p>
    {data.reinforcement.length ? <ul className="evidence-list">{data.reinforcement.map(f => <li key={f.sectionId}><strong>{f.content}</strong><p>{f.belowThreshold} de {f.students} estudiantes evaluados ({number(f.belowThresholdPercent)} %) bajo el umbral. Promedio: {formatGrade(gradeFromStats(f.statistics))} / 10.</p><p>Actividades: {f.activityIds.map(id => data.activityResults.find(a => a.itemId === id)?.name ?? id).join(', ')}</p><small>Asociación verificable: {f.mappingSource}</small></li>)}</ul> : <p>No hay evidencia suficiente para señalar contenidos que requieren refuerzo.</p>}
    <p className="table-note">{data.unmappedActivities} actividades sin asociación temática verificable. No se deducen contenidos desde el nombre de la actividad.</p>
  </div>;
}

export function AcademicProfile({ course, studentId, snapshot, request }) {
  const { data, loading, error } = useAcademic(`/academic/api/courses/${course.id}/students/${studentId}`, snapshot?.syncedAt, request);
  const [state, setState] = useState('all');
  const [search, setSearch] = useState('');
  useEffect(() => { setState('all'); setSearch(''); }, [course.id, studentId]);
  if (loading) return <p role="status">Consultando perfil académico…</p>;
  if (error) return <p role="alert">{error}</p>;
  const grades = data.grades.filter(g => (state === 'all' || g.state === state) && g.name.toLocaleLowerCase('es').includes(search.trim().toLocaleLowerCase('es')));
  return <section><h3>Perfil de {data.student.name}</h3><p>Fuente: {data.source} · Actualización: {date(data.syncedAt)}</p><Statistics stats={data.statistics} />
    <h4>Resultados por actividad disponible</h4><p>Consulta las notas y el estado de cada actividad. Las fechas aparecen cuando están disponibles.</p>
    <div className="academic-filters"><label className="select-label">Buscar actividad<input type="search" value={search} onChange={e => setSearch(e.target.value)} placeholder="Nombre de la actividad" /></label><label className="select-label">Estado<select value={state} onChange={e => setState(e.target.value)}><option value="all">Todos los estados</option>{Object.entries(stateLabels).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label><span role="status">{grades.length} de {data.grades.length} actividades · Los filtros no cambian los indicadores.</span></div>
    <div className="table-wrap"><table><thead><tr><th>Actividad</th><th>Nota</th><th>Escala</th><th>Estado</th><th>Fecha límite</th><th>Entrega</th></tr></thead><tbody>{grades.map(g => <tr key={g.itemId}><td>{g.name}</td><td>{number(g.value)}</td><td>{g.minimum} – {g.maximum}{g.scaleId ? ' (cualitativa)' : ''}</td><td>{stateLabels[g.state] ?? stateLabels.unknown}</td><td>{date(g.dueAt)}</td><td>{date(g.submittedAt)}</td></tr>)}</tbody></table></div>
    {!grades.length && <p>No hay actividades que coincidan con esta selección.</p>}
    <h4>Situaciones para revisar</h4><AlertCards alerts={data.alerts} studentName={() => data.student.name} courseId={course.id} showProfile={false} />
  </section>;
}
