// ---- mount -------------------------------------------------------------------------------------------
const GROUPS = [
  ['work', 'The day’s work', 'What the owner opens the app for: who is waiting, where, and what to do next. Everything here is worked, not just read.'],
  ['accounts', 'Accounts and readers', 'Every login and every channel reader, with the evidence behind each number, so a quiet account can always be told apart from a broken one.'],
  ['reviews', 'Reviews', 'Google reviews for each location, worst and unanswered first. Reply drafts are copied, never posted by the app.'],
  ['reports', 'Reports', 'How the week actually went: reply times, lateness by location, busy hours, backlog, reopened chats and missed calls, with export for the people who were not watching.'],
  ['assistant', 'Assistant', 'Plain-language questions answered on this PC by a local model, off until switched on. Every answer shows the figures it came from.'],
  ['workspace', 'Workspace and membership', 'Google sign-in, one workspace per business, members invited by email, and safe removal of anyone who leaves.'],
  ['settings', 'Settings', 'Only settings the app obeys. Opening hours, holidays, notifications, the assistant and privacy live here.'],
  ['system', 'Shipping and system states', 'Moving from the previous version, updates, being offline, and what a notification looks like outside the app.'],
];
const slug = (t) => t.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
const shots = document.getElementById('shots');
shots.innerHTML = GROUPS.map(([key, name, blurb]) => {
  const list = boards.filter((b) => b.group === key).sort((a, b) => (a.order ?? 50) - (b.order ?? 50));
  if (!list.length) return '';
  return `<section class="group-head" id="g-${key}"><h2>${name}</h2><p>${blurb}</p></section>` + list.map((b) => `<section class="shot" id="${slug(b.title)}">
    <header>${'<div>'}${phaseChip(b.phase)}<h2>${b.title}</h2></div><p>${b.note}</p></header>
    <div class="frame"><div class="board">${b.html}</div></div>
  </section>`).join('');
}).join('');
document.getElementById('contents').innerHTML = GROUPS.map(([key, name]) => {
  const n = boards.filter((b) => b.group === key).length;
  return n ? `<a href="#g-${key}">${name}<b>${n}</b></a>` : '';
}).join('');
document.getElementById('pageTheme').innerHTML = theme3().replace(/^<div class="theme3"[^>]*>|<\/div>$/g, '');
document.addEventListener('click', (e) => { const b = e.target.closest('.theme3 button'); if (b) applyTheme(b.dataset.t); });
applyTheme(theme);
const fit = () => document.querySelectorAll('.frame').forEach((f) => f.style.setProperty('--s', String(f.clientWidth / 1440)));
new ResizeObserver(fit).observe(document.body);
fit();
