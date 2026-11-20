// The song of a dance as Lily# hands it over one stem at a time (its exports' renderStems, DESIGN.md 98):
// each part of the score through the sound font, each singing voice, and the recording the song was
// made from with its separated parts. They are mixed here, by the preview's mixer, and the same mix
// is what an MP4 and an exported MP3 or WAV carry.
//
// A stem is { kind: 'part' | 'voice' | 'recording' | 'recordingPart', name, file, at }: the sound file
// (its bytes come from the extension) and the moment of the timeline its sample 0 sounds at (it may be
// before 0). The timeline is the recording's seconds (its 0 the recording's 0) or the score's.
//
// While it sounds it is the clock, as a recording alone is (recording.js): every stem is started once,
// together, and the dance is moved to where they are heard.

/** A mixer row's level: 0 off … 1 as made … 2 (200 %). */
export const MaxLevel = 2;

/**
 * A row of the mixer: one per part, voice and separated part of the recording; the recording itself only
 * when it has no parts. The score's rows first, as Lily# hands them out; then the recording's, always in
 * the order of RecordingOrder (the owner's, 2026-11-20), the names it does not know after them as they came.
 */
export function stemRows(stems) {
  const parted = stems.some(s => s.kind === 'recordingPart');
  const rows = stems.filter(s => s.kind !== 'recording' || !parted).map(s => ({ id: rowId(s), kind: s.kind, name: s.name }));
  const recorded = r => r.kind === 'recording' || r.kind === 'recordingPart';
  const rank = r => {
    const name = r.name.toLowerCase();
    const at = RecordingOrder.findIndex(o => name === o || name === o + 's' || name + 's' === o);
    return at < 0 ? RecordingOrder.length : at;
  };
  const ranked = rows.filter(recorded).map((r, i) => ({ r, i })).sort((a, b) => rank(a.r) - rank(b.r) || a.i - b.i).map(x => x.r);
  return [...rows.filter(r => !recorded(r)), ...ranked];
}

/** The separated parts of a recording, top to bottom (with or without a plural s: vocals is vocal, drum is drums). */
export const RecordingOrder = ['vocal', 'guitar', 'piano', 'other', 'bass', 'drums'];

export const rowId = s => (s.kind === 'recording' || s.kind === 'recordingPart' ? 'recording:' : s.kind + ':') + s.name;

/**
 * The stems that sound, with their gains, for the mixer's levels (rowId -> { on, level }; a row not
 * there is on at 1). Lily#'s rule for the recording (its recording-design.md §8): its separated parts
 * all on at 100 % play as the recording itself (the separation's error left out); else the sum of
 * the parts that are on.
 */
export function soundingStems(stems, levels) {
  const of = s => levels[rowId(s)] ?? { on: true, level: 1 };
  const gain = s => {
    const row = of(s);
    return row.on ? Math.max(0, Math.min(MaxLevel, row.level)) : 0;
  };
  const parts = stems.filter(s => s.kind === 'recordingPart');
  const whole = parts.length > 0 && parts.every(s => gain(s) === 1);
  const out = [];
  for (const s of stems) {
    let g;
    if (s.kind === 'recording') g = parts.length === 0 ? gain(s) : whole ? 1 : 0;
    else if (s.kind === 'recordingPart') g = whole ? 0 : gain(s);
    else g = gain(s);
    if (g > 0) out.push({ stem: s, gain: g });
  }
  return out;
}

/** The recording as it was recorded and nothing else: it does not go through the limiter (its look-ahead would delay it). */
export const untouched = sounding => sounding.length === 1 && sounding[0].stem.kind === 'recording' && sounding[0].gain === 1;

/** How far Chromium's limiter (DynamicsCompressorNode) holds its input back: Lily# measured 264 samples at 44.1 kHz. */
export const Lookahead = 0.006;

/** The master as Lily#'s preview has it: 0.9, then a limiter at -1 dB. */
function master(context, untouchedMix) {
  if (untouchedMix) return { input: context.destination, nodes: [] };
  const gain = context.createGain();
  gain.gain.value = 0.9;
  const limiter = context.createDynamicsCompressor();
  limiter.threshold.value = -1;
  limiter.knee.value = 0;
  limiter.ratio.value = 20;
  limiter.attack.value = 0.002;
  limiter.release.value = 0.1;
  gain.connect(limiter);
  limiter.connect(context.destination);
  return { input: gain, nodes: [gain, limiter] };
}

/** Starts `buffer` so that its sample 0 is at `at` of the timeline, the timeline being at `t` at context time `when`. */
function startAt(node, when, at, t, rate) {
  const lead = at - t;   // seconds of the timeline before the stem begins (negative: it began already)
  if (-lead >= node.buffer.duration) return false;
  node.start(when + Math.max(0, lead) / rate, Math.max(0, -lead));
  return true;
}

export function createStemPlayer() {
  const files = new Map();   // file -> { bytes, decoded: Promise|null, buffer, error }
  let audio = null;
  let stems = [];            // the stems of the song shown
  let levels = {};
  let source = null;         // what sounds: { nodes, rate, t (timeline seconds), when (context seconds), plan }

  function context() {
    if (!audio) audio = new AudioContext();
    return audio;
  }

  function stop() {
    if (!source) return;
    for (const node of source.nodes) {
      try { node.stop?.(); } catch { /* not started */ }
      node.disconnect();
    }
    source = null;
  }

  // The file's sound, decoded the first time it is needed (the recording's parts only when they
  // sound: a 5-minute song's four parts decoded take some 200 MB). Null while there are no bytes.
  function decode(file) {
    const known = files.get(file);
    if (!known || (!known.bytes && !known.decoded)) return null;
    if (!known.decoded) {
      const bytes = known.bytes;
      known.bytes = null;
      known.decoded = context().decodeAudioData(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength)).then(
        buffer => (known.buffer = buffer),
        error => {
          known.error = file.split(/[\\/]/).pop() + ' could not be decoded: ' + String(error && error.message || error);
          throw new Error(known.error);
        });
      known.decoded.catch(() => undefined);
    }
    return known.decoded;
  }

  const sounding = () => soundingStems(stems, levels);
  const planOf = list => list.map(s => s.stem.file + '|' + s.stem.at).join('\n') + (untouched(list) ? '\nuntouched' : '');

  return {
    /** The bytes of a stem's file (or why there are none), from the extension. */
    setFile(file, data, error) {
      if (source && stems.some(s => s.file === file)) stop();
      files.set(file, { bytes: data ?? null, decoded: null, buffer: null, error: error ?? '' });
      if (data && stems.some(s => s.file === file && s.kind !== 'recordingPart')) decode(file);
    },

    /** Lets go of every file not in `used` (a Set): the stems of before, once a song's sound was made again. */
    prune(used) {
      for (const file of [...files.keys()]) if (!used.has(file)) files.delete(file);
    },

    /** The rate the files are decoded at (the page's audio). */
    sampleRate() {
      return context().sampleRate;
    },

    /** The stems of the song shown ([] for none). */
    setStems(next) {
      const same = next.length === stems.length && next.every((s, i) => s.file === stems[i].file && s.at === stems[i].at);
      if (!same) stop();
      stems = next;
      for (const s of sounding()) decode(s.stem.file);
    },

    /** The mixer's levels: rowId -> { on, level }. A sounding mix follows at once. */
    setLevels(next) {
      levels = next;
      if (!source) return;
      const list = sounding();
      if (planOf(list) !== source.plan) {
        stop();   // other files sound now (the recording or its parts): the next play() starts them
        for (const s of list) decode(s.stem.file);
        return;
      }
      list.forEach((s, i) => { source.gains[i].gain.value = s.gain; });
    },

    /** 'ready', 'loading', or the words of what went wrong, for the files of the mix as the levels have it. */
    state() {
      let loading = false;
      for (const { stem } of sounding()) {
        const known = files.get(stem.file);
        if (!known) { loading = true; continue; }
        if (known.error) return known.error;
        if (!known.buffer) loading = true;
      }
      return loading ? 'loading' : 'ready';
    },

    /** The decoded files of the mix; throws with words for the user. */
    async buffers() {
      const list = sounding();
      for (let i = 0; i < 300 && list.some(s => !files.has(s.stem.file)); i++) await new Promise(resolve => setTimeout(resolve, 100));
      return Promise.all(list.map(async s => {
        const known = files.get(s.stem.file);
        if (!known) throw new Error(s.stem.file.split(/[\\/]/).pop() + ' did not arrive');
        if (known.error && !known.decoded) throw new Error(known.error);
        return { ...s, buffer: await decode(s.stem.file) };
      }));
    },

    /** A browser lets sound start only from a click or a key: this is called from one. */
    start() {
      if (context().state === 'suspended') void audio.resume();
    },

    /**
     * Sounds the mix from `t` of the timeline at `rate` times its speed. Called every frame while playing;
     * starts it only when it is not sounding at that rate. False while a file of the mix is not decoded.
     */
    play(t, rate) {
      const list = sounding();
      if (list.some(s => !files.get(s.stem.file)?.buffer)) {
        for (const s of list) decode(s.stem.file);
        return false;
      }
      const plan = planOf(list);
      if (source && source.rate === rate && source.plan === plan) return true;
      stop();
      const when = context().currentTime + 0.05;   // a moment ahead: every stem starts on time
      const out = master(audio, untouched(list));
      const nodes = [...out.nodes], gains = [];
      for (const s of list) {
        const node = audio.createBufferSource();
        node.buffer = files.get(s.stem.file).buffer;
        node.playbackRate.value = rate;
        const gain = audio.createGain();
        gain.gain.value = s.gain;
        node.connect(gain);
        gain.connect(out.input);
        startAt(node, when, s.stem.at, t, rate);
        nodes.push(node, gain);
        gains.push(gain);
      }
      source = { nodes, gains, rate, t, when, plan, delay: out.nodes.length ? Lookahead : 0 };
      return true;
    },

    /** Where on the timeline the mix is heard now; null when it is not sounding. The output's own delay is taken off. */
    position() {
      if (!source) return null;
      const stamp = audio.getOutputTimestamp ? audio.getOutputTimestamp() : null;
      const now = stamp && stamp.contextTime > 0 ? stamp.contextTime : audio.currentTime - (audio.outputLatency || audio.baseLatency || 0);
      return source.t + Math.max(0, now - source.delay - source.when) * source.rate;
    },

    /** Silence: the next play() starts again from where the dance is. */
    cue: stop,

    /**
     * `seconds` of the mix from `t` of the timeline, as the mixer has it: { sampleRate, left, right }
     * (Float32Array). Rendered by the browser (OfflineAudioContext) through the same gains and limiter
     * as the mix that sounds. `sampleRate` is the files' (they are decoded at the page's rate) unless given.
     */
    async render(t, seconds, sampleRate) {
      const list = await this.buffers();
      const rate = sampleRate || (list[0] ? list[0].buffer.sampleRate : 48000);
      const length = Math.max(1, Math.round(seconds * rate));
      const offline = new OfflineAudioContext(2, length, rate);
      const out = master(offline, untouched(list));
      // Through the limiter everything comes out late by its look-ahead: started that much earlier, it is on time.
      const from = t + (out.nodes.length ? Lookahead : 0);
      for (const s of list) {
        const node = offline.createBufferSource();
        node.buffer = s.buffer;
        const gain = offline.createGain();
        gain.gain.value = s.gain;
        node.connect(gain);
        gain.connect(out.input);
        startAt(node, 0, s.stem.at, from, 1);
      }
      const rendered = await offline.startRendering();
      return { sampleRate: rate, left: rendered.getChannelData(0), right: rendered.getChannelData(1) };
    },
  };
}
