// The synthesiser of the previews (src/preview/sound.js), against a WebAudio that only keeps
// a list of what it was asked to play. Run with `npm test`.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';

// As text, so that it does not matter what kind of module Node takes a .js here to be.
const text = readFileSync(new URL('../src/preview/sound.js', import.meta.url), 'utf8');
const { createSound } = await import('data:text/javascript,' + encodeURIComponent(text));

const param = () => ({ value: 0, setValueAtTime() {}, linearRampToValueAtTime() {}, exponentialRampToValueAtTime() {}, setTargetAtTime() {} });
const node = extra => ({ connect: to => to, disconnect() {}, ...extra });

class FakeAudio {
  currentTime = 0;
  sampleRate = 8000;
  destination = node();
  started = [];   // [seconds, pitch] of every oscillator
  resume() {}
  createGain() { return node({ gain: param() }); }
  createBiquadFilter() { return node({ frequency: param() }); }
  createDynamicsCompressor() { return node(); }
  createBuffer(_, length) { return { getChannelData: () => new Float32Array(length) }; }
  createBufferSource() { return node({ start: when => this.started.push([when, 'noise']), stop() {} }); }
  createOscillator() {
    const frequency = param();
    return node({ frequency, start: when => this.started.push([when, Math.round(69 + 12 * Math.log2(frequency.value / 440))]), stop() {} });
  }
}

// A tune: a note on every beat of 8, rising from middle C, with a bass drum on the first.
const tune = [[0, 1, 36, 100, 9], ...Array.from({ length: 8 }, (_, i) => [i, 1, 60 + i, 90, 0])].sort((a, b) => a[0] - b[0]);
const secondsPerBeat = 0.5;

// Plays from one beat to another at 60 frames a second, as the previews' tick does.
function run(player, audio, from, to) {
  for (let beat = from; beat < to; beat += 1 / 60 / secondsPerBeat) {
    audio.currentTime = beat * secondsPerBeat;
    player.play(beat, secondsPerBeat);
  }
}

function begin() {
  let audio;
  globalThis.AudioContext = class extends FakeAudio { constructor() { super(); audio = this; } };
  const player = createSound();
  player.start();
  player.setSong(tune);
  return { player, audio: () => audio };
}

test('every note sounds once, on its beat', () => {
  const { player, audio } = begin();
  player.cue(0);
  run(player, audio(), 0, 8);

  const started = audio().started;
  assert.equal(started.length, tune.length);
  assert.deepEqual(started.map(s => s[1]).filter(p => p >= 60), [60, 61, 62, 63, 64, 65, 66, 67]);
  for (const [when, pitch] of started) {
    if (pitch >= 60) assert.ok(Math.abs(when - (pitch - 60) * secondsPerBeat) < 1e-9, `pitch ${pitch} at ${when}`);
  }
});

test('after a seek the notes go on from there', () => {
  const { player, audio } = begin();
  player.cue(5);
  run(player, audio(), 5, 8);
  assert.deepEqual(audio().started.map(s => s[1]), [65, 66, 67]);
});

test('what a hidden view missed is not played all at once', () => {
  const { player, audio } = begin();
  player.cue(0);
  run(player, audio(), 0, 1.5);
  run(player, audio(), 6.5, 8);   // no frames between
  assert.deepEqual(audio().started.map(s => s[1]).filter(p => p >= 60), [60, 61, 67]);
});

test('a loop goes back to the first note', () => {
  const { player, audio } = begin();
  player.cue(6);
  run(player, audio(), 6, 8);
  player.rewind();
  run(player, audio(), 0, 1.5);
  assert.deepEqual(audio().started.map(s => s[1]).filter(p => p >= 60), [66, 67, 60, 61]);
});

test('without a song, or before the click that allows sound, nothing happens', () => {
  globalThis.AudioContext = FakeAudio;
  const player = createSound();
  player.setSong(tune);
  player.cue(0);
  player.play(0, secondsPerBeat);   // no start(): no AudioContext yet
  player.start();
  player.setSong(null);
  player.cue(0);
  player.play(0, secondsPerBeat);
});
