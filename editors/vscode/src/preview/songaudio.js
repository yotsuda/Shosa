// The song of a dance as samples, for a video's sound: the notes the preview plays, through the
// sound font it plays them with, rendered ahead of time instead of live (spessasynth_core, the
// engine under the preview's synthesiser; a .sf2, a .sf3 or a DLS bank alike).
//
// The notes are fontsound.js's — [beat, beats, pitch, velocity, timbre, program, channel] — and
// are turned into the same events: the instrument of a channel when it changes, a quarter tone
// as the key below with the wheel up, a note ended a moment early.
import { SoundBankLoader, SpessaSynthProcessor } from 'spessasynth_core';

const Families = [0, 73, 71, 48, 24, 33, 61, 19, 52];
const Drums = 9, Centre = 8192, Semitone = 4096;
const Block = 128;              // the synthesiser's own step: events fall on its edges
const Peak = Math.pow(10, -1 / 20);   // the loudest sample of the file: -1 dBFS

/** The events of the notes, in order of time: { t (seconds), kind, c, a, b }. `secondsAt(beat)` is the dance's clock. */
export function songEvents(notes, secondsAt) {
  const events = [], programs = new Map();
  let order = 0;
  const add = (t, kind, c, a, b) => events.push({ t, kind, c, a, b, order: order++ });
  for (const [at, beats, pitch, velocity, timbre, program = -1, channel = 0] of notes) {
    const drums = timbre === Drums || channel === Drums;
    const c = drums ? Drums : channel >= 0 && channel <= 15 ? channel : 0;
    const start = secondsAt(at), seconds = Math.max(0.05, secondsAt(at + beats) - start);
    if (!drums) {
      const instrument = program >= 0 && program <= 127 ? program : Families[timbre] ?? 0;
      if (programs.get(c) !== instrument) {
        programs.set(c, instrument);
        add(start, 'program', c, instrument);
      }
    }
    const key = Math.max(0, Math.min(127, Math.floor(pitch + 0.01))), bent = pitch - key >= 0.01;
    if (bent) add(start, 'bend', c, Math.round(Centre + (pitch - key) * Semitone));
    add(start, 'on', c, key, Math.max(1, Math.min(127, Math.round(velocity))));
    const end = start + seconds - Math.min(0.01, seconds / 4);
    add(end, 'off', c, key);
    if (bent) add(end, 'bend', c, Centre);
  }
  // By time; at the same instant in the order made (a note's own events keep theirs).
  return events.sort((x, y) => x.t - y.t || x.order - y.order);
}

/**
 * Renders `seconds` of the notes: { sampleRate, left, right } (Float32Array), the whole scaled
 * so its loudest sample stands at -1 dBFS. `font` is the sound font's bytes (an ArrayBuffer);
 * `pause`, when given, is awaited now and then so the page stays alive.
 */
export async function renderSong({ font, notes, secondsAt, seconds, sampleRate = 44100, pause }) {
  const synth = new SpessaSynthProcessor(sampleRate, { eventsEnabled: false });
  synth.soundBankManager.addSoundBank(SoundBankLoader.fromArrayBuffer(font), 'song');
  await synth.processorInitialized;

  const events = songEvents(notes, secondsAt);
  const total = Math.max(1, Math.round(seconds * sampleRate));
  const left = new Float32Array(total), right = new Float32Array(total);
  let next = 0;
  for (let filled = 0; filled < total;) {
    for (; next < events.length && Math.round(events[next].t * sampleRate) <= filled; next++) {
      const e = events[next];
      if (e.kind === 'program') synth.programChange(e.c, e.a);
      else if (e.kind === 'bend') synth.pitchWheel(e.c, e.a);
      else if (e.kind === 'on') synth.noteOn(e.c, e.a, e.b);
      else synth.noteOff(e.c, e.a);
    }
    const n = Math.min(Block, total - filled);
    synth.process(left, right, filled, n);
    filled += n;
    if (pause && filled % (Block * 1024) === 0) await pause();
  }
  synth.destroySynthProcessor();

  let peak = 0;
  for (let i = 0; i < total; i++) peak = Math.max(peak, Math.abs(left[i]), Math.abs(right[i]));
  if (peak > 0) {
    const gain = Peak / peak;
    for (let i = 0; i < total; i++) {
      left[i] *= gain;
      right[i] *= gain;
    }
  }
  return { sampleRate, left, right };
}
