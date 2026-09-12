import { defineConfig } from '@playwright/test';

// Only tests/: the *.test.ts files beside the code belong to node --test, and Playwright's default pattern
// would pick them up too.
export default defineConfig({
  testDir: 'tests',
  timeout: 90_000,
  reporter: process.env.CI ? 'list' : 'line',
});
