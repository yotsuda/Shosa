// The timing of the sound-font player (src/preview/fontsound.js), against a synthesiser that
// only writes down what it is told, and oscillators that only count.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';

const text = readFileSync(new URL('../src/preview/fontsound.js', import.meta.url), 'utf8');
const { createFontSound } = await import('data:text/javascript,' + encodeURIComponent(text));

function setup(ready = true) {
  const said = [];
  const port = {
    ready, clock: 10,
    start: () => said.push(['start']),
    now() { return this.clock; },
    program: (c, p, t) => said.push(['program', c, p, +t.toFixed(3)]),
    noteOn: (c, k, v, t) => said.push(['on', c, k, v, +t.toFixed(3)]),
    noteOff: (c, k, t) => said.push(['off', c, k, +t.toFixed(3)]),
    bend: (c, v, t) => said.push(['bend', c, v, +t.toFixed(3)]),
    quiet: t => said.push(['quiet', +t.toFixed(3)]),
  };
  const plain = { played: 0, cued: 0, start() {}, setSong() {}, cue() { this.cued++; }, rewind() {}, play() { this.played++; } };
  return { said, port, plain, player: createFontSound(port, plain) };
}

// [beat, beats, pitch, velocity, timbre, program, channel]: a flute tune, a bass with no program, a drum.
const song = [
  [0, 1, 60, 80, 1, 73, 0],
  [0, 2, 36, 70, 5, -1, 1],
  [0, 0.5, 38, 100, 9, -1, 9],
  [1, 1, 62, 80, 1, 73, 0],
  [2, 1, 62.5, 80, 1, 73, 0],
];

// Half a second a beat, a frame every 50 ms: plays from `from` for `beats`.
function run(port, player, from, beats) {
  for (let beat = from; beat < from + beats - 1e-9; beat += 0.1) {
    player.play(beat, 0.5);
    port.clock += 0.05;
  }
}

test('every note is struck once, at its beat, with its instrument on its channel', () => {
  const { said, port, player } = setup();
  player.setSong(song);
  player.cue(0);
  run(port, player, 0, 3.5);

  const ons = said.filter(e => e[0] === 'on');
  assert.deepEqual(ons, [['on', 0, 60, 80, 10], ['on', 1, 36, 70, 10], ['on', 9, 38, 100, 10], ['on', 0, 62, 80, 10.5], ['on', 0, 62, 80, 11]]);
  // The flute its part names; the bass of the timbre family; nothing for the drums; and only once.
  assert.deepEqual(said.filter(e => e[0] === 'program'), [['program', 0, 73, 10], ['program', 1, 33, 10]]);
  // Each ends a moment before its length is up, so that the same key struck again then is a new note.
  assert.deepEqual(said.filter(e => e[0] === 'off'), [['off', 9, 38, 10.24], ['off', 0, 60, 10.49], ['off', 1, 36, 10.99], ['off', 0, 62, 10.99], ['off', 0, 62, 11.49]]);
  // The quarter tone: the wheel up a quarter of the way to the next key but one, and back when it ends.
  assert.deepEqual(said.filter(e => e[0] === 'bend'), [['bend', 0, 8192 + 2048, 11], ['bend', 0, 8192, 11.49]]);
});

test('a stop silences what was handed over, and the notes go on from the new beat', () => {
  const { said, port, player } = setup();
  player.setSong(song);
  player.cue(0);
  run(port, player, 0, 0.5);
  said.length = 0;

  player.cue(1);
  assert.equal(said[0][0], 'quiet');
  run(port, player, 1, 0.3);
  assert.deepEqual(said.filter(e => e[0] === 'on').map(e => e[2]), [62]);
  // The instrument is said again: the silence may have been a new song.
  assert.deepEqual(said.filter(e => e[0] === 'program').map(e => e.slice(0, 3)), [['program', 0, 73]]);
  assert.equal(said.filter(e => e[0] === 'off').length, 0);   // the old notes' ends went with the silence
});

test('until the sound font is read the oscillators play, and it takes over where it becomes ready', () => {
  const { said, port, plain, player } = setup(false);
  player.setSong(song);
  player.cue(0);
  run(port, player, 0, 0.5);
  assert.equal(plain.played, 5);
  assert.deepEqual(said, []);

  port.ready = true;
  const cued = plain.cued;
  run(port, player, 0.5, 1);
  assert.equal(plain.played, 5);
  assert.equal(plain.cued, cued + 1);
  assert.deepEqual(said.filter(e => e[0] === 'on').map(e => e[2]), [62]);   // not the notes of beat 0 again
});

test('a loop goes back to the first note without a silence', () => {
  const { said, port, player } = setup();
  player.setSong(song);
  player.cue(0);
  run(port, player, 0, 3);
  said.length = 0;
  player.rewind();
  run(port, player, 0, 0.3);
  assert.deepEqual(said.filter(e => e[0] === 'on').map(e => e[2]), [60, 36, 38]);
  assert.equal(said.filter(e => e[0] === 'quiet').length, 0);
});
