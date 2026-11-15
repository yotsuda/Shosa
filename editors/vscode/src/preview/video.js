// An MP4 written inside the page, with nothing installed: the browser's own encoders (WebCodecs:
// H.264 for the pictures, AAC for the sound) and a muxer in JavaScript (mp4-muxer, MIT). VS Code's
// webviews have both encoders; where one is missing, `encodeVideo` says so.
//
// The pictures are drawn by the caller, frame by frame, as fast as the encoder takes them: the
// video is exact whatever the machine's speed. This file knows nothing of Shosa.
import { ArrayBufferTarget, Muxer } from 'mp4-muxer';

/** The H.264 a frame of this size needs: High profile, the lowest level that holds it at 30 a second. */
export function avcCodec(width, height) {
  const blocks = Math.ceil(width / 16) * Math.ceil(height / 16);   // the frame in macroblocks
  const level = blocks <= 3600 ? 0x1f : blocks <= 8192 ? 0x28 : blocks <= 22080 ? 0x32 : 0x33;   // 3.1, 4.0, 5.0, 5.1
  return 'avc1.6400' + level.toString(16).padStart(2, '0');
}

const breathe = () => new Promise(resolve => setTimeout(resolve, 0));

/**
 * Encodes `count` frames at `fps` and returns { data: the MP4 as an ArrayBuffer, silent: why it
 * has no sound although a `sound` was given, or '' } — or null when stopped.
 *   canvas          what is filmed: `draw(f)` paints frame f onto it (and may be async)
 *   width, height   the canvas's size in pixels, both even
 *   sound           { sampleRate, left, right } (Float32Array) to sound with it, cut to the video's length (optional)
 *   progress(done)  called with 0..1
 *   stopped()       true: give up
 * Throws with words for the user when this browser cannot encode.
 */
export async function encodeVideo({ canvas, width, height, fps, count, draw, sound, progress, stopped }) {
  if (typeof VideoEncoder !== 'function') {
    throw new Error('this editor has no video encoder (WebCodecs).');
  }
  const config = { codec: avcCodec(width, height), width, height, framerate: fps, bitrate: Math.round(Math.max(2e6, width * height * 4)) };
  if (!(await VideoEncoder.isConfigSupported(config)).supported) {
    throw new Error(`this editor cannot encode H.264 at ${width}×${height} on this machine.`);
  }

  // The sound, if it can be encoded; else the video is silent and says why.
  let silent = '';
  const audioConfig = sound && { codec: 'mp4a.40.2', sampleRate: sound.sampleRate, numberOfChannels: 2, bitrate: 192000 };
  if (sound && (typeof AudioEncoder !== 'function' || !(await AudioEncoder.isConfigSupported(audioConfig)).supported)) {
    sound = null;
    silent = 'this editor cannot encode AAC on this machine';
  }

  const muxer = new Muxer({
    target: new ArrayBufferTarget(),
    video: { codec: 'avc', width, height, frameRate: fps },
    audio: sound ? { codec: 'aac', numberOfChannels: 2, sampleRate: sound.sampleRate } : undefined,
    fastStart: 'in-memory',   // the index at the front: a page can play it as it loads
  });

  let failure = null;
  const video = new VideoEncoder({ output: (chunk, meta) => muxer.addVideoChunk(chunk, meta), error: e => { failure = e; } });
  video.configure(config);
  const micro = 1e6 / fps;
  for (let f = 0; f < count; f++) {
    if (failure) throw failure;
    if (stopped && stopped()) {
      video.close();
      return null;
    }
    await draw(f);
    const frame = new VideoFrame(canvas, { timestamp: Math.round(f * micro), duration: Math.round(micro) });
    video.encode(frame, { keyFrame: f % (fps * 2) === 0 });
    frame.close();
    // Not faster than the encoder: its queue would hold every frame of a long dance.
    while (video.encodeQueueSize > 4 && !failure) await breathe();
    if (f % 10 === 0) {
      if (progress) progress(f / count);
      await breathe();   // the page stays alive: the Stop click is heard
    }
  }
  await video.flush();
  video.close();
  if (failure) throw failure;

  if (sound) {
    const audio = new AudioEncoder({ output: (chunk, meta) => muxer.addAudioChunk(chunk, meta), error: e => { failure = e; } });
    audio.configure(audioConfig);
    const total = Math.min(sound.left.length, Math.round(count / fps * sound.sampleRate));
    const block = sound.sampleRate;   // a second at a time
    for (let at = 0; at < total && !failure; at += block) {
      const frames = Math.min(block, total - at);
      const planes = new Float32Array(frames * 2);   // planar: all of the left, then all of the right
      planes.set(sound.left.subarray(at, at + frames), 0);
      planes.set(sound.right.subarray(at, at + frames), frames);
      const data = new AudioData({
        format: 'f32-planar', sampleRate: sound.sampleRate, numberOfChannels: 2, numberOfFrames: frames,
        timestamp: Math.round(at / sound.sampleRate * 1e6), data: planes,
      });
      audio.encode(data);
      data.close();
      while (audio.encodeQueueSize > 8 && !failure) await breathe();
    }
    await audio.flush();
    audio.close();
    if (failure) throw failure;
  }

  muxer.finalize();
  if (progress) progress(1);
  return { data: muxer.target.buffer, silent };
}
