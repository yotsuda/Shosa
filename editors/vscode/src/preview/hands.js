// A character's fingers (DESIGN.md 126). The figure has no joints for them: a hand's row (`hands { fist }`) says how
// far each finger is closed, the server sends that at every frame (`hands`: for the left hand and then the right,
// the thumb and the four fingers 0..1 and the degrees between two fingers), and the bones are turned from it here
// exactly as Shosa.Core\Motion\Skeleton.cs turns the ones it writes to a file (Finger). How the hand is turned on its
// wrist (`fingers(up)`, `palm(fwd)`) is in the hand's own turn, with the other bones. A hand nobody wrote is relaxed.
import * as THREE from 'three';

const FINGERS = ['Thumb', 'Index', 'Middle', 'Ring', 'Little'];
const JOINTS = [['Metacarpal', 'Proximal', 'Distal'], ['Proximal', 'Intermediate', 'Distal']];
/** Degrees each bone of a finger turns closed into the fist, from the palm out. */
const CURL = [80, 95, 65];
/** The thumb: down under the palm and in toward the fingers at its root, then in at its two joints. */
const THUMB_UNDER = 40, THUMB_IN = 25, THUMB_MIDDLE = 45, THUMB_TIP = 50;
/** How far each finger goes to the thumb's side as the hand spreads, in shares of the degrees between two fingers. */
const SPREAD = [0, 1, 0, -1, -2];
/** The relaxed hand, for both: what `Vocabulary.ShapeOfHand("relaxed")` is. */
export const RELAXED = [30 / 90, 20 / 90, 26 / 90, 32 / 90, 38 / 90, 4, 30 / 90, 20 / 90, 26 / 90, 32 / 90, 38 / 90, 4];

const RAD = Math.PI / 180;
const qa = new THREE.Quaternion(), qb = new THREE.Quaternion();

/** A character's finger bones and its rest frame's axes, found once. */
function rig(entry) {
  if (entry.fingers && entry.fingers.vrm === entry.vrm) return entry.fingers;
  const vrm = entry.vrm;
  // The normalized rig's bones all rest turned as its scene is: the axes of the room, as a bone at rest has them.
  const inverse = vrm.scene.getWorldQuaternion(new THREE.Quaternion()).invert();
  const axis = (x, y, z) => new THREE.Vector3(x, y, z).applyQuaternion(inverse);
  const bones = [];
  for (const side of ['left', 'right']) {
    for (let finger = 0; finger < 5; finger++) {
      for (let joint = 0; joint < 3; joint++) {
        bones.push(vrm.humanoid.getNormalizedBoneNode(side + FINGERS[finger] + JOINTS[finger === 0 ? 0 : 1][joint]));
      }
    }
  }
  entry.fingers = { vrm, bones, x: axis(1, 0, 0), y: axis(0, 1, 0), z: axis(0, 0, 1) };
  return entry.fingers;
}

/**
 * Turns a character's fingers for a frame: `a` and `b` are the frames either side (twelve numbers each, or
 * nothing for the relaxed hand) and `k` how far from the first to the second.
 */
export function poseFingers(entry, a, b, k) {
  const { bones, x, y, z } = rig(entry);
  a = a ?? RELAXED;
  b = b ?? a;
  for (let i = 0; i < 30; i++) {
    const bone = bones[i];
    if (!bone) continue;   // (a character without that bone: fingers are optional in VRM)
    const side = i < 15 ? 1 : -1, finger = Math.floor((i % 15) / 3), joint = i % 3, at = i < 15 ? 0 : 6;
    const closed = Math.max(0, Math.min(1, a[at + finger] + (b[at + finger] - a[at + finger]) * k));
    const spread = a[at + 5] + (b[at + 5] - a[at + 5]) * k;
    if (finger === 0) {
      if (joint === 0) bone.quaternion.copy(qa.setFromAxisAngle(y, side * closed * THUMB_IN * RAD)).multiply(qb.setFromAxisAngle(x, closed * THUMB_UNDER * RAD));
      else bone.quaternion.setFromAxisAngle(y, side * closed * (joint === 1 ? THUMB_MIDDLE : THUMB_TIP) * RAD);
      continue;
    }
    qb.setFromAxisAngle(z, -side * closed * CURL[joint] * RAD);
    // A closed finger lies against its neighbours whatever the hand's spread.
    if (joint === 0) bone.quaternion.copy(qa.setFromAxisAngle(y, -side * SPREAD[finger] * spread * (1 - closed) * RAD)).multiply(qb);
    else bone.quaternion.copy(qb);
  }
}
