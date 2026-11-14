// Where a clip's beats are in time and in its bars (src/preview/time.js): the same answers
// as TempoMap and Bars in Shosa.Core (Shosa.Tests/MusicTests.cs asks the same of the same song).
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';

const text = readFileSync(new URL('../src/preview/time.js', import.meta.url), 'utf8');
const { barAt, beatAt, markText, marksAt, secondsAt, tempoAt } = await import('data:text/javascript,' + encodeURIComponent(text));

// A: two bars of 4/4 at 120, slowing to 60 after the first. B: two bars of 3/4, still at 60.
const changing = {
  tempo: 120, beatsPerBar: 4,
  tempos: [{ beat: 0, bpm: 120 }, { beat: 4, bpm: 60 }],
  meters: [{ beat: 0, beatsPerBar: 4, beatUnit: 4 }, { beat: 8, beatsPerBar: 3, beatUnit: 4 }],
  sections: [{ name: 'A', t0: 0, t1: 8, pickup: 0, bar: 1 }, { name: 'B', t0: 8, t1: 14, pickup: 0, bar: 3 }],
};

test('a change of tempo moves the seconds, not the beats', () => {
  assert.deepEqual([0, 4, 6, 14].map(b => secondsAt(changing, b)), [0, 2, 4, 12]);
  assert.deepEqual([0, 2, 3, 12].map(s => beatAt(changing, s)), [0, 4, 5, 14]);
  assert.deepEqual([0, 3.9, 4, 13].map(b => tempoAt(changing, b)), [120, 120, 60, 60]);
});

test('a change of time changes the bars', () => {
  assert.deepEqual([7, 8, 11, 13].map(b => barAt(changing, b)), [[2, 3], [3, 0], [4, 0], [4, 2]]);
});

test('a clip with one tempo and one time needs neither list', () => {
  const plain = { tempo: 90, beatsPerBar: 3, sections: [{ t0: 0, t1: 7, pickup: 1, bar: 1 }] };
  assert.equal(secondsAt(plain, 3), 2);
  assert.equal(beatAt(plain, 2), 3);
  // The upbeat belongs to the bar before the first.
  assert.deepEqual([0, 1, 5].map(b => barAt(plain, b)), [[0, 2], [1, 0], [2, 1]]);
});

test('a mark shows from its moment for a little while, by the clock and not the beat', () => {
  const marked = { ...changing, figures: [{}, {}], marks: [
    { beat: 2, name: 'hit', dancer: 'ann' }, { beat: 2, name: 'hit', dancer: 'ben' }, { beat: 6, name: 'land', dancer: 'ann' }] };
  const names = beat => marksAt(marked, beat).map(m => m.name + ' ' + m.dancer);
  assert.deepEqual(names(1.9), []);
  assert.deepEqual(names(2), ['hit ann', 'hit ben']);
  // 0.6 seconds: more than a beat at 120, less than a beat at 60.
  assert.deepEqual(names(3.1), ['hit ann', 'hit ben']);
  assert.deepEqual(names(3.3), []);
  assert.deepEqual(names(6.5), ['land ann']);
  assert.deepEqual(names(6.7), []);

  assert.equal(markText(marked, marksAt(marked, 2)), '!hit ann, ben');
  assert.equal(markText({ ...marked, figures: [{}] }, marksAt(marked, 6)), '!land');
  assert.equal(markText(marked, []), '');
});
