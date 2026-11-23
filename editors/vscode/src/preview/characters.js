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
import { gripHands, heldBy, palmAlong, palmsUp } from './juggle.js';

// VRM's mouth expressions, in the order the server sends their weights (`mouth`, Mouth.Shapes).
const MOUTH = ['aa', 'ih', 'ou', 'ee', 'oh'];

export function createCharacters(scene, changed) {
  const loader = new GLTFLoader();
  loader.register(parser => new VRMLoaderPlugin(parser));

  const files = new Map();   // file -> { data } or { error }
  let dressed = [];          // per figure of the clip shown: { file, vrm, hips, flip, loading, error } or null
  let lastFrame = null;
  const q0 = new THREE.Quaternion(), q1 = new THREE.Quaternion(), v = new THREE.Vector3();
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
      // From the head bone to the top of the hair, at rest: where the top of the character is, wherever its head goes (bounds).
      const headBone = vrm.humanoid.getRawBoneNode('head');
      entry.headRoom = headBone && top > 0.05 ? Math.max(0, top - headBone.getWorldPosition(new THREE.Vector3()).y) : 0.15;
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

  /**
   * Poses a character's bones for a frame, as far as nothing but the frame decides it (the turns, the hips, the arms kept
   * out of its body, a juggler's palms): what its springs and expressions do is left to the caller. Returns its scale.
   */
  function place(entry, clip, data, frame) {
    const vrm = entry.vrm, turns = data.turns, frames = data.frames;
    const names = clip.skeletonBones ?? [];
    const pelvis = clip.joints.indexOf('pelvis');
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
      palmsUp(vrm, w0 + (w1 - w0) * k, data.juggle.prop === 'rings', data.juggle.styles ? data.juggle.styles[Math.min(data.juggle.styles.length - 1, Math.round(frame))] : [0, 0], data.juggle.prop === 'clubs');
    }
    return size;
  }

  /** A posed character's palms (past the wrist along the hand toward the fingers, and up) and forearms, [right, left]. */
  function measure(vrm, prop, styles = [0, 0]) {
    const palms = [], forearms = [];
    for (const side of ['right', 'left']) {
      const lower = vrm.humanoid.getNormalizedBoneNode(side + 'LowerArm'), hand = vrm.humanoid.getNormalizedBoneNode(side + 'Hand');
      if (!lower || !hand) {
        palms.push(null);
        forearms.push(null);
        continue;
      }
      const w = hand.getWorldPosition(new THREE.Vector3()), e = lower.getWorldPosition(new THREE.Vector3());
      const k = e.distanceTo(w) / 0.31;
      const along = w.clone().sub(e).normalize();
      // A club is in the fist (the user: gripped): from the hand as it is turned, along its fingers and out of its palm,
      // as mic.js finds the fist. The palms of a hand whose wrist is bent are not on the line of the forearm.
      const penguin = (styles?.[palms.length] | 0) === 2;
      if ((prop === 'clubs' && (styles?.[palms.length] | 0) === 0) || penguin) {
        const rest = vrm.scene.getWorldQuaternion(new THREE.Quaternion()).invert(), turned = hand.getWorldQuaternion(new THREE.Quaternion());
        const isLeft = side === 'left';
        const fingers = new THREE.Vector3(isLeft ? 1 : -1, 0, 0).applyQuaternion(rest).applyQuaternion(turned);
        const thumb = new THREE.Vector3(0, 0, 1).applyQuaternion(rest).applyQuaternion(turned);
        const palm = fingers.clone().cross(thumb).multiplyScalar(isLeft ? 1 : -1);
        palms.push(w.clone().addScaledVector(fingers, (penguin ? 0.07 : 0.06) * k).addScaledVector(palm, 0.03 * k));
        forearms.push(along);
        continue;
      }
      // A claw's palm is down: the ball under it (DESIGN.md 118.12).
      const up = (styles?.[palms.length] | 0) === 1 ? -0.05 : 0.04;
      palms.push(w.clone().addScaledVector(along, palmAlong(prop) * k).add(new THREE.Vector3(0, up * k, 0)));
      forearms.push(along);
    }
    return { palms, forearms };
  }

  /**
   * A juggling character's palms and forearms at every frame of the clip, posed one frame after another (only what the
   * frame decides: no springs). Its hands are not the figure's (a child's short arms move less), so the balls are drawn
   * between these: from the palm at the throw to the palm at the catch, on a parabola (the user: with the figure's arc
   * moved by the difference of the hands, a ball flew where no ball can).
   */
  function trackPalms(entry, clip, data) {
    const count = data.frames.length;
    const at = { data, fps: clip.fps, palms: [[], []], forearms: [[], []], hips: [] };
    const hipsBone = entry.vrm.humanoid.getNormalizedBoneNode('hips');
    for (let f = 0; f < count; f++) {
      place(entry, clip, data, f);
      entry.vrm.scene.updateMatrixWorld(true);
      const { palms, forearms } = measure(entry.vrm, data.juggle.prop, data.juggle.styles?.[f]);
      at.hips.push(hipsBone ? hipsBone.getWorldPosition(new THREE.Vector3()) : new THREE.Vector3());
      for (const hand of [0, 1]) {
        at.palms[hand].push(palms[hand] ?? new THREE.Vector3());
        at.forearms[hand].push(forearms[hand] ?? new THREE.Vector3(0, 0, 1));
      }
    }
    /** A hand's palm (or forearm) at a time, seconds, between two frames. */
    const between = (list, hand, sec) => {
      const x = Math.max(0, Math.min(count - 1, sec * at.fps)), f = Math.floor(x), g = Math.min(count - 1, f + 1);
      return list[hand][f].clone().lerp(list[hand][g], x - f);
    };
    at.palm = (hand, sec) => between(at.palms, hand, sec);
    at.forearm = (hand, sec) => between(at.forearms, hand, sec).normalize();
    at.hipsAt = sec => { const x = Math.max(0, Math.min(count - 1, sec * at.fps)), f = Math.floor(x), g = Math.min(count - 1, f + 1); return at.hips[f].clone().lerp(at.hips[g], x - f); };
    return at;
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
      dressed.forEach((entry, i) => {
        if (!entry || !entry.vrm) return;
        const data = figures[i];
        const turns = data && data.turns, frames = data && data.frames;
        if (!turns || !turns.length || !frames || !frames.length) return;
        const vrm = entry.vrm;
        // A juggler's palms at every frame, before this one is posed: the balls fly between them (juggle.js).
        if (data.juggle && entry.track?.data !== data) entry.track = trackPalms(entry, clip, data);
        const f0 = Math.max(0, Math.min(turns.length - 1, Math.floor(frame))), f1 = Math.min(turns.length - 1, f0 + 1), k = Math.max(0, frame - f0);
        const size = place(entry, clip, data, frame);
        // A hand holding a ball or a ring is a fist; an empty one opens to catch (the user).
        if (data.juggle && data.juggle.frames && data.juggle.frames.length) {
          gripHands(vrm, heldBy(data.juggle, frame));
          entry.gripped = true;
        } else if (entry.gripped) {
          gripHands(vrm, [0, 0]);
          entry.gripped = false;
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
        // Where its palms are, for the balls it juggles (juggle.js). Right, left.
        if (data.juggle) {
          vrm.scene.updateMatrixWorld(true);
          ({ palms: entry.palms, forearms: entry.forearms } = measure(vrm, data.juggle.prop, data.juggle.styles?.[Math.min(data.juggle.styles.length - 1, Math.round(frame))]));
        } else {
          entry.palms = null;
          entry.forearms = null;
        }
      });
    },

    /**
     * Figure i's character's palms at every frame, [right, left] (null when it is not shown as its character): the balls
     * fly from the palm at the throw to the palm at the catch (juggle.js).
     */
    track(i) {
      const entry = dressed[i];
      return entry && entry.vrm && entry.vrm.scene.visible && entry.track ? entry.track : null;
    },

    /** The forearms [right, left] of figure i's character (elbow to wrist, of length 1) as last posed, or null. */
    forearms(i) {
      const entry = dressed[i];
      return entry && entry.vrm && entry.vrm.scene.visible && entry.forearms ? entry.forearms : null;
    },

    /**
     * Every character shown, as last posed, as balls `add(point, radius)` (the camera's Fit): its bones, with room for
     * the body round them, and the top of its hair above its head.
     */
    points(add) {
      for (const entry of dressed) {
        if (!entry || !entry.vrm || !entry.vrm.scene.visible) continue;
        const vrm = entry.vrm, size = vrm.scene.scale.y;
        vrm.scene.updateMatrixWorld(true);
        for (const name of Object.keys(vrm.humanoid.humanBones)) {
          const node = vrm.humanoid.getRawBoneNode(name);
          if (node) add(node.getWorldPosition(v), 0.06 * size);
        }
        const head = vrm.humanoid.getRawBoneNode('head');
        if (head) {
          head.getWorldPosition(v);
          v.y += (entry.headRoom ?? 0.15) * size;
          add(v, 0.04 * size);
        }
      }
    },

    /** The palms [right, left] of figure i's character as last posed, or null when it is shown as sticks. */
    palms(i) {
      const entry = dressed[i];
      return entry && entry.vrm && entry.vrm.scene.visible && entry.palms ? entry.palms : null;
    },
  };
}
