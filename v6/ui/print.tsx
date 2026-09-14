// The page a report is saved from. Main opens it in a hidden window as index.html#print=weekly&week=last, sends it
// the same state the screens get, and turns it into a PDF or an image once it says it is drawn. It renders the
// weekly report component the Reports screen uses, and nothing else: no navigation, no reading, no account pages.
import { useEffect, useState } from 'react';
import type { UiState } from '../app/view-model.ts';
import { bridge } from './parts.tsx';
import { WeeklyDocument } from './screens/reports.tsx';

export const printRequest = () => {
  const params = new URLSearchParams(location.hash.replace(/^#/, ''));
  return params.get('print') === 'weekly' ? { week: params.get('week') === 'this' ? 'this' as const : 'last' as const } : null;
};

export function PrintApp({ week }: { week: 'this' | 'last' }) {
  const [state, setState] = useState<UiState | null>(null);
  useEffect(() => {
    // Reports are paper: always the light theme, whatever the app is set to.
    document.documentElement.dataset.theme = 'light';
    bridge.onState(setState);
    bridge.ready();
  }, []);
  useEffect(() => {
    if (!state?.reports) return;
    // Fonts and one frame first, so the saved page is the drawn page, not the page mid-layout.
    void document.fonts.ready.then(() => requestAnimationFrame(() => requestAnimationFrame(() => {
      const doc = document.querySelector('[data-weekly-doc]');
      bridge.printRendered(Math.ceil(doc?.getBoundingClientRect().bottom ?? document.body.scrollHeight));
    })));
  }, [state]);
  if (!state?.reports) return null;
  return (
    <div className="print-page">
      <WeeklyDocument doc={state.reports.weekly[week]} include={state.settings.weeklyReport.include} backlog={state.reports.backlog} scope={state.reports.scope} />
    </div>
  );
}
