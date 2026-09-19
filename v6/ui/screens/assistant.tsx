// The assistant: questions answered on this PC by a local model, each beside the figures it was given. The answer
// is the model's; the figures column is the app's own, so when they disagree the owner can see which to believe.
// The conversation lives only on this screen: it is not saved, and it is gone when the app closes.
import { useState } from 'react';
import { durationText } from '../../core/duration.ts';
import { Icon } from '../icons.tsx';
import { bridge, Btn, Chip, Headline, type ScreenProps } from '../parts.tsx';

interface Turn { question: string; answer: string | null; error: string | null; people: { accountId: string; key: string; customer: string }[]; figures: [string, string, boolean?][] }

const SUGGESTED = ['Who has waited longest?', 'How many customers are past the target?', 'Which location has the most people waiting?', 'Does any account need signing in?'];

export function AssistantScreen({ state, nav }: ScreenProps) {
  const [draft, setDraft] = useState('');
  const [turns, setTurns] = useState<Turn[]>([]);
  const [asking, setAsking] = useState(false);
  const engine = state.assistant.state;
  const ready = engine.phase === 'ready';

  const ask = async (question: string) => {
    const q = question.trim();
    if (!q || asking || !ready) return;
    setDraft('');
    setAsking(true);
    const late = state.queue.filter((r) => r.tone === 'late').length;
    const figures: [string, string, boolean?][] = [
      ['Waiting now', String(state.queueTotal)],
      ['Past target', String(state.figures.find((f) => f.label === 'Past target')?.value ?? late), late > 0],
      ['Longest wait', state.queue[0] ? durationText(state.queue[0].waited) : '—'],
      ['Figures', state.freshness.text.replace(/^Updated /, '')],
    ];
    const history = turns.filter((t) => t.answer).slice(-3).flatMap((t) => [{ role: 'user' as const, content: t.question }, { role: 'assistant' as const, content: t.answer! }]);
    setTurns((all) => [...all, { question: q, answer: null, error: null, people: [], figures }]);
    const result = await bridge.askAssistant(q, history);
    setTurns((all) => all.map((t, i) => (i === all.length - 1 ? { ...t, answer: result.answer ?? null, error: result.error ?? null, people: result.people ?? [] } : t)));
    setAsking(false);
  };

  return (
    <main className="main" style={{ gap: 16, maxWidth: 1180 }}>
      <Headline title="Assistant" actions={<><Chip tone="ok" icon="shield">On this PC</Chip><Btn kind="quiet" onClick={() => nav.go('settings', null, 'Assistant')}>Assistant settings</Btn></>}>
        Answers are written on this PC by a local model, from the figures the app already shows. It can point you to a chat; it has no way to send one.
      </Headline>
      {!ready && (
        <div className="panel" style={{ display: 'grid', gap: 10 }}>
          <b style={{ fontWeight: 600 }}>{engine.phase === 'off' ? 'The assistant is off.' : state.assistant.sentence}</b>
          <span className="sub">{engine.phase === 'off' ? 'Switch it on in Settings › Assistant. It runs entirely on this PC.' : 'It answers once Settings › Assistant says it is ready.'}</span>
          <div><Btn icon="gear" kind="primary" onClick={() => nav.go('settings', null, 'Assistant')}>Open assistant settings</Btn></div>
        </div>
      )}
      <div className="chat" style={{ flex: 1 }} aria-live="polite">
        {turns.map((t, i) => (
          <div key={i} style={{ display: 'contents' }}>
            <div className="q-bub">{t.question}</div>
            <div className="a-bub">
              <div>
                {t.answer === null && !t.error && <p className="sub">Thinking on this PC…</p>}
                {t.error && <p className="late">{t.error}</p>}
                {t.answer && t.answer.split(/\n+/).map((p, j) => <p key={j}>{p}</p>)}
                {t.people.length > 0 && (
                  <div style={{ display: 'flex', gap: 8, marginTop: 12, flexWrap: 'wrap' }}>
                    {t.people.map((p) => <Btn key={`${p.accountId}:${p.key}`} icon="open" onClick={() => nav.go('dock', p.accountId, p.key)}>Open {p.customer}’s chat</Btn>)}
                  </div>
                )}
              </div>
              <div className="evidence">
                <h5>Figures given, from the app</h5>
                {t.figures.map(([k, v, bad]) => <div key={k}><span>{k}</span><b style={bad ? { color: 'var(--late)' } : undefined}>{v}</b></div>)}
                <span className="sub" style={{ marginTop: 4 }}>If these and the answer disagree, the figures are right.</span>
              </div>
            </div>
          </div>
        ))}
      </div>
      <div className="suggest">
        {SUGGESTED.map((s) => <button key={s} disabled={!ready || asking} onClick={() => void ask(s)}><span>{s}</span></button>)}
      </div>
      <form className="ask" onSubmit={(e) => { e.preventDefault(); void ask(draft); }}>
        <Icon name="spark" size={16} />
        <input value={draft} onChange={(e) => setDraft(e.target.value)} placeholder="Ask about who is waiting, targets, locations or accounts…" aria-label="Ask the assistant"
          disabled={!ready} style={{ flex: 1, border: 0, background: 'transparent', font: 'inherit', color: 'var(--ink)', outline: 'none' }} />
        <Btn kind="primary" disabled={!ready || asking || !draft.trim()}>{asking ? 'Thinking…' : 'Ask'}</Btn>
      </form>
    </main>
  );
}
