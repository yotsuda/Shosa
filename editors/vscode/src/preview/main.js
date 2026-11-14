// The preview: the figures of one Shosa file, moving, inside a VS Code webview.
//
// It is given the solved movement ("clip": every joint at every frame, plus which source
// span is performed when) and draws it. It never parses anything. Messages:
//   in   { type: 'clip', clip, reset? }   a new movement (after every edit)
//   in   { type: 'cursor', offset }       the editor's cursor moved
//   out  { type: 'ready' }
//   out  { type: 'active', spans }        [start, length] of the words being performed
//   out  { type: 'reveal', start, length} a limb was clicked: its word
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

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
  button { font: inherit; padding: 3px 12px; border: 0; border-radius: 3px; cursor: pointer; min-width: 5em;
           color: var(--vscode-button-foreground, #fff); background: var(--vscode-button-background, #0a66c2); }
  button:hover { background: var(--vscode-button-hoverBackground, #0858a8); }
  input[type=range] { flex: 1; min-width: 60px; }
  #beat { font-variant-numeric: tabular-nums; min-width: 13ch; color: var(--vscode-descriptionForeground, #777); }
  label { display: flex; gap: 4px; align-items: center; white-space: nowrap; }
  select { font: inherit; color: var(--vscode-dropdown-foreground, inherit); background: var(--vscode-dropdown-background, transparent);
           border: 1px solid var(--vscode-dropdown-border, #999); border-radius: 3px; }
`;
document.head.append(style);
document.body.innerHTML = `
  <div id="stage"><div id="empty">Nothing to perform yet: the file needs a figure and a phrase.</div></div>
  <footer>
    <button id="play">Play</button>
    <input id="seek" type="range" min="0" max="1000" value="0" aria-label="Position">
    <span id="beat"></span>
    <label><input id="loop" type="checkbox" checked> Loop</label>
    <label>Speed <select id="speed"><option>0.25</option><option>0.5</option><option selected>1</option></select></label>
  </footer>`;

const stage = document.getElementById('stage'), empty = document.getElementById('empty');
const playButton = document.getElementById('play'), seek = document.getElementById('seek');
const beatLabel = document.getElementById('beat'), speed = document.getElementById('speed'), loop = document.getElementById('loop');

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

let clip = null;
let figures = [];       // { data, bones: Mesh[], joints: Mesh[], points: Vector3[] }
let beat = 0, playing = false, last = 0, lastActive = '';

function channelOfBone(index) {
  // Which track moves this bone: named by the joint at its far end.
  const name = clip.joints[clip.bones[index][1]];
  const side = name.startsWith('left') ? 'left' : name.startsWith('right') ? 'right' : '';
  if (/Elbow|Hand/.test(name)) return [side + 'Arm'];
  if (/Knee|Ankle|Toe|Hip/.test(name)) return [side + 'Leg', 'support'];
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
  if (!clip) return;
  pose(beat * 60 * clip.fps / clip.tempo);

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

  const bar = Math.floor(beat / clip.beatsPerBar - 1e-9) + 1, inBar = beat - (Math.max(1, bar) - 1) * clip.beatsPerBar;
  beatLabel.textContent = clip.totalBeats ? 'bar ' + Math.max(1, bar) + '  beat ' + (inBar + 1).toFixed(2) : '';
  seek.value = clip.totalBeats ? Math.round(1000 * beat / clip.totalBeats) : 0;
}

function seekTo(value) {
  beat = Math.max(0, Math.min(clip ? clip.totalBeats : 0, value));
  show();
}

function setPlaying(value) {
  playing = value;
  playButton.textContent = playing ? 'Pause' : 'Play';
  last = performance.now();
}

playButton.addEventListener('click', () => {
  if (!clip) return;
  if (!playing && beat >= clip.totalBeats - 1e-6) beat = 0;
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
    const rebuild = !clip || clip.figures.length !== message.clip.figures.length;
    clip = message.clip;
    if (rebuild) build(); else figures.forEach((figure, i) => { figure.data = clip.figures[i]; });
    empty.style.display = clip.figures.length && clip.totalBeats > 0 ? 'none' : 'flex';
    if (message.reset) {
      beat = 0;
      setPlaying(false);
    }
    // An edit keeps the moment being looked at: change a word and see that pose change.
    lastActive = '';
    seekTo(beat);
  } else if (message.type === 'cursor' && clip && !playing) {
    const at = clip.spans.filter(s => message.offset >= s.start && message.offset <= s.start + s.length);
    if (at.length) {
      // A word in a repeated phrase is performed several times: prefer the next one.
      const next = at.find(s => s.t1 > beat - 1e-9 && s.t0 <= beat + 1e-9) ?? at.find(s => s.t1 > beat) ?? at[0];
      seekTo(next.t1);
    }
  }
});

function resize() {
  const width = stage.clientWidth, height = stage.clientHeight;
  renderer.setSize(width, height, false);
  camera.aspect = width / Math.max(1, height);
  camera.updateProjectionMatrix();
}
new ResizeObserver(resize).observe(stage);
resize();

function tick(now) {
  if (playing && clip) {
    beat += (now - last) / 1000 * clip.tempo / 60 * Number(speed.value);
    if (beat >= clip.totalBeats) {
      if (loop.checked && clip.totalBeats > 0) {
        beat = 0;
      } else {
        beat = clip.totalBeats;
        setPlaying(false);
      }
    }
    show();
  }
  last = now;
  controls.update();
  renderer.render(scene, camera);
  requestAnimationFrame(tick);
}
requestAnimationFrame(tick);

vscode.postMessage({ type: 'ready' });
