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

export function createCharacters(scene, changed) {
  const loader = new GLTFLoader();
  loader.register(parser => new VRMLoaderPlugin(parser));

  const files = new Map();   // file -> { data } or { error }
  let dressed = [];          // per figure of the clip shown: { file, vrm, hips, flip, loading, error } or null
  let lastFrame = null;
  const q0 = new THREE.Quaternion(), q1 = new THREE.Quaternion();

  function undress(entry) {
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
      // A VRM 0.x model's rig faces -z inside its scene (rotateVRM0 turns the scene round): the turns are mirrored to it.
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
        const next = { file };
        load(next);
        return next;
      });
      changed();
    },

    /** The figures of the clip shown, and where each character's file is (`models`: as written -> file). */
    dress(figures, models) {
      dressed = figures.map((figure, i) => {
        const file = figure.model ? models[figure.model] : null;
        const was = dressed[i];
        if (was && was.file === file) return was;
        undress(was);
        if (!file) return null;
        const entry = { file };
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
      for (const entry of dressed) if (entry && entry.vrm) entry.vrm.scene.visible = visible;
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
        if (hips && pelvis >= 0) {
          const p = frames[f0], q = frames[Math.min(frames.length - 1, f0 + 1)];
          const scale = entry.restHips / (clip.hipsHeight || 1);
          const at = j => (p[pelvis * 3 + j] + (q[pelvis * 3 + j] - p[pelvis * 3 + j]) * k) * scale;
          hips.position.set(s * at(0), at(1), s * at(2));
        }
        if (jumped && vrm.springBoneManager) {
          vrm.update(0);
          vrm.springBoneManager.reset();
        }
        vrm.update(jumped ? 0 : Math.min(dt, 0.1));
      });
    },
  };
}
