import { useEffect, useRef, useState } from 'react';
import { courseTitle } from './WorkspacePages';
import { formatGrade, gradeFromStats } from './Charts';

export function AgentChat({ courses, selectedCourse, request }) {
  const [open, setOpen] = useState(false);
  const [courseId, setCourseId] = useState(String(selectedCourse?.id ?? courses[0]?.id ?? ''));
  const [conversations, setConversations] = useState({});
  const [draft, setDraft] = useState('');
  const [status, setStatus] = useState({ loading:true });
  const [working, setWorking] = useState(false);
  const [error, setError] = useState('');
  const pending = useRef(null);
  const composer = useRef(null);
  const end = useRef(null);
  const launcher = useRef(null);
  useEffect(() => { if (selectedCourse) setCourseId(String(selectedCourse.id)); }, [selectedCourse?.id]);
  useEffect(() => {
    pending.current?.abort(); setWorking(false); setError(''); setDraft('');
    return () => pending.current?.abort();
  }, [courseId]);
  useEffect(() => {
    if (!open || !courseId) return;
    const controller = new AbortController();
    setStatus({ loading:true });
    request(`/agent/api/courses/${courseId}/agent-status`, { signal:controller.signal })
      .then(data => { if (!controller.signal.aborted) setStatus({ ...data, available:data.chatAvailable }); })
      .catch(() => { if (!controller.signal.aborted) setStatus({ error:true }); });
    return () => controller.abort();
  }, [open, courseId, request]);
  useEffect(() => { if (open) composer.current?.focus(); }, [open]);
  useEffect(() => { if (open) end.current?.scrollIntoView({ block:'nearest' }); }, [conversations, working]);
  const messages = conversations[courseId] ?? [];
  const messageLimit = status.maximumMessageLength ?? 1000;
  const close = () => { setOpen(false); launcher.current?.focus(); };
  const send = async event => {
    event.preventDefault();
    if (!draft.trim() || draft.trim().length > messageLimit || working || !status.available || status.loading) return;
    const question = draft.trim();
    const outgoing = [...messages, { role:'user', content:question }];
    setConversations(current => ({ ...current, [courseId]:outgoing }));
    setDraft(''); setWorking(true); setError('');
    const controller = new AbortController(); pending.current = controller;
    try {
      const history = (status.supportsHistory === false ? outgoing.slice(-1) : outgoing.slice(-7)).map(m => ({ role:m.role, content:m.content.slice(0,messageLimit) }));
      while (history.length > 1 && new TextEncoder().encode(JSON.stringify({ messages:history })).length > 15000) history.shift();
      const result = await request(`/agent/api/courses/${courseId}/chat`, { method:'POST', signal:controller.signal,
        headers:{ 'Content-Type':'application/json' }, body:JSON.stringify({ messages:history }) });
      if (!controller.signal.aborted) setConversations(current => ({ ...current, [courseId]:[...outgoing, { role:'assistant', content:result.answer, result }] }));
    } catch (e) {
      if (!controller.signal.aborted) { setError(e.code === 'agent_configuration_required' ? 'El asistente no está disponible en este momento.' : e.message); setDraft(question); setConversations(current => ({ ...current, [courseId]:messages })); }
    } finally { if (!controller.signal.aborted) setWorking(false); }
  };
  return <>
    <button ref={launcher} className="chat-launcher" aria-expanded={open} aria-controls="academic-chat" onClick={() => setOpen(!open)}><span aria-hidden="true">✦</span>Asistente académico</button>
    {open && <section id="academic-chat" className="agent-chat" role="dialog" aria-modal="false" aria-labelledby="chat-title" onKeyDown={e => { if (e.key === 'Escape') close(); }}>
      <header className="chat-header"><span className="chat-mark" aria-hidden="true">✦</span><div><h2 id="chat-title">Tu asistente académico</h2><p>Una ayuda para orientar el acompañamiento</p></div><button className="icon-button" aria-label="Cerrar chat" onClick={close}>×</button></header>
      <label className="chat-context">Curso de la conversación<select value={courseId} onChange={e => setCourseId(e.target.value)}>{courses.map(c => <option key={c.id} value={c.id}>{courseTitle(c.name)}</option>)}</select></label>
      <div className="chat-messages" role="log" aria-live="polite" aria-label="Conversación con el asistente">
        {!messages.length && <div className="chat-intro"><span aria-hidden="true">✧</span><h3>¿Qué te gustaría revisar?</h3><p>Haz una pregunta o pide ideas para acompañar a tus estudiantes.</p>{['¿Cómo puedo organizar un repaso?', '¿Qué actividad de refuerzo puedo proponer?'].map(text => <button key={text} onClick={() => { setDraft(text); composer.current?.focus(); }}>{text}</button>)}</div>}
        {messages.map((message, i) => <article className={`chat-bubble ${message.role}`} key={i}><span>{message.role === 'user' ? 'Tú' : 'Asistente académico'}</span><p>{message.content}</p>{message.result?.confirmedData && <details><summary>Ver datos confirmados</summary><p>Promedio: {formatGrade(gradeFromStats(message.result.confirmedData.statistics))} / 10 · {message.result.confirmedData.statistics.observations} notas · {message.result.confirmedData.alertCount} alertas.</p><small>Fuente: {message.result.source} · {new Date(message.result.syncedAt).toLocaleString('es-EC')}. La interpretación requiere criterio docente.</small></details>}</article>)}
        {working && <p role="status">Preparando una respuesta…</p>}<div ref={end} />
      </div>
      {(status.loading || status.error || !status.available) && <div className="chat-service-status" role="status">{status.loading ? 'Preparando el asistente…' : status.error ? 'No se pudo conectar con el asistente. Intenta de nuevo más tarde.' : 'El asistente no está disponible en este momento.'}</div>}
      {error && <p className="chat-error" role="alert">{error}</p>}
      <form className="chat-compose" onSubmit={send}><label className="sr-only" htmlFor="chat-question">Escribe tu consulta</label><textarea ref={composer} id="chat-question" rows="2" maxLength={messageLimit} placeholder="Escribe tu consulta sobre este curso…" value={draft} disabled={working} onChange={e => setDraft(e.target.value)} /><button className="button button-primary" type="submit" disabled={!draft.trim() || draft.trim().length > messageLimit || working || status.loading || !status.available}>Enviar</button></form>
      <div className="chat-foot"><span>{draft.length}/{messageLimit} caracteres</span><button className="text-button" disabled={working || !messages.length} onClick={() => setConversations(current => ({ ...current, [courseId]:[] }))}>Limpiar conversación</button></div>
    </section>}
  </>;
}
