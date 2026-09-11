// v5 had no direct tests for MetricMath; these pin the cases its comments describe.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { honestPercent } from './percent.ts';

test('the endpoints are claims, reserved for counts that support them', () => {
  assert.equal(honestPercent(996, 1000), 99);
  assert.equal(honestPercent(1, 1000), 1);
  assert.equal(honestPercent(5, 5), 100);
  assert.equal(honestPercent(0, 5), 0);
});

test('ordinary values round normally, and nothing measured reads 100', () => {
  assert.equal(honestPercent(2, 3), 67);
  assert.equal(honestPercent(95, 100), 95);
  assert.equal(honestPercent(0, 0), 100);
});
