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
import { millsAngles, millsArms } from './arms.js';
import { elbowSwings, keepArmsOut, measureBody, refresh } from './clearance.js';
import { createMicHands } from './mic.js';
import { clubCatching, gripHands, heldBy, palmAlong, palmsUp, penguinShares, styleShares } from './juggle.js';

// VRM's mouth expressions, in the order the server sends their weights (`mouth`, Mouth.Shapes).
const MOUTH = ['aa', 'ih', 'ou', 'ee', 'oh'];
/** How long the eyes' resting place is eased over while juggling, seconds (the Gaussian's spread). */
const EYES_SOFTEN = 0.3;
/** Over how far above the eyes a place comes to be looked at fully, metres (below the eyes, not at all). */
const EYES_ABOVE = 0.3;
/** How far a club's handle lies from the middle of the hand toward the thumb, metres (on the figure's 0.31 m forearm). */
const CLUB_TOWARD_THUMB = 0.025;
/** Takeouts' gripped ball: how much further toward the fingertips than before, metres (the user: 2 cm). */
const GRIP_FURTHER = 0.02;

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
  function place(entry, clip, data, frame, clear = true, millsFound = null) {
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
    // In Mills' Mess the arms are solved again on the character's own body, its elbows held as the figure's (arms.js).
    // (Test pages: globalThis.shosaNoMillsArms leaves them as the turns put them, shosaNoClearance keeps nothing out after.)
    // The elbows' angles are worked out for the whole clip beforehand (millsOf); `millsFound` is that working out, which
    // takes what each frame finds alone.
    // Takeouts the same way (DESIGN.md 118.49, juggle.follow), but the elbows pointing as the figure's: with the turns alone
    // on a character with shorter arms, the hand holding the swiped ball slowed from 1.6 to 0.7 m/s as it went up past the
    // other arm's elbow, where the figure's went on at 1.5 (the user: the hand and the elbow go past each other smoothly).
    const mills = data.juggle && data.juggle.mills, follow = data.juggle && data.juggle.follow;
    let millsGot = null;
    if (mills && mills.length && entry.body && !globalThis.shosaArmsAsSolved && !globalThis.shosaNoMillsArms) {
      const m0 = mills[Math.min(mills.length - 1, f0)], m1 = mills[Math.min(mills.length - 1, f0 + 1)];
      const o0 = follow?.[Math.min(follow.length - 1, f0)] ?? 0, o1 = follow?.[Math.min(follow.length - 1, f0 + 1)] ?? 0;
      const held = m0 + (m1 - m0) * k, followed = globalThis.shosaNoFollowArms ? 0 : o0 + (o1 - o0) * k;
      const weight = Math.max(held, followed);
      if (weight > 0) {
        const p = frames[f0], q = frames[Math.min(frames.length - 1, f0 + 1)];
        // The windmill's top forearm drawn in (DESIGN.md 118.33) and its wheel's held elbow (118.34), [right, left] at each frame.
        const sides = list => {
          const a0 = list?.[Math.min(list.length - 1, f0)], a1 = list?.[Math.min(list.length - 1, f0 + 1)];
          return a0 && a1 ? { right: a0[0] + (a1[0] - a0[0]) * k, left: a0[1] + (a1[1] - a0[1]) * k } : null;
        };
        millsGot = millsArms(vrm, entry.body, size, name => {
          const j = clip.joints.indexOf(name);
          return new THREE.Vector3(p[j * 3] + (q[j * 3] - p[j * 3]) * k, p[j * 3 + 1] + (q[j * 3 + 1] - p[j * 3 + 1]) * k, p[j * 3 + 2] + (q[j * 3 + 2] - p[j * 3 + 2]) * k);
        }, weight, millsFound ? null : millsAt(entry, data, frame), sides(data.juggle.pull), sides(data.juggle.wheel), followed >= held ? 1 : 0, sides(data.juggle.plane));
      }
    }
    if (millsFound) millsFound.push(millsGot);
    // The elbows' swings are worked out for the whole clip beforehand (swingsOf): found afresh in each frame they flapped.
    if (entry.body && clear && !globalThis.shosaArmsAsSolved && !globalThis.shosaNoClearance) keepArmsOut(vrm, entry.body, size, swingsAt(entry, data, frame));
    // Juggling (DESIGN.md 118): the palms up while it throws, so the balls lie on them; for rings an edge, the thumb up.
    const juggled = data.juggle && data.juggle.weight;
    if (juggled && juggled.length) {
      const w0 = juggled[Math.min(juggled.length - 1, f0)], w1 = juggled[Math.min(juggled.length - 1, f0 + 1)];
      const clubs = data.juggle.prop === 'clubs';
      // At a whole frame, what was worked out for it once (handsOf): the passes over the clip all ask there.
      const told = frame === f0 ? handsOf(clip, data)[f0] : null;
      if (told) palmsUp(vrm, w0 + (w1 - w0) * k, data.juggle.prop === 'rings', told.styles, clubs, told.catching, told.penguin, told.outward);
      else palmsUp(vrm, w0 + (w1 - w0) * k, data.juggle.prop === 'rings', data.juggle.styles ? data.juggle.styles[Math.min(data.juggle.styles.length - 1, Math.round(frame))] : [0, 0], clubs,
        clubs && data.juggle.flights ? clubCatching(data.juggle.flights, frame / clip.fps) : [0, 0], penguinShares(data.juggle.styles, frame, data.juggle.flights, clip.fps),
        styleShares(data.juggle.styles, frame, clip.fps, 5));
    }
    return size;
  }

  /**
   * What a juggler's hands are told at each whole frame besides the rows (palmsUp's and measure's arguments: the style, how
   * much a club's catch, a penguin's, the palm turned out), worked out once per figure: every pass over the clip asks at
   * the same frames, and after an edit they say in which frames the hands are told otherwise (DESIGN.md 120.15).
   */
  const handsCache = new WeakMap();
  function handsOf(clip, data) {
    const got = handsCache.get(data);
    if (got && got.fps === clip.fps) return got.at;
    const juggle = data.juggle, clubs = juggle.prop === 'clubs', at = [];
    const count = Math.max(data.turns.length, data.frames.length);
    for (let f = 0; f < count; f++) {
      at.push({
        styles: juggle.styles ? juggle.styles[Math.min(juggle.styles.length - 1, f)] : [0, 0],
        catching: clubs && juggle.flights ? clubCatching(juggle.flights, f / clip.fps) : [0, 0],
        penguin: penguinShares(juggle.styles, f, juggle.flights, clip.fps),
        outward: styleShares(juggle.styles, f, clip.fps, 5),
      });
    }
    handsCache.set(data, { fps: clip.fps, at });
    return at;
  }

  /** The elbows' swings for a clip's figure at every frame, worked out once per movement (clearance.js elbowSwings). */
  function swingsOf(entry, clip, data) {
    const size = entry.height && entry.ownHeight ? entry.height / entry.ownHeight : 1;
    if (entry.swings?.data === data && entry.swings.size === size) return entry.swings;
    const count = data.turns.length;
    // After an edit, only the frames it moved (DESIGN.md 120).
    // (A test page works every frame out again with globalThis.shosaSwingsWhole, to compare.)
    const was = entry.swings && entry.swings.size === size && entry.swings.vrm === entry.vrm && !globalThis.shosaSwingsWhole ? entry.swings : null;
    // A juggler's arms are solved again first (millsOf): a frame whose elbow came out at another angle is posed otherwise too.
    const dirty = millsOf(entry, clip, data, size, was, was ? movedFrames(clip, data, was.clip, was.data) : null);
    const swings = elbowSwings(entry.vrm, entry.body, size, count, f => place(entry, clip, data, f, false), was, dirty);
    // Where the arms are otherwise than before the edit, kept out and all (`place` at a whole frame): a juggler's palms
    // are measured again there (trackPalms). Null when every frame may be; `from` is the figure before the edit.
    let moved = null;
    if (dirty) {
      moved = dirty.slice();
      for (let f = 0; f < count; f++) {
        if (moved[f]) continue;   // (and the frames past the old end, which have nothing to compare with)
        if (swings.left[f] !== was.left[f] || swings.right[f] !== was.right[f] || !swings.out.left[f].equals(was.out.left[f]) || !swings.out.right[f].equals(was.out.right[f])) moved[f] = 1;
      }
    }
    entry.swings = { data, clip, size, vrm: entry.vrm, ...swings, moved, from: dirty ? was.data : null };
    return entry.swings;
  }

  /**
   * Which frames of a figure are posed otherwise than those of the figure it was before an edit (`place` at a whole
   * frame, before the arms are solved again or kept out: the bones' turns and where the pelvis is; for a juggler the
   * figure's joints too, and what the frame says of the juggling), 1 for each; null when every frame may be, so nothing
   * of the old one can be kept: another skeleton, another start (the hips are placed from the first frame), a juggler
   * where there was none. A figure with more frames than it had (a bar added at the end) is posed otherwise in those.
   */
  function movedFrames(clip, data, wasClip, wasData) {
    const turns = data.turns, frames = data.frames, wasTurns = wasData.turns, wasFrames = wasData.frames;
    const count = turns.length, had = wasTurns.length;
    if (!data.juggle !== !wasData.juggle || frames.length < count || wasFrames.length < had || !count || !had) return null;
    if (clip.hipsHeight !== wasClip.hipsHeight || (clip.skeletonBones ?? []).join() !== (wasClip.skeletonBones ?? []).join()) return null;
    const pelvis = clip.joints.indexOf('pelvis') * 3;
    if (pelvis < 0 || pelvis !== wasClip.joints.indexOf('pelvis') * 3) return null;
    const same = (a, b, from, to) => {
      for (let i = from; i < to; i++) if (a[i] !== b[i]) return false;
      return true;
    };
    if (!same(frames[0], wasFrames[0], pelvis, pelvis + 3)) return null;
    const juggled = data.juggle ? juggledOtherwise(clip, data, wasClip, wasData) : null;
    if (data.juggle && !juggled) return null;
    const dirty = new Uint8Array(count);
    for (let f = 0; f < count; f++) {
      if (f >= had) {
        dirty[f] = 1;
        continue;
      }
      // (A row that is the same array is a row a patch left as it was.)
      const a = turns[f], b = wasTurns[f], p = frames[f], q = wasFrames[f];
      // A juggler's arms are solved again from where the figure's shoulders, elbows and hands are (arms.js): the whole row.
      const joints = juggled ? p.length === q.length && same(p, q, 0, p.length) : same(p, q, pelvis, pelvis + 3);
      if ((a !== b && (a.length !== b.length || !same(a, b, 0, a.length))) || (p !== q && !joints) || (juggled && juggled(f))) dirty[f] = 1;
    }
    return dirty;
  }

  /**
   * For a juggler after an edit: `f => whether what the frame says of the juggling is otherwise than it was` (what `place`
   * and `measure` read at a whole frame: how much Mills' Mess or takeouts have the arms, the forearm drawn in, the elbow
   * held, the plane, the palms' weight, and what the hands are told, handsOf), for the frames both have. Null when every
   * frame may be: another prop, other joints, a list that is not one entry per frame.
   */
  function juggledOtherwise(clip, data, wasClip, wasData) {
    const now = data.juggle, was = wasData.juggle;
    const count = data.turns.length, had = wasData.turns.length;
    if (now.prop !== was.prop || clip.fps !== wasClip.fps || clip.joints.join() !== wasClip.joints.join() || data.frames.length !== count || wasData.frames.length !== had) return null;
    const amounts = ['mills', 'follow', 'weight'], sides = ['pull', 'wheel', 'plane'];
    // (A list is read at the frame, or at its last entry past its end: the same frame of both only when both go to the end.)
    for (const key of [...amounts, ...sides, 'styles']) {
      const a = now[key]?.length ?? -1, b = was[key]?.length ?? -1;
      if (!(a === b && a <= 0) && !(a === count && b === had)) return null;
    }
    const hands = handsOf(clip, data), wasHands = handsOf(wasClip, wasData);
    const at = (list, f) => list && list.length ? list[Math.min(list.length - 1, f)] : undefined;
    const pair = (a, b) => a === b || (!!a && !!b && a[0] === b[0] && a[1] === b[1]);
    return f => {
      for (const key of amounts) if (at(now[key], f) !== at(was[key], f)) return true;
      // (`place` takes a pair only when the next frame has one too.)
      for (const key of sides) if (!pair(at(now[key], f), at(was[key], f)) || !at(now[key], f + 1) !== !at(was[key], f + 1)) return true;
      const a = hands[f], b = wasHands[f];
      return !(pair(a.styles, b.styles) && pair(a.catching, b.catching) && pair(a.penguin, b.penguin) && pair(a.penguin.relax, b.penguin.relax) && pair(a.outward, b.outward));
    };
  }

  /**
   * Mills' elbow angles for a clip's figure at every frame, worked out once per movement (arms.js millsAngles).
   *
   * After an edit (`was`: the swings before it; `dirty[f]`: 1 where frame f is posed otherwise, movedFrames) an elbow's
   * place is found again only in those frames: what a frame finds alone is the frame's own. The smoothing is done whole
   * (arithmetic). Returns `dirty` with the frames whose smoothed angle came out otherwise, or null when every frame was
   * worked out.
   */
  function millsOf(entry, clip, data, size, was = null, dirty = null) {
    const before = was && dirty && entry.mills && entry.mills.data === was.data && entry.mills.size === size ? entry.mills : null;
    if (!before) dirty = null;
    const count = data.turns.length;
    // (`found` null: neither Mills' Mess nor takeouts has the arms anywhere.)
    entry.mills = { data, size, found: null, left: null, right: null };
    if (data.juggle && (data.juggle.mills?.some(w => w > 0) || data.juggle.follow?.some(w => w > 0))) {
      const found = [];
      for (let f = 0; f < count; f++) {
        if (dirty && !dirty[f]) found.push(before.found ? before.found[f] : null);
        else place(entry, clip, data, f, false, found);
      }
      Object.assign(entry.mills, { found }, millsAngles(count, f => found[f]));
    }
    if (!dirty) return null;
    const moved = dirty.slice();
    const other = (a, b, f) => !Object.is(a ? a[f] : NaN, b ? b[f] : NaN);
    for (let f = 0; f < count; f++) if (other(entry.mills.left, before.left, f) || other(entry.mills.right, before.right, f)) moved[f] = 1;
    return moved;
  }

  /** Mills' elbow angles at a frame (between two frames, blended), or null to find them in the frame itself. */
  function millsAt(entry, data, frame) {
    const mills = entry.mills?.data === data && entry.mills.left ? entry.mills : null;
    if (!mills || globalThis.shosaMillsEachFrame) return null;   // a test page compares with the angles found frame by frame
    const count = mills.left.length, x = Math.max(0, Math.min(count - 1, frame)), f = Math.floor(x), g = Math.min(count - 1, f + 1), k = x - f;
    const at = list => Number.isFinite(list[f]) && Number.isFinite(list[g]) ? list[f] + (list[g] - list[f]) * k : Number.isFinite(list[f]) ? list[f] : list[g];
    return { left: at(mills.left), right: at(mills.right) };
  }

  /** The elbows' swings at a frame (between two frames, blended), or null to find them in the frame itself. */
  function swingsAt(entry, data, frame) {
    const swings = entry.swings?.data === data ? entry.swings : null;
    if (!swings || globalThis.shosaSwingsEachFrame) return null;   // a test page compares with the swings found frame by frame
    const count = swings.left.length, x = Math.max(0, Math.min(count - 1, frame)), f = Math.floor(x), g = Math.min(count - 1, f + 1), k = x - f;
    const out = swings.out ? {
      left: swings.out.left[f].clone().slerp(swings.out.left[g], k),
      right: swings.out.right[f].clone().slerp(swings.out.right[g], k),
    } : null;
    return { left: swings.left[f] + (swings.left[g] - swings.left[f]) * k, right: swings.right[f] + (swings.right[g] - swings.right[f]) * k, out };
  }

  /** A posed character's palms (past the wrist along the hand toward the fingers, and up) and forearms, [right, left]. */
  function measure(vrm, prop, styles = [0, 0], shares = null, outs = null) {
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
      // From the hand as it is turned: along its fingers, out of its palm, toward its thumb (as mic.js finds the fist).
      const isLeft = side === 'left';
      const rest = vrm.scene.getWorldQuaternion(new THREE.Quaternion()).invert(), turned = hand.getWorldQuaternion(new THREE.Quaternion());
      const fingers = new THREE.Vector3(isLeft ? 1 : -1, 0, 0).applyQuaternion(rest).applyQuaternion(turned);
      const thumb = new THREE.Vector3(0, 0, 1).applyQuaternion(rest).applyQuaternion(turned);
      const palm = fingers.clone().cross(thumb).multiplyScalar(isLeft ? 1 : -1);
      const style = styles?.[palms.length] | 0;
      // A club is in the fist (the user: gripped). The palms of a hand whose wrist is bent are not on the line of the forearm.
      if (prop === 'clubs' && style === 0) {
        // A club's handle between the thumb and the first finger (the user: through the middle of the hand it went between
        // the first and the middle finger): toward the thumb by about a finger and a half.
        palms.push(w.clone().addScaledVector(fingers, 0.06 * k).addScaledVector(palm, 0.03 * k).addScaledVector(thumb, CLUB_TOWARD_THUMB * k));
        forearms.push(along);
        continue;
      }
      // A penguin's ball is in the palm turned back and out, as much as the catch is a penguin's (its share, smoothed over a
      // few frames, so the ball goes from the one place to the other as the hand turns).
      const share = shares ? shares[palms.length] : (style === 2 ? 1 : 0);
      const inPenguin = w.clone().addScaledVector(fingers, 0.07 * k).addScaledVector(palm, 0.03 * k);
      // A claw's palm is down: the ball under it (DESIGN.md 118.12).
      const up = style === 1 ? -0.05 : 0.04;
      const onPalm = w.clone().addScaledVector(along, palmAlong(prop) * k).add(new THREE.Vector3(0, up * k, 0));
      // Turned out (takeouts' swipe and round): the ball against the palm, as much as the hand is turned so; and 2 cm further
      // toward the fingertips on the line from the wrist to the middle finger (the user, DESIGN.md 118.49).
      const middle = vrm.humanoid.getNormalizedBoneNode(side + 'MiddleProximal');
      const toMiddle = middle ? middle.getWorldPosition(new THREE.Vector3()).sub(w) : null;
      const gripWay = toMiddle && toMiddle.lengthSq() > 1e-8 ? toMiddle.normalize() : fingers;
      const inPalm = w.clone().addScaledVector(fingers, 0.07 * k).addScaledVector(palm, 0.04 * k).addScaledVector(gripWay, GRIP_FURTHER);
      palms.push(onPalm.lerp(inPenguin, share).lerp(inPalm, outs ? outs[palms.length] : (style === 5 ? 1 : 0)));
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
  /**
   * Where the eyes rest while juggling at a time (seconds): the top of the highest way of the balls in the air (on the
   * parabola juggle.js flies them on, from the character's palm at the throw to its palm at the catch), not the ball
   * itself (the user: following each ball, the eyes rolled about too much). Eased over a little time either side, so
   * they glide from one top to the next. Only a top above the eyes (`eyeY`, world): one below it is not followed side to
   * side either, the eyes kept ahead (the user: a penguin's low throws looked wrong). Null when no such top is up.
   */
  function highestTop(track, flights, sec, eyeY) {
    flights = flights || [];
    const gravityOf = i => track.millsGravity?.get(i) ?? 9.81;
    // A flight's ball at a time, and the top of its way and when.
    const ballAt = (i, at) => {
      const [, from, to, thrown, caught] = flights[i];
      const tf = Math.max(1e-6, caught - thrown), s = Math.max(0, Math.min(tf, at - thrown));
      const p = track.palm(from, thrown).lerp(track.palm(to, caught), s / tf);
      p.y += gravityOf(i) / 2 * s * (tf - s);
      return p;
    };
    const topTime = i => {
      const [, from, to, thrown, caught] = flights[i];
      const tf = Math.max(1e-6, caught - thrown);
      const rise = track.palm(to, caught).y - track.palm(from, thrown).y;
      return thrown + Math.max(0, Math.min(tf, tf / 2 + rise / (gravityOf(i) * tf)));
    };
    const topAt = at => {
      let best = null;
      flights.forEach((fl, i) => {
        if (at < fl[3] || at > fl[4]) return;
        const p = ballAt(i, topTime(i));
        if (!best || p.y > best.y) best = p;
      });
      return best;
    };
    // The run of throws round now (while some ball is up): its first and its last flight.
    const run = runsOf(flights).find(r => sec >= r.start && sec <= r.end);
    if (!run) return null;
    const firstTop = topTime(run.first), lastTop = topTime(run.last);
    // Where the eyes would be at a time: the first ball itself as it goes up to its top (the user: the eyes went up at
    // another time, not with the ball), the last ball itself from its top down to the catch (followed down), and between,
    // the top of the highest way.
    const rawAt = at => {
      if (at < run.start || at > run.end) return null;
      if (at < firstTop) return ballAt(run.first, at);
      if (at > lastTop) return ballAt(run.last, at);
      return topAt(at);
    };
    // Eased over less time toward the run's ends, none at them: so the first ball is seen as it is, and nothing after the
    // last catch or before the first throw is looked at ahead of time.
    const soften = Math.min(EYES_SOFTEN, (sec - run.start) / 2.5, (run.end - sec) / 2.5);
    // A place below the eyes is not looked at (left and right neither): its share of the eyes eases in over EYES_ABOVE.
    const up = p => { const x = Math.max(0, Math.min(1, (p.y - eyeY) / EYES_ABOVE)); return x * x * (3 - 2 * x); };
    const sum = new THREE.Vector3();
    let total = 0, all = 0;
    const add = (at, k) => {
      all += k;
      const p = rawAt(at);
      if (!p) return;
      const w = k * up(p);
      sum.addScaledVector(p, w);
      total += w;
    };
    // On a fixed grid of times, weighted by how far each is from now (sampled round now instead, the highest top changing
    // between samples, the place hopped to and fro 1.5 cm from frame to frame).
    const step = 1 / 60;
    if (soften < 2 * step) add(sec, 1);
    else
      for (let n = Math.ceil((sec - 2.5 * soften) / step); n * step <= sec + 2.5 * soften; n++)
        add(n * step, Math.exp(-0.5 * ((n * step - sec) / soften) ** 2));
    // How much the eyes are on the balls rather than ahead.
    return total > 0 ? { at: sum.multiplyScalar(1 / total), share: total / all } : null;
  }

  /** The runs of throws in a juggle's flights: the times some ball is up, each with its first and its last flight (indices). */
  const runsCache = new WeakMap();
  function runsOf(flights) {
    if (runsCache.has(flights)) return runsCache.get(flights);
    const order = flights.map((fl, i) => i).sort((a, b) => flights[a][3] - flights[b][3]);
    const runs = [];
    for (const i of order) {
      const [, , , thrown, caught] = flights[i];
      const last = runs[runs.length - 1];
      if (last && thrown <= last.end + 0.02) {
        if (caught > last.end) { last.end = caught; last.last = i; }
      } else {
        runs.push({ start: thrown, end: caught, first: i, last: i });
      }
    }
    runsCache.set(flights, runs);
    return runs;
  }

  /**
   * After an edit only the frames it moved are posed and measured again (DESIGN.md 120.15): `was` is the track of the
   * figure before the edit (the same character and size) and `moved[f]` is 1 where frame f is posed otherwise than it was
   * (swingsOf). The points of the other frames are the same objects as before (nothing changes them: they are read copied).
   */
  function trackPalms(entry, clip, data, was = null, moved = null) {
    const count = data.frames.length;
    if (!was || !moved || moved.length !== count || was.fps !== clip.fps) was = moved = null;
    const at = { data, fps: clip.fps, size: entry.height && entry.ownHeight ? entry.height / entry.ownHeight : 1, vrm: entry.vrm, palms: [[], []], forearms: [[], []], hips: [] };
    const hipsBone = entry.vrm.humanoid.getNormalizedBoneNode('hips');
    const hands = handsOf(clip, data);
    for (let f = 0; f < count; f++) {
      if (was && !moved[f]) {
        at.hips.push(was.hips[f]);
        for (const hand of [0, 1]) {
          at.palms[hand].push(was.palms[hand][f]);
          at.forearms[hand].push(was.forearms[hand][f]);
        }
        continue;
      }
      place(entry, clip, data, f);
      refresh(entry.vrm);
      const { palms, forearms } = measure(entry.vrm, data.juggle.prop, data.juggle.styles?.[f], hands[f].penguin, hands[f].outward);
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

  // Test pages read where a character's joints are (out\pv\harness-elbows.html); nothing in the extension does.
  globalThis.shosaPreviewCharacters = {
    /** The elbows' swings of figure i's character as last worked out (harness-perf.html), or null. */
    swings(i) {
      return dressed[i] ? dressed[i].swings ?? null : null;
    },
    /** A juggler's palms at every frame and its elbows' angles as last worked out (harness-perf.html), or null. */
    tracked(i) {
      return dressed[i] ? dressed[i].track ?? null : null;
    },
    mills(i) {
      return dressed[i] ? dressed[i].mills ?? null : null;
    },
    joint(i, name) {
      const entry = dressed[i];
      const bone = entry && entry.vrm && entry.vrm.humanoid.getNormalizedBoneNode(name);
      return bone ? bone.getWorldPosition(new THREE.Vector3()).toArray() : null;
    },
    /** Where the eyes look, [yaw, pitch] in degrees, or null. */
    look(i) {
      const entry = dressed[i];
      return entry && entry.vrm && entry.vrm.lookAt ? [+entry.vrm.lookAt.yaw.toFixed(2), +entry.vrm.lookAt.pitch.toFixed(2), ...(entry.lookTarget ? entry.lookTarget.toArray().map(v => +v.toFixed(3)) : [])] : null;
    },
  };

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
        // The elbows' swings round the body at every frame, then a juggler's palms (posed with them): the balls fly
        // between those (juggle.js).
        const swings = entry.body && !globalThis.shosaArmsAsSolved ? entry.swings : null;
        let tracked = null;
        if (entry.body && !globalThis.shosaArmsAsSolved && swingsOf(entry, clip, data) !== swings) {   // the palms move with the elbows
          tracked = entry.track;
          entry.track = null;
        }
        if (data.juggle && entry.track?.data !== data) {
          // After an edit, from the track before it, where the swings say the arms are as they were.
          const now = entry.swings?.data === data ? entry.swings : null;
          const keep = tracked && now && now.moved && tracked.data === now.from && tracked.size === now.size && tracked.vrm === entry.vrm;
          entry.track = trackPalms(entry, clip, data, keep ? tracked : null, keep ? now.moved : null);
        }
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
        // Juggling: the eyes on the top of the highest way in the air (the user; the head tips back for a very high one in
        // the figure itself), turned from and back to ahead as balls go up and come down. vrm.update below turns the eyes.
        if (vrm.lookAt) {
          let target = null;
          if (data.juggle && entry.track) {
            vrm.humanoid.update();
            vrm.scene.updateMatrixWorld(true);
            target = highestTop(entry.track, data.juggle.flights, frame / clip.fps, vrm.lookAt.getLookAtWorldPosition(new THREE.Vector3()).y);
          }
          if (target) {
            vrm.lookAt.lookAt(target.at);
            vrm.lookAt.yaw *= target.share;
            // Never below ahead (the user). three-vrm's pitch is down positive.
            vrm.lookAt.pitch = Math.min(0, vrm.lookAt.pitch) * target.share;
            entry.lookTarget = target.at;
            entry.looking = true;
          } else if (entry.looking) {
            vrm.lookAt.reset();
            entry.looking = false;
          }
        }
        if (jumped && vrm.springBoneManager) {
          vrm.update(0);
          vrm.springBoneManager.reset();
        }
        vrm.update(jumped ? 0 : Math.min(dt, 0.1));
        // Where its palms are, for the balls it juggles (juggle.js). Right, left.
        if (data.juggle) {
          vrm.scene.updateMatrixWorld(true);
          ({ palms: entry.palms, forearms: entry.forearms } = measure(vrm, data.juggle.prop, data.juggle.styles?.[Math.min(data.juggle.styles.length - 1, Math.round(frame))], penguinShares(data.juggle.styles, frame, data.juggle.flights, clip.fps), styleShares(data.juggle.styles, frame, clip.fps, 5)));
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
