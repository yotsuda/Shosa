// The recording a dance keeps time with (a song with `X.orecchiante.json` beside it: DESIGN.md 97),
// played instead of the song's notes.
//
// The notes of sound.js follow the figure's clock, frame by frame. A recording cannot: it is one
// long sound, started once at a point of it. So while it sounds, it is the clock — the dance is
// moved to where the recording is heard (`position`), and cannot drift from it however long it
// plays. Beat 0 of the dance is `offset` seconds into the recording.
//
// The extension reads the file and hands its bytes over (the page need not be let read its
// folder); they are decoded here, once per file.
export function createRecording() {
  const files = new Map();   // file -> { bytes, decoded: Promise<AudioBuffer>|null, buffer, error }
  let audio = null, gain = null;
  let source = null;         // what sounds: { node, rate, at (recording seconds), when (context seconds) }

  function context() {
    if (!audio) {
      audio = new AudioContext();
      gain = audio.createGain();
      gain.connect(audio.destination);
    }
    return audio;
  }

  function stop() {
    if (!source) return;
    try { source.node.stop(); } catch { /* not started yet */ }
    source.node.disconnect();
    source = null;
  }

  // The file's sound, decoded the first time it is asked for. Null while there are no bytes.
  function decode(file) {
    const known = files.get(file);
    if (!known || (!known.bytes && !known.decoded)) return null;
    if (!known.decoded) {
      const bytes = known.bytes;
      known.bytes = null;   // decodeAudioData takes the buffer over
      known.decoded = context().decodeAudioData(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength)).then(
        buffer => (known.buffer = buffer),
        error => {
          known.error = 'the recording ' + file + ' could not be decoded: ' + String(error && error.message || error);
          throw new Error(known.error);
        });
      known.decoded.catch(() => undefined);
    }
    return known.decoded;
  }

  return {
    /** The bytes of a recording (or why there are none), from the extension. */
    setFile(file, data, error) {
      if (source && source.file === file) stop();
      files.set(file, { bytes: data ?? null, decoded: null, buffer: null, error: error ?? '' });
      if (data) decode(file);
    },

    /** 'ready', 'loading' (asked for, or being decoded) or the words of what went wrong. */
    state(file) {
      const known = files.get(file);
      return !known ? 'loading' : known.error ? known.error : known.buffer ? 'ready' : 'loading';
    },

    /** The decoded sound of a file: an AudioBuffer, or throws with words for the user. */
    async buffer(file) {
      for (let i = 0; i < 100 && !files.has(file); i++) await new Promise(resolve => setTimeout(resolve, 100));   // asked for with the clip
      const known = files.get(file);
      if (!known) throw new Error('the recording ' + file + ' did not arrive');
      if (known.error && !known.decoded) throw new Error(known.error);
      return decode(file);
    },

    /** A browser lets sound start only from a click or a key: this is called from one. */
    start() {
      if (context().state === 'suspended') void audio.resume();
    },

    /**
     * Sounds the recording from `seconds` of the dance (`offset` + seconds into the file), at `rate`
     * times its speed. Called every frame while playing; starts it only when it is not already
     * sounding at that rate. False while the file is not decoded (nothing sounds).
     */
    play(file, offset, seconds, rate) {
      const buffer = files.get(file)?.buffer;
      if (!buffer) return false;
      if (source && source.file === file && source.rate === rate) return true;
      stop();
      const at = offset + seconds, when = context().currentTime + 0.03;   // a moment ahead: it starts on time, not late
      const node = audio.createBufferSource();
      node.buffer = buffer;
      node.playbackRate.value = rate;
      node.connect(gain);
      // Before the recording's start (a dance that begins before it): silence until it comes.
      node.start(when + Math.max(0, -at) / rate, Math.max(0, at));
      source = { node, file, rate, at, when };
      return true;
    },

    /**
     * Where in the dance the recording is heard now, in the dance's seconds; null when it is not
     * sounding. The moment heard, not the moment handed over: the output's own delay is taken off.
     */
    position(offset) {
      if (!source) return null;
      const stamp = audio.getOutputTimestamp ? audio.getOutputTimestamp() : null;
      const now = stamp && stamp.contextTime > 0 ? stamp.contextTime : audio.currentTime - (audio.outputLatency || audio.baseLatency || 0);
      return source.at + Math.max(0, now - source.when) * source.rate - offset;
    },

    /** Silence: the next `play` starts it again from where the dance is. */
    cue: stop,
  };
}

/**
 * `seconds` of a decoded recording from `offset` on, for a video's sound: { sampleRate, left, right }
 * (Float32Array), silence where the dance is before the recording's start or after its end. As
 * recorded: not made louder or softer.
 */
export function cutRecording(buffer, offset, seconds) {
  const rate = buffer.sampleRate, total = Math.max(1, Math.round(seconds * rate));
  const first = Math.round(offset * rate);
  const left = new Float32Array(total), right = new Float32Array(total);
  const channels = [buffer.getChannelData(0), buffer.getChannelData(Math.min(1, buffer.numberOfChannels - 1))];
  const from = Math.max(0, first), to = Math.min(buffer.length, first + total);
  if (to > from) {
    left.set(channels[0].subarray(from, to), from - first);
    right.set(channels[1].subarray(from, to), from - first);
  }
  return { sampleRate: rate, left, right };
}
