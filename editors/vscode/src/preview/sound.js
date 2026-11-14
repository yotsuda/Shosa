// Sound: the song a clip is danced to (`music "song.lys"`), on WebAudio.
//
// The notes come in the clip's beats, and the figure's clock is the only clock: each frame,
// the notes that start within the next moment are handed to WebAudio, timed from the beat the
// figure is at. So the music cannot drift from the dance, at any speed, through any seek. The
// instruments are a few oscillators and noise: enough to hear the tune and the beat it is
// danced to, not how the song will sound (`shosa midi` and `shosa wav` are for that).
//
// Two pages use this file: the VS Code preview bundles it, and `shosa preview` writes it into
// the page (Shosa.Core embeds it and drops the `export`). So it imports nothing.
export function createSound() {
  const Ahead = 0.15, Late = 0.1;   // seconds: how far ahead notes are scheduled; how late one may still sound
  let song = null;                  // the notes: [beat, beats, pitch, velocity, timbre], by beat
  let nextNote = 0;                 // the first note not yet handed to WebAudio
  let audio = null, bus = null, noise = null;

  // Lily#'s timbre families. `decay`: struck or plucked, the sound dies away while it is held.
  const voices = [
    { wave: 'triangle', attack: 0.005, decay: 0.45 },             // piano
    { wave: 'sine', attack: 0.04 },                               // flute
    { wave: 'square', attack: 0.03, gain: 0.4 },                  // clarinet
    { wave: 'sawtooth', attack: 0.08, gain: 0.45 },               // strings
    { wave: 'triangle', attack: 0.003, decay: 0.3 },              // guitar
    { wave: 'triangle', attack: 0.005, decay: 0.6, gain: 1.5 },   // bass
    { wave: 'sawtooth', attack: 0.04, gain: 0.5 },                // brass
    { wave: 'square', attack: 0.01, gain: 0.3 },                  // organ
    { wave: 'triangle', attack: 0.05 },                           // voice
  ];

  function note(when, seconds, pitch, level, voice) {
    const oscillator = audio.createOscillator(), gain = audio.createGain();
    const peak = level * (voice.gain ?? 1), end = when + Math.max(0.06, seconds);
    oscillator.type = voice.wave;
    oscillator.frequency.value = 440 * 2 ** ((pitch - 69) / 12);
    gain.gain.setValueAtTime(0, when);
    const attack = Math.min(voice.attack, (end - when) / 2);
    gain.gain.linearRampToValueAtTime(peak, when + attack);
    if (voice.decay) gain.gain.setTargetAtTime(peak * 0.12, when + attack, voice.decay);
    gain.gain.setTargetAtTime(0, end, 0.03);
    oscillator.connect(gain).connect(bus.tone);
    oscillator.start(when);
    oscillator.stop(end + 0.25);
  }

  // The drum kit, by General MIDI number: a falling sine for the skins, filtered noise for the rest.
  function drum(when, pitch, level) {
    const gain = audio.createGain();
    gain.connect(bus.out);
    const skin = pitch === 35 || pitch === 36 ? [150, 45, 0.16] : [41, 43, 45, 47, 48, 50].includes(pitch) ? [90 + (pitch - 41) * 14, 60 + (pitch - 41) * 8, 0.2] : null;
    if (skin) {
      const oscillator = audio.createOscillator();
      oscillator.frequency.setValueAtTime(skin[0], when);
      oscillator.frequency.exponentialRampToValueAtTime(skin[1], when + skin[2] * 0.6);
      gain.gain.setValueAtTime(level * 2.5, when);
      gain.gain.exponentialRampToValueAtTime(0.0005, when + skin[2]);
      oscillator.connect(gain);
      oscillator.start(when);
      oscillator.stop(when + skin[2] + 0.02);
      return;
    }
    if (!noise) {
      noise = audio.createBuffer(1, audio.sampleRate, audio.sampleRate);
      const samples = noise.getChannelData(0);
      for (let i = 0; i < samples.length; i++) samples[i] = Math.random() * 2 - 1;
    }
    // [filter, frequency, seconds]: snare and clap; closed and open hi-hat; cymbals; anything else.
    const [type, frequency, seconds] = [37, 38, 39, 40].includes(pitch) ? ['bandpass', 1800, 0.14]
      : pitch === 42 || pitch === 44 ? ['highpass', 7000, 0.05]
      : pitch === 46 ? ['highpass', 7000, 0.28]
      : [49, 51, 52, 53, 55, 57, 59].includes(pitch) ? ['highpass', 5000, 0.5]
      : ['bandpass', 3000, 0.07];
    const source = audio.createBufferSource(), filter = audio.createBiquadFilter();
    source.buffer = noise;
    filter.type = type;
    filter.frequency.value = frequency;
    gain.gain.setValueAtTime(level * 1.4, when);
    gain.gain.exponentialRampToValueAtTime(0.0005, when + seconds);
    source.connect(filter).connect(gain);
    source.start(when);
    source.stop(when + seconds + 0.02);
  }

  return {
    // A browser lets sound start only from a click or a key: call this from one.
    start() {
      audio ??= new AudioContext();
      void audio.resume();
    },

    // The notes to play from now on (null: none). Follow it with cue().
    setSong(notes) {
      song = notes;
    },

    // What sounds stops, and the notes go on from this beat.
    cue(beat) {
      if (bus) {
        const old = bus;
        old.out.gain.setTargetAtTime(0, audio.currentTime, 0.015);
        setTimeout(() => old.out.disconnect(), 300);
        bus = null;
      }
      nextNote = 0;
      if (song) {
        let low = 0, high = song.length;
        while (low < high) {
          const middle = (low + high) >> 1;
          if (song[middle][0] < beat - 1e-6) low = middle + 1; else high = middle;
        }
        nextNote = low;
      }
    },

    // Back to the first note without stopping what sounds: the join of a loop.
    rewind() {
      nextNote = 0;
    },

    // Every frame while playing: hand over the notes that start within the next moment.
    play(beat, secondsPerBeat) {
      if (!song || !audio) return;
      if (!bus) {
        // One run of playing has its own way out, so that stopping it is one fade.
        const out = audio.createGain(), tone = audio.createBiquadFilter(), limiter = audio.createDynamicsCompressor();
        out.gain.value = 0.6;
        tone.type = 'lowpass';
        tone.frequency.value = 3200;
        tone.connect(out).connect(limiter).connect(audio.destination);
        bus = { out, tone };
      }
      const until = beat + Ahead / secondsPerBeat;
      for (; nextNote < song.length && song[nextNote][0] < until; nextNote++) {
        const [at, beats, pitch, velocity, timbre] = song[nextNote];
        const wait = (at - beat) * secondsPerBeat;
        if (wait < -Late) continue;   // the view was hidden for a while: do not play what was missed all at once
        const when = audio.currentTime + Math.max(0, wait), level = (velocity / 127) ** 1.5 * 0.3;
        if (timbre === 9) drum(when, pitch, level); else note(when, beats * secondsPerBeat, pitch, level, voices[timbre] ?? voices[0]);
      }
    },
  };
}
