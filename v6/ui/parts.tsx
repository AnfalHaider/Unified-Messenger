// The bridge to the main process, and the small pieces every screen is built from. Kept in one file: they are
// few, they change together, and none of them is worth a file of its own.
import type { Route, Tone, UiState } from '../app/view-model.ts';
import { Spark, toneInk } from './charts.tsx';
import { Icon, type IconName } from './icons.tsx';

declare global {
  interface Window {
    um: {
      onState(fn: (state: UiState) => void): void;
      ready(): void;
      navigate(route: Route, accountId?: string | null): void;
      readNow(): void;
      reloadAccount(accountId: string): void;
      sleepAccount(accountId: string): void;
      setSettings(patch: Record<string, unknown>): void;
      setTheme(theme: 'system' | 'light' | 'dark'): void;
      windowAction(action: 'minimise' | 'maximise' | 'close'): void;
    };
  }
}

/** Opened in a plain browser for design work there is no main process, so the screens draw sample state. */
export const isPreview = !(typeof window !== 'undefined' && window.um);

let previewSettings: ((patch: Record<string, unknown>) => void) | null = null;
export const onPreviewSettings = (fn: typeof previewSettings) => { previewSettings = fn; };

export const bridge: Window['um'] = !isPreview ? window.um : {
  onState() {}, ready() {}, navigate() {}, readNow() {}, reloadAccount() {}, sleepAccount() {}, windowAction() {},
  setSettings(patch) { previewSettings?.(patch); },
  setTheme(theme) { previewSettings?.({ theme }); },
};

// ---- navigation ------------------------------------------------------------------------------------------

/** Full-window screens that replace the shell: signing in, a removed PC, a paused workspace, the v5 move. */
export type LockScreen = 'sign-in' | 'new-pc' | 'removed' | 'suspended' | 'upgrade';
export type Overlay = 'palette' | 'needs' | 'add-account' | 'remove-member' | 'update';

export interface View { route: Route; accountId: string | null; sub: string; overlay: Overlay | null; lock: LockScreen | null; offline: boolean }

export interface Nav {
  view: View;
  go(route: Route, accountId?: string | null, sub?: string): void;
  open(overlay: Overlay | null): void;
  lock(screen: LockScreen | null): void;
  setOffline(offline: boolean): void;
}

export interface ScreenProps { state: UiState; nav: Nav }

// ---- pieces ----------------------------------------------------------------------------------------------

/** Marks a screen whose feature is not connected yet, so its figures cannot pass for the owner's own. */
export const Sample = () => <span className="sample" title="This screen's feature is not connected yet. The figures are invented."><Icon name="alert" size={12} />Sample figures, not connected yet</span>;

export function Headline({ title, children, actions, sample, eyebrow }: {
  title: React.ReactNode; children?: React.ReactNode; actions?: React.ReactNode; sample?: boolean; eyebrow?: React.ReactNode;
}) {
  return (
    <div className="headline">
      <div>
        {(eyebrow || sample) && <div style={{ display: 'flex', gap: 8, marginBottom: 8, flexWrap: 'wrap' }}>{eyebrow}{sample && <Sample />}</div>}
        <h1>{title}</h1>
        {children && <p>{children}</p>}
      </div>
      {actions && <div className="actions">{actions}</div>}
    </div>
  );
}

export const Btn = ({ icon, children, kind, onClick, disabled, title }: {
  icon?: IconName; children?: React.ReactNode; kind?: 'primary' | 'quiet' | 'danger'; onClick?: () => void; disabled?: boolean; title?: string;
}) => (
  <button className={`btn ${kind ?? ''}`} onClick={onClick} disabled={disabled} title={title}>
    {icon && <Icon name={icon} size={14} />}{children}
  </button>
);

export const Chip = ({ tone, icon, children }: { tone: Tone | 'neutral'; icon?: IconName; children: React.ReactNode }) => (
  <span className={`chip ${tone === 'neutral' ? 'neu' : tone}`}>{icon && <Icon name={icon} size={12} />}{children}</span>
);

export const Panel = ({ title, children, style, note }: { title?: React.ReactNode; children: React.ReactNode; style?: React.CSSProperties; note?: React.ReactNode }) => (
  <div className="panel" style={style}>
    {title && <h3>{title}</h3>}
    {note && <p className="sub" style={{ margin: '-4px 0 8px' }}>{note}</p>}
    {children}
  </div>
);

export function Seg<T extends string>({ options, value, onChange, label }: { options: readonly (T | readonly [T, React.ReactNode])[]; value: T; onChange?: (v: T) => void; label: string }) {
  return (
    <div className="seg" role="group" aria-label={label}>
      {options.map((o) => {
        const [key, text] = typeof o === 'string' ? [o, o] : o;
        return <button key={key} aria-pressed={key === value} onClick={() => onChange?.(key)}>{text}</button>;
      })}
    </div>
  );
}

export const Toggle = ({ on, onChange, label }: { on: boolean; onChange?: (on: boolean) => void; label: string }) => (
  <button className={`toggle ${on ? '' : 'off'}`} role="switch" aria-checked={on} aria-label={label} onClick={() => onChange?.(!on)} />
);

export interface Fact { label: string; value: string; unit: string; note: string; tone: Tone | 'neutral'; trend?: number[] }

export const Facts = ({ facts }: { facts: Fact[] }) => (
  <div className="facts" style={{ gridTemplateColumns: `repeat(${facts.length}, minmax(0, 1fr))` }}>
    {facts.map((f) => (
      <div className="fact" key={f.label}>
        <span>{f.label}</span>
        <div style={{ display: 'flex', alignItems: 'end', justifyContent: 'space-between', gap: 8 }}>
          <b className="num">{f.value}<small>{f.unit}</small></b>
          {f.trend && <Spark values={f.trend} width={70} height={26} />}
        </div>
        <em style={{ color: toneInk(f.tone) }}>{f.note}</em>
      </div>
    ))}
  </div>
);

export function Timeline({ items }: { items: readonly { at: string; tone: Tone | 'neutral'; title: string; detail: string }[] }) {
  const ring = (t: Tone | 'neutral') => (t === 'neutral' ? undefined : `var(--m-${t})`);
  return (
    <div className="tl-wrap"><div className="tl">
      {items.map((it, i) => (
        <div className="tl-row" key={i}>
          <time>{it.at}</time><i style={{ borderColor: ring(it.tone) }} />
          <div><b>{it.title}</b><span className="sub">{it.detail}</span></div>
        </div>
      ))}
    </div></div>
  );
}

export const Check = ({ icon, tone, title, children }: { icon: IconName; tone: Tone | 'neutral'; title: string; children: React.ReactNode }) => (
  <div className="check"><span style={{ color: toneInk(tone) }}><Icon name={icon} size={16} /></span><span><b>{title}</b><span>{children}</span></span></div>
);

export const SettingRow = ({ title, detail, children, columns }: { title: string; detail?: React.ReactNode; children?: React.ReactNode; columns?: string }) => (
  <div className="srow" style={columns ? { gridTemplateColumns: columns } : undefined}>
    <span><b>{title}</b>{detail && <span>{detail}</span>}</span>{children}
  </div>
);

export function Stepper({ value, options, format, onChange, label }: { value: number; options: number[]; format: (v: number) => string; onChange?: (v: number) => void; label: string }) {
  const i = Math.max(0, options.indexOf(value));
  return (
    <div className="stepper" role="group" aria-label={label}>
      <button aria-label={`Less ${label}`} disabled={i === 0} onClick={() => onChange?.(options[i - 1])}>−</button>
      <span className="num">{format(options[i] ?? value)}</span>
      <button aria-label={`More ${label}`} disabled={i === options.length - 1} onClick={() => onChange?.(options[i + 1])}>+</button>
    </div>
  );
}

export const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;
