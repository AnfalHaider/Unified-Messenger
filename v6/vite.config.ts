import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// Only the renderer needs a build. Electron runs app/ and core/ as TypeScript directly, which is why they
// stay strippable; this exists because a browser page cannot resolve node_modules on its own.
export default defineConfig({
  root: 'ui',
  base: './', // the packaged app loads the screens from file://
  plugins: [react()],
  build: { outDir: '../dist-ui', emptyOutDir: true },
  server: { port: 5173, strictPort: true },
});
