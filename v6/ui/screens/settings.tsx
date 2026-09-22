// Settings. "Look and reading" and "Notifications" write to config.json and take effect at once. Opening hours and
// holidays too. The assistant, the workspace and parts of privacy are sample settings until their features
// are wired, and say so.
import { useState } from 'react';
import { Icon, type IconName } from '../icons.tsx';
import { bridge, Btn, Chip, Headline, Panel, Sample, Seg, SettingRow, Stepper, Toggle, type ScreenProps } from '../parts.tsx';
import { durationText } from '../../core/duration.ts';
import { updateSentence } from '../../core/update.ts';
import type { UiState } from '../../app/view-model.ts';

export const SETTINGS_SECTIONS = ['Look and reading', 'Opening hours', 'Notifications', 'Saved replies', 'Assistant', 'Workspace', 'Privacy', 'About'] as const;
type Section = typeof SETTINGS_SECTIONS[number];

export function SettingsScreen(props: ScreenProps) {
  const { nav } = props;
  const section = (SETTINGS_SECTIONS as readonly string[]).includes(nav.view.sub) ? nav.view.sub as Section : 'Look and reading';
  return (
    <main className="main">
      <Headline title="Settings">Kept on this PC. Settings marked as shared follow every PC in your workspace once it is connected.</Headline>
      <div className="settings">
        <nav className="stabs" aria-label="Settings sections">
          {SETTINGS_SECTIONS.map((s) => <button key={s} aria-current={s === section ? 'page' : undefined} onClick={() => nav.go('settings', null, s)}>{s}</button>)}
        </nav>
        <div style={{ display: 'grid', gap: 22, alignContent: 'start', minWidth: 0 }}>
          {section === 'Look and reading' && <Look {...props} />}
          {section === 'Opening hours' && <Hours {...props} />}
          {section === 'Notifications' && <Notifications {...props} />}
          {section === 'Saved replies' && <SavedReplies {...props} />}
          {section === 'Assistant' && <AssistantSettings {...props} />}
          {section === 'Workspace' && <Workspace {...props} />}
          {section === 'Privacy' && <Privacy {...props} />}
          {section === 'About' && <About {...props} />}
        </div>
      </div>
    </main>
  );
}

const minutes = (m: number) => `${m} min`;
const seconds = (s: number) => (s < 60 ? `${s} s` : `${s / 60} min`);

function Look({ state }: ScreenProps) {
  const s = state.settings;
  const set = (patch: Record<string, unknown>) => bridge.setSettings(patch);
  const themes: [typeof s.theme, IconName, string][] = [['system', 'monitor', 'Match Windows'], ['light', 'sun', 'Light'], ['dark', 'moon', 'Dark']];
  return (
    <>
      <div className="sgroup"><h3>Appearance</h3><p>Applies to the whole app: title bar, sidebar, menus, scrollbars, and the WhatsApp and Instagram pages inside it.</p>
        <div className="themes">
          {themes.map(([t, icon, label]) => (
            <button key={t} className="tcard" aria-pressed={s.theme === t} onClick={() => bridge.setTheme(t)}>
              <span className={`prev ${t === 'dark' ? 'd' : t === 'system' ? 's' : ''}`}>
                <span className="l" /><span className="r"><i style={{ width: '70%' }} /><i /><i style={{ width: '50%' }} /></span>
                {t === 'system' && <span className="r" style={{ background: '#1B1F1C' }}><i style={{ width: '70%', background: '#3E4540' }} /><i style={{ background: '#3E4540' }} /><i style={{ width: '50%', background: '#3E4540' }} /></span>}
              </span>
              <span className="tname"><Icon name={icon} size={15} />{label}</span>
            </button>
          ))}
        </div>
      </div>
      <div className="sgroup"><h3>Reading</h3>
        <div className="panel" style={{ padding: 0 }}>
          <SettingRow title="Reply target" detail="A customer waiting longer than this is past target. Counted in opening hours.">
            <Stepper label="reply target" value={s.slaMinutes} options={[5, 10, 15, 20, 30, 45, 60, 90, 120]} format={minutes} onChange={(v) => set({ slaMinutes: v })} />
          </SettingRow>
          <SettingRow title="Read each account every" detail="Reading is quiet and never opens a chat.">
            <Stepper label="read interval" value={s.readEverySeconds} options={[30, 60, 120, 300]} format={seconds} onChange={(v) => set({ readEverySeconds: v })} />
          </SettingRow>
          <SettingRow title="Backlog after" detail="Waiting longer than this is counted as backlog and reported separately, never hidden.">
            <Stepper label="backlog" value={s.backlogAfterDays} options={[1, 3, 7, 14, 30]} format={(d) => `${d} day${d === 1 ? '' : 's'}`} onChange={(v) => set({ backlogAfterDays: v })} />
          </SettingRow>
          <SettingRow title="Chats each WhatsApp read takes in"
            detail="How far back a read looks. Higher sees older conversations and costs a little more time on every pass; Instagram has no such number, because its page holds only the top threads of Primary.">
            <Stepper label="chats per WhatsApp read" value={s.readLimits.whatsappChats} options={[100, 250, 500, 1000, 1500, 2000]}
              format={(n) => `${n} chats`} onChange={(v) => set({ readLimits: { ...s.readLimits, whatsappChats: v } })} />
          </SettingRow>
          <SettingRow title="Leave out chats that ended themselves" detail="A last message like “ok thanks” is not someone waiting.">
            <Toggle label="Leave out chats that ended themselves" on={s.filterClosedConversations} onChange={(v) => set({ filterClosedConversations: v })} />
          </SettingRow>
        </div>
      </div>
      <NotCustomers state={state} />
      <div className="sgroup"><h3>Closing the window</h3>
        <div className="panel" style={{ padding: 0 }}>
          <SettingRow title="When I close the window" detail={s.closeToBackground
            ? 'The app keeps reading in the background. Open it again or quit from its icon beside the clock.'
            : 'The app quits. Nobody is counted until you open it again.'}>
            <Seg label="When I close the window" value={s.closeToBackground ? 'background' : 'quit'} onChange={(v) => set({ closeToBackground: v === 'background' })}
              options={[['background', 'Keep reading in the background'], ['quit', 'Quit']] as const} />
          </SettingRow>
        </div>
      </div>
      <div className="sgroup"><h3>Memory</h3>
        <div className="panel" style={{ padding: 0 }}>
          <SettingRow title="Sleep accounts I’m not using" detail="Closes idle pages and keeps their logins. An account on the line is never slept.">
            <Toggle label="Sleep accounts I’m not using" on={s.sleepUnusedAccounts} onChange={(v) => set({ sleepUnusedAccounts: v })} />
          </SettingRow>
          <SettingRow title="Sleep after" detail="Only used when sleeping is on.">
            <Stepper label="sleep after" value={s.sleepAfterMinutes} options={[10, 20, 30, 60, 120]} format={minutes} onChange={(v) => set({ sleepAfterMinutes: v })} />
          </SettingRow>
        </div>
      </div>
    </>
  );
}

type Hours = UiState['openingHours']['locations'][number]['hours'];
type Holiday = UiState['openingHours']['holidays'][number];
type DayWindow = { open: number; close: number } | null;

const DAY_NAMES = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
/** Monday first, as a week is read here. Indexes stay 0 = Sunday, as the rules store them. */
const WEEK_ORDER = [1, 2, 3, 4, 5, 6, 0];
const pad2 = (n: number) => String(n).padStart(2, '0');
const toTime = (m: number) => { const v = Math.min(m, 1439); return `${pad2(Math.floor(v / 60))}:${pad2(v % 60)}`; };
const fromTime = (v: string) => { const [h, m] = v.split(':').map(Number); return Number.isFinite(h) && Number.isFinite(m) ? h * 60 + m : null; };
const todayKey = () => { const d = new Date(); return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`; };
const longDate = (key: string) => { const [y, m, d] = key.split('-').map(Number); return new Date(y, m - 1, d).toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }); };

/** Seven day windows, whatever form the hours were saved in. v5 wrote one window for every working day. */
function weekOf(hours: Hours): DayWindow[] {
  if (hours?.week?.length === 7) return hours.week;
  const days = hours?.workingDays?.length ? hours.workingDays : [1, 2, 3, 4, 5, 6];
  const open = hours?.openMinutes ?? 11 * 60, close = hours?.closeMinutes ?? 21 * 60;
  return DAY_NAMES.map((_, d) => (days.includes(d) ? { open, close } : null));
}

function Hours({ state }: ScreenProps) {
  const { locations, holidays } = state.openingHours;
  const [picked, setPicked] = useState<string | null>(null);
  const current = locations.find((l) => l.name === picked) ?? locations[0];
  if (!current) {
    return (
      <div className="sgroup"><h3>Opening hours</h3>
        <Panel><p className="sub" style={{ margin: 0 }}>No locations yet. Give accounts a location to set its hours; until then waits count around the clock.</p></Panel>
      </div>
    );
  }
  const week = weekOf(current.hours);
  const enabled = current.hours?.enabled ?? false;
  const save = (name: string, next: { enabled: boolean; week: DayWindow[] }) => bridge.setLocationHours(name, next);
  const setDay = (day: number, window: DayWindow) => save(current.name, { enabled, week: week.map((w, d) => (d === day ? window : w)) });
  const allClosed = week.every((w) => !w);
  return (
    <>
      <div className="sgroup">
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}><h3>Opening hours</h3>
          {locations.length > 1 && <div style={{ marginLeft: 'auto' }}><Seg label="Location" value={current.name} onChange={setPicked} options={locations.map((l) => l.name)} /></div>}
        </div>
        <p>The reply clock only runs while a location is open, so a message at closing time is not late by morning. Kept on this PC.</p>
        <div className="panel" style={{ padding: 0 }}>
          <SettingRow title={`Count waits only while ${current.name} is open`} detail={enabled ? (allClosed ? 'Every day is closed, so waits still count around the clock. Open at least one day.' : 'Outside these hours, and on holidays, waits stand still.') : 'Off: waits count around the clock, overnight included.'}>
            <Toggle label="Count waits only while open" on={enabled} onChange={(v) => save(current.name, { enabled: v, week })} />
          </SettingRow>
        </div>
        <Panel>
          <div className="hours" role="group" aria-label={`Hours for ${current.name}`} style={{ gridTemplateColumns: '180px repeat(2, 130px) 1fr' }}>
            {WEEK_ORDER.map((d) => {
              const w = week[d];
              const name = DAY_NAMES[d];
              return [
                <span key={`${d}n`} style={{ display: 'flex', alignItems: 'center', gap: 10 }}><Toggle label={`${name} open`} on={!!w} onChange={(v) => setDay(d, v ? { open: 11 * 60, close: 21 * 60 } : null)} /><b style={{ fontWeight: 600 }}>{name}</b></span>,
                w ? <input key={`${d}o`} className="input" type="time" step={900} aria-label={`${name} opens`} value={toTime(w.open)} onChange={(e) => { const m = fromTime(e.target.value); if (m !== null && m < w.close) setDay(d, { ...w, open: m }); }} /> : <span key={`${d}o`} className="closed">Closed</span>,
                w ? <input key={`${d}c`} className="input" type="time" step={900} aria-label={`${name} closes`} value={toTime(w.close)} onChange={(e) => { const m = fromTime(e.target.value); if (m !== null && m > w.open) setDay(d, { ...w, close: m }); }} /> : <span key={`${d}c`} />,
                <span key={`${d}x`} className="sub">{w ? `${Math.round(((w.close - w.open) / 60) * 10) / 10} hours` : ''}</span>,
              ];
            })}
          </div>
        </Panel>
        {locations.length > 1 && (
          <div><Btn icon="copy" onClick={() => { for (const l of locations) if (l.name !== current.name) save(l.name, { enabled, week }); }}>Use these hours for every location</Btn></div>
        )}
      </div>
      <Holidays holidays={holidays} locations={locations.map((l) => l.name)} />
    </>
  );
}

function Holidays({ holidays, locations }: { holidays: Holiday[]; locations: string[] }) {
  const [name, setName] = useState('');
  const [date, setDate] = useState('');
  const [where, setWhere] = useState<string[]>([]);
  const today = todayKey();
  const add = () => {
    bridge.setHolidays([...holidays, { name: name.trim(), date, locations: where }]);
    setName(''); setDate(''); setWhere([]);
  };
  return (
    <div className="sgroup"><h3>Holidays</h3><p>Closed all day. A customer who writes on a holiday starts waiting at the next opening. Only locations with opening hours switched on are affected.</p>
      <div className="panel" style={{ padding: 0 }}>
        {holidays.map((h) => (
          <div key={`${h.date}|${h.locations.join('|')}`} style={h.date < today ? { opacity: 0.6 } : undefined}>
            <SettingRow title={h.name} detail={`${longDate(h.date)}${h.date < today ? ' · passed' : ''}`} columns="minmax(0,1fr) auto auto">
              <span className="sub">{h.locations.length ? h.locations.join(', ') : 'All locations'}</span>
              <Btn kind="quiet" icon="x" onClick={() => bridge.setHolidays(holidays.filter((x) => x !== h))}>Remove</Btn>
            </SettingRow>
          </div>
        ))}
        {holidays.length === 0 && <SettingRow title="No holidays yet" detail="Add the days a location is closed." />}
        <div className="srow" style={{ display: 'grid', gap: 10, gridTemplateColumns: 'minmax(0,1fr)' }}>
          <b>Add a holiday</b>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'end' }}>
            <label className="field"><span>Name</span><input value={name} maxLength={80} placeholder="For example, National day" onChange={(e) => setName(e.target.value)} /></label>
            <label className="field"><span>Date</span><input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></label>
            {locations.length > 1 && (
              <div className="field"><span>Where</span><div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', minHeight: 36, alignItems: 'center' }}>
                {locations.map((l) => (
                  <label key={l} style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 13 }}>
                    <input type="checkbox" checked={where.includes(l)} onChange={(e) => setWhere(e.target.checked ? [...where, l] : where.filter((x) => x !== l))} />{l}
                  </label>
                ))}
                <span className="sub">{where.length ? '' : 'None ticked: all locations'}</span>
              </div></div>
            )}
            <Btn icon="cal" kind="primary" disabled={!name.trim() || !date} onClick={add}>Add</Btn>
          </div>
        </div>
      </div>
    </div>
  );
}

const HOURS = Array.from({ length: 24 }, (_, h) => h);
const hour = (h: number) => new Date(2000, 0, 1, h).toLocaleTimeString(undefined, { hour: 'numeric' });

function Notifications({ state }: ScreenProps) {
  const s = state.settings;
  const alerts: [keyof typeof s.alerts, string, string][] = [
    ['nearTarget', 'A customer is about to pass the target', '2 minutes before, while the location is open'],
    ['waitedHour', 'Someone has waited over an hour', 'Once per customer'],
    ['signedOut', 'An account needs signing in again', 'As soon as the app notices'],
    ['callNotReturned', 'A missed call has not been returned', '30 minutes after the call, with no call back or reply'],
    ['readerStopped', 'An account has stopped being read', 'After three failed reads in a row, once until it reads again'],
    ['unhappyReview', 'A one- or two-star review arrives', 'Within an hour of it appearing, once per review'],
  ];
  // Settings merge one level deep, so a nested group is always sent whole.
  const setAlert = (key: keyof typeof s.alerts, on: boolean) => bridge.setSettings({ alerts: { ...s.alerts, [key]: on } });
  const setQuiet = (patch: Partial<typeof s.quietHours>) => bridge.setSettings({ quietHours: { ...s.quietHours, ...patch } });
  return (
    <>
      <div className="sgroup"><h3>Tell me when</h3>
        <p>Windows notifications with Open chat and Snooze buttons. They name the customer and the account, never the message.</p>
        <div className="panel" style={{ padding: 0 }}>
          {alerts.map(([key, title, detail]) => (
            <SettingRow key={key} title={title} detail={detail}><Toggle label={title} on={s.alerts[key]} onChange={(v) => setAlert(key, v)} /></SettingRow>
          ))}

        </div>
      </div>
      <div className="grid2" style={{ alignItems: 'start' }}>
        <div className="sgroup"><h3>Quiet hours</h3><div className="panel" style={{ padding: 0 }}>
          <SettingRow title="Hold alerts back" detail="Reading goes on and the tray count still updates."><Toggle label="Quiet hours" on={s.quietHours.enabled} onChange={(v) => setQuiet({ enabled: v })} /></SettingRow>
          <SettingRow title="From" detail="Quiet hours can run past midnight."><Stepper label="quiet hours start" value={s.quietHours.startHour} options={HOURS} format={hour} onChange={(v) => setQuiet({ startHour: v })} /></SettingRow>
          <SettingRow title="Until"><Stepper label="quiet hours end" value={s.quietHours.endHour} options={HOURS} format={hour} onChange={(v) => setQuiet({ endHour: v })} /></SettingRow>
        </div></div>
        <div className="sgroup"><h3>Summaries</h3><div className="panel" style={{ padding: 0 }}>
          <SettingRow title="Morning digest" detail="Opens the first time the app is opened each day: who is still owed a reply, and how yesterday went">
            <Toggle label="Morning digest" on={s.morningDigest} onChange={(v) => bridge.setSettings({ morningDigest: v })} />
          </SettingRow>
          <SettingRow title="Weekly report" detail="Saves last week’s PDF from Monday 10 am, to Documents › Unified Messenger reports">
            <Toggle label="Weekly report" on={s.weeklyReport.autoSave} onChange={(v) => bridge.setSettings({ weeklyReport: { ...s.weeklyReport, autoSave: v } })} />
          </SettingRow>
        </div></div>
      </div>
    </>
  );
}

/** The local assistant: off until switched on, reusing an Ollama already on this PC, and downloading anything only
 *  when a button here is pressed. */
function AssistantSettings({ state }: ScreenProps) {
  const settings = state.settings.assistant;
  const view = state.assistant;
  const engine = view.state;
  const set = (patch: Partial<typeof settings>) => bridge.setSettings({ assistant: { ...settings, ...patch } });
  const chosen = view.models.find((m) => m.model === settings.model);
  const busy = engine.phase === 'downloading-runtime' || engine.phase === 'downloading-model';
  const tooBig = chosen && view.memoryGB > 0 && view.memoryGB < chosen.minMemoryGB;
  return (
    <>
      <div className="sgroup"><h3>Assistant</h3>
        <p>Answers questions about waiting, replies, reviews and calls, and drafts replies to copy. It runs entirely on this PC, through a free program called Ollama; nothing it reads or writes leaves this PC.</p>
        <div className="panel" style={{ padding: 0 }}>
          <SettingRow title="Use the assistant" detail="Off by default. Nothing is downloaded or started until this is on.">
            <Toggle label="Use the assistant" on={settings.enabled} onChange={(v) => set({ enabled: v })} />
          </SettingRow>
          <SettingRow title="Model" detail={`Suggested for this PC's ${view.memoryGB} GB of memory: ${view.suggested.label}. Larger answers better and more slowly.`}>
            <Seg label="Model size" value={settings.model} onChange={(v) => set({ model: v })}
              options={view.models.map((m) => [m.model, `${m.label}, ${m.sizeGB} GB`] as const)} />
          </SettingRow>
        </div>
        {tooBig && <p className="sub" style={{ marginTop: 8 }}>This PC has less memory than the {chosen!.label.toLowerCase()} model needs ({chosen!.minMemoryGB} GB); answers may be very slow.</p>}
      </div>
      {settings.enabled && (
        <Panel style={{ display: 'grid', gap: 10 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 12 }}>
            <b role="status" style={{ fontWeight: 600 }}>{view.sentence}</b>
            {engine.phase === 'ready' && <Chip tone="ok" icon="check">Ready</Chip>}
          </div>
          {busy && <div className="progress" role="progressbar" aria-label="Download" aria-valuenow={Math.round((engine.progress ?? 0) * 100)} aria-valuemin={0} aria-valuemax={100}><i style={{ width: `${Math.round((engine.progress ?? 0) * 100)}%` }} /></div>}
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {engine.phase === 'no-runtime' && <Btn icon="download" kind="primary" onClick={() => bridge.installAssistant()}>Download Ollama (about 1.2 GB)</Btn>}
            {engine.phase === 'no-model' && <Btn icon="download" kind="primary" onClick={() => bridge.pullAssistantModel()}>Download the model ({chosen?.sizeGB ?? '?'} GB)</Btn>}
            {engine.phase === 'error' && <Btn icon="refresh" onClick={() => set({ enabled: true })}>Try again</Btn>}
          </div>
          <div style={{ display: 'flex', gap: 18, flexWrap: 'wrap' }} className="sub">
            <span style={{ opacity: engine.runtime ? 1 : 0.6 }}><Icon name={engine.runtime ? 'check' : 'download'} size={13} stroke={2} /> Ollama {engine.runtime ? 'found' : 'needed'}</span>
            <span style={{ opacity: engine.phase === 'ready' ? 1 : 0.6 }}><Icon name={engine.phase === 'ready' ? 'check' : 'download'} size={13} stroke={2} /> Model {engine.phase === 'ready' ? 'on this PC' : 'needed'}</span>
          </div>
        </Panel>
      )}
      <div className="sgroup"><h3>What it can see</h3>
        <div className="panel" style={{ padding: 0 }}>
          <SettingRow title="Figures and waiting customers" detail="Counts, waits, names and previews the app already shows you, when you ask a question." />
          <SettingRow title="A chat's messages, when you ask for a reply" detail="Only the chat that is open, only when you press Suggest a reply, and only what WhatsApp has loaded for it." />
          <SettingRow title="Customer notes" detail="Never: your notes stay out of the assistant." />
        </div>
      </div>
    </>
  );
}

function Workspace({ state, nav }: ScreenProps) {
  const c = state.cloud;
  const since = c.phase === 'signed-in' ? new Date(c.since).toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric' }) : '';
  return (
    <>
      <div className="sgroup"><h3>Your sign-in</h3>
        <div className="panel" style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
          {c.phase === 'signed-in' ? (
            <>
              <div style={{ display: 'grid', gap: 2 }}><b style={{ fontWeight: 600 }}>Signed in as {c.name || c.email}</b><span className="sub">{c.name ? `${c.email} · ` : ''}since {since}</span></div>
              <div style={{ marginLeft: 'auto' }}><Btn onClick={() => bridge.signOut()}>Sign out</Btn></div>
            </>
          ) : c.phase === 'waiting' ? (
            <>
              <span role="status">Finish signing in in your browser.</span>
              <div style={{ marginLeft: 'auto' }}><Btn onClick={() => bridge.cancelSignIn()}>Cancel</Btn></div>
            </>
          ) : c.phase === 'unavailable' ? (
            <span className="sub">Sign-in is not available in this build of the app.</span>
          ) : (
            <>
              <div style={{ display: 'grid', gap: 2 }}><b style={{ fontWeight: 600 }}>Not signed in</b>
                <span className={c.error ? 'late' : 'sub'} role={c.error ? 'alert' : undefined}>{c.error ?? 'Sign in to start a workspace, or to bring this business’s setup to this PC.'}</span></div>
              <div style={{ marginLeft: 'auto' }}><Btn kind="primary" onClick={() => bridge.signIn()}>Sign in with Google</Btn></div>
            </>
          )}
        </div>
      </div>
      {c.phase === 'signed-in' && state.owner.isOwner && (
        <div className="sgroup"><h3>Unified Messenger</h3>
          <div className="panel" style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
            <div style={{ display: 'grid', gap: 2 }}><b style={{ fontWeight: 600 }}>You are signed in as the product owner</b>
              <span className="sub">The owner console lists every workspace, how many members each has and when one was last seen, and suspends or restores one.</span></div>
            <div style={{ marginLeft: 'auto' }}><Btn icon="key" onClick={() => nav.go('owner')}>Owner console</Btn></div>
          </div>
        </div>
      )}
      {c.phase === 'signed-in' && <YourWorkspace state={state} />}
      {c.phase === 'signed-in' && state.workspace.phase === 'member' && <Members state={state} nav={nav} />}
      <div className="grid2">
        <Panel title="If a PC goes offline for a week"><p className="sub" style={{ margin: 0 }}>After 7 days without checking in, the app asks that PC to reconnect before it shows anything. A removed member cannot keep reading by staying offline.</p></Panel>
        <Panel title="What syncs"><p className="sub" style={{ margin: 0 }}>Accounts, locations, opening hours, holidays, targets and saved replies. Customer data never syncs.</p></Panel>
      </div>
    </>
  );
}

/** The workspace this PC belongs to (6.3): start one from this PC's setup, or see that the setup is in step. */
function YourWorkspace({ state }: { state: UiState }) {
  const w = state.workspace;
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const create = async () => {
    setBusy(true); setError('');
    const r = await bridge.createWorkspace(name);
    setBusy(false);
    if (r.error) setError(r.error);
  };
  const synced = w.phase === 'member' && w.syncedAt ? new Date(w.syncedAt).toLocaleString('en-GB', { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit', hour12: true }) : '';
  return (
    <div className="sgroup"><h3>Your workspace</h3>
      <div className="panel" style={{ display: 'grid', gap: 10 }}>
        {(w.phase === 'checking' || w.phase === 'signed-out') && <span className="sub" role="status">Looking for your workspace…</span>}
        {w.phase === 'error' && <><span className="late" role="alert">{w.error}</span><div><Btn onClick={() => bridge.syncWorkspace()}>Try again</Btn></div></>}
        {w.phase === 'none' && w.invitations.map((i) => (
          <div key={i.id} style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
            <div style={{ display: 'grid', gap: 2 }}>
              <b style={{ fontWeight: 600 }}>You are invited to {i.workspaceName || 'a workspace'}</b>
              <span className="sub">As {i.role === 'admin' ? 'an admin' : 'a member'}. Joining brings its accounts, locations and business rules to this PC; each account then needs signing in here once. Accounts already on this PC stay.</span>
            </div>
            <div style={{ marginLeft: 'auto' }}><Btn kind="primary" disabled={busy} onClick={async () => { setBusy(true); setError(''); const r = await bridge.joinWorkspace(i.id); setBusy(false); if (r.error) setError(r.error); }}>Join {i.workspaceName}</Btn></div>
          </div>
        ))}
        {w.phase === 'none' && (
          <>
            <b style={{ fontWeight: 600 }}>{w.invitations.length ? 'Or start a workspace of your own' : 'No workspace yet'}</b>
            <span className="sub">Start one from this PC's setup. Its accounts, locations, opening hours, holidays, reply target, saved replies and not-a-customer rules are kept in the workspace, so another PC signed in to it gets them too. Logins, messages, customers and figures never leave this PC.</span>
            <div style={{ display: 'flex', gap: 8, alignItems: 'flex-end', flexWrap: 'wrap' }}>
              <label className="field" style={{ minWidth: 260 }}><span>Workspace name</span><input value={name} onChange={(e) => setName(e.target.value)} placeholder="The business’s name" maxLength={80} /></label>
              <Btn kind="primary" disabled={busy || !name.trim()} onClick={() => void create()}>{busy ? 'Starting…' : 'Start the workspace'}</Btn>
            </div>
            {error && <span className="late" role="alert">{error}</span>}
          </>
        )}
        {w.phase === 'member' && (
          <>
            <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
              <div style={{ display: 'grid', gap: 2 }}>
                <b style={{ fontWeight: 600 }}>{w.name}</b>
                <span className="sub">{w.role === 'admin' ? 'You are an admin: changes made here reach the workspace.' : 'You are a member: this PC takes its setup from the workspace.'}</span>
              </div>
              {w.status === 'suspended' && <Chip tone="late">Suspended</Chip>}
              <div style={{ marginLeft: 'auto' }}><Btn onClick={() => bridge.syncWorkspace()}>Sync now</Btn></div>
            </div>
            <span className="sub" role="status">{w.status === 'suspended' ? 'The workspace is suspended, so its setup is not read. This PC keeps the setup it has.' : synced ? `Setup in step with the workspace, last checked ${synced}.` : 'Bringing the setup from the workspace…'}</span>
            {w.note && <span className="due" role="alert">{w.note}</span>}
            {w.error && <span className="late" role="alert">{w.error}</span>}
          </>
        )}
        {w.phase === 'removed' && <span className="late" role="alert">This Google account was removed from {w.name || 'the workspace'}. Ask one of its admins to invite you again.</span>}
      </div>
    </div>
  );
}

/** When someone's PC last checked in, in words. */
function seen(ms: number) {
  if (!ms) return 'Not yet';
  const minutes = Math.max(0, (Date.now() - ms) / 60_000);
  return minutes < 2 ? 'Just now' : `${durationText(minutes)} ago`;
}

/** The members of the workspace (6.4). Everyone sees who is in it; admins invite, remove, restore and change roles.
 *  The app sends no email: an admin tells the person to sign in with the address they were invited with. */
function Members({ state }: ScreenProps) {
  const w = state.workspace;
  const [inviting, setInviting] = useState(false);
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<'admin' | 'member'>('member');
  const [confirm, setConfirm] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  if (w.phase !== 'member') return null;
  const admin = w.role === 'admin' && w.status === 'active';
  const me = state.cloud.phase === 'signed-in' ? state.cloud.email.toLowerCase() : '';
  const act = async (run: Promise<{ error?: string }>) => {
    setBusy(true); setError('');
    const r = await run;
    setBusy(false);
    if (r.error) setError(r.error);
    return !r.error;
  };
  const people = [...w.people].sort((a, b) => Number(a.status === 'removed') - Number(b.status === 'removed') || a.email.localeCompare(b.email));
  return (
    <div className="sgroup">
      <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}><h3>Members of the workspace</h3>
        {admin && <div style={{ marginLeft: 'auto' }}><Btn icon="users" kind="primary" onClick={() => { setInviting(!inviting); setError(''); }}>Invite someone</Btn></div>}</div>
      {inviting && admin && (
        <div className="panel" style={{ display: 'grid', gap: 10 }}>
          <div style={{ display: 'flex', gap: 8, alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <label className="field" style={{ minWidth: 280 }}><span>Their Google address</span><input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="name@example.com" /></label>
            <label className="field"><span>Role</span><select className="input" value={role} onChange={(e) => setRole(e.target.value === 'admin' ? 'admin' : 'member')}><option value="member">Member</option><option value="admin">Admin</option></select></label>
            <Btn kind="primary" disabled={busy || !email.trim()} onClick={async () => { if (await act(bridge.inviteMember(email, role))) { setEmail(''); setInviting(false); } }}>Save the invitation</Btn>
          </div>
          <span className="sub">The app sends no email. Tell them to open Unified Messenger and sign in with this Google address: the invitation is waiting there. Members see the setup and can change nothing shared; admins can change it and manage members.</span>
        </div>
      )}
      {error && <p className="late" role="alert" style={{ margin: 0 }}>{error}</p>}
      <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
        <table className="table"><thead><tr><th>Member</th><th>Role</th><th>Last online</th><th /></tr></thead><tbody>
          {people.map((p) => (
            <tr key={p.uid} style={p.status === 'removed' ? { opacity: 0.7 } : undefined}>
              <td><b style={{ fontWeight: 600 }}>{p.name || p.email}</b>{p.email === me && <span className="sub"> (you)</span>}<div className="sub">{p.email}</div></td>
              <td>{p.status === 'removed' ? <Chip tone="neutral">Removed</Chip> : <Chip tone={p.role === 'admin' ? 'ok' : 'neutral'}>{p.role === 'admin' ? 'Admin' : 'Member'}</Chip>}</td>
              <td>{seen(p.lastSeen)}</td>
              <td className="r">
                {admin && p.email !== me && confirm !== p.uid && (p.status === 'removed'
                  ? <Btn kind="quiet" disabled={busy} onClick={() => void act(bridge.setMemberStatus(p.uid, 'active'))}>Restore</Btn>
                  : <div style={{ display: 'inline-flex', gap: 6 }}>
                      <Btn kind="quiet" disabled={busy} onClick={() => void act(bridge.setMemberRole(p.uid, p.role === 'admin' ? 'member' : 'admin'))}>{p.role === 'admin' ? 'Make member' : 'Make admin'}</Btn>
                      <Btn kind="quiet" disabled={busy} onClick={() => setConfirm(p.uid)}>Remove</Btn>
                    </div>)}
                {confirm === p.uid && (
                  <div role="alertdialog" aria-label={`Remove ${p.name || p.email}`} style={{ display: 'grid', gap: 8, justifyItems: 'end', textAlign: 'right', maxWidth: 420, marginLeft: 'auto' }}>
                    <span className="sub">Their PCs sign out and wipe the logins they had from this workspace at their next check, which is when the app starts and every six hours while it runs. To cut access at once, also remove those PCs on the phone: WhatsApp › Linked devices.</span>
                    <div style={{ display: 'flex', gap: 6 }}>
                      <Btn kind="quiet" onClick={() => setConfirm(null)}>Cancel</Btn>
                      <Btn kind="danger" disabled={busy} onClick={async () => { if (await act(bridge.setMemberStatus(p.uid, 'removed'))) setConfirm(null); }}>Remove and wipe their logins</Btn>
                    </div>
                  </div>
                )}
              </td>
            </tr>
          ))}
          {w.invites.map((i) => (
            <tr key={i.email}>
              <td><b style={{ fontWeight: 600 }}>{i.email}</b><div className="sub">Invited {seen(i.invitedAt).replace('Just now', 'just now')}</div></td>
              <td><Chip tone="due">Invite waiting</Chip></td>
              <td className="sub">{i.role === 'admin' ? 'As an admin' : 'As a member'}</td>
              <td className="r">{admin && <Btn kind="quiet" disabled={busy} onClick={() => void act(bridge.withdrawInvite(i.email))}>Withdraw</Btn>}</td>
            </tr>
          ))}
        </tbody></table>
      </div>
    </div>
  );
}

/** Sentences the owner keeps to hand. They are copied into a chat by the owner: the app never sends one. */
function SavedReplies({ state }: ScreenProps) {
  const replies = state.settings.savedReplies;
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const save = (next: { title: string; body: string }[]) => bridge.setSettings({ savedReplies: next });
  const add = () => {
    if (!body.trim()) return;
    save([...replies, { title: title.trim() || body.trim().slice(0, 24), body: body.trim() }]);
    setTitle('');
    setBody('');
  };
  return (
    <>
      <Panel title="Saved replies" note="Kept on this PC and shown beside every chat, where you copy one and send it yourself. The app cannot send messages.">
        <div className="saved" style={{ marginBottom: 12 }}>
          {replies.map((r, i) => (
            <div key={`${r.title}:${i}`}>
              <span><b>{r.title}</b>{r.body}</span>
              <Btn icon="x" title={`Remove ${r.title}`} onClick={() => save(replies.filter((_, at) => at !== i))}>Remove</Btn>
            </div>
          ))}
          {replies.length === 0 && <p className="sub" style={{ margin: 0 }}>None yet. The first one could be the sentence you type most often.</p>}
        </div>
        <div className="grid2" style={{ gap: 12, alignItems: 'start' }}>
          <label className="field"><span>Name</span>
            <input value={title} maxLength={40} placeholder="For example, Prices" onChange={(e) => setTitle(e.target.value)} />
          </label>
          <label className="field"><span>Reply</span>
            <textarea value={body} rows={3} maxLength={1200} placeholder="Our current price list is…"
              onChange={(e) => setBody(e.target.value)}
              style={{ resize: 'vertical', border: '1px solid var(--line-2)', borderRadius: 8, background: 'var(--raised)', padding: '8px 11px', font: 'inherit', fontSize: 13.5, color: 'var(--ink)' }} />
          </label>
        </div>
        <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
          <Btn icon="check" kind="primary" disabled={!body.trim()} onClick={add}>Add reply</Btn>
          <span className="sub" style={{ alignSelf: 'center' }}>{replies.length} saved</span>
        </div>
      </Panel>
    </>
  );
}

/** Who is never counted: staff, the team's own numbers, suppliers. Group chats, broadcasts and channels are left
 *  out already by the readers. Every chat these rules leave out is listed in Set aside with the rule that did it. */
function NotCustomers({ state }: { state: UiState }) {
  const rules = state.settings.notCustomers;
  const [words, setWords] = useState(rules.words.join(', '));
  const [numbers, setNumbers] = useState(rules.numbers.join('\n'));
  const saveWords = () => bridge.setSettings({ notCustomers: { ...rules, words: words.split(',').map((w) => w.trim()).filter(Boolean) } });
  const saveNumbers = () => bridge.setSettings({ notCustomers: { ...rules, numbers: numbers.split(/[\n,;]+/).map((n) => n.trim()).filter(Boolean) } });
  const field = { border: '1px solid var(--line-2)', borderRadius: 8, background: 'var(--raised)', padding: '8px 11px', font: 'inherit', fontSize: 13.5, color: 'var(--ink)', width: '100%', boxSizing: 'border-box' as const };
  return (
    <div className="sgroup"><h3>Not customers</h3>
      <div className="panel" style={{ display: 'grid', gap: 14 }}>
        <p className="sub" style={{ margin: 0 }}>
          Chats that should never be counted: staff, the team's own numbers, suppliers. Group chats, broadcasts and channels are
          already left out. A single chat can also be marked from the chat itself, with <b>Not a customer</b>. Everything left
          out is listed in Set aside with the reason.
        </p>
        <label className="field"><span>Names containing any of these words</span>
          <input value={words} placeholder="For example: Staff, Team, Supplier" onChange={(e) => setWords(e.target.value)} onBlur={saveWords} style={field} />
          <span className="sub" style={{ fontSize: 12 }}>Separate words with commas. A whole word only: “Staff” leaves out “Bilal Staff”, not “Staffordshire”.</span>
        </label>
        <label className="field"><span>The team's own numbers</span>
          <textarea value={numbers} rows={3} placeholder={'0300 1234567\n+92 321 7654321'} onChange={(e) => setNumbers(e.target.value)} onBlur={saveNumbers} style={{ ...field, resize: 'vertical' }} />
          <span className="sub" style={{ fontSize: 12 }}>One per line, written any way: +92 300…, 0300…, with or without spaces. Kept on this PC.</span>
        </label>
        <span className="sub" style={{ fontSize: 12 }}>{rules.words.length} word{rules.words.length === 1 ? '' : 's'} and {rules.numbers.length} number{rules.numbers.length === 1 ? '' : 's'} in use. Saved when you click away.</span>
      </div>
    </div>
  );
}

function Privacy({ state }: ScreenProps) {
  const kept = state.kept;
  return (
    <>
      <div className="sgroup"><h3>Kept on this PC</h3>
        <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
          <table className="table"><thead><tr><th>What</th><th>Kept for</th><th className="r">Size</th><th>Where it is managed</th></tr></thead><tbody>
            {kept?.rows.map((k, i) => (
              <tr key={k.what}>
                <td><b style={{ fontWeight: 600 }}>{k.what}</b></td>
                <td className="sub">{k.kept}</td>
                <td className="r">{kept.sizes[i]}</td>
                <td className="sub">{k.where}</td>
              </tr>
            ))}
            {kept && <tr><td><b style={{ fontWeight: 600 }}>All of it</b></td><td /><td className="r"><b style={{ fontWeight: 600 }}>{kept.total}</b></td><td /></tr>}
          </tbody></table>
        </div>
        <p className="sub">Measured from this PC when the screen opened. Anything that could not be measured says so
          rather than showing nothing: an unread folder and an empty one are different answers.</p>
      </div>
      <div className="grid2">
        <Panel title="Sent off this PC"><p className="sub" style={{ margin: 0 }}>Your name and email to Google at sign-in, and the workspace setup: accounts, locations, hours, targets and saved replies. No messages, customers, figures or logins. No analytics, no crash reports.</p></Panel>
        <Panel title="The support log"><p className="sub" style={{ margin: 0 }}>app.log holds counts and timings only, never a name, number or message, so it can be sent to support as it is. Save a copy from Channel readers.</p></Panel>
      </div>
    </>
  );
}

function About({ state, nav }: ScreenProps) {
  const screens: [string, () => void][] = [
    ['Sign in with Google', () => nav.lock('sign-in')],
    ['A new PC: bring the accounts in', () => nav.lock('new-pc')],
    ['On the PC of someone removed', () => nav.lock('removed')],
    ['A suspended workspace', () => nav.lock('suspended')],
    ['Moving from the previous version', () => nav.lock('upgrade')],
    ['Offline', () => nav.setOffline(!nav.view.offline)],
    ['The morning digest', () => nav.go('digest')],
  ];
  return (
    <>
      <div className="sgroup"><h3>Unified Messenger 6</h3>
        <div className="panel" style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
          <div style={{ display: 'grid', gap: 2 }}><b style={{ fontWeight: 600 }}>Version {state.version || '6'}</b>
            <span className="sub" role="status">{updateSentence(state.update)}</span></div>
          <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
            {state.update.phase !== 'none' && <Btn icon="download" onClick={() => nav.open('update')}>Show the update</Btn>}
            <Btn icon="refresh" onClick={() => bridge.checkForUpdate()}>Check for updates</Btn>
          </div>
        </div>
        <Panel><p className="sub" style={{ margin: 0 }}>Every screen draws your own accounts and your own figures. Where something could not be read, it says so rather than showing a zero. Google reviews are the one thing still waiting: Google does not allow signing in inside the app, so they need its official API.</p></Panel>
      </div>
      <div className="sgroup"><h3>Preview the other screens</h3><p>Screens the app shows only in particular moments. Opened here they describe what they would say, from this PC’s own workspace — never an invented one.</p>
        <div className="panel" style={{ padding: 0 }}>
          {screens.map(([name, open]) => <SettingRow key={name} title={name}><Btn icon="open" onClick={open}>Show</Btn></SettingRow>)}
        </div>
      </div>
    </>
  );
}
