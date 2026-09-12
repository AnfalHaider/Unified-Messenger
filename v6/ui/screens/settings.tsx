// Settings. "Look and reading" and "Notifications" write to config.json and take effect at once. Opening hours,
// the assistant, the workspace, the summaries and parts of privacy are sample settings until their features
// are wired, and say so.
import { useState } from 'react';
import { Icon, type IconName } from '../icons.tsx';
import { bridge, Btn, Chip, Headline, Panel, Sample, Seg, SettingRow, Stepper, Toggle, type ScreenProps } from '../parts.tsx';
import { ALERTS, HOLIDAYS, KEPT, MEMBERS, OPENING_HOURS, SAMPLE_LOCATIONS } from '../sample.ts';

export const SETTINGS_SECTIONS = ['Look and reading', 'Opening hours', 'Notifications', 'Assistant', 'Workspace', 'Privacy', 'About'] as const;
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
          {section === 'Opening hours' && <Hours />}
          {section === 'Notifications' && <Notifications {...props} />}
          {section === 'Assistant' && <AssistantSettings />}
          {section === 'Workspace' && <Workspace {...props} />}
          {section === 'Privacy' && <Privacy />}
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
          <SettingRow title="Leave out chats that ended themselves" detail="A last message like “ok thanks” is not someone waiting.">
            <Toggle label="Leave out chats that ended themselves" on={s.filterClosedConversations} onChange={(v) => set({ filterClosedConversations: v })} />
          </SettingRow>
        </div>
      </div>
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

function Hours() {
  const [location, setLocation] = useState(SAMPLE_LOCATIONS[0]);
  return (
    <>
      <div className="sgroup">
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}><h3>Opening hours</h3><Sample /><div style={{ marginLeft: 'auto' }}><Seg label="Location" value={location} onChange={setLocation} options={SAMPLE_LOCATIONS} /></div></div>
        <p><Icon name="cloud" size={13} /> Shared with the workspace. The reply clock pauses outside these hours.</p>
        <Panel><div className="hours">{OPENING_HOURS.map(([d, o, c, n]) => [<b key={d} style={{ fontWeight: 600 }}>{d}</b>, <span key={d + 'o'} className="input">{o}</span>, <span key={d + 'c'} className="input">{c}</span>, <span key={d + 'n'} className="sub">{n}</span>])}</div></Panel>
      </div>
      <div className="sgroup"><h3>Holidays</h3><p>Customers who write on these days start waiting at the next opening.</p>
        <div className="panel" style={{ padding: 0 }}>
          {HOLIDAYS.map((h) => <div key={h.name} style={h.past ? { opacity: 0.6 } : undefined}><SettingRow title={h.name} detail={h.date}><span className="sub">{h.where}</span></SettingRow></div>)}
          <SettingRow title="Add a holiday or a closed afternoon"><Btn icon="cal" disabled title="Not connected yet">Add</Btn></SettingRow>
        </div>
      </div>
    </>
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
          {ALERTS.map((a) => (
            <SettingRow key={a.title} title={a.title} detail={a.detail}><span className="sub">Not connected yet</span></SettingRow>
          ))}
        </div>
      </div>
      <div className="grid2" style={{ alignItems: 'start' }}>
        <div className="sgroup"><h3>Quiet hours</h3><div className="panel" style={{ padding: 0 }}>
          <SettingRow title="Hold alerts back" detail="Reading goes on and the tray count still updates."><Toggle label="Quiet hours" on={s.quietHours.enabled} onChange={(v) => setQuiet({ enabled: v })} /></SettingRow>
          <SettingRow title="From" detail="Quiet hours can run past midnight."><Stepper label="quiet hours start" value={s.quietHours.startHour} options={HOURS} format={hour} onChange={(v) => setQuiet({ startHour: v })} /></SettingRow>
          <SettingRow title="Until"><Stepper label="quiet hours end" value={s.quietHours.endHour} options={HOURS} format={hour} onChange={(v) => setQuiet({ endHour: v })} /></SettingRow>
        </div></div>
        <div className="sgroup"><div style={{ display: 'flex', gap: 12, alignItems: 'center' }}><h3>Summaries</h3><Sample /></div><div className="panel" style={{ padding: 0 }}>
          <SettingRow title="Morning digest" detail="When the app first opens each day"><Toggle label="Morning digest" on /></SettingRow>
          <SettingRow title="Weekly report" detail="Monday, 10:00 am, as a PDF"><Toggle label="Weekly report" on /></SettingRow>
        </div></div>
      </div>
    </>
  );
}

function AssistantSettings() {
  const [enabled, setEnabled] = useState(false);
  const [model, setModel] = useState('4B');
  return (
    <>
      <div className="sgroup"><div style={{ display: 'flex', gap: 12, alignItems: 'center' }}><h3>Assistant</h3><Sample /></div>
        <p>Answers questions about waiting, replies, reviews and calls, and drafts replies to copy. It runs entirely on this PC.</p>
        <div className="panel" style={{ padding: 0 }}>
          <SettingRow title="Use the assistant" detail="Off by default. Nothing is downloaded until this is on."><Toggle label="Use the assistant" on={enabled} onChange={setEnabled} /></SettingRow>
          <SettingRow title="Model" detail="Chosen from this PC’s memory, so answers stay quick.">
            <Seg label="Model size" value={model} onChange={setModel} options={[['1B', 'Small, 1B'], ['4B', 'Balanced, 4B'], ['12B', 'Large, 12B']] as const} />
          </SettingRow>
        </div>
      </div>
      {enabled && (
        <Panel style={{ display: 'grid', gap: 10 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}><b style={{ fontWeight: 600 }}>Downloading the {model} model</b><span className="sub num">2.1 of 3.3 GB · about 4 minutes left</span></div>
          <div className="progress"><i style={{ width: '64%' }} /></div>
          <div style={{ display: 'flex', gap: 18 }} className="sub"><span><Icon name="check" size={13} stroke={2} /> Engine installed</span><span><Icon name="download" size={13} /> Model downloading</span><span style={{ opacity: 0.6 }}>Ready to answer</span></div>
        </Panel>
      )}
      <div className="sgroup"><h3>What it can see</h3>
        <div className="panel" style={{ padding: 0 }}>
          <SettingRow title="Figures and waiting customers" detail="Counts, times, names and previews the app already shows you."><Toggle label="Figures" on /></SettingRow>
          <SettingRow title="A chat’s last messages, when you ask for a reply" detail="Only that conversation, only when you press Suggest a reply."><Toggle label="Chat messages" on /></SettingRow>
          <SettingRow title="Customer notes" detail="Off: notes stay out of the assistant unless you allow it."><Toggle label="Customer notes" on={false} /></SettingRow>
        </div>
      </div>
    </>
  );
}

function Workspace({ nav }: ScreenProps) {
  return (
    <>
      <div className="sgroup">
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}><h3>Members of the workspace</h3><Sample />
          <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}><Btn kind="quiet" icon="key" onClick={() => nav.go('owner')}>Owner console</Btn><Btn icon="users" kind="primary" disabled title="Invitations are not connected yet">Invite someone</Btn></div></div>
        <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
          <table className="table"><thead><tr><th>Member</th><th>Role</th><th>PCs</th><th>Last online</th><th /></tr></thead><tbody>
            {MEMBERS.map((m) => (
              <tr key={m.name} style={m.role === 'Removed' ? { opacity: 0.7 } : undefined}>
                <td><b style={{ fontWeight: 600 }}>{m.name}</b><div className="sub">{m.email}</div></td>
                <td><Chip tone={m.role === 'Admin' ? 'ok' : m.role === 'Invited' ? 'due' : 'neutral'}>{m.role === 'Invited' ? 'Invite waiting' : m.role}</Chip></td>
                <td className="sub">{m.pcs}</td><td>{m.seen}</td>
                <td className="r">{m.role === 'Member' && <Btn kind="quiet" onClick={() => nav.open('remove-member')}>Remove</Btn>}{m.role === 'Invited' && <Btn disabled title="Not connected yet">Resend</Btn>}</td>
              </tr>
            ))}
          </tbody></table>
        </div>
      </div>
      <div className="grid2">
        <Panel title="If a PC goes offline for a week"><p className="sub" style={{ margin: 0 }}>After 7 days without checking in, the app asks that PC to reconnect before it shows anything. A removed member cannot keep reading by staying offline.</p></Panel>
        <Panel title="What syncs"><p className="sub" style={{ margin: 0 }}>Accounts, locations, opening hours, holidays, targets and saved replies. Customer data never syncs.</p></Panel>
      </div>
    </>
  );
}

function Privacy() {
  return (
    <>
      <div className="sgroup"><div style={{ display: 'flex', gap: 12, alignItems: 'center' }}><h3>Kept on this PC</h3><Sample /></div>
        <div className="panel" style={{ padding: 0, overflow: 'hidden' }}>
          <table className="table"><thead><tr><th>What</th><th>Kept for</th><th className="r">Size</th><th /></tr></thead><tbody>
            {KEPT.map((k) => <tr key={k.what}><td><b style={{ fontWeight: 600 }}>{k.what}</b></td><td className="sub">{k.kept}</td><td className="r">{k.size}</td><td className="r">{k.action && <Btn kind="quiet" disabled title="Not connected yet">{k.action}</Btn>}</td></tr>)}
          </tbody></table>
        </div>
      </div>
      <div className="grid2">
        <Panel title="Sent off this PC"><p className="sub" style={{ margin: 0 }}>Your name and email to Google at sign-in, and the workspace setup: accounts, locations, hours, targets and saved replies. No messages, customers, figures or logins. No analytics, no crash reports.</p></Panel>
        <Panel title="The support log"><p className="sub" style={{ margin: 0 }}>app.log holds counts and timings only, never a name, number or message, so it can be sent to support as it is.</p></Panel>
      </div>
    </>
  );
}

function About({ nav }: ScreenProps) {
  const screens: [string, () => void][] = [
    ['Sign in with Google', () => nav.lock('sign-in')],
    ['A new PC: bring the accounts in', () => nav.lock('new-pc')],
    ['On the PC of someone removed', () => nav.lock('removed')],
    ['A suspended workspace', () => nav.lock('suspended')],
    ['Moving from the previous version', () => nav.lock('upgrade')],
    ['An update, when it suits', () => nav.open('update')],
    ['Offline', () => nav.setOffline(!nav.view.offline)],
    ['The morning digest', () => nav.go('digest')],
  ];
  return (
    <>
      <div className="sgroup"><h3>Unified Messenger 6</h3>
        <Panel><p className="sub" style={{ margin: 0 }}>In development. The line, the docked page, accounts, readers and the reading settings work on your real accounts. Screens marked “Sample figures” show their final layout with invented numbers until their feature is connected.</p></Panel>
      </div>
      <div className="sgroup"><h3>Preview the other screens</h3><p>Screens the app shows only in particular moments, so they can be reviewed now.</p>
        <div className="panel" style={{ padding: 0 }}>
          {screens.map(([name, open]) => <SettingRow key={name} title={name}><Btn icon="open" onClick={open}>Show</Btn></SettingRow>)}
        </div>
      </div>
    </>
  );
}
