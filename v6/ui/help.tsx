// Help: the page for the screen you are on, in a drawer beside it (the ? in the title bar, or F1), and every page on
// the Help screen. The pages are Markdown in help/, bundled with the app so they work offline; the pictures are taken
// from invented data by `npm run help:shots`, so they can never show a real customer.
//
// The Markdown is a small, fixed subset, drawn as React elements rather than injected HTML: headings, paragraphs,
// lists, **bold**, `keys`, [links](help:page), ![pictures](shot:name) and > notes.
import { Fragment, type ReactNode } from 'react';
import { HELP_FOR_ROUTE, HELP_PAGES, pageById, type HelpGroup } from './help-index.ts';
import { Icon } from './icons.tsx';
import { Btn, Headline, type Nav, type ScreenProps } from './parts.tsx';

const SOURCES = import.meta.glob('../help/*.md', { query: '?raw', import: 'default', eager: true }) as Record<string, string>;
const SHOTS = import.meta.glob('../help/shots/*.jpg', { import: 'default', eager: true }) as Record<string, string>;

const sourceOf = (id: string) => SOURCES[`../help/${id}.md`] ?? `# ${id}\n\nThis page is missing.`;
const shotOf = (name: string) => SHOTS[`../help/shots/${name}.jpg`];

// ---- the renderer ----------------------------------------------------------------------------------------

function inline(text: string, go: (id: string) => void): ReactNode[] {
  const parts = text.split(/(\*\*[^*]+\*\*|`[^`]+`|\[[^\]]+\]\(help:[a-z-]+\))/g);
  return parts.map((part, i) => {
    if (part.startsWith('**') && part.endsWith('**')) return <b key={i}>{part.slice(2, -2)}</b>;
    if (part.startsWith('`') && part.endsWith('`')) return <kbd key={i}>{part.slice(1, -1)}</kbd>;
    const link = /^\[([^\]]+)\]\(help:([a-z-]+)\)$/.exec(part);
    if (link) return <button key={i} className="help-link" onClick={() => go(link[2])}>{link[1]}</button>;
    return <Fragment key={i}>{part}</Fragment>;
  });
}

export function Markdown({ source, go, skipTitle }: { source: string; go: (id: string) => void; skipTitle?: boolean }) {
  const blocks: ReactNode[] = [];
  const lines = source.replace(/\r\n/g, '\n').split('\n');
  for (let i = 0; i < lines.length;) {
    const line = lines[i];
    if (!line.trim()) { i++; continue; }
    const key = blocks.length;
    const image = /^!\[([^\]]*)\]\(shot:([a-z-]+)\)$/.exec(line.trim());
    if (line.startsWith('# ')) { if (!skipTitle) blocks.push(<h2 key={key}>{line.slice(2)}</h2>); i++; continue; }
    if (line.startsWith('## ')) { blocks.push(<h3 key={key}>{inline(line.slice(3), go)}</h3>); i++; continue; }
    if (image) {
      const src = shotOf(image[2]);
      blocks.push(<figure key={key}>{src ? <img src={src} alt={image[1]} loading="lazy" /> : null}<figcaption>{image[1]}</figcaption></figure>);
      i++; continue;
    }
    const list = /^(- |\d+\. )/;
    if (list.test(line)) {
      const ordered = /^\d+\. /.test(line);
      const items: string[] = [];
      while (i < lines.length && list.test(lines[i])) { items.push(lines[i].replace(list, '')); i++; }
      const children = items.map((t, n) => <li key={n}>{inline(t, go)}</li>);
      blocks.push(ordered ? <ol key={key}>{children}</ol> : <ul key={key}>{children}</ul>);
      continue;
    }
    if (line.startsWith('> ')) {
      const note: string[] = [];
      while (i < lines.length && lines[i].startsWith('> ')) { note.push(lines[i].slice(2)); i++; }
      blocks.push(<aside key={key} className="help-note"><Icon name="alert" size={15} /><p>{inline(note.join(' '), go)}</p></aside>);
      continue;
    }
    const para: string[] = [];
    while (i < lines.length && lines[i].trim() && !/^(#|- |\d+\. |> |!\[)/.test(lines[i])) { para.push(lines[i]); i++; }
    blocks.push(<p key={key}>{inline(para.join(' '), go)}</p>);
  }
  return <div className="help-page">{blocks}</div>;
}

// ---- the drawer: help for this screen --------------------------------------------------------------------

export function HelpDrawer({ nav }: { nav: Nav }) {
  const id = HELP_FOR_ROUTE[nav.view.route] ?? 'start';
  const page = pageById(id);
  const open = (target: string) => { nav.open(null); nav.go('help', null, target); };
  return (
    <aside className="drawer help-drawer" aria-label={`Help: ${page.title}`}>
      <div className="help-drawer-top">
        <span className="sub">Help</span>
        <h2>{page.title}</h2>
        <span style={{ display: 'flex', gap: 6 }}>
          <Btn icon="open" kind="quiet" onClick={() => open(id)}>All help</Btn>
          <Btn icon="x" kind="quiet" onClick={() => nav.open(null)}>Close</Btn>
        </span>
      </div>
      <div className="help-drawer-body"><Markdown source={sourceOf(id)} go={open} skipTitle /></div>
    </aside>
  );
}

// ---- the Help screen: every page ----------------------------------------------------------------------------

const GROUPS: HelpGroup[] = ['Start here', 'Guides', 'Screens'];

export function HelpScreen({ nav }: ScreenProps) {
  const id = HELP_PAGES.some((p) => p.id === nav.view.sub) ? nav.view.sub : 'start';
  const go = (target: string) => nav.go('help', null, target);
  return (
    <main className="main">
      <Headline title="Help">How each screen works, what every part of it shows, and the keys. Press <kbd>F1</kbd> on any screen for its own page.</Headline>
      <div className="help-screen">
        <nav aria-label="Help pages" className="help-nav">
          {GROUPS.map((g) => (
            <div key={g}>
              <h4>{g}</h4>
              {HELP_PAGES.filter((p) => p.group === g).map((p) => (
                <button key={p.id} aria-current={p.id === id ? 'page' : undefined} onClick={() => go(p.id)}>{p.title}</button>
              ))}
            </div>
          ))}
        </nav>
        <article className="panel help-article" aria-label={pageById(id).title}><Markdown source={sourceOf(id)} go={go} /></article>
      </div>
    </main>
  );
}
