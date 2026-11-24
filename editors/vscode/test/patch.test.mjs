// What an edit changed, put into the clips as they were (src/preview/patch.js; the server's half is
// Shosa.Lsp\ClipPatch.cs, whose tests put its own patches into its own clips).
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { applyClipPatch } from '../src/preview/patch.js';

const clips = () => [
  {
    name: 'Long', totalBeats: 8, spans: [{ start: 10 }],
    figures: [
      { id: 'a', model: 'a.vrm', frames: [[0], [1], [2], [3]], turns: [[10], [11], [12], [13]], mic: { hand: 'right', up: [0, 0, 1, 1] } },
      { id: 'b', frames: [[5], [6], [7], [8]] },
    ],
  },
  { name: 'Short', totalBeats: 4, spans: [], figures: [{ id: 'a', frames: [[0], [1]] }] },
];

test('a clip, a figure and a row an edit did not touch are the same objects as before', () => {
  const was = clips();
  const now = applyClipPatch(was, { clips: [{ from: 0, figures: [{ rows: { frames: [[1, [21], [22]]] } }, null] }, { from: 1 }] });
  assert.equal(now[1], was[1]);
  assert.equal(now[0].figures[1], was[0].figures[1]);
  assert.notEqual(now[0], was[0]);
  assert.notEqual(now[0].figures[0], was[0].figures[0]);
  assert.deepEqual(now[0].figures[0].frames, [[0], [21], [22], [3]]);
  assert.equal(now[0].figures[0].frames[0], was[0].figures[0].frames[0]);
  assert.equal(now[0].figures[0].frames[3], was[0].figures[0].frames[3]);
  assert.equal(now[0].figures[0].turns, was[0].figures[0].turns);
  assert.equal(now[0].spans, was[0].spans);
  // What was is as it was.
  assert.deepEqual(was, clips());
});

test('several runs of rows, in several lists', () => {
  const now = applyClipPatch(clips(), { clips: [{ from: 0, figures: [{ rows: { frames: [[0, [20]], [3, [23]]], turns: [[2, [32], [33]]] } }, null] }, { from: 1 }] });
  assert.deepEqual(now[0].figures[0].frames, [[20], [1], [2], [23]]);
  assert.deepEqual(now[0].figures[0].turns, [[10], [11], [32], [33]]);
});

test('a head is the clip without its figures, whole: what it has no more is gone', () => {
  const was = clips();
  const now = applyClipPatch(was, { clips: [{ from: 0, head: { name: 'Long', totalBeats: 8, marks: [] } }, { from: 1 }] });
  assert.deepEqual(Object.keys(now[0]).sort(), ['figures', 'marks', 'name', 'totalBeats']);
  assert.equal(now[0].figures, was[0].figures);
});

test("a figure's head keeps its rows; a mic or a juggling changed comes whole, or goes", () => {
  const was = clips();
  const now = applyClipPatch(was, { clips: [{ from: 0, figures: [{ head: { id: 'a', model: 'b.vrm', modelHeight: 1.5 }, mic: null }, { juggle: { prop: 'balls' } }] }, { from: 1 }] });
  assert.deepEqual(now[0].figures[0], { id: 'a', model: 'b.vrm', modelHeight: 1.5, frames: was[0].figures[0].frames, turns: was[0].figures[0].turns });
  assert.equal(now[0].figures[0].frames, was[0].figures[0].frames);
  assert.deepEqual(now[0].figures[1].juggle, { prop: 'balls' });
});

test('clips come whole, go, and change places', () => {
  const was = clips();
  const brief = { name: 'Brief', figures: [] };
  const now = applyClipPatch(was, { clips: [{ from: 1 }, { whole: brief }] });
  assert.equal(now.length, 2);
  assert.equal(now[0], was[1]);
  assert.equal(now[1], brief);
  assert.deepEqual(applyClipPatch(was, { clips: [] }), []);
});
