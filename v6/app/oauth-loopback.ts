// Google's way in for a desktop app, used twice: signing in to the workspace (app/cloud.ts) and connecting a Google
// Business profile (app/google-api.ts). Google refuses sign-in inside the app's own pages, so it happens in the
// owner's own browser and comes back to a port this app listens on, once, on 127.0.0.1.
import { createServer } from 'node:http';

const PAGE = (title: string, line: string) => `<!doctype html><meta charset="utf-8"><title>Unified Messenger</title>
<body style="font-family:system-ui,sans-serif;display:grid;place-items:center;min-height:90vh;margin:0;color:#1d211e">
<div style="max-width:420px;text-align:center"><h1 style="font-size:20px;font-weight:600">${title}</h1><p style="color:#5f6660">${line}</p></div>`;

export interface LoopbackOptions {
  /** Opens the owner's browser at Google's page. */
  open: (url: string) => Promise<unknown>;
  /** The page to open, once the port is known. */
  url: (redirect: string) => string;
  /** Reads Google's reply: the code, or why there is none, in words. */
  read: (search: string) => { code: string } | { error: string };
  /** What the browser tab says when it is done. */
  done: { title: string; line: string };
  /** Hands back a way to stop waiting, for a Cancel button. */
  onCancel?: (cancel: () => void) => void;
  waitMs?: number;
}

/** Waits for Google's reply, then closes the port. Never leaves a listener behind. */
export async function loopback(o: LoopbackOptions): Promise<{ code: string; redirect: string } | { error: string }> {
  const server = createServer();
  try {
    await new Promise<void>((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve); });
    const redirect = `http://127.0.0.1:${(server.address() as { port: number }).port}`;
    const reply = await new Promise<{ code: string } | { error: string }>((resolve) => {
      const timer = setTimeout(() => resolve({ error: 'Nothing came back from the browser in five minutes. Try again.' }), o.waitMs ?? 5 * 60_000);
      o.onCancel?.(() => { clearTimeout(timer); resolve({ error: '' }); });
      server.on('request', (req, res) => {
        const url = new URL(req.url ?? '/', redirect);
        if (url.pathname !== '/') { res.writeHead(404).end(); return; }
        const r = o.read(url.search);
        res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' }).end('code' in r
          ? PAGE(o.done.title, o.done.line)
          : PAGE('Not done', `${r.error} You can close this tab.`));
        clearTimeout(timer);
        resolve(r);
      });
      void o.open(o.url(redirect)).catch(() => resolve({ error: 'The browser could not be opened.' }));
    });
    return 'code' in reply ? { code: reply.code, redirect } : reply;
  } finally {
    server.close();
  }
}
