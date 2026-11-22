// The characters the dancers are dressed in: `dancer alice humanoid "girl.vrm"`. The extension hands over
// the bytes of each VRM file (the page is not let read the disk); three-vrm parses them and runs what a
// plain glTF reader leaves out (the constraints of sleeves and twist bones, the spring bones of hair and
// cloth, MToon). Each frame the bones are posed with the rotations the server sends for a dressed figure
// (`turns`, one quaternion per bone of `skeletonBones`, relative to its parent, from a T-pose facing +z:
// the VRM humanoid's own rest pose), and the hips put where the pelvis is, scaled to the character's legs.
// The same as `shosa vrma` and tools/vrm_video.html, without the file in between.
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { VRMLoaderPlugin, VRMUtils } from '@pixiv/three-vrm';
import { keepArmsOut, measureBody } from './clearance.js';
import { createMicHands } from './mic.js';
import { palmsUp } from './juggle.js';

// VRM's mouth expressions, in the order the server sends their weights (`mouth`, Mouth.Shapes).
const MOUTH = ['aa', 'ih', 'ou', 'ee', 'oh'];

export function createCharacters(scene, changed) {
  const loader = new GLTFLoader();
  loader.register(parser => new VRMLoaderPlugin(parser));

  const files = new Map();   // file -> { data } or { error }
  let dressed = [];          // per figure of the clip shown: { file, vrm, hips, flip, loading, error } or null
  let lastFrame = null;
  const q0 = new THREE.Quaternion(), q1 = new THREE.Quaternion();
  const mics = createMicHands(scene);

  function undress(entry) {
    if (entry) mics.drop(entry);
    if (entry && entry.vrm) {
      scene.remove(entry.vrm.scene);
      VRMUtils.deepDispose(entry.vrm.scene);
    }
    if (entry) entry.gone = true;
  }

  function load(entry) {
    const known = files.get(entry.file);
    if (!known) return;               // the bytes are on their way
    if (known.error) {
      entry.error = known.error;
      changed();
      return;
    }
    entry.loading = true;
    // Parsing may take the buffer over: each dancer parses its own copy (a VRM cannot be cloned).
    loader.parse(known.data.slice(0), '', gltf => {
      entry.loading = false;
      const vrm = gltf.userData.vrm;
      if (!vrm) {
        entry.error = entry.file + ' is not a VRM model.';
        changed();
        return;
      }
      if (entry.gone) {
        VRMUtils.deepDispose(vrm.scene);
        return;
      }
      VRMUtils.rotateVRM0(vrm);
      vrm.scene.traverse(o => { if (o.isMesh) o.frustumCulled = false; });
      const hips = vrm.humanoid.getNormalizedBoneNode('hips');
      entry.vrm = vrm;
      entry.restHips = hips ? hips.getWorldPosition(new THREE.Vector3()).y : 1;
      // How tall the file makes the character, standing at rest: the floor (its feet are at 0) to the top of
      // the highest mesh, hair and ears and all, as it is seen. `height` is measured the same way.
      vrm.scene.updateMatrixWorld(true);
      const top = new THREE.Box3().setFromObject(vrm.scene).max.y;
      entry.ownHeight = top > 0.05 ? top : 0;
      // Its trunk and head, to keep its arms out of them (clearance.js). A model it cannot measure is shown as posed.
      try {
        entry.body = measureBody(vrm);      } catch {
        entry.body = null;
      }      // A VRM 0.x model's rig faces -z inside its scene (rotateVRM0 turns the scene round): the turns are mirrored to it.
      entry.flip = vrm.meta && vrm.meta.metaVersion === '0';
      entry.bones = null;
      entry.error = '';
      scene.add(vrm.scene);
      lastFrame = null;
      changed();
    }, error => {
      entry.loading = false;
      entry.error = entry.file + ' could not be read as a VRM model: ' + (error && error.message || error);
      changed();
    });
  }

  return {
    /** The bytes of a file (or why there are none) have come. Every figure dressed in it is dressed again. */
    setFile(file, data, error) {
      files.set(file, error ? { error } : { data: data.buffer.slice(data.byteOffset, data.byteOffset + data.byteLength) });
      dressed = dressed.map(entry => {
        if (!entry || entry.file !== file) return entry;
        undress(entry);
        const next = { file, height: entry.height };
        load(next);
        return next;
      });
      changed();
    },

    /** The figures of the clip shown, and where each character's file is (`models`: as written -> file). */
    dress(figures, models) {
      dressed = figures.map((figure, i) => {
        const file = figure.model ? models[figure.model] : null;
        // `height 1.5m` on the dancer line: how tall the character is drawn, to the top of its hair (DESIGN.md 115).
        const height = figure.modelHeight > 0 ? figure.modelHeight : 0;
        const was = dressed[i];
        if (was && was.file === file) {
          was.height = height;
          return was;
        }
        undress(was);
        if (!file) return null;
        const entry = { file, height };
        load(entry);
        return entry;
      });
      // Figures the clip no longer has.
      changed();
    },

    clear() {
      dressed.forEach(undress);
      dressed = [];
    },

    /** Whether figure i is shown as its character (the sticks give way to it). */
    isDressed(i) {
      return !!(dressed[i] && dressed[i].vrm);
    },

    setVisible(visible) {
      for (const entry of dressed) {
        if (entry && entry.vrm) entry.vrm.scene.visible = visible;
        if (entry && entry.mic) entry.mic.mesh.visible = visible;
      }
    },

    /** What went wrong, one line per file; '' when nothing did. */
    problems() {
      return [...new Set(dressed.filter(e => e && e.error).map(e => e.error))].join('\n');
    },

    /** Whether any figure of the clip is to be dressed (loaded or not). */
    any() {
      return dressed.some(e => e);
    },

    /** Poses every character at a frame (between two frames, slerped). */
    pose(clip, figures, frame) {
      // The springs and the constraints move on by the time since the last pose; a jump (a seek) settles them anew.
      const dt = lastFrame === null ? 0 : (frame - lastFrame) / clip.fps;
      const jumped = lastFrame === null || dt < 0 || dt > 0.25;
      lastFrame = frame;
      const names = clip.skeletonBones ?? [];
      const pelvis = clip.joints.indexOf('pelvis');
      dressed.forEach((entry, i) => {
        if (!entry || !entry.vrm) return;
        const data = figures[i];
        const turns = data && data.turns, frames = data && data.frames;
        if (!turns || !turns.length || !frames || !frames.length) return;
        const vrm = entry.vrm;
        if (!entry.bones) entry.bones = names.map(name => vrm.humanoid.getNormalizedBoneNode(name));
        const f0 = Math.max(0, Math.min(turns.length - 1, Math.floor(frame))), f1 = Math.min(turns.length - 1, f0 + 1), k = Math.max(0, frame - f0);
        const a = turns[f0], b = turns[f1];
        const s = entry.flip ? -1 : 1;
        entry.bones.forEach((bone, j) => {
          if (!bone) return;   // a bone this character does not have (chest is optional in VRM)
          q0.set(s * a[j * 4], a[j * 4 + 1], s * a[j * 4 + 2], a[j * 4 + 3]);
          q1.set(s * b[j * 4], b[j * 4 + 1], s * b[j * 4 + 2], b[j * 4 + 3]);
          bone.quaternion.slerpQuaternions(q0, q1, k);
        });
        const hips = entry.bones[names.indexOf('hips')];
        const size = entry.height && entry.ownHeight ? entry.height / entry.ownHeight : 1;
        vrm.scene.scale.setScalar(size);
        if (hips && pelvis >= 0) {
          const p = frames[f0], q = frames[Math.min(frames.length - 1, f0 + 1)];
          // The pelvis's way is scaled to the character's legs (its steps are its own length, the feet do not slide),
          // but from where the figure stands at the start of the clip: the dancer is where `at` puts it, in metres,
          // whatever the character's size. The scene is scaled by `size`: the bone's position is in its units.
          const legs = entry.restHips * size / (clip.hipsHeight || 1);
          const first = frames[0];
          const at = j => p[pelvis * 3 + j] + (q[pelvis * 3 + j] - p[pelvis * 3 + j]) * k;
          const ground = j => first[pelvis * 3 + j] + (at(j) - first[pelvis * 3 + j]) * legs;
          hips.position.set(s * ground(0) / size, at(1) * legs / size, s * ground(2) / size);
        }
        // The arms out of the character's own body, which is not the figure's the turns were solved on (DESIGN.md 116).
        // (A test page turns it off with globalThis.shosaArmsAsSolved, to compare.)
        if (entry.body && !globalThis.shosaArmsAsSolved) keepArmsOut(vrm, entry.body, size);
        // Juggling (DESIGN.md 118): the palms up while it throws, so the balls lie on them; for rings an edge, the thumb up.
        const juggled = data.juggle && data.juggle.weight;
        if (juggled && juggled.length) {
          const w0 = juggled[Math.min(juggled.length - 1, f0)], w1 = juggled[Math.min(juggled.length - 1, f0 + 1)];
          palmsUp(vrm, w0 + (w1 - w0) * k, data.juggle.prop === 'rings');
        }
        // A mic in a hand (`mic right`): drawn, the hand closed on it, and at the character's own mouth while it
        // sings (DESIGN.md 117). After the arms are kept out: the hand at the mouth is where it has to be.
        const mic = data.mic;
        if (mic && mic.up && mic.up.length) {
          const u0 = mic.up[Math.min(mic.up.length - 1, f0)], u1 = mic.up[Math.min(mic.up.length - 1, f0 + 1)];
          mics.hold(entry, mic.hand, u0 + (u1 - u0) * k, entry.body ? entry.body.head : null, size);
        } else {
          mics.drop(entry);
        }
        // A figure that sings (`sings melody`): the weights of the five mouth expressions, between two frames
        // blended (DESIGN.md 99). vrm.update below applies them.
        const mouth = data.mouth;
        if (mouth && mouth.length && vrm.expressionManager) {
          const m0 = mouth[Math.min(mouth.length - 1, f0)], m1 = mouth[Math.min(mouth.length - 1, f0 + 1)];
          MOUTH.forEach((name, j) => vrm.expressionManager.setValue(name, m0[j] + (m1[j] - m0[j]) * k));
          entry.sang = true;
        } else if (entry.sang && vrm.expressionManager) {
          MOUTH.forEach(name => vrm.expressionManager.setValue(name, 0));
          entry.sang = false;
        }
        if (jumped && vrm.springBoneManager) {
          vrm.update(0);
          vrm.springBoneManager.reset();
        }
        vrm.update(jumped ? 0 : Math.min(dt, 0.1));
        // Where its palms are, for the balls it juggles (juggle.js): past the wrist along the hand, and up. Right, left.
        if (data.juggle) {
          vrm.scene.updateMatrixWorld(true);
          entry.palms = ['right', 'left'].map(side => {
            const lower = vrm.humanoid.getNormalizedBoneNode(side + 'LowerArm'), hand = vrm.humanoid.getNormalizedBoneNode(side + 'Hand');
            if (!lower || !hand) return null;
            const w = hand.getWorldPosition(new THREE.Vector3()), e = lower.getWorldPosition(new THREE.Vector3());
            const k = e.distanceTo(w) / 0.31;
            return w.clone().add(w.clone().sub(e).normalize().multiplyScalar(0.05 * k)).add(new THREE.Vector3(0, 0.04 * k, 0));
          });
        } else {
          entry.palms = null;
        }
      });
    },

    /** The palms [right, left] of figure i's character as last posed, or null when it is shown as sticks. */
    palms(i) {
      const entry = dressed[i];
      return entry && entry.vrm && entry.vrm.scene.visible && entry.palms ? entry.palms : null;
    },
  };
}
