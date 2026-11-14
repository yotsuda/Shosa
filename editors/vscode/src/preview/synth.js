// The synthesiser behind fontsound.js: spessasynth_lib (Apache-2.0) playing a sound font —
// the one Lily# plays its scores with, which the extension finds (extension.ts, soundFontFile).
// Only the VS Code preview has it: a webview can be handed a 40 MB file of this machine, the
// page `shosa preview` writes cannot.
//
// The font is fetched as soon as the extension names it. The rest waits for the first Play:
// a browser runs the audio thread only after a click or a key, and the synthesiser is built,
// and the font read, on that thread.
import { WorkletSynthesizer } from 'spessasynth_lib';

const Sustain = 64, AllSoundOff = 120, Centre = 8192;

// onStatus({ state, detail }): 'none' (no sound font: the oscillators play), 'loading',
// 'ready' (detail: the font's name), 'failed' (detail: why; the oscillators play).
export function createSynthPort(onStatus) {
  let config = null;      // { processor, soundFont, name }: URLs the webview may load, and what to call the font
  let bytes = null;       // the font's file, being fetched: a promise of an ArrayBuffer
  let context = null, synth = null, booting = null;
  let ready = false, serial = 0, bank = null;

  // The worklet's module from its URL; a host that will not hand a worklet a module of
  // another origin gets the same text as a blob.
  async function addProcessor(url) {
    try {
      await context.audioWorklet.addModule(url);
    } catch (first) {
      const blob = URL.createObjectURL(new Blob([await (await fetch(url)).text()], { type: 'text/javascript' }));
      try {
        await context.audioWorklet.addModule(blob);
      } catch {
        throw first;
      } finally {
        URL.revokeObjectURL(blob);
      }
    }
  }

  async function boot() {
    await addProcessor(config.processor);
    synth = new WorkletSynthesizer(context);
    const level = context.createGain(), limiter = context.createDynamicsCompressor();
    level.gain.value = 0.9;
    limiter.threshold.value = -10;
    limiter.ratio.value = 12;
    limiter.attack.value = 0.002;
    synth.connect(level);
    level.connect(limiter).connect(context.destination);
    await synth.isReady;
  }

  // The font named last becomes the one that plays.
  async function load() {
    const mine = ++serial, name = config.name;
    try {
      await (booting ??= boot());
      const buffer = await bytes;
      if (mine !== serial) return;
      bytes = null;   // the synthesiser takes the buffer; another font is fetched anew
      const id = 'font-' + mine;
      await synth.soundBankManager.addSoundBank(buffer, id);
      if (bank) await synth.soundBankManager.deleteSoundBank(bank);
      bank = id;
      if (mine !== serial) return;
      ready = true;
      onStatus({ state: 'ready', detail: name });
    } catch (error) {
      if (mine !== serial) return;
      ready = false;
      onStatus({ state: 'failed', detail: String(error && error.message || error) });
    }
  }

  const at = time => ({ time });

  return {
    get ready() { return ready; },

    // From the extension: where the synthesiser's worklet and the sound font are. No font: the oscillators.
    configure(next) {
      const same = config && next && config.soundFont === next.soundFont;
      config = next;
      if (same) return;
      ready = false;
      serial++;
      if (!config || !config.soundFont) {
        bytes = null;
        onStatus({ state: 'none' });
        return;
      }
      onStatus({ state: 'loading', detail: config.name });
      bytes = fetch(config.soundFont).then(response => {
        if (!response.ok) throw new Error('the sound font could not be read (HTTP ' + response.status + ')');
        return response.arrayBuffer();
      });
      bytes.catch(() => undefined);   // said when it is awaited
      if (context) void load();
    },

    // A click or a key: sound is allowed from now on.
    start() {
      if (!config || !config.soundFont) return;
      const first = !context;
      context ??= new AudioContext();
      void context.resume();
      if (first) void load();
    },

    now: () => context.currentTime,
    program: (channel, program, time) => synth.programChange(channel, program, at(time)),
    noteOn: (channel, key, velocity, time) => synth.noteOn(channel, key, velocity, at(time)),
    noteOff: (channel, key, time) => synth.noteOff(channel, key, at(time)),
    bend: (channel, value, time) => synth.pitchWheel(channel, value, at(time)),

    quiet(time) {
      synth.stopAll(true);
      for (let channel = 0; channel < 16; channel++) {
        // All Sound Off lifts no pedal and centres no wheel.
        synth.controllerChange(channel, Sustain, 0, at(time));
        synth.controllerChange(channel, AllSoundOff, 0, at(time));
        synth.pitchWheel(channel, Centre, at(time));
      }
    },
  };
}
