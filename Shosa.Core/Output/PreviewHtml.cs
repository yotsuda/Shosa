using System.Text.Json;
using Shosa.Core.Motion;

namespace Shosa.Core.Output;

/// <summary>
/// One self-contained page: the source on the left, the figures moving on the right, and
/// the two tied together — the word being performed is lit, and a click on a word goes to
/// its moment. It is the page a VS Code webview would show; there the clip arrives from the
/// language server on every keystroke instead of being written into the file.
/// </summary>
public static class PreviewHtml
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <param name="notes">
    /// The song the clip is danced to, in the clip's beats (`SongReader.Notes`): the page then
    /// plays it with the movement. Null for a clip without a song.
    /// </param>
    public static string Render(MotionClip clip, string source, IReadOnlyList<double[]>? notes = null)
    {
        // "</" must not appear inside a script element.
        string data = JsonSerializer.Serialize(new { clip, source, notes }, Json).Replace("</", "<\\/");
        return Template.Replace("/*TITLE*/", System.Net.WebUtility.HtmlEncode(clip.Title.Length > 0 ? clip.Title : "Shosa preview"))
            .Replace("/*SOUND*/", Sound.Value)
            .Replace("/*DATA*/", data);
    }

    /// <summary>
    /// What the VS Code preview is made of too (editors/vscode/src/preview): the synthesiser
    /// (sound.js) and where a clip's beats are in time and in its bars (time.js), as plain functions.
    /// </summary>
    private static readonly Lazy<string> Sound = new(() => string.Concat(new[] { "sound.js", "time.js" }.Select(name =>
    {
        using var stream = typeof(PreviewHtml).Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("export function", "function") + "\n";
    })));

    private const string Template = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>/*TITLE*/</title>
<style>
  :root { --bg:#f6f7f9; --panel:#ffffff; --ink:#23272e; --faint:#7b838f; --line:#dfe3e8; --on:#ffe28a; --accent:#2f6fdf; --mark:#1e8e5a; }
  @media (prefers-color-scheme: dark) {
    :root { --bg:#16181c; --panel:#1e2126; --ink:#e3e6ea; --faint:#8a929e; --line:#30353c; --on:#6b5a12; --accent:#6fa2ff; --mark:#4fd08f; }
  }
  * { box-sizing: border-box; }
  html, body { height: 100%; margin: 0; }
  body { background: var(--bg); color: var(--ink); font: 14px/1.5 system-ui, sans-serif; display: flex; flex-direction: column; }
  main { flex: 1; display: flex; min-height: 0; }
  #source { width: 42%; max-width: 640px; margin: 0; padding: 16px; overflow: auto; background: var(--panel);
            border-right: 1px solid var(--line); font: 13px/1.6 ui-monospace, Consolas, monospace; white-space: pre; tab-size: 2; }
  #source span.ev { border-radius: 3px; cursor: pointer; }
  #source span.ev:hover { outline: 1px solid var(--accent); }
  #source span.ev.on { background: var(--on); }
  #stage { flex: 1; min-width: 0; position: relative; }
  #stage canvas { display: block; width: 100%; height: 100%; }
  footer { display: flex; gap: 12px; align-items: center; padding: 8px 16px; background: var(--panel); border-top: 1px solid var(--line); }
  button { font: inherit; padding: 4px 14px; border: 1px solid var(--line); border-radius: 6px; background: var(--bg); color: var(--ink); cursor: pointer; }
  input[type=range] { flex: 1; margin: 0; }
  #track { flex: 1; display: flex; position: relative; }
  #ticks i { position: absolute; bottom: -5px; width: 2px; margin-left: -1px; height: 5px; background: var(--mark); pointer-events: none; }
  #marks { position: absolute; left: 14px; top: 10px; font-weight: 600; color: var(--mark); pointer-events: none; white-space: pre; }
  #beat { font-variant-numeric: tabular-nums; min-width: 12ch; color: var(--faint); }
  [hidden] { display: none; }
  @media (max-width: 720px) { main { flex-direction: column; } #source { width: 100%; max-width: none; height: 40%; border-right: 0; border-bottom: 1px solid var(--line); } }
</style>
</head>
<body>
<main>
  <pre id="source"></pre>
  <div id="stage"><div id="marks"></div></div>
</main>
<footer>
  <button id="play">Play</button>
  <div id="track"><input id="seek" type="range" min="0" max="1000" value="0" aria-label="Position"><div id="ticks"></div></div>
  <span id="beat"></span>
  <label id="soundLabel" hidden><input id="sound" type="checkbox" checked> Sound</label>
  <label>Speed <select id="speed"><option>0.25</option><option>0.5</option><option selected>1</option></select></label>
</footer>
<script id="data" type="application/json">/*DATA*/</script>
<script type="importmap">
{ "imports": { "three": "https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js",
               "three/addons/": "https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/" } }
</script>
<script type="module">
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

/*SOUND*/

const { clip, source, notes } = JSON.parse(document.getElementById('data').textContent);
// The song the clip is danced to, if it has one: played from the figure's own clock.
const player = createSound(), soundBox = document.getElementById('sound');
player.setSong(notes ?? null);
document.getElementById('soundLabel').hidden = !notes;
const frameCount = clip.figures.length ? clip.figures[0].frames.length : 1;

// ---- Source pane: every event of the source becomes a span that knows when it is performed.
const byStart = new Map();
for (const s of clip.spans) {
  const key = s.start + ':' + s.length;
  if (!byStart.has(key)) byStart.set(key, { start: s.start, length: s.length, times: [] });
  byStart.get(key).times.push([s.t0, s.t1]);
}
const events = [...byStart.values()].sort((a, b) => a.start - b.start);
const pane = document.getElementById('source');
let at = 0;
for (const ev of events) {
  if (ev.start < at) continue;
  pane.append(source.slice(at, ev.start));
  const el = document.createElement('span');
  el.className = 'ev';
  el.textContent = source.slice(ev.start, ev.start + ev.length);
  el.addEventListener('click', () => {
    const next = ev.times.find(t => t[0] > beat + 1e-6) ?? ev.times[0];
    seekTo(next[0]);
  });
  ev.el = el;
  pane.append(el);
  at = ev.start + ev.length;
}
pane.append(source.slice(at));

// ---- Stage.
const stage = document.getElementById('stage');
const dark = matchMedia('(prefers-color-scheme: dark)').matches;
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(devicePixelRatio);
stage.append(renderer.domElement);
const scene = new THREE.Scene();
scene.background = new THREE.Color(dark ? 0x16181c : 0xf6f7f9);
const camera = new THREE.PerspectiveCamera(40, 1, 0.1, 100);
camera.position.set(2.4, 1.7, 4.6);
const controls = new OrbitControls(camera, renderer.domElement);
controls.target.set(0, 0.9, 0);
scene.add(new THREE.HemisphereLight(0xffffff, 0x8890a0, 2.2));
const sun = new THREE.DirectionalLight(0xffffff, 1.4);
sun.position.set(2, 5, 3);
scene.add(sun);
scene.add(new THREE.GridHelper(12, 12, dark ? 0x4a515c : 0xb9c0ca, dark ? 0x2b3037 : 0xdfe3e8));
// The audience side: a line along the front of the floor.
const front = new THREE.Mesh(new THREE.BoxGeometry(12, 0.01, 0.04), new THREE.MeshBasicMaterial({ color: 0x2f6fdf }));
front.position.set(0, 0, 6);
scene.add(front);

const colors = { c: dark ? 0xd5d9df : 0x3a4048, l: 0x2f6fdf, r: 0xd9482b };
const boneGeometry = new THREE.CylinderGeometry(0.028, 0.028, 1, 10);
const jointGeometry = new THREE.SphereGeometry(0.036, 12, 10);
const headGeometry = new THREE.SphereGeometry(0.105, 20, 16);
const yAxis = new THREE.Vector3(0, 1, 0);
const HEAD = clip.joints.indexOf('head'), NOSE = clip.joints.indexOf('nose');

const figures = clip.figures.map(figure => {
  const bones = clip.bones.map((pair, i) => {
    const mesh = new THREE.Mesh(boneGeometry, new THREE.MeshStandardMaterial({ color: colors[clip.boneSides[i]], roughness: 0.6 }));
    scene.add(mesh);
    return mesh;
  });
  const joints = clip.joints.map((name, i) => {
    const geometry = i === HEAD ? headGeometry : jointGeometry;
    const mesh = new THREE.Mesh(geometry, new THREE.MeshStandardMaterial({ color: i === NOSE ? 0xd9482b : colors.c, roughness: 0.6 }));
    scene.add(mesh);
    return mesh;
  });
  return { figure, bones, joints, points: clip.joints.map(() => new THREE.Vector3()) };
});

const a = new THREE.Vector3(), b = new THREE.Vector3(), d = new THREE.Vector3();
function pose(frame) {
  const f0 = Math.max(0, Math.min(frameCount - 1, Math.floor(frame))), f1 = Math.min(frameCount - 1, f0 + 1), k = frame - f0;
  for (const fig of figures) {
    const p = fig.figure.frames[f0], q = fig.figure.frames[f1];
    fig.points.forEach((v, j) => {
      v.set(p[j * 3] + (q[j * 3] - p[j * 3]) * k, p[j * 3 + 1] + (q[j * 3 + 1] - p[j * 3 + 1]) * k, p[j * 3 + 2] + (q[j * 3 + 2] - p[j * 3 + 2]) * k);
      fig.joints[j].position.copy(v);
    });
    clip.bones.forEach((pair, i) => {
      a.copy(fig.points[pair[0]]); b.copy(fig.points[pair[1]]);
      d.subVectors(b, a);
      const length = d.length() || 1e-6;
      const mesh = fig.bones[i];
      mesh.position.addVectors(a, b).multiplyScalar(0.5);
      mesh.quaternion.setFromUnitVectors(yAxis, d.multiplyScalar(1 / length));
      mesh.scale.set(1, length, 1);
    });
  }
}

// ---- Transport.
const playButton = document.getElementById('play'), seek = document.getElementById('seek');
const beatLabel = document.getElementById('beat'), speed = document.getElementById('speed');
let beat = 0, playing = false, last = 0;

// The moments named for the engine (`!hit`): a tick under the slider at each (the knob travels
// the slider's width less its own), and the name over the stage as it arrives.
const marksLabel = document.getElementById('marks');
if (clip.totalBeats > 0) {
  for (const at of new Set((clip.marks ?? []).map(m => m.beat))) {
    const tick = document.createElement('i');
    tick.style.left = `calc(8px + (100% - 16px) * ${at / clip.totalBeats})`;
    document.getElementById('ticks').append(tick);
  }
}

function show() {
  pose(secondsAt(clip, beat) * clip.fps);
  const arrived = markText(clip, marksAt(clip, beat));
  if (marksLabel.textContent !== arrived) marksLabel.textContent = arrived;
  for (const ev of events) {
    if (!ev.el) continue;
    ev.el.classList.toggle('on', ev.times.some(t => beat >= t[0] && beat < t[1]));
  }
  const [bar, inBar] = barAt(clip, beat);
  beatLabel.textContent = 'bar ' + bar + '  beat ' + (inBar + 1).toFixed(2);
  seek.value = clip.totalBeats ? Math.round(1000 * beat / clip.totalBeats) : 0;
}
function seekTo(value) { beat = Math.max(0, Math.min(clip.totalBeats, value)); show(); player.cue(beat); }
playButton.addEventListener('click', () => {
  playing = !playing;
  if (playing && beat >= clip.totalBeats - 1e-6) beat = 0;
  playButton.textContent = playing ? 'Pause' : 'Play';
  last = performance.now();
  if (playing && notes) player.start();   // a browser lets sound start only from a click or a key
  player.cue(beat);
});
soundBox.addEventListener('change', () => player.cue(beat));
seek.addEventListener('input', () => { playing = false; playButton.textContent = 'Play'; seekTo(clip.totalBeats * seek.value / 1000); });
addEventListener('keydown', e => { if (e.code === 'Space') { e.preventDefault(); playButton.click(); } });

function resize() {
  const w = stage.clientWidth, h = stage.clientHeight;
  renderer.setSize(w, h, false);
  camera.aspect = w / Math.max(1, h);
  camera.updateProjectionMatrix();
}
new ResizeObserver(resize).observe(stage);
resize();

function tick(now) {
  if (playing) {
    beat = beatAt(clip, secondsAt(clip, beat) + (now - last) / 1000 * Number(speed.value));
    if (beat >= clip.totalBeats) { beat = clip.totalBeats; playing = false; playButton.textContent = 'Play'; }
    else if (soundBox.checked) player.play(beat, 60 / (tempoAt(clip, beat) * Number(speed.value)));
    show();
  }
  last = now;
  controls.update();
  renderer.render(scene, camera);
  requestAnimationFrame(tick);
}
show();
requestAnimationFrame(tick);
</script>
</body>
</html>
""";
}
