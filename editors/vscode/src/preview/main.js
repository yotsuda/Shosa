// The preview: the figures of one Shosa file, moving, inside a VS Code webview.
//
// It is given the solved movement (the file's "clips", one per `score`: every joint at every frame, plus
// which source span is performed when) and draws ONE clip at a time: the one whose section
// the cursor is in, or the one picked in the selector. It never parses anything. Messages:
//   in   { type: 'clip', clips, models, offset, reset? }   the movement (after every edit); offset
//                                                  is the cursor, -1 if unknown; models: each character
//                                                  file a dancer names, as written -> the file
//   in   { type: 'model', file, data?, error? }   the bytes of a character file (VRM), or why not
//   in   { type: 'recording', file, data?, error? }   the bytes of the recording a clip keeps time with
//                                                  (clip.music.recording.audio), or why not
//   in   { type: 'stems', key, state, timeline?, stems?, notes?, progress?, error? }   the song of the clips
//                                                  one stem at a time, from Lily# (stems.js; key: the song as
//                                                  written, its score, whether the recording is followed)
//   in   { type: 'stem', file, data?, error? }   the bytes of a stem's file, or why not
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
//   in   { type: 'mix', id }              mix the song of the score shown as the mixer has it
//   out  { type: 'mixed', id, left?, right?, sampleRate?, error? }   its samples (ArrayBuffers of floats), or why not
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { createCharacters } from './characters.js';
import { createFontSound } from './fontsound.js';
import { createJuggle } from './juggle.js';
import { applyClipPatch } from './patch.js';
import { createRecording, cutRecording } from './recording.js';
import { createSound } from './sound.js';
import { MaxLevel, createStemPlayer, stemRows } from './stems.js';
import { createSynthPort } from './synth.js';
import { barAt, beatAt, clicksOf, markText, marksAt, secondsAt, tempoAt } from './time.js';
import { renderSong } from './songaudio.js';
import { encodeVideo } from './video.js';

const vscode = acquireVsCodeApi();

// ---- Page.
const style = document.createElement('style');
style.textContent = `
  html, body { height: 100%; margin: 0; }
  /* The buttons and the words on the page are not selected by a drag (the user): it is a tool, not a document. */
  body { user-select: none; -webkit-user-select: none; }
  input[type=text], textarea { user-select: text; -webkit-user-select: text; }
  body { display: flex; flex-direction: column; padding: 0; overflow: hidden;
         color: var(--vscode-foreground, #333); font: 13px/1.4 var(--vscode-font-family, system-ui, sans-serif); }
  #stage { flex: 1; min-height: 0; position: relative; display: flex; align-items: center; justify-content: center;
           background: var(--vscode-sideBar-background, #8882); }
  #stage canvas { display: block; width: 100%; height: 100%; cursor: grab; flex: none; }
  #empty { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center;
           color: var(--vscode-descriptionForeground, #777); padding: 24px; text-align: center; pointer-events: none; }
  footer { display: flex; gap: 10px; align-items: center; padding: 6px 12px;
           border-top: 1px solid var(--vscode-panel-border, #ccc); }
  header { display: flex; gap: 8px; align-items: center; padding: 4px 12px; min-height: 22px;
           border-bottom: 1px solid var(--vscode-panel-border, #ccc); }
  header[hidden], header [hidden], footer [hidden] { display: none; }
  #name { font-weight: 600; }
  #modelProblem { color: var(--vscode-errorForeground, #c33); font-size: 12px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 40ch; }
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
  footer #mixerButton, footer #cameraButton { min-width: 0; padding: 1px 10px; color: var(--vscode-button-secondaryForeground, #fff);
                        background: var(--vscode-button-secondaryBackground, #5f6a79); }
  footer #mixerButton[aria-pressed=true], footer #cameraButton[aria-pressed=true] { background: var(--vscode-button-secondaryHoverBackground, #4c5561); }
  #mixer, #camera { position: absolute; right: 8px; bottom: 8px; max-height: calc(100% - 16px); overflow-y: auto; min-width: 260px; max-width: calc(100% - 16px);
           padding: 8px 10px; border-radius: 4px; border: 1px solid var(--vscode-panel-border, #ccc);
           background: var(--vscode-editorWidget-background, #f3f3f3); color: var(--vscode-editorWidget-foreground, inherit); }
  #mixer[hidden], #camera[hidden] { display: none; }
  #camera { min-width: 220px; }
  #cameraRows { display: grid; grid-template-columns: auto 1fr; gap: 6px 8px; align-items: center; }
  #cameraButtons { display: flex; gap: 6px; grid-column: 1 / -1; }
  #cameraButtons button { min-width: 0; padding: 1px 10px; }
  #cameraButtons #cameraReset { color: var(--vscode-button-secondaryForeground, #fff); background: var(--vscode-button-secondaryBackground, #5f6a79); }
  #mixerRows { display: grid; grid-template-columns: auto 1fr 4ch; gap: 4px 8px; align-items: center; }
  #mixerRows small { color: var(--vscode-descriptionForeground, #777); }
  #mixerRows output { text-align: right; font-variant-numeric: tabular-nums; }
  #mixerNotes { font-size: 12px; color: var(--vscode-descriptionForeground, #777); white-space: pre-wrap; }
  #mixerNotes:not(:empty) { margin-top: 6px; }
`;
document.head.append(style);
document.body.innerHTML = `
  <header id="bar" hidden>
    <select id="clips" aria-label="Score" hidden></select>
    <span id="name" hidden></span>
    <span id="kind" hidden></span>
    <span id="space"></span>
    <span id="modelProblem" hidden></span>
    <label id="modelLabel" hidden title="Show the dancers as the characters their dancer lines name (dancer alice humanoid &quot;girl.vrm&quot;), or as sticks."><input id="modelBox" type="checkbox" checked> Character</label>
    <select id="shape" aria-label="Video shape" title="The shape of the MP4: the view as it is, or a frame the view shows (16:9 is 1920×1080; 9:16 is 1080×1920, for YouTube Shorts).">
      <option value="view">View</option><option value="wide">16:9</option><option value="tall">9:16</option></select>
    <button id="exportVideo" title="Write the score shown as an MP4 video: this view, with its song.">MP4</button>
    <button id="exportAudio" hidden title="Write the song of the score shown as an MP3 or a WAV, mixed as the Mixer has it, for as long as the dance (the sound of its MP4).">Audio</button>
    <button id="exportModel" title="Write the score shown as a model file for an engine: glTF (.glb), VRM Animation (.vrma) or BVH (.bvh).">Model</button>
  </header>
  <div id="stage"><div id="empty">Nothing to perform yet: the file needs a section.</div><div id="marks"></div>
    <div id="mixer" hidden><div id="mixerRows"></div><div id="mixerNotes"></div></div>
    <div id="camera" hidden><div id="cameraRows">
      <div id="cameraButtons">
        <button id="fit" title="Frame the whole score: every dancer and everything they throw, from start to end, as close as it fits (F).">Fit</button>
        <button id="cameraReset" title="Back to where the camera starts.">Reset</button>
      </div>
      <span>Zoom</span><input id="zoom" type="range" min="0" max="1000" aria-label="Zoom" title="Nearer (right) or further (left).">
      <span>Height</span><input id="lift" type="range" min="-100" max="400" aria-label="Height" title="The view up (right) or down (left): less floor, more sky.">
      <span>Angle</span><input id="tilt" type="range" min="-20" max="80" aria-label="Angle" title="From level with the dancers (left) to from above (right).">
    </div></div></div>
  <footer>
    <button id="play">Play</button>
    <div id="track"><input id="seek" type="range" min="0" max="1000" value="0" aria-label="Position"><div id="ticks"></div></div>
    <span id="beat"></span>
    <label><input id="loop" type="checkbox" checked> Loop</label>
    <label id="soundLabel" hidden><input id="sound" type="checkbox" checked> Sound</label>
    <button id="cameraButton" aria-pressed="false" title="Where the view is from: Fit frames the whole score; Zoom, Height and Angle move it. A drag turns the view, the wheel zooms, a right drag moves it. The MP4 is filmed from here.">Camera</button>
    <button id="mixerButton" hidden aria-pressed="false" title="The level of each part of the song, as Lily# made them.">Mixer</button>
    <label>Speed <select id="speed"><option>0.25</option><option>0.5</option><option selected>1</option></select></label>
  </footer>`;

const stage = document.getElementById('stage'), empty = document.getElementById('empty');
/** What the empty stage says when a file has nothing to perform (a 'waiting' message says something else for a while). */
const EMPTY = empty.textContent;
const playButton = document.getElementById('play'), seek = document.getElementById('seek');
const beatLabel = document.getElementById('beat'), speed = document.getElementById('speed'), loop = document.getElementById('loop');
const bar = document.getElementById('bar'), clipSelect = document.getElementById('clips');
const nameLabel = document.getElementById('name'), kindLabel = document.getElementById('kind');
const soundLabel = document.getElementById('soundLabel'), soundBox = document.getElementById('sound');
const ticks = document.getElementById('ticks'), marksLabel = document.getElementById('marks');
const videoButton = document.getElementById('exportVideo'), modelButton = document.getElementById('exportModel');
const shapeSelect = document.getElementById('shape');
const modelLabel = document.getElementById('modelLabel'), modelBox = document.getElementById('modelBox'), modelProblem = document.getElementById('modelProblem');
const audioButton = document.getElementById('exportAudio'), mixerButton = document.getElementById('mixerButton');
const mixerPanel = document.getElementById('mixer'), mixerRows = document.getElementById('mixerRows'), mixerNotes = document.getElementById('mixerNotes');

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
// Test pages move the camera close (out\pv\harness-mic.html); nothing in the extension reads it.
globalThis.shosaPreviewCamera = { camera, controls };
scene.add(new THREE.HemisphereLight(0xffffff, 0x8890a0, 2.2));
const sun = new THREE.DirectionalLight(0xffffff, 1.4);
sun.position.set(2, 5, 3);
scene.add(sun);
scene.add(new THREE.GridHelper(12, 12, dark ? 0x5a616c : 0xb9c0ca, dark ? 0x353a42 : 0xdfe3e8));
// The audience side of the floor.
const front = new THREE.Mesh(new THREE.BoxGeometry(12, 0.01, 0.04), new THREE.MeshBasicMaterial({ color: 0x2f6fdf }));
front.position.set(0, 0, 6);
scene.add(front);

// The characters the dancer lines name (characters.js). A dressed figure's sticks are hidden but stay
// in the scene: a click on the character still finds the limb inside it, and its word.
let models = {};   // the character files of the clips: as written -> the file
const characters = createCharacters(scene, () => {
  look();
  show();
});

const juggle = createJuggle(scene);

function look() {
  const wanted = characters.any();
  modelLabel.hidden = !wanted;
  if (wanted) bar.hidden = false;
  const problems = characters.problems();
  modelProblem.hidden = !problems;
  modelProblem.textContent = problems.split('\n')[0];
  modelProblem.title = problems;
  characters.setVisible(modelBox.checked);
  figures.forEach((figure, i) => {
    const stick = !(modelBox.checked && characters.isDressed(i));
    for (const mesh of [...figure.bones, ...figure.joints]) mesh.visible = stick;
  });
}
modelBox.addEventListener('change', () => {
  look();
  modelBox.blur();
});

// Left limbs blue, right limbs red — the same as the contact sheet.
const colors = { c: dark ? 0xd5d9df : 0x3a4048, l: 0x2f6fdf, r: 0xd9482b };
const boneGeometry = new THREE.CylinderGeometry(0.028, 0.028, 1, 10);
const jointGeometry = new THREE.SphereGeometry(0.036, 12, 10);
const headGeometry = new THREE.SphereGeometry(0.105, 20, 16);
const yAxis = new THREE.Vector3(0, 1, 0);

let clips = [];         // every clip of the file
let clipsSeq = null;    // the number of the message they came by, or were last patched by: a patch names the one it is made on
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
  if (/Hip/.test(name)) return ['hips', side + 'Leg', 'support', 'height'];   // the line of the hips is what `hips` turns
  if (/Knee|Ankle|Toe/.test(name)) return [side + 'Leg', 'support', 'height'];
  if (/head|nose/.test(name)) return ['head', 'torso'];
  if (/Shoulder/.test(name)) return ['shoulders', 'torso'];   // the shoulder line is what `shoulders` turns
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
  if (clip) characters.pose(clip, clip.figures, frame);
  // The balls juggled (DESIGN.md 118): in the character's hands where it has one, else in the figure's.
  if (clip) juggle.pose(clip, figures, frame, i => modelBox.checked ? characters.palms(i) : null, true, i => modelBox.checked ? characters.forearms(i) : null, i => modelBox.checked ? characters.track(i) : null);
  else juggle.clear();
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
// oscillators of sound.js. A song with a recording beside it (clip.music.recording) sounds the
// recording instead (recording.js), once it is decoded; the notes until then.
const songs = new Map();   // key (file, score, order) -> notes
let songKey = null;        // the key of the shown clip's song; null without one
let songError = '', fontStatus = { state: 'none' };
const recorder = createRecording();
const recordingOf = c => (c && c.music && c.music.recording) || null;
let shownRecording = null; // the recording file of the shown clip; null without one
let fromRecording = false; // what sounded last: the recording (true) or the notes

// The song one stem at a time, from Lily# (stems.js; DESIGN.md 98): mixed by the Mixer, and the
// first choice whenever a clip's are there — before the recording alone, and before the notes.
const stemSongs = new Map(); // key -> the last { type: 'stems' } about it
const stemPlayer = createStemPlayer();
let fromStems = false;       // what sounded last was the stems
// A clip danced to its song as the song is played (no order or tempo of its own) can have them.
const stemsKeyOf = c => c && c.music && !(c.music.order && c.music.order.length) && c.music.tempo == null
  ? JSON.stringify([c.music.file, c.music.score ?? null, !!c.music.recording]) : null;
// Where the dance's 0 is on the stems' timeline, and whether they are on the dance's clock.
function stemsOf(c) {
  const key = stemsKeyOf(c), message = key ? stemSongs.get(key) : null;
  if (!message || message.state !== 'ready' || !message.stems || !message.stems.length) return null;
  if (message.timeline !== (c.music.recording ? 'recording' : 'score')) return null;
  return { message, offset: c.music.recording ? c.music.recording.offset : 0 };
}

// The Mixer's levels, kept for each song (its file and score) while the editor keeps the page's state.
const saved = vscode.getState() || {};
const mixes = saved.mixes || {};   // song -> rowId -> { on, level }
const mixKeyOf = c => c && c.music ? JSON.stringify([c.music.file, c.music.score ?? null]) : '';
const levelsOf = c => mixes[mixKeyOf(c)] || {};
function setLevel(id, change) {
  const key = mixKeyOf(clip);
  const levels = mixes[key] = { ...(mixes[key] || {}) };
  levels[id] = { ...(levels[id] || { on: true, level: 1 }), ...change };
  vscode.setState({ ...(vscode.getState() || {}), mixes });
  stemPlayer.setLevels(levels);
  soundTitle();
}

let mixerShown = '';   // what the rows were made for: they are made again only when it changes
function mixer() {
  const key = stemsKeyOf(clip), message = key ? stemSongs.get(key) : null;
  mixerButton.hidden = !message || message.state === 'none' && !message.error;
  const usable = stemsOf(clip);
  audioButton.hidden = !usable || videoButton.hidden;
  if (mixerPanel.hidden) return;
  const stems = usable ? usable.message.stems : [];
  const shown = JSON.stringify([mixKeyOf(clip), stems.map(s => [s.kind, s.name])]);
  if (shown !== mixerShown) {
    mixerShown = shown;
    const levels = levelsOf(clip);
    mixerRows.replaceChildren(...stemRows(stems).flatMap(row => {
      const level = levels[row.id] ?? { on: true, level: 1 };
      const name = document.createElement('label');
      const box = Object.assign(document.createElement('input'), { type: 'checkbox', checked: level.on });
      const kind = Object.assign(document.createElement('small'), {
        textContent: row.kind === 'voice' ? 'voice' : row.kind === 'part' ? '' : 'recording' });
      name.append(box, ' ' + row.name + ' ', kind);
      const slider = Object.assign(document.createElement('input'), {
        type: 'range', min: '0', max: String(MaxLevel * 100), step: '5', value: String(Math.round(level.level * 100)) });
      slider.setAttribute('aria-label', row.name + ' level');
      const value = Object.assign(document.createElement('output'), { textContent: Math.round(level.level * 100) + '%' });
      box.addEventListener('change', () => setLevel(row.id, { on: box.checked }));
      slider.addEventListener('input', () => {
        value.textContent = slider.value + '%';
        setLevel(row.id, { level: Number(slider.value) / 100 });
      });
      slider.addEventListener('dblclick', () => {   // back to as made
        slider.value = '100';
        value.textContent = '100%';
        setLevel(row.id, { level: 1 });
      });
      return [name, slider, value];
    }));
  }
  // Why some of it is missing, or how far Lily# is.
  const lines = [];
  if (message && message.state === 'loading') lines.push('Lily# is making the song\'s parts' + (message.progress ? ': ' + message.progress : '') + '…');
  if (message && message.state === 'none' && message.error) lines.push(message.error);
  if (message && message.state === 'ready' && !usable && message.stems && message.stems.length) {
    lines.push(clip.music.recording
      ? 'Lily# could not put the parts on the recording\'s beats, which this dance keeps time with: the recording alone plays.'
      : 'Lily# put the parts on the recording\'s beats, which this dance does not keep time with: the song\'s notes play.');
  }
  if (usable) {
    const state = stemPlayer.state();
    if (state === 'loading') lines.push('Reading the parts…');
    else if (state !== 'ready') lines.push(state);
  }
  for (const note of (message && message.notes) || []) lines.push(note);
  mixerNotes.textContent = lines.join('\n');
}
mixerButton.addEventListener('click', () => {
  mixerPanel.hidden = !mixerPanel.hidden;
  mixerButton.setAttribute('aria-pressed', String(!mixerPanel.hidden));
  mixerShown = '';
  mixer();
});

// The stems of the clip shown, to the player; the files no song uses any more are let go.
function applyStems() {
  const usable = stemsOf(clip);
  stemPlayer.setStems(usable ? usable.message.stems : []);
  stemPlayer.setLevels(levelsOf(clip));
  if (!usable && fromStems) cue();
  const used = new Set();
  for (const message of stemSongs.values()) for (const s of message.stems || []) used.add(s.file);
  stemPlayer.prune(used);
  mixer();
}

// The Sound box says on hover what is wrong with the song, or else what it is played with.
function soundTitle() {
  mixer();
  const usable = stemsOf(clip);
  if (usable) {
    const state = stemPlayer.state();
    soundLabel.title = state === 'ready' ? 'Played from the parts Lily# made of the song, at the levels the Mixer gives them.'
      : state === 'loading' ? 'Reading the parts Lily# made of the song; until then, as before.'
      : state;
    return;
  }
  if (!songKey) {
    soundLabel.title = 'No song: a click on every beat (louder on the first of a bar) and a clap at every mark.';
    return;
  }
  const recording = recordingOf(clip);
  if (recording) {
    const name = recording.audio.split(/[\\/]/).pop(), state = recorder.state(recording.audio);
    soundLabel.title = state === 'ready' ? 'Played from the recording ' + name + ': beat 0 is at ' + recording.offset.toFixed(2) + ' s of it.'
      : state === 'loading' ? 'Reading the recording ' + name + '; the song\'s notes until then.'
      : state + ' The song\'s notes instead.';
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

// What sounds stops, and the notes (or the recording) go on from the beat shown.
const cue = () => {
  player.cue(beat);
  recorder.cue();
  fromRecording = false;
  stemPlayer.cue();
  fromStems = false;
};

function sound() {
  if (!soundBox.checked) return;
  const stems = stemsOf(clip), rate = Number(speed.value);
  if (stems && stemPlayer.play(stems.offset + secondsAt(clip, beat), rate)) {
    if (!fromStems) {   // read while the notes or the recording were sounding: they stop
      player.cue(beat);
      recorder.cue();
      fromRecording = false;
    }
    fromStems = true;
    return;
  }
  if (fromStems) {      // the Mixer asked for files still being read: as before until then
    stemPlayer.cue();
    fromStems = false;
  }
  const recording = recordingOf(clip);
  if (recording && recorder.play(recording.audio, recording.offset, secondsAt(clip, beat), Number(speed.value))) {
    if (!fromRecording) player.cue(beat);   // decoded while the notes were sounding: they stop
    fromRecording = true;
    return;
  }
  player.play(beat, 60 / (tempoAt(clip, beat) * Number(speed.value)));
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
    : 'The end joins the start. The dancer travels, so each round starts again from where the first began.';
  // The bar is there whenever there is something to export.
  const movement = !!(clip && clip.figures.length && clip.totalBeats > 0);
  bar.hidden = !movement && clipSelect.hidden && nameLabel.hidden && kindLabel.hidden;
  videoButton.hidden = modelButton.hidden = shapeSelect.hidden = !movement;
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
  if (clip) characters.dress(clip.figures, models); else { characters.clear(); juggle.clear(); }
  look();
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
  const recording = recordingOf(clip)?.audio ?? null;
  if (recording !== shownRecording) {
    shownRecording = recording;
    recorder.cue();
    fromRecording = false;
  }
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
  applyStems();
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
    recorder.start();
    stemPlayer.start();
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
  // The song as the Mixer mixes it, when Lily# handed it out one stem at a time.
  const stems = stemsOf(filmed);
  if (stems) {
    try {
      return { sound: await stemPlayer.render(stems.offset, seconds) };
    } catch {
      // the Mixer says why; the recording or the notes below
    }
  }
  // The recording the dance keeps time with, as recorded; the notes only when it cannot be read.
  const recording = recordingOf(filmed);
  if (recording) {
    try {
      const buffer = await recorder.buffer(recording.audio);
      if (buffer) return { sound: cutRecording(buffer, recording.offset, seconds) };
    } catch {
      // the Sound box says why; the notes below
    }
  }
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
  modelButton.disabled = shapeSelect.disabled = true;
  videoButton.title = 'Stop writing the video.';

  // The picture: the frame the view shows (1920×1080, or 1080×1920 for Shorts), or the view as it is,
  // 1080 pixels high (less when that would be wider than 1920).
  let width, height;
  if (FRAMES[shapeSelect.value]) {
    [width, height] = FRAMES[shapeSelect.value];
  } else {
    const aspect = stage.clientWidth / Math.max(1, stage.clientHeight);
    height = 1080;
    width = 2 * Math.round(height * aspect / 2);
    if (width > 1920) {
      width = 1920;
      height = 2 * Math.round(width / aspect / 2);
    }
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
  modelButton.disabled = shapeSelect.disabled = false;
  videoButton.textContent = 'MP4';
  videoButton.title = 'Write the score shown as an MP4 video: this view, with its song.';
  renderer.setPixelRatio(window.devicePixelRatio);
  resize();
  beat = keep;
  show();
  vscode.postMessage(answer);
}

// The song of the score shown, mixed as the Mixer has it, for as long as the dance (the sound its MP4 has):
// the samples go to the extension, and Lily# writes them as an MP3 or a WAV.
async function mixSong(id) {
  const stems = stemsOf(clip);
  const answer = { type: 'mixed', id };
  if (!stems) {
    answer.error = 'the song of this score was not handed out by Lily# one part at a time.';
  } else {
    // An MP3 is 32, 44.1 or 48 kHz: the page's own rate when it is one of them, so nothing is resampled.
    const native = stemPlayer.sampleRate();
    try {
      const mix = await stemPlayer.render(stems.offset, secondsAt(clip, clip.totalBeats), [32000, 44100, 48000].includes(native) ? native : 48000);
      answer.left = mix.left.buffer;
      answer.right = mix.right.buffer;
      answer.sampleRate = mix.sampleRate;
    } catch (error) {
      answer.error = String(error && error.message || error);
    }
  }
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
  if (message.type === 'waiting') {
    // A file the server has sent nothing for (yet), or a server that has stopped: the stage is cleared, and says which.
    clips = [];
    clipsSeq = null;
    setPlaying(false);
    setClip(null, false);
    empty.textContent = message.text;
  } else if (message.type === 'clip' || message.type === 'patch') {
    if (message.type === 'patch' && message.base !== clipsSeq) {
      // Made on other clips than the page has: every clip is asked for again.
      vscode.postMessage({ type: 'resync' });
      return;
    }
    empty.textContent = EMPTY;
    const shown = clip && !message.reset ? clip.name : null;
    // After an edit, what it changed is put into the clips as they were (patch.js): a figure it did not touch is
    // the same object as before, and what was worked out for it stays.
    clips = message.type === 'patch' ? applyClipPatch(clips, message.patch) : message.clips ?? [];
    clipsSeq = message.seq;
    models = message.models ?? {};
    // An edit keeps the clip being looked at (by name: the objects are all new), and the
    // moment: change a word and see that pose change. If the name is gone (it is being
    // renamed, or the file is another one), the clip the cursor is in, else the first.
    const same = shown === null ? null : clips.find(c => c.name === shown) ?? null;
    const under = same ? null : clips.find(c => holds(c, typeof message.offset === 'number' ? message.offset : -1)) ?? null;
    if (message.reset) setPlaying(false);
    setClip(same ?? under ?? clips[0] ?? null, !message.reset && (same !== null || under !== null));
  } else if (message.type === 'model') {
    characters.setFile(message.file, message.data, message.error);
  } else if (message.type === 'stems') {
    stemSongs.set(message.key, message);
    applyStems();
    soundTitle();
  } else if (message.type === 'stem') {
    stemPlayer.setFile(message.file, message.data, message.error);
    soundTitle();
    stemPlayer.buffers().then(soundTitle, soundTitle);
  } else if (message.type === 'mix') {
    void mixSong(message.id);
  } else if (message.type === 'recording') {
    recorder.setFile(message.file, message.data, message.error);
    soundTitle();
    recorder.buffer(message.file).then(soundTitle, soundTitle);
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

// The MP4's frame, in pixels, for each shape but the view as it is: the view shows it, so what is seen is what is filmed.
const FRAMES = { wide: [1920, 1080], tall: [1080, 1920] };

function resize() {
  if (recording) return;   // the canvas has the video's size until it is written
  let width = stage.clientWidth, height = stage.clientHeight;
  const frame = FRAMES[shapeSelect.value];
  if (frame) {
    // The largest box of the frame's shape in the stage, in the middle.
    const aspect = frame[0] / frame[1];
    if (width / Math.max(1, height) > aspect) width = Math.round(height * aspect);
    else height = Math.round(width / aspect);
  }
  renderer.domElement.style.width = frame ? width + 'px' : '';
  renderer.domElement.style.height = frame ? height + 'px' : '';
  renderer.setSize(width, height, false);
  camera.aspect = width / Math.max(1, height);
  camera.updateProjectionMatrix();
}
new ResizeObserver(resize).observe(stage);

// The shape is kept with the page's state, as the Mixer's levels are.
shapeSelect.value = (vscode.getState() || {}).shape || 'view';
if (!shapeSelect.value) shapeSelect.value = 'view';
shapeSelect.addEventListener('change', () => {
  vscode.setState({ ...(vscode.getState() || {}), shape: shapeSelect.value });
  resize();
});
resize();

// ---- The camera panel. A high throw or a tall character went out of the top of the view while the floor filled
// the bottom (the user): Fit frames everything the score shows, start to end; the sliders move the view by hand. The
// drag, the wheel and the right drag still work, and move the sliders. Kept with the page's state; the MP4 is filmed
// from here.
const cameraButton = document.getElementById('cameraButton'), cameraPanel = document.getElementById('camera');
const zoomSlider = document.getElementById('zoom'), liftSlider = document.getElementById('lift'), tiltSlider = document.getElementById('tilt');
const START_VIEW = { position: [2.4, 1.7, 4.6], target: [0, 0.9, 0] };
/** The camera's distance from what it looks at, metres, at either end of the Zoom slider (a log scale between). */
const NEAREST = 0.6, FURTHEST = 25;
const spherical = new THREE.Spherical(), fromTarget = new THREE.Vector3();

function keepCamera() {
  vscode.setState({ ...(vscode.getState() || {}), camera: { position: camera.position.toArray(), target: controls.target.toArray() } });
}
function placeCamera(view) {
  camera.position.fromArray(view.position);
  controls.target.fromArray(view.target);
  camera.lookAt(controls.target);
  controls.update();
  syncCamera();
}
/** The sliders, from where the camera is. */
function syncCamera() {
  spherical.setFromVector3(fromTarget.subVectors(camera.position, controls.target));
  zoomSlider.value = String(Math.round(1000 * Math.log(spherical.radius / NEAREST) / Math.log(FURTHEST / NEAREST)));
  liftSlider.value = String(Math.round(controls.target.y * 100));
  tiltSlider.value = String(Math.round(90 - THREE.MathUtils.radToDeg(spherical.phi)));
}
/** The camera, from the sliders: the way round it looks from is kept. */
function slideCamera() {
  spherical.setFromVector3(fromTarget.subVectors(camera.position, controls.target));
  spherical.radius = NEAREST * Math.pow(FURTHEST / NEAREST, Number(zoomSlider.value) / 1000);
  spherical.phi = THREE.MathUtils.clamp(THREE.MathUtils.degToRad(90 - Number(tiltSlider.value)), 0.01, Math.PI - 0.01);
  controls.target.y = Number(liftSlider.value) / 100;
  camera.position.copy(controls.target).add(fromTarget.setFromSpherical(spherical));
  camera.lookAt(controls.target);
  controls.update();
}
for (const slider of [zoomSlider, liftSlider, tiltSlider]) {
  slider.addEventListener('input', slideCamera);
  slider.addEventListener('change', () => { keepCamera(); slider.blur(); });   // Space plays again
}
controls.addEventListener('change', () => { if (!cameraPanel.hidden) syncCamera(); });
controls.addEventListener('end', keepCamera);
/**
 * The panel just above its button, its right edge at the button's (the user: near the button), kept in the stage; above
 * the Mixer when that is open too.
 */
function placeCameraPanel() {
  if (cameraPanel.hidden) return;
  const area = stage.getBoundingClientRect(), button = cameraButton.getBoundingClientRect();
  const right = Math.max(8, Math.min(area.right - button.right, area.width - cameraPanel.offsetWidth - 8));
  cameraPanel.style.right = right + 'px';
  cameraPanel.style.bottom = (mixerPanel.hidden ? 8 : mixerPanel.offsetHeight + 16) + 'px';
}
new ResizeObserver(placeCameraPanel).observe(stage);
new ResizeObserver(placeCameraPanel).observe(mixerPanel);
cameraButton.addEventListener('click', () => {
  cameraPanel.hidden = !cameraPanel.hidden;
  cameraButton.setAttribute('aria-pressed', String(!cameraPanel.hidden));
  if (!cameraPanel.hidden) syncCamera();
  placeCameraPanel();
  cameraButton.blur();
});
document.getElementById('cameraReset').addEventListener('click', event => {
  placeCamera(START_VIEW);
  keepCamera();
  event.currentTarget.blur();
});
document.getElementById('fit').addEventListener('click', event => {
  fitView();
  event.currentTarget.blur();
});
window.addEventListener('keydown', event => {
  if (event.code === 'KeyF' && !event.ctrlKey && !event.metaKey && !event.altKey && !(event.target instanceof HTMLInputElement || event.target instanceof HTMLSelectElement)) fitView();
});

/** How many moments of the score Fit poses the characters at (the sticks and the props are read at every frame). */
const FIT_SAMPLES = 120;
/** A little room round everything framed. */
const FIT_ROOM = 0.06;
const fitBox = new THREE.Box3();
globalThis.shosaPreviewCamera.fitBox = fitBox;   // what the last Fit framed, for test pages (out\pv\harness-camera.html)

/**
 * Everything the score shows, start to end, as balls (x, y, z, radius, flat): the figures, the characters as posed,
 * the props. Measured point by point, not as one box: a box's empty corners pushed the camera back.
 */
function shownPoints() {
  const points = [];
  fitBox.makeEmpty();
  if (!clip) return points;
  const add = (x, y, z, r) => {
    // Nothing under the floor: it was most of the view (the user).
    const low = Math.min(r, Math.max(0, y + 0.02));
    points.push(x, y, z, r);
    fitBox.expandByPoint(new THREE.Vector3(x - r, y - low, z - r));
    fitBox.expandByPoint(new THREE.Vector3(x + r, y + r, z + r));
  };
  const dressed = i => modelBox.checked && characters.isDressed(i);
  // A prop's size: a club's end and a ring's edge reach past the middle the movement gives.
  const reachOf = prop => prop === 'clubs' ? 0.25 : prop === 'rings' ? 0.17 : 0.04;
  // The sticks and the props, at every frame, from the movement itself (a throw's top is at one frame only).
  clip.figures.forEach((data, i) => {
    if (!dressed(i)) {
      const head = clip.joints.indexOf('head');
      for (const frame of data.frames) {
        for (let j = 0; j * 3 + 2 < frame.length; j++) add(frame[j * 3], frame[j * 3 + 1], frame[j * 3 + 2], j === head ? 0.105 : 0.036);
      }
    }
    const juggled = data.juggle;
    if (juggled && juggled.frames) {
      const reach = reachOf(juggled.prop);
      for (const frame of juggled.frames) {
        for (let b = 0; b * 7 + 2 < frame.length; b++) add(frame[b * 7], frame[b * 7 + 1], frame[b * 7 + 2], reach);
      }
    }
  });
  // The characters, posed at moments through the score, and the props in their hands as drawn.
  if (clip.figures.some((_, i) => dressed(i))) {
    const count = Math.max(1, ...clip.figures.map(f => f.frames.length));
    const samples = Math.min(count, FIT_SAMPLES);
    const put = (v, r) => add(v.x, v.y, v.z, r);
    for (let s = 0; s < samples; s++) {
      pose(samples === 1 ? 0 : s * (count - 1) / (samples - 1));
      characters.points(put);
      juggle.points(put);
    }
    show();   // back to the moment shown
  }
  return points;
}

/** Brings the camera as near as everything shown fits in the view, looking at its middle, from the way round it looks now. */
function fitView() {
  const points = shownPoints();
  if (!points.length) return;
  const center = fitBox.getCenter(new THREE.Vector3());
  const back = fromTarget.subVectors(camera.position, controls.target).normalize();   // toward the camera
  const ahead = back.clone().negate();
  const right = new THREE.Vector3().crossVectors(ahead, camera.up).normalize();
  const up = new THREE.Vector3().crossVectors(right, ahead);
  const tanUp = Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)), tanSide = tanUp * camera.aspect;
  let distance = NEAREST;
  const p = new THREE.Vector3();
  for (let i = 0; i < points.length; i += 4) {
    p.set(points[i], points[i + 1], points[i + 2]).sub(center);
    const r = points[i + 3] + FIT_ROOM;
    const toward = p.dot(back) + r;
    distance = Math.max(distance, toward + (Math.abs(p.dot(up)) + r) / tanUp, toward + (Math.abs(p.dot(right)) + r) / tanSide);
  }
  controls.target.copy(center);
  camera.position.copy(center).addScaledVector(back, Math.min(distance, FURTHEST));
  camera.lookAt(center);
  controls.update();
  syncCamera();
  keepCamera();
}

placeCamera((vscode.getState() || {}).camera || START_VIEW);

function tick(now) {
  if (recording) {
    // The canvas is the film's: nothing else draws on it.
    last = now;
    requestAnimationFrame(tick);
    return;
  }
  if (playing && clip) {
    // While the stems or a recording sound they are the clock: the dance is where they are heard.
    const recording = recordingOf(clip), stems = fromStems ? stemsOf(clip) : null;
    const timeline = stems ? stemPlayer.position() : null;
    const heard = timeline !== null ? timeline - stems.offset
      : recording && fromRecording ? recorder.position(recording.offset) : null;
    beat = beatAt(clip, heard ?? secondsAt(clip, beat) + (now - last) / 1000 * Number(speed.value));
    if (beat >= clip.totalBeats) {
      if (clip.loop && clip.totalBeats > 0) {
        // The last frame of a loop IS its first: carry on from there with no stop, keeping
        // the time left over. A loop that travels starts again from frame 0.
        beat %= clip.totalBeats;
        player.rewind();   // the last notes ring on into the first
        recorder.cue();    // a recording, and the stems, start again from there (the next sound())
        fromRecording = false;
        stemPlayer.cue();
        fromStems = false;
      } else if (wantLoop && clip.totalBeats > 0) {
        beat = 0;
        player.rewind();
        recorder.cue();
        fromRecording = false;
        stemPlayer.cue();
        fromStems = false;
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
