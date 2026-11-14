// Sound through a sound font: the same song, the same clock as sound.js, but each note goes to
// a synthesiser that plays real instruments (synth.js: the sound font Lily# plays with).
//
// It has the shape of sound.js's player and wraps one: while the synthesiser is not ready — no
// sound font on this machine, or it is still being read — the oscillators play, and the
// synthesiser takes over from the beat it becomes ready at.
//
// The synthesiser is reached through a port, so that this file is only the timing and a test
// can stand in for it:
//   ready                      can it play now
//   start()                    a click or a key allows sound: begin
//   now()                      its clock, in seconds
//   program(channel, program, time)   noteOn(channel, key, velocity, time)   noteOff(channel, key, time)
//   bend(channel, value, time) the pitch wheel, 8192 at rest, 4096 a semitone
//   quiet(time)                everything sounding or queued before `time` stops there
export function createFontSound(port, fallback) {
  const Ahead = 0.15, Late = 0.1;   // seconds: how far ahead notes are handed over; how late one may still sound
  // The General MIDI instrument of a timbre family, for a part that names none:
  // piano, flute, clarinet, strings, guitar, bass, brass, organ, voice.
  const Families = [0, 73, 71, 48, 24, 33, 61, 19, 52];
  const Drums = 9, Centre = 8192, Semitone = 4096;

  let song = null;         // [beat, beats, pitch, velocity, timbre, program, channel], by beat
  let nextNote = 0;        // the first note not yet handed over
  let offs = [];           // notes sounding: when each ends
  let programs = new Map();// what each channel was last told to play
  let handedUntil = 0;     // the time of the last note handed over
  let quietAt = 0;         // nothing may start before this: what was queued is being silenced
  let onFont = false;      // the synthesiser, not the oscillators, has this run

  function seek(beat) {
    nextNote = 0;
    if (!song) return;
    let low = 0, high = song.length;
    while (low < high) {
      const middle = (low + high) >> 1;
      if (song[middle][0] < beat - 1e-6) low = middle + 1; else high = middle;
    }
    nextNote = low;
  }

  // The synthesiser keeps what it was handed in order of time, so the silence goes in behind
  // the last note handed over, and the next note after that.
  function hush() {
    if (port.ready && onFont) {
      quietAt = Math.max(port.now(), handedUntil) + 0.02;
      port.quiet(quietAt);
    }
    offs = [];
    programs = new Map();
  }

  return {
    start() {
      fallback.start();
      port.start();
    },

    setSong(notes) {
      song = notes;
      fallback.setSong(notes);
    },

    // What sounds stops, and the notes go on from this beat.
    cue(beat) {
      fallback.cue(beat);
      hush();
      seek(beat);
    },

    // Back to the first note without stopping what sounds: the join of a loop.
    rewind() {
      fallback.rewind();
      nextNote = 0;
    },

    // Every frame while playing: hand over the notes that start within the next moment.
    play(beat, secondsPerBeat) {
      if (!song) return;
      if (!port.ready) {
        onFont = false;
        fallback.play(beat, secondsPerBeat);
        return;
      }
      if (!onFont) {
        // It became ready (or this is its first run): the oscillators stop, it goes on from here.
        fallback.cue(beat);
        onFont = true;
        seek(beat);
      }

      const now = port.now();
      offs = offs.filter(off => {
        if (off.time > now + Ahead) return true;
        port.noteOff(off.channel, off.key, off.time);
        if (off.bent) port.bend(off.channel, Centre, off.time);
        return false;
      });

      const until = beat + Ahead / secondsPerBeat;
      for (; nextNote < song.length && song[nextNote][0] < until; nextNote++) {
        const [at, beats, pitch, velocity, timbre, program = -1, channel = 0] = song[nextNote];
        const wait = (at - beat) * secondsPerBeat;
        if (wait < -Late) continue;   // the view was hidden for a while: do not play what was missed all at once
        const drums = timbre === Drums || channel === Drums;
        const c = drums ? Drums : channel >= 0 && channel <= 15 ? channel : 0;
        const when = Math.max(now + Math.max(0, wait), quietAt + 0.01);
        if (!drums) {
          const instrument = program >= 0 && program <= 127 ? program : Families[timbre] ?? 0;
          if (programs.get(c) !== instrument) {
            programs.set(c, instrument);
            port.program(c, instrument, when);
          }
        }
        // A quarter tone is the key below, with the wheel up for as long as it sounds.
        const key = Math.max(0, Math.min(127, Math.floor(pitch + 0.01))), bent = pitch - key >= 0.01;
        if (bent) port.bend(c, Math.round(Centre + (pitch - key) * Semitone), when);
        port.noteOn(c, key, Math.max(1, Math.min(127, Math.round(velocity))), when);
        // It ends a moment early, so that the same key struck again at that instant is a new note.
        const seconds = Math.max(0.05, beats * secondsPerBeat);
        offs.push({ time: when + seconds - Math.min(0.01, seconds / 4), channel: c, key, bent });
        handedUntil = Math.max(handedUntil, when);
      }
    },
  };
}
