// The assistant: questions answered on this PC, each beside the figures it used. Sample conversation until
// the assistant is wired (Phase 5); the figures column already reads the real view model, because that is the
// part that must never be invented.
import { useState } from 'react';
import { Icon } from '../icons.tsx';
import { Btn, Chip, Headline, type ScreenProps } from '../parts.tsx';

export function AssistantScreen({ state, nav }: ScreenProps) {
  const [draft, setDraft] = useState('');
  const longest = state.queue[0];
  const late = state.queue.filter((r) => r.tone === 'late').length;
  const figures: [string, string, boolean?][] = [
    ['Waiting now', String(state.split.needsReply)],
    ['Past target', String(late), late > 0],
    ['Longest wait', longest ? `${longest.waited} min` : '—'],
    ['Read', state.freshness.text.replace(/^Updated /, '')],
  ];
  return (
    <main className="main" style={{ gap: 16, maxWidth: 1180 }}>
      <Headline sample title="Assistant" actions={<><Chip tone="ok" icon="shield">On this PC</Chip><Btn kind="quiet" onClick={() => nav.go('settings', null, 'Assistant')}>Assistant settings</Btn></>}>
        Answers are written on this PC by a local model, from a summary the app builds. It can open a chat for you; it has no way to send one.
      </Headline>
      <div className="chat" style={{ flex: 1 }}>
        <div className="q-bub">Who has waited longest, and is anyone being ignored?</div>
        <div className="a-bub">
          <div>
            {longest
              ? <p><b>{longest.customer} at {longest.location || 'no location'} has waited longest</b>, {longest.waited} minutes, on {longest.accountName}.</p>
              : <p><b>Nobody is waiting right now.</b></p>}
            <p>{late > 0 ? `${late} customers are past the ${state.settings.slaMinutes}-minute target. Answer them in the order the line shows.` : 'Nobody is past the target.'}</p>
            {longest && <div style={{ display: 'flex', gap: 8, marginTop: 12 }}><Btn icon="open" onClick={() => nav.go('dock', longest.accountId, longest.customer)}>Open {longest.customer.split(' ')[0]}’s chat</Btn></div>}
          </div>
          <div className="evidence">
            <h5>Figures used, from the app</h5>
            {figures.map(([k, v, bad]) => <div key={k}><span>{k}</span><b style={bad ? { color: 'var(--late)' } : undefined}>{v}</b></div>)}
            <span className="sub" style={{ marginTop: 4 }}>If these and the answer disagree, the figures are right.</span>
          </div>
        </div>
      </div>
      <div className="suggest">
        {['Which account is slowest this week?', 'Who wrote overnight?', 'How many missed calls are not returned?', 'Summarise today for the manager'].map((s) => <button key={s} onClick={() => setDraft(s)}><span>{s}</span></button>)}
      </div>
      <label className="ask">
        <Icon name="spark" size={16} />
        <input value={draft} onChange={(e) => setDraft(e.target.value)} placeholder="Ask about waiting, replies, reviews or calls…" aria-label="Ask the assistant"
          style={{ flex: 1, border: 0, background: 'transparent', font: 'inherit', color: 'var(--ink)', outline: 'none' }} />
        <Btn kind="primary" disabled title="The assistant is not connected yet">Ask</Btn>
      </label>
    </main>
  );
}
