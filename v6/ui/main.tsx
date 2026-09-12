import { createRoot } from 'react-dom/client';
import { App } from './App.tsx';
// Bundled, not fetched: the app must work with no connection and never reach out on its own.
import '@fontsource-variable/instrument-sans/wdth.css';
import '@fontsource-variable/bricolage-grotesque/opsz.css';
import './tokens.css';

const root = document.getElementById('root');
if (!root) throw new Error('no #root in index.html');
createRoot(root).render(<App />);
