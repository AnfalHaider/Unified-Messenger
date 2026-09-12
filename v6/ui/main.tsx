import { createRoot } from 'react-dom/client';
import { App } from './App.tsx';
import './tokens.css';

const root = document.getElementById('root');
if (!root) throw new Error('no #root in index.html');
createRoot(root).render(<App />);
