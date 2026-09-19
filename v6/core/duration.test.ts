import { test } from 'node:test';
import assert from 'node:assert/strict';
import { durationParts, durationText } from './duration.ts';

const H = 60, D = 24 * H;

test('under a day it is minutes and hours, as before', () => {
  assert.equal(durationText(0), '0 min');
  assert.equal(durationText(38.4), '38 min');
  assert.equal(durationText(2 * H + 5), '2 h 5 min');
  assert.equal(durationText(5 * H), '5 h');
  assert.equal(durationText(13 * H + 7), '13 h', 'minutes stop mattering past ten hours');
  assert.equal(durationText(23 * H + 59), '23 h');
});

test('from a day it is days, keeping the hours while they still matter', () => {
  assert.equal(durationText(24 * H), '1 day');
  assert.equal(durationText(46 * H), '1 day 22 h');
  assert.equal(durationText(47 * H + 30), '1 day 23 h');
  assert.equal(durationText(50 * H), '2 days 2 h');
  assert.equal(durationText(88 * H), '4 days');
  assert.equal(durationText(148 * H), '6 days');
});

test('then weeks, months and years', () => {
  assert.equal(durationText(14 * D), '2 weeks');
  assert.equal(durationText(20 * D), '3 weeks');
  assert.equal(durationText(36 * D), '5 weeks');
  assert.equal(durationText(60 * D), '2 months');
  assert.equal(durationText(200 * D), '7 months');
  assert.equal(durationText(400 * D), '1 year');
  assert.equal(durationText(800 * D), '2 years');
});

test('the parts split the number from its unit, for screens that draw the number large', () => {
  assert.deepEqual(durationParts(46 * H), ['1', 'day 22 h']);
  assert.deepEqual(durationParts(148 * H), ['6', 'days']);
  assert.deepEqual(durationParts(-5), ['0', 'min'], 'a clock skew never shows a negative wait');
});
