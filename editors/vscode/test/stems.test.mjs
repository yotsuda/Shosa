// The Mixer's rows and what sounds for its levels (src/preview/stems.js): Lily#'s rule for a
// recording with separated parts (its recording-design.md §8).
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';

const text = readFileSync(new URL('../src/preview/stems.js', import.meta.url), 'utf8');
const { soundingStems, stemRows, untouched } = await import('data:text/javascript,' + encodeURIComponent(text));

const piano = { kind: 'part', name: 'piano', file: 'p.wav', at: 0 };
const melody = { kind: 'voice', name: 'melody', file: 'v.wav', at: -0.3 };
const recording = { kind: 'recording', name: 'ito.mp3', file: 'ito.mp3', at: 0 };
const vocals = { kind: 'recordingPart', name: 'vocals', file: 'vocals.flac', at: 0 };
const band = { kind: 'recordingPart', name: 'band', file: 'band.flac', at: 0 };
const names = list => list.map(s => s.stem.name + ' ' + s.gain);

test('a row for each part and voice, and for each part of the recording instead of the recording', () => {
  assert.deepEqual(stemRows([piano, melody, recording, vocals, band]).map(r => r.id),
    ['part:piano', 'voice:melody', 'recording:vocals', 'recording:band']);
  assert.deepEqual(stemRows([piano, recording]).map(r => r.id), ['part:piano', 'recording:ito.mp3']);
});

test('a voice and a part of the same name are two rows', () => {
  const part = { ...piano, name: 'melody' };
  assert.deepEqual(stemRows([part, melody]).map(r => r.id), ['part:melody', 'voice:melody']);
});

test('everything at 100 %: the recording itself, not its parts', () => {
  const all = [piano, recording, vocals, band];
  assert.deepEqual(names(soundingStems(all, {})), ['piano 1', 'ito.mp3 1']);
});

test('a part of the recording off or not at 100 %: the parts that are on, and not the recording', () => {
  const all = [piano, recording, vocals, band];
  assert.deepEqual(names(soundingStems(all, { 'recording:vocals': { on: false, level: 1 } })), ['piano 1', 'band 1']);
  assert.deepEqual(names(soundingStems(all, { 'recording:band': { on: true, level: 0.5 } })), ['piano 1', 'vocals 1', 'band 0.5']);
});

test('a row off, or at 0, is silent; a level is kept within 0 to 200 %', () => {
  assert.deepEqual(names(soundingStems([piano, melody], { 'part:piano': { on: false, level: 1 }, 'voice:melody': { on: true, level: 5 } })), ['melody 2']);
  assert.deepEqual(names(soundingStems([piano], { 'part:piano': { on: true, level: 0 } })), []);
});

test('a recording without parts is its own row', () => {
  assert.deepEqual(names(soundingStems([recording], { 'recording:ito.mp3': { on: true, level: 0.8 } })), ['ito.mp3 0.8']);
});

test('only the recording as recorded leaves the limiter out', () => {
  assert.equal(untouched(soundingStems([recording, vocals], {})), true);
  assert.equal(untouched(soundingStems([recording], { 'recording:ito.mp3': { on: true, level: 0.8 } })), false);
  assert.equal(untouched(soundingStems([piano, recording], {})), false);
});

test('the recording\'s parts in the owner\'s order: vocal guitar piano other bass drums, the unknown after', () => {
  const part = name => ({ kind: 'recordingPart', name, file: name + '.flac', at: 0 });
  // As Orecchiante's file lists them for 糸 (Bank Band), and one it does not know.
  const stems = [piano, ...['drums', 'bass', 'other', 'strings', 'vocals', 'guitar', 'piano'].map(part), recording];
  assert.deepEqual(stemRows(stems).map(r => r.name), ['piano', 'vocals', 'guitar', 'piano', 'other', 'bass', 'drums', 'strings']);
});

