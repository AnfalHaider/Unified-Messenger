import { defineConfig } from '@playwright/test';

// Only tests/: the *.test.ts files beside the code belong to node --test, and Playwright's default pattern
// would pick them up too.
// The test timeout is the only limit on a quit (see `quit()` in tests/smoke.spec.ts), and a quit alone can take
// up to 90 seconds because it closes every account page in turn. A test does its work *and then* quits, so 90
// here left no room for the quit and failed healthy shutdowns. 180 costs nothing when things are well — a test
// averages about 13 seconds — and only a test that is already failing waits out the difference.
export default defineConfig({
  testDir: 'tests',
  timeout: 180_000,
  reporter: process.env.CI ? 'list' : 'line',
});
