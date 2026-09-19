// The local assistant's engine: finds Ollama on this PC, starts it when needed, downloads it or a model only when
// the owner presses the button, and asks it questions. Every call goes to the endpoint in settings, which is this
// PC (127.0.0.1); nothing here talks to any other address except the two downloads the owner asks for.
//
// The decisions are in core/assistant.ts. This file only carries them out, and never throws at its callers: every
// failure becomes a state with a plain sentence.
import { spawn, execFile, type ChildProcess } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createWriteStream, existsSync, mkdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { Readable } from 'node:stream';
import { pipeline } from 'node:stream/promises';
import { hasModel, offState, pullFraction, runtimeCandidates, type EngineState, type Runtime } from '../core/assistant.ts';

/** v5's pinned Ollama release and its checksum: the download is checked before it is unpacked. */
const RUNTIME_URL = 'https://github.com/ollama/ollama/releases/download/v0.30.8/ollama-windows-amd64.zip';
const RUNTIME_SHA256 = 'c2d26d97e698027329c252629d7113bbc05d874b49960cbb03e93a39ae9fd95c';

export interface EngineDeps {
  data: string;
  localAppData: string;
  log: (entry: Record<string, unknown>) => void;
  /** Called on every change of state, so the screens can follow a download. */
  changed: () => void;
}

export interface ChatMessage { role: 'system' | 'user' | 'assistant'; content: string }

const withTimeout = async (url: string, init: RequestInit, ms: number) => {
  const signal = AbortSignal.timeout(ms);
  return fetch(url, { ...init, signal });
};

export class Engine {
  state: EngineState;
  private endpoint = 'http://127.0.0.1:11434/';
  private child: ChildProcess | null = null;
  private busy = false;

  private deps: EngineDeps;

  constructor(deps: EngineDeps, model: string) {
    this.deps = deps;
    this.state = offState(model);
  }

  private set(next: Partial<EngineState>) {
    this.state = { ...this.state, ...next };
    this.deps.changed();
  }

  private async alive() {
    try { return (await withTimeout(`${this.endpoint}api/version`, {}, 1500)).ok; } catch { return false; }
  }

  private async models(): Promise<string[]> {
    const res = await withTimeout(`${this.endpoint}api/tags`, {}, 5000);
    const body = await res.json() as { models?: { name?: string }[] };
    return (body.models ?? []).map((m) => m.name ?? '').filter(Boolean);
  }

  /** Starts `ollama serve` on the settings' port and waits up to twenty seconds for it to answer. */
  private async start(r: Runtime) {
    const host = new URL(this.endpoint).host;
    this.child = spawn(r.exe!, ['serve'], {
      env: { ...process.env, OLLAMA_HOST: host, ...(r.models ? { OLLAMA_MODELS: r.models } : {}) },
      stdio: 'ignore', windowsHide: true,
    });
    this.child.on('exit', (code) => {
      this.deps.log({ event: 'assistant-engine-exited', code });
      this.child = null;
    });
    for (let i = 0; i < 40; i++) {
      if (await this.alive()) return;
      await new Promise((r) => setTimeout(r, 500));
    }
    throw new Error('Ollama did not start. Another program may be using its port.');
  }

  /** Brings the engine to the state the settings ask for. Safe to call again at any time. */
  async ensure(settings: { enabled: boolean; model: string; endpoint: string }) {
    this.endpoint = settings.endpoint;
    if (!settings.enabled) { this.stop(); this.state = offState(settings.model); this.deps.changed(); return; }
    if (this.busy) return;
    this.busy = true;
    try {
      this.set({ phase: 'checking', model: settings.model, problem: null, progress: null });
      let runtime = this.state.runtime;
      if (await this.alive()) {
        runtime ??= 'running';
      } else {
        const found = runtimeCandidates({ localAppData: this.deps.localAppData, data: this.deps.data }).find((r) => r.exe && existsSync(r.exe));
        if (!found) { this.set({ phase: 'no-runtime' }); return; }
        this.set({ phase: 'starting', runtime: found.kind });
        await this.start(found);
        runtime = found.kind;
      }
      const models = await this.models();
      this.set({ runtime, models, phase: hasModel(models, settings.model) ? 'ready' : 'no-model' });
      this.deps.log({ event: 'assistant-engine', phase: this.state.phase, runtime, models: models.length });
    } catch (e) {
      this.set({ phase: 'error', problem: (e as Error).message.startsWith('Ollama') ? (e as Error).message : 'Ollama could not be reached on this PC.' });
      this.deps.log({ event: 'assistant-engine-failed', error: (e as Error).message.slice(0, 120) });
    } finally {
      this.busy = false;
    }
  }

  /** Downloads Ollama into the app's own folder, checks it, unpacks it. Only when the owner asks. */
  async installRuntime(settings: { enabled: boolean; model: string; endpoint: string }) {
    const dir = join(this.deps.data, 'ollama');
    const zip = join(dir, 'ollama.zip');
    try {
      mkdirSync(join(dir, 'runtime'), { recursive: true });
      this.set({ phase: 'downloading-runtime', progress: 0, problem: null });
      const res = await fetch(RUNTIME_URL);
      if (!res.ok || !res.body) throw new Error(`the download answered ${res.status}`);
      const total = Number(res.headers.get('content-length') ?? 0);
      const hash = createHash('sha256');
      let got = 0, shown = 0;
      const counted = Readable.fromWeb(res.body as never).on('data', (chunk: Buffer) => {
        hash.update(chunk);
        got += chunk.length;
        if (total && got / total - shown >= 0.01) { shown = got / total; this.set({ progress: shown }); }
      });
      await pipeline(counted, createWriteStream(zip));
      if (hash.digest('hex') !== RUNTIME_SHA256) throw new Error('the download did not match its checksum');
      await new Promise<void>((done, fail) => execFile('tar', ['-xf', zip, '-C', join(dir, 'runtime')], (err) => (err ? fail(err) : done())));
      rmSync(zip, { force: true });
      this.deps.log({ event: 'assistant-runtime-installed' });
    } catch (e) {
      rmSync(zip, { force: true });
      this.set({ phase: 'error', progress: null, problem: `Ollama could not be downloaded: ${(e as Error).message}.` });
      this.deps.log({ event: 'assistant-runtime-failed', error: (e as Error).message.slice(0, 120) });
      return;
    }
    this.state = { ...this.state, runtime: null };
    await this.ensure(settings);
  }

  /** Downloads a model through Ollama itself, following its progress. Only when the owner asks. */
  async pullModel(settings: { enabled: boolean; model: string; endpoint: string }) {
    try {
      this.set({ phase: 'downloading-model', progress: 0, problem: null });
      const res = await fetch(`${this.endpoint}api/pull`, { method: 'POST', body: JSON.stringify({ model: settings.model, stream: true }) });
      if (!res.ok || !res.body) throw new Error(`Ollama answered ${res.status}`);
      let rest = '';
      for await (const chunk of res.body as unknown as AsyncIterable<Uint8Array>) {
        rest += Buffer.from(chunk).toString('utf8');
        const lines = rest.split('\n');
        rest = lines.pop() ?? '';
        for (const l of lines) {
          if (!l.trim()) continue;
          const line = JSON.parse(l) as { status?: string; error?: string; total?: number; completed?: number };
          if (line.error) throw new Error(line.error);
          const f = pullFraction(line);
          if (f !== null) this.set({ progress: f });
        }
      }
      this.deps.log({ event: 'assistant-model-pulled', model: settings.model });
    } catch (e) {
      this.set({ phase: 'error', progress: null, problem: `The model could not be downloaded: ${(e as Error).message}.` });
      this.deps.log({ event: 'assistant-model-failed', error: (e as Error).message.slice(0, 120) });
      return;
    }
    await this.ensure(settings);
  }

  /** One answer from the local model. The caller builds the messages; nothing is kept here. */
  async chat(messages: ChatMessage[], timeoutMs = 120_000): Promise<string> {
    if (this.state.phase !== 'ready') throw new Error('The assistant is not ready.');
    const res = await withTimeout(`${this.endpoint}api/chat`, {
      method: 'POST',
      body: JSON.stringify({ model: this.state.model, messages, stream: false, options: { temperature: 0.1, num_ctx: 8192 } }),
    }, timeoutMs);
    if (!res.ok) throw new Error(`Ollama answered ${res.status}`);
    const body = await res.json() as { message?: { content?: string } };
    return (body.message?.content ?? '').trim();
  }

  /** Stops the Ollama this app started. One that was already running belongs to someone else and is left alone. */
  stop() {
    if (this.child) { this.child.kill(); this.child = null; }
  }
}
