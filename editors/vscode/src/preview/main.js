// The preview: the figures of one Shosa file, moving, inside a VS Code webview.
//
// It is given the solved movement (the file's "clips", one per `score`: every joint at every frame, plus
// which source span is performed when) and draws ONE clip at a time: the one whose section
// the cursor is in, or the one picked in the selector. It never parses anything. Messages:
//   in   { type: 'clip', clips, offset, reset? }   the movement (after every edit); offset
//                                                  is the cursor, -1 if unknown
//   in   { type: 'cursor', offset }       the editor's cursor moved
//   in   { type: 'song', key, song }      the notes asked for: { error, notes }
//   in   { type: 'synth', processor, soundFont, name }   where the synthesiser and the sound font are
//                                                  (URLs the webview may load; no soundFont: there is none)
//   out  { type: 'ready' }
//   out  { type: 'song', key, music }     the notes of the song this clip is danced to, please
//   out  { type: 'active', spans }        [start, length] of the words being performed
//   out  { type: 'reveal', start, length} a limb was clicked: its word
// Export (the two buttons at the top right; the extension asks where to write, then answers):
//   out  { type: 'export', what: 'model' | 'video', score, music, id? }   a button was clicked
//   in   { type: 'export', what, id }     the same click, made by a command (id comes back in the out)
//   in   { type: 'record', id }           film the score shown, with its song as the preview plays it
//   out  { type: 'recorded', id, data?, silent?, error?, stopped? }   the MP4's bytes, or why not
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { createFontSound } from './fontsound.js';
import { createSound } from './sound.js';
import { createSynthPort } from './synth.js';
import { barAt, beatAt, clicksOf, markText, marksAt, secondsAt, tempoAt } from './time.js';
import { renderSong } from './songaudio.js';
import { encodeVideo } from './video.js';

const vscode = acquireVsCodeApi();

// ---- Page.
const style = document.createElement('style');
style.textContent = `
  html, body { height: 100%; margin: 0; }
  body { display: flex; flex-direction: column; padding: 0; overflow: hidden;
         color: var(--vscode-foreground, #333); font: 13px/1.4 var(--vscode-font-family, system-ui, sans-serif); }
  #stage { flex: 1; min-height: 0; position: relative; }
  #stage canvas { display: block; width: 100%; height: 100%; cursor: grab; }
  #empty { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center;
           color: var(--vscode-descriptionForeground, #777); padding: 24px; text-align: center; pointer-events: none; }
  footer { display: flex; gap: 10px; align-items: center; padding: 6px 12px;
           border-top: 1px solid var(--vscode-panel-border, #ccc); }
  header { display: flex; gap: 8px; align-items: center; padding: 4px 12px; min-height: 22px;
           border-bottom: 1px solid var(--vscode-panel-border, #ccc); }
  header[hidden], header [hidden], footer [hidden] { display: none; }
  #name { font-weight: 600; }
  #kind { font-size: 11px; padding: 0 6px; border-radius: 8px;
          color: var(--vscode-badge-foreground, #fff); background: var(--vscode-badge-background, #616161); }
  button { font: inherit; padding: 3px 12px; border: 0; border-radius: 3px; cursor: pointer; min-width: 5em;
           color: var(--vscode-button-foreground, #fff); background: var(--vscode-button-background, #0a66c2); }
  button:hover { background: var(--vscode-button-hoverBackground, #0858a8); }
  header button { min-width: 0; padding: 1px 10px; color: var(--vscode-button-secondaryForeground, #fff);
                  background: var(--vscode-button-secondaryBackground, #5f6a79); }
  header button:hover { background: var(--vscode-button-secondaryHoverBackground, #4c5561); }
  header button:disabled { opacity: 0.5; cursor: default; }
  #space { flex: 1; }
  input[type=range] { flex: 1; min-width: 60px; margin: 0; }
  #track { flex: 1; display: flex; position: relative; min-width: 60px; }
  #ticks i { position: absolute; bottom: -5px; width: 2px; margin-left: -1px; height: 5px; background: var(--mark); pointer-events: none; }
  #marks { position: absolute; left: 12px; top: 8px; font-weight: 600; color: var(--mark); pointer-events: none; white-space: pre; }
  body { --mark: #1e8e5a; }
  body.vscode-dark, body.vscode-high-contrast { --mark: #4fd08f; }
  #beat { font-variant-numeric: tabular-nums; min-width: 13ch; color: var(--vscode-descriptionForeground, #777); }
  label { display: flex; gap: 4px; align-items: center; white-space: nowrap; }
  select { font: inherit; color: var(--vscode-dropdown-foreground, inherit); background: var(--vscode-dropdown-background, transparent);
           border: 1px solid var(--vscode-dropdown-border, #999); border-radius: 3px; }
`;
document.head.append(style);
document.body.innerHTML = `
  <header id="bar" hidden>
    <select id="clips" aria-label="Score" hidden></select>
    <span id="name" hidden></span>
    <span id="kind" hidden></span>
    <span id="space"></span>
    <button id="exportVideo" title="Write the score shown as an MP4 video: this view, with its song.">MP4</button>
    <button id="exportModel" title="Write the score shown as a model file for an engine: glTF (.glb), VRM Animation (.vrma) or BVH (.bvh).">Model</button>
  </header>
  <div id="stage"><div id="empty">Nothing to perform yet: the file needs a section.</div><div id="marks"></div></div>
  <footer>
    <button id="play">Play</button>
    <div id="track"><input id="seek" type="range" min="0" max="1000" value="0" aria-label="Position"><div id="ticks"></div></div>
    <span id="beat"></span>
    <label><input id="loop" type="checkbox" checked> Loop</label>
    <label id="soundLabel" hidden><input id="sound" type="checkbox" checked> Sound</label>
    <label>Speed <select id="speed"><option>0.25</option><option>0.5</option><option selected>1</option></select></label>
  </footer>`;

const stage = document.getElementById('stage'), empty = document.getElementById('empty');
const playButton = document.getElementById('play'), seek = document.getElementById('seek');
const beatLabel = document.getElementById('beat'), speed = document.getElementById('speed'), loop = document.getElementById('loop');
const bar = document.getElementById('bar'), clipSelect = document.getElementById('clips');
const nameLabel = document.getElementById('name'), kindLabel = document.getElementById('kind');
const soundLabel = document.getElementById('soundLabel'), soundBox = document.getElementById('sound');
const ticks = document.getElementById('ticks'), marksLabel = document.getElementById('marks');
const videoButton = document.getElementById('exportVideo'), modelButton = document.getElementById('exportModel');

// ---- Scene.
const dark = document.body.classList.contains('vscode-dark') || document.body.classList.contains('vscode-high-contrast');
const background = getComputedStyle(document.body).getPropertyValue('--vscode-editor-background').trim() || (dark ? '#1e1e1e' : '#ffffff');

const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(window.devicePixelRatio);
stage.append(renderer.domElement);
const scene = new THREE.Scene();
scene.background = new THREE.Color(background);
const camera = new THREE.PerspectiveCamera(40, 1, 0.1, 100);
camera.position.set(2.4, 1.7, 4.6);
const controls = new OrbitControls(camera, renderer.domElement);
controls.target.set(0, 0.9, 0);
scene.add(new THREE.HemisphereLight(0xffffff, 0x8890a0, 2.2));
const sun = new THREE.DirectionalLight(0xffffff, 1.4);
sun.position.set(2, 5, 3);
scene.add(sun);
scene.add(new THREE.GridHelper(12, 12, dark ? 0x5a616c : 0xb9c0ca, dark ? 0x353a42 : 0xdfe3e8));
// The audience side of the floor.
const front = new THREE.Mesh(new THREE.BoxGeometry(12, 0.01, 0.04), new THREE.MeshBasicMaterial({ color: 0x2f6fdf }));
front.position.set(0, 0, 6);
scene.add(front);

// Left limbs blue, right limbs red — the same as the contact sheet.
const colors = { c: dark ? 0xd5d9df : 0x3a4048, l: 0x2f6fdf, r: 0xd9482b };
const boneGeometry = new THREE.CylinderGeometry(0.028, 0.028, 1, 10);
const jointGeometry = new THREE.SphereGeometry(0.036, 12, 10);
const headGeometry = new THREE.SphereGeometry(0.105, 20, 16);
const yAxis = new THREE.Vector3(0, 1, 0);

let clips = [];         // every clip of the file
let clip = null;        // the one shown: spans, sections and times below are all its own
let figures = [];       // { data, bones: Mesh[], joints: Mesh[], points: Vector3[] }
let beat = 0, playing = false, last = 0;
let lastActive = null;  // what the editor was last told is performed; null: tell it again
let wantLoop = true;    // the Loop box, for a clip that is not itself a loop
let listed = null;      // the names the selector lists

function channelOfBone(index) {
  // Which track moves this bone: named by the joint at its far end.
  const name = clip.joints[clip.bones[index][1]];
  const side = name.startsWith('left') ? 'left' : name.startsWith('right') ? 'right' : '';
  if (/Elbow|Hand/.test(name)) return [side + 'Arm'];
  if (/Knee|Ankle|Toe|Hip/.test(name)) return [side + 'Leg', 'support', 'height'];
  if (/head|nose/.test(name)) return ['head', 'torso'];
  return ['torso'];
}

function build() {
  for (const figure of figures) {
    for (const mesh of [...figure.bones, ...figure.joints]) {
      scene.remove(mesh);
      mesh.material.dispose();
    }
  }
  if (!clip) {
    figures = [];
    return;
  }
  const head = clip.joints.indexOf('head'), nose = clip.joints.indexOf('nose');
  figures = clip.figures.map((data, figureIndex) => {
    const bones = clip.bones.map((pair, i) => {
      const mesh = new THREE.Mesh(boneGeometry, new THREE.MeshStandardMaterial({ color: colors[clip.boneSides[i]], roughness: 0.6 }));
      mesh.userData = { figure: figureIndex, bone: i };
      scene.add(mesh);
      return mesh;
    });
    const joints = clip.joints.map((name, i) => {
      const mesh = new THREE.Mesh(i === head ? headGeometry : jointGeometry,
        new THREE.MeshStandardMaterial({ color: i === nose ? 0xd9482b : colors.c, roughness: 0.6 }));
      scene.add(mesh);
      return mesh;
    });
    return { data, bones, joints, points: clip.joints.map(() => new THREE.Vector3()) };
  });
}

const a = new THREE.Vector3(), b = new THREE.Vector3(), d = new THREE.Vector3();
function pose(frame) {
  for (const figure of figures) {
    const frames = figure.data.frames;
    if (!frames.length) continue;
    const f0 = Math.max(0, Math.min(frames.length - 1, Math.floor(frame))), f1 = Math.min(frames.length - 1, f0 + 1), k = frame - f0;
    const p = frames[f0], q = frames[f1];
    figure.points.forEach((v, j) => {
      v.set(p[j * 3] + (q[j * 3] - p[j * 3]) * k, p[j * 3 + 1] + (q[j * 3 + 1] - p[j * 3 + 1]) * k, p[j * 3 + 2] + (q[j * 3 + 2] - p[j * 3 + 2]) * k);
      figure.joints[j].position.copy(v);
    });
    clip.bones.forEach((pair, i) => {
      a.copy(figure.points[pair[0]]);
      b.copy(figure.points[pair[1]]);
      d.subVectors(b, a);
      const length = d.length() || 1e-6;
      const mesh = figure.bones[i];
      mesh.position.addVectors(a, b).multiplyScalar(0.5);
      mesh.quaternion.setFromUnitVectors(yAxis, d.multiplyScalar(1 / length));
      mesh.scale.set(1, length, 1);
    });
  }
}

// An event is "being performed" from just after it starts until it ends: at its last
// instant the part is at the target the word names, so that is the pose the word means.
const isActive = span => beat > span.t0 + 1e-9 && beat <= span.t1 + 1e-9;

function show() {
  if (!clip) {
    if (lastActive !== '') {
      lastActive = '';
      vscode.postMessage({ type: 'active', spans: [] });
    }
    beatLabel.textContent = '';
    marksLabel.textContent = '';
    seek.value = 0;
    return;
  }
  pose(secondsAt(clip, beat) * clip.fps);
  // The moments named for the engine (`!hit`): written over the stage as they arrive.
  const arrived = markText(clip, marksAt(clip, beat));
  if (marksLabel.textContent !== arrived) marksLabel.textContent = arrived;

  const seen = new Set(), spans = [];
  for (const span of clip.spans) {
    const key = span.start + ':' + span.length;
    if (isActive(span) && !seen.has(key)) {
      seen.add(key);
      spans.push([span.start, span.length]);
    }
  }
  const signature = spans.join('|');
  if (signature !== lastActive) {
    lastActive = signature;
    vscode.postMessage({ type: 'active', spans });
  }

  const [bar, inBar] = barAt(clip, beat);
  beatLabel.textContent = clip.totalBeats ? 'bar ' + bar + '  beat ' + (inBar + 1).toFixed(2) : '';
  seek.value = clip.totalBeats ? Math.round(1000 * beat / clip.totalBeats) : 0;
}

function seekTo(value) {
  beat = Math.max(0, Math.min(clip ? clip.totalBeats : 0, value));
  show();
}

// ---- Sound: the song the clip is danced to (`music "song.lys"`). Played through the sound
// font Lily# plays with (synth.js) when there is one; else, and until it is read, by the
// oscillators of sound.js.
const songs = new Map();   // key (file, score, order) -> notes
let songKey = null;        // the key of the shown clip's song; null without one
let songError = '', fontStatus = { state: 'none' };

// The Sound box says on hover what is wrong with the song, or else what it is played with.
function soundTitle() {
  if (!songKey) {
    soundLabel.title = 'No song: a click on every beat (louder on the first of a bar) and a clap at every mark.';
    return;
  }
  soundLabel.title = songError || (
    fontStatus.state === 'ready' ? 'Played with the sound font ' + fontStatus.detail + '.'
    : fontStatus.state === 'loading' ? 'Reading the sound font ' + fontStatus.detail + ' (it is used from the first Play on); simple tones until then.'
    : fontStatus.state === 'failed' ? 'Simple tones: ' + fontStatus.detail
    : 'Simple tones: no sound font was found. Set shosa.playback.soundFont, or install the Lily# Sound Font extension.');
}

const port = createSynthPort(status => {
  fontStatus = status;
  soundTitle();
});
const player = createFontSound(port, createSound());
soundTitle();

const keyOf = c => c && c.music ? JSON.stringify([c.music.file, c.music.score ?? null, c.music.order ?? null]) : null;

// Asked again at every Play: the server answers from memory unless the .lys was saved since.
function askSong() {
  if (songKey) vscode.postMessage({ type: 'song', key: songKey, music: clip.music });
}

// What sounds stops, and the notes go on from the beat shown.
const cue = () => player.cue(beat);

function sound() {
  if (soundBox.checked) player.play(beat, 60 / (tempoAt(clip, beat) * Number(speed.value)));
}

soundBox.addEventListener('change', cue);

// ---- Which clip is shown.
const sectionsOf = c => c.sections ?? [];
const holds = (c, offset) => sectionsOf(c).some(s => offset >= s.src && offset <= s.src + s.len);

// The clip with a section the cursor is in: the one shown if it has one (a section can be
// in several clips), else the first. Null when the cursor is in no section.
function clipAt(offset) {
  if (typeof offset !== 'number' || offset < 0) return null;
  return (clip && clips.includes(clip) && holds(clip, offset) ? clip : clips.find(c => holds(c, offset))) ?? null;
}

// A clip is one score of the file; the unnamed score is listed as Lily# lists its own.
const clipLabel = c => c.name || '(Default)';

function header() {
  const names = clips.map(clipLabel);
  if (listed !== names.join('\n')) {
    // Only when the names changed: filling it again would close an open list on every edit.
    listed = names.join('\n');
    clipSelect.replaceChildren(...names.map((name, i) => new Option(name, String(i))));
  }
  clipSelect.hidden = clips.length < 2;
  clipSelect.value = String(clips.indexOf(clip));
  nameLabel.hidden = clips.length !== 1 || !clip || !clip.name;
  nameLabel.textContent = clip ? clip.name : '';
  const kind = !clip ? '' : clip.loop ? (clip.inplace ? 'loop, in place' : 'loop') : clip.inplace ? 'in place' : '';
  kindLabel.hidden = !kind;
  kindLabel.textContent = kind;
  kindLabel.title = !clip || !clip.loop ? '' : clip.inplace
    ? 'The end joins the start; the travel over the floor is taken out.'
    : 'The end joins the start. The figure travels, so each round starts again from where the first began.';
  // The bar is there whenever there is something to export.
  const movement = !!(clip && clip.figures.length && clip.totalBeats > 0);
  bar.hidden = !movement && clipSelect.hidden && nameLabel.hidden && kindLabel.hidden;
  videoButton.hidden = modelButton.hidden = !movement;
  // A loop repeats whatever the box says; the box shows it and comes back afterwards.
  loop.checked = (clip && clip.loop) || wantLoop;
  loop.disabled = !!(clip && clip.loop);
}

// A tick under the slider at every mark of the clip. The slider's knob travels its width less
// its own (about 16 pixels), and so do the ticks.
function markTicks() {
  const beats = clip && clip.totalBeats > 0 ? [...new Set((clip.marks ?? []).map(m => m.beat))] : [];
  ticks.replaceChildren(...beats.map(at => {
    const tick = document.createElement('i');
    tick.style.left = `calc(8px + (100% - 16px) * ${at / clip.totalBeats})`;
    return tick;
  }));
}

function setClip(next, keepBeat) {
  const rebuild = !next || !clip || figures.length !== next.figures.length;
  clip = next;
  if (rebuild) build(); else figures.forEach((figure, i) => { figure.data = clip.figures[i]; });
  empty.style.display = clip && clip.figures.length && clip.totalBeats > 0 ? 'none' : 'flex';
  if (!keepBeat) beat = 0;
  if (!clip) setPlaying(false);
  lastActive = null;
  header();
  markTicks();
  seekTo(beat);

  // An edit of the dance leaves the song as it is, and the music goes on; another song,
  // or another moment, starts it again.
  const key = keyOf(clip);
  soundLabel.hidden = !clip;
  if (!key) {
    // The clicks are the clip's own: an edit moves its marks and its bars.
    songKey = null;
    player.setSong(clicksOf(clip));
    soundTitle();
    cue();
  } else if (key !== songKey) {
    songKey = key;
    player.setSong(songs.get(key) ?? null);
    soundTitle();
    askSong();
    cue();
  } else if (!keepBeat) {
    cue();
  }
}

clipSelect.addEventListener('change', () => {
  const picked = clips[Number(clipSelect.value)];
  if (picked && picked !== clip) setClip(picked, false);
  clipSelect.blur();   // so that Space plays again instead of opening the list
});
loop.addEventListener('change', () => { wantLoop = loop.checked; });

function setPlaying(value) {
  playing = value;
  playButton.textContent = playing ? 'Pause' : 'Play';
  last = performance.now();
  cue();
}

playButton.addEventListener('click', () => {
  if (!clip) return;
  if (!playing && beat >= clip.totalBeats - 1e-6) beat = 0;
  if (!playing) {
    // A browser lets sound start only from a click or a key: this is the one.
    player.start();
    askSong();
  }
  setPlaying(!playing);
});
seek.addEventListener('input', () => {
  setPlaying(false);
  seekTo((clip ? clip.totalBeats : 0) * seek.value / 1000);
});
window.addEventListener('keydown', event => {
  if (event.code === 'Space') {
    event.preventDefault();
    playButton.click();
  }
});

// ---- Export. The extension asks where to write; a model file is the server's work, the video
// is filmed here: every frame of the score shown, from where the camera stands, encoded by the
// page itself (video.js).
const VIDEO_FPS = 30;
let recording = null;   // { stop } while a video is being filmed
let synthConfig = null; // where the sound font is (the 'synth' message)

function askExport(what, id) {
  if (!clip || recording) return false;
  vscode.postMessage({ type: 'export', what, id, score: clip.name ?? '', music: clip.music ?? null });
  return true;
}
modelButton.addEventListener('click', () => askExport('model'));
videoButton.addEventListener('click', () => {
  if (recording) recording.stop = true; else askExport('video');
});

// What the preview plays for the clip, as samples: the same notes (the song, or the clicks of a
// score without one), the same sound font. { sound } or { silent: why the video has none }.
async function songSound(filmed, seconds) {
  if (!soundBox.checked) return { silent: 'the Sound box is off' };
  const key = keyOf(filmed);
  for (let i = 0; key && i < 30 && !songs.has(key); i++) await new Promise(resolve => setTimeout(resolve, 100));   // asked for when the clip was shown
  // Without a song the video has what the preview plays: the clicks and the claps.
  const notes = key ? songs.get(key) : clicksOf(filmed);
  if (!notes || !notes.length) return key ? { silent: songError || 'the song gave no notes' } : {};
  if (!synthConfig || !synthConfig.soundFont) return { silent: 'no sound font was found (set shosa.playback.soundFont)' };
  try {
    const response = await fetch(synthConfig.soundFont);
    if (!response.ok) throw new Error('the sound font could not be read (HTTP ' + response.status + ')');
    return {
      sound: await renderSong({
        font: await response.arrayBuffer(), notes, seconds, secondsAt: at => secondsAt(filmed, at),
        pause: () => new Promise(resolve => setTimeout(resolve, 0)),
      }),
    };
  } catch (error) {
    return { silent: String(error && error.message || error) };
  }
}

async function record(id) {
  if (!clip || recording) {
    vscode.postMessage({ type: 'recorded', id, error: clip ? 'a video is already being written.' : 'there is no movement to film.' });
    return;
  }
  const filmed = clip, keep = beat;
  setPlaying(false);
  recording = { stop: false };
  modelButton.disabled = true;
  videoButton.title = 'Stop writing the video.';

  // The picture: the view as it is, 1080 pixels high (less when that would be wider than 1920).
  const aspect = stage.clientWidth / Math.max(1, stage.clientHeight);
  let height = 1080, width = 2 * Math.round(height * aspect / 2);
  if (width > 1920) {
    width = 1920;
    height = 2 * Math.round(width / aspect / 2);
  }
  renderer.setPixelRatio(1);
  renderer.setSize(width, height, false);
  camera.aspect = width / height;
  camera.updateProjectionMatrix();

  // A loop's last frame is its first: left out, so the video itself loops without a stutter.
  const seconds = secondsAt(filmed, filmed.totalBeats);
  const count = Math.max(1, Math.round(seconds * VIDEO_FPS) + (filmed.loop ? 0 : 1));
  const answer = { type: 'recorded', id };
  try {
    videoButton.textContent = 'Stop';
    const { sound, silent } = await songSound(filmed, count / VIDEO_FPS);
    const made = await encodeVideo({
      canvas: renderer.domElement, width, height, fps: VIDEO_FPS, count, sound,
      draw: f => {
        pose(Math.min(seconds, f / VIDEO_FPS) * filmed.fps);
        renderer.render(scene, camera);
      },
      progress: done => { videoButton.textContent = 'Stop ' + Math.round(done * 100) + '%'; },
      stopped: () => recording.stop || clip !== filmed,
    });
    if (made) {
      answer.data = made.data;
      answer.silent = silent || made.silent;
    } else {
      answer.stopped = true;
    }
  } catch (error) {
    answer.error = String(error && error.message || error);
  }
  recording = null;
  modelButton.disabled = false;
  videoButton.textContent = 'MP4';
  videoButton.title = 'Write the score shown as an MP4 video: this view, with its song.';
  renderer.setPixelRatio(window.devicePixelRatio);
  resize();
  beat = keep;
  show();
  vscode.postMessage(answer);
}

// ---- Click a limb: go to the word that put it where it is.
const raycaster = new THREE.Raycaster(), pointer = new THREE.Vector2();
let downAt = null;
renderer.domElement.addEventListener('pointerdown', event => { downAt = [event.clientX, event.clientY]; });
renderer.domElement.addEventListener('pointerup', event => {
  if (!clip || !downAt || Math.hypot(event.clientX - downAt[0], event.clientY - downAt[1]) > 4) return;   // a drag turns the view
  const box = renderer.domElement.getBoundingClientRect();
  pointer.set((event.clientX - box.left) / box.width * 2 - 1, -((event.clientY - box.top) / box.height) * 2 + 1);
  raycaster.setFromCamera(pointer, camera);
  const hit = raycaster.intersectObjects(figures.flatMap(f => f.bones), false)[0];
  if (!hit) return;

  const figureId = clip.figures[hit.object.userData.figure].id;
  for (const channel of channelOfBone(hit.object.userData.bone)) {
    const own = clip.spans.filter(s => s.figure === figureId && s.channel === channel);
    // The event in progress, else the last one finished, else the first to come.
    const span = own.find(isActive) ?? own.filter(s => s.t1 <= beat + 1e-9).pop() ?? null;
    if (span) {
      vscode.postMessage({ type: 'reveal', start: span.start, length: span.length });
      return;
    }
  }
});

// ---- Messages from the extension.
window.addEventListener('message', event => {
  const message = event.data;
  if (message.type === 'clip') {
    const shown = clip && !message.reset ? clip.name : null;
    clips = message.clips ?? [];
    // An edit keeps the clip being looked at (by name: the objects are all new), and the
    // moment: change a word and see that pose change. If the name is gone (it is being
    // renamed, or the file is another one), the clip the cursor is in, else the first.
    const same = shown === null ? null : clips.find(c => c.name === shown) ?? null;
    const under = same ? null : clips.find(c => holds(c, typeof message.offset === 'number' ? message.offset : -1)) ?? null;
    if (message.reset) setPlaying(false);
    setClip(same ?? under ?? clips[0] ?? null, !message.reset && (same !== null || under !== null));
  } else if (message.type === 'song') {
    const notes = message.song && !message.song.error ? message.song.notes ?? [] : [];
    // The same notes again (the answer to every Play) change nothing that is sounding.
    const known = songs.get(message.key);
    const same = known && known.length === notes.length && known.every((n, i) => n.every((value, j) => value === notes[i][j]));
    if (!same) {
      songs.set(message.key, notes);
      if (message.key === songKey) {
        player.setSong(notes);
        cue();
      }
    }
    songError = message.key === songKey && message.song && message.song.error ? message.song.error : '';
    soundTitle();
  } else if (message.type === 'synth') {
    synthConfig = message;
    port.configure(message);
  } else if (message.type === 'export') {
    if (!askExport(message.what, message.id)) {
      vscode.postMessage({ type: 'recorded', id: message.id, error: recording ? 'a video is being written.' : 'there is no movement to export.' });
    }
  } else if (message.type === 'record') {
    void record(message.id);
  } else if (message.type === 'cursor' && clip) {
    // The cursor in a section of another clip shows that clip; in no section (the headers,
    // a pose or a move) it changes nothing.
    const under = clipAt(message.offset);
    if (under && under !== clip) setClip(under, false);
    if (playing) return;
    const at = clip.spans.filter(s => message.offset >= s.start && message.offset <= s.start + s.length);
    if (at.length) {
      // A word in a repeated section is performed several times: prefer the next one.
      // A word is at the beat the body is there: show that beat.
      const next = at.find(s => s.t1 > beat + 1e-9 && s.t0 <= beat + 1e-9) ?? at.find(s => s.t0 > beat) ?? at[0];
      seekTo(next.t0);
    }
  }
});

function resize() {
  if (recording) return;   // the canvas has the video's size until it is written
  const width = stage.clientWidth, height = stage.clientHeight;
  renderer.setSize(width, height, false);
  camera.aspect = width / Math.max(1, height);
  camera.updateProjectionMatrix();
}
new ResizeObserver(resize).observe(stage);
resize();

function tick(now) {
  if (recording) {
    // The canvas is the film's: nothing else draws on it.
    last = now;
    requestAnimationFrame(tick);
    return;
  }
  if (playing && clip) {
    beat = beatAt(clip, secondsAt(clip, beat) + (now - last) / 1000 * Number(speed.value));
    if (beat >= clip.totalBeats) {
      if (clip.loop && clip.totalBeats > 0) {
        // The last frame of a loop IS its first: carry on from there with no stop, keeping
        // the time left over. A loop that travels starts again from frame 0.
        beat %= clip.totalBeats;
        player.rewind();   // the last notes ring on into the first
      } else if (wantLoop && clip.totalBeats > 0) {
        beat = 0;
        player.rewind();
      } else {
        beat = clip.totalBeats;
        setPlaying(false);
      }
    }
    sound();
    show();
  }
  last = now;
  controls.update();
  renderer.render(scene, camera);
  requestAnimationFrame(tick);
}
requestAnimationFrame(tick);

vscode.postMessage({ type: 'ready' });
