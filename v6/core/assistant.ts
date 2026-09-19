// The local assistant's engine, as decisions: where Ollama is, which model suits this PC, how far a download has
// got, and how to say the engine's state in plain words. Everything runs on this PC through Ollama; nothing here
// sends anything anywhere. Main (app/assistant.ts) carries these out.
//
// Owner decisions, 2026-09-19: off until switched on; reuse an Ollama already on the PC and download one only when
// there is none; never start a second Ollama on the same port.

/** Where an Ollama can come from, in the order they are tried. */
export type RuntimeKind = 'running' | 'installed' | 'v5' | 'bundled';

export interface Runtime {
  kind: RuntimeKind;
  /** The program to start, or null when one is already answering. */
  exe: string | null;
  /** Where its models live, when it must be told (v5's and the app's own copy keep theirs apart). */
  models: string | null;
}

/**
 * The installed Ollamas to try, best first: the one the owner installed themselves (it keeps its models in the
 * owner's own folder, and may already have the model), then v5's bundled copy, then the one this app downloads.
 * Paths only; whether each exists is main's question.
 */
export function runtimeCandidates(env: { localAppData: string; data: string }): Runtime[] {
  const win = (...parts: string[]) => parts.join('\\');
  return [
    { kind: 'installed', exe: win(env.localAppData, 'Programs', 'Ollama', 'ollama.exe'), models: null },
    { kind: 'v5', exe: win(env.localAppData, 'UnifiedMessenger', 'ollama', 'runtime', 'ollama.exe'), models: win(env.localAppData, 'UnifiedMessenger', 'ollama', 'models') },
    { kind: 'bundled', exe: win(env.data, 'ollama', 'runtime', 'ollama.exe'), models: win(env.data, 'ollama', 'models') },
  ];
}

export interface ModelChoice { model: string; label: string; sizeGB: number; minMemoryGB: number }

/** The models offered, smallest first. Sizes are Ollama's download sizes, rounded up. */
export const MODELS: ModelChoice[] = [
  { model: 'gemma3:1b', label: 'Small', sizeGB: 0.9, minMemoryGB: 4 },
  { model: 'gemma3:4b', label: 'Balanced', sizeGB: 3.3, minMemoryGB: 8 },
  { model: 'gemma3:12b', label: 'Large', sizeGB: 8.1, minMemoryGB: 24 },
];

/** The largest model this PC's memory runs comfortably; the balanced one is the ceiling suggested, because answers
 *  must stay quick on a front-desk PC doing other work. */
export function suggestModel(totalMemoryGB: number): ModelChoice {
  const fits = MODELS.filter((m) => totalMemoryGB >= m.minMemoryGB && m.minMemoryGB <= 8);
  return fits.at(-1) ?? MODELS[0];
}

/** Whether a model name is one Ollama already has. Ollama answers "gemma3:4b"; ":latest" is implied when absent. */
export function hasModel(installed: string[], model: string): boolean {
  const norm = (m: string) => (m.includes(':') ? m : `${m}:latest`).toLowerCase();
  return installed.some((m) => norm(m) === norm(model));
}

/** One line of Ollama's pull stream as a fraction done, or null when it carries no sizes (the "pulling manifest"
 *  and "verifying" lines). */
export function pullFraction(line: { status?: unknown; total?: unknown; completed?: unknown }): number | null {
  const total = typeof line.total === 'number' ? line.total : 0;
  const done = typeof line.completed === 'number' ? line.completed : 0;
  return total > 0 ? Math.min(1, Math.max(0, done / total)) : null;
}

export type EnginePhase =
  | 'off' | 'checking' | 'no-runtime' | 'downloading-runtime' | 'starting' | 'no-model' | 'downloading-model' | 'ready' | 'error';

export interface EngineState {
  phase: EnginePhase;
  /** 0 to 1 while downloading. */
  progress: number | null;
  /** Where Ollama came from, once found. */
  runtime: RuntimeKind | null;
  /** Models Ollama has, once it answers. */
  models: string[];
  /** The model in use. */
  model: string;
  /** Why it is not ready, in words the owner can act on. */
  problem: string | null;
}

export const offState = (model: string): EngineState => ({ phase: 'off', progress: null, runtime: null, models: [], model, problem: null });

const SOURCE: Record<RuntimeKind, string> = {
  running: 'the Ollama already running on this PC',
  installed: 'the Ollama installed on this PC',
  v5: 'the Ollama the previous version downloaded',
  bundled: 'the Ollama this app downloaded',
};

/** The engine's state as one sentence for Settings. */
export function engineSentence(s: EngineState): string {
  const pct = s.progress === null ? '' : ` ${Math.round(s.progress * 100)}%`;
  switch (s.phase) {
    case 'off': return 'Off. Nothing is downloaded or started until it is switched on.';
    case 'checking': return 'Looking for Ollama on this PC…';
    case 'no-runtime': return 'Ollama is not on this PC yet. It is free and runs entirely on this PC.';
    case 'downloading-runtime': return `Downloading Ollama…${pct}`;
    case 'starting': return `Starting ${s.runtime ? SOURCE[s.runtime] : 'Ollama'}…`;
    case 'no-model': return `Ready to download the ${s.model} model.`;
    case 'downloading-model': return `Downloading the ${s.model} model…${pct}`;
    case 'ready': return `Ready, using ${s.model} through ${s.runtime ? SOURCE[s.runtime] : 'Ollama'}.`;
    default: return s.problem ?? 'The assistant stopped with an error.';
  }
}
