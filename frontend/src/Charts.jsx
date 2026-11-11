import { Link } from 'react-router';

export const formatGrade = value => value == null ? 'Sin datos' : new Intl.NumberFormat('es-EC', { maximumFractionDigits: 2 }).format(value);
export const gradeFromStats = stats => stats?.meanGrade ?? (stats?.meanPercent == null ? null : stats.meanPercent / 10);

export function GradeDonut({ distribution = {}, observations = 0 }) {
  const values = [distribution.below7 ?? 0, distribution.from7To89 ?? 0, distribution.from9To10 ?? 0];
  const labels = ['Necesita refuerzo · 0 a menos de 7', 'Logro esperado · 7 a menos de 9', 'Destacado · 9 a 10'];
  const colors = ['var(--chart-risk)', 'var(--chart-expected)', 'var(--chart-highlight)'];
  const circumference = 2 * Math.PI * 70;
  let offset = 0;
  return <figure className="donut-chart"><div className="donut-visual"><svg viewBox="0 0 180 180" role="img" aria-label={`Distribución de ${observations} notas: ${values[0]} menores a 7, ${values[1]} entre 7 y menos de 9, ${values[2]} entre 9 y 10.`}>
    <circle cx="90" cy="90" r="70" fill="none" stroke="#edf1f5" strokeWidth="19" />
    {values.map((count, i) => { const length = observations ? count / observations * circumference : 0; const start = offset; offset += length;
      return count > 0 && <circle key={labels[i]} cx="90" cy="90" r="70" fill="none" stroke={colors[i]} strokeWidth="19" strokeDasharray={`${length} ${circumference - length}`} strokeDashoffset={-start} transform="rotate(-90 90 90)"><title>{labels[i]}: {count} notas</title></circle>;
    })}</svg><div className="donut-center"><strong>{observations}</strong><span>{observations ? 'notas válidas' : 'Sin datos'}</span></div></div>
    <figcaption><h4>Distribución de calificaciones</h4><p>Escala de 0 a 10 · umbral mínimo 7</p><ul className="chart-legend">{values.map((count, i) => <li key={labels[i]}><span className="legend-dot" style={{ background:colors[i] }} /><span>{labels[i]}</span><strong>{count}</strong></li>)}</ul></figcaption>
  </figure>;
}

export function GradeComparison({ points, title = 'Comparación de promedios' }) {
  const width = 760, height = 230, left = 40, right = 25, top = 22, bottom = 40;
  const x = i => left + (width - left - right) * (points.length === 1 ? .5 : i / Math.max(1, points.length - 1));
  const y = value => top + (10 - value) / 10 * (height - top - bottom);
  let connected = false;
  const path = points.map((point, i) => {
    if (point.value == null) { connected = false; return ''; }
    const part = `${connected ? 'L' : 'M'} ${x(i)} ${y(point.value)}`; connected = true; return part;
  }).join(' ');
  return <figure className="comparison-chart"><figcaption><h3>{title}</h3><p>Selecciona un punto o su nombre para abrir el detalle. Comparación por categoría, sin orden temporal.</p></figcaption>
    {points.some(p => p.value != null) ? <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={title}>
      {[0, 2, 4, 6, 8, 10].map(tick => <g key={tick}><line x1={left} x2={width-right} y1={y(tick)} y2={y(tick)} stroke="var(--chart-grid)" /><text x={24} y={y(tick)+4} textAnchor="end">{tick}</text></g>)}
      <line x1={left} x2={width-right} y1={y(7)} y2={y(7)} stroke="#d29950" strokeDasharray="5 5" /><text x={width-right} y={y(7)-7} textAnchor="end" className="threshold-label">Umbral 7</text>
      <path d={path} fill="none" stroke="var(--chart-line)" strokeWidth="3" strokeLinejoin="round" />
      {points.map((point, i) => point.value != null && <Link key={point.href} to={point.href} aria-label={`${point.label}: ${formatGrade(point.value)} sobre 10. Abrir detalle`}><circle cx={x(i)} cy={y(point.value)} r="10" fill={point.value < 7 ? '#fbf0f2' : '#edf2f9'} /><circle cx={x(i)} cy={y(point.value)} r="5" fill={point.value < 7 ? 'var(--chart-risk)' : 'var(--chart-highlight)'} /><title>{point.label}: {formatGrade(point.value)} / 10</title><text x={x(i)} y={y(point.value)-15} textAnchor="middle" className="point-score">{formatGrade(point.value)}</text></Link>)}
    </svg> : <p className="chart-empty">No hay calificaciones válidas para comparar.</p>}
    <div className="chart-links">{points.map((point, i) => <Link key={point.href} to={point.href}><span>{String(i+1).padStart(2,'0')}</span>{point.label}<strong>{formatGrade(point.value)}{point.value != null && ' / 10'}</strong></Link>)}</div>
  </figure>;
}
