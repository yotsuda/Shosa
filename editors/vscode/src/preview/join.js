// A hand in another dancer's hand (DESIGN.md 134). A word sends an arm to another dancer's hand (`b.hand`,
// `b.rightHand`, `neighbor.hand`); the solver brings the two figures' hands to one place, and the preview puts the arms'
// turns on the characters, whose arms are not the figures' (a child's are short, and a character with short arms
// beside one with long ones held nothing: the hands hung apart). The clip says how much each hand is at whose hand at
// every frame (`join`, Solver.Joins); here, by that much, the two characters' wrists are reached to one place between
// where the turns left them — or, when only one of the two reaches, that one's to the other's hand where it is.
// Only the preview's characters change: the movement and what is exported stay as solved.
import * as THREE from 'three';
import { reach } from './mic.js';

/** How far short of the place two hands share each wrist stays, as a share of its own arm: the palms are there, not the wrists. */
const SHORT = 0.07;

/** How much a join has its hand there at a frame (between two frames, blended). */
function weightAt(weight, frame) {
  if (!weight || !weight.length) return 0;
  const last = weight.length - 1, f0 = Math.max(0, Math.min(last, Math.floor(frame))), f1 = Math.min(last, f0 + 1);
  return weight[f0] + (weight[f1] - weight[f0]) * Math.max(0, frame - f0);
}

/** An arm of a character: its bones and where its joints are in the world, or null when the character has not all of them. */
function armOf(vrm, side) {
  const node = name => vrm.humanoid.getNormalizedBoneNode(name);
  const upper = node(side + 'UpperArm'), lower = node(side + 'LowerArm'), hand = node(side + 'Hand');
  if (!upper || !lower || !hand) return null;
  const s = upper.getWorldPosition(new THREE.Vector3()), e = lower.getWorldPosition(new THREE.Vector3()), w = hand.getWorldPosition(new THREE.Vector3());
  return { upper, lower, hand, s, e, w, length: s.distanceTo(e) + e.distanceTo(w) };
}

/** Reaches an arm's wrist toward `place` by `share`, a little short of it, the elbow pointing as it does. */
function bring(arm, place, share) {
  const back = arm.s.clone().sub(place);
  const wrist = back.lengthSq() > 1e-10 ? place.clone().addScaledVector(back.normalize(), SHORT * arm.length) : place.clone();
  // The hand turned in the world as it was: the forearm's new way is not the palm's.
  const handWorld = arm.hand.getWorldQuaternion(new THREE.Quaternion());
  reach(arm.upper, arm.lower, arm.hand, wrist, arm.e.clone().sub(arm.s), share);
  arm.hand.quaternion.copy(arm.hand.parent.getWorldQuaternion(new THREE.Quaternion()).invert().multiply(handWorld));
  arm.hand.updateMatrixWorld(true);
}

/** How far from its shoulder an arm's shared place may be, as a share of the arm: nearly straight, and the wrist SHORT of the place. */
const REACH = 0.97 + SHORT;

/** The place nearest `place` that both arms reach (each within REACH of its shoulder); if they cannot both, between the nearest each gets. */
function within(place, a, b) {
  const pull = (p, arm) => {
    const from = p.clone().sub(arm.s), most = arm.length * REACH;
    return from.length() > most ? arm.s.clone().addScaledVector(from.normalize(), most) : p;
  };
  let p = place;
  for (let i = 0; i < 12; i++) {
    const next = pull(pull(p, a), b);
    if (next.distanceToSquared(p) < 1e-8) break;
    p = next;
  }
  // (After the last pull it is within b's reach; if a's is still short of it, the two do not reach one place.)
  const back = pull(p, a);
  return back === p ? p : p.clone().lerp(back, 0.5);
}

/**
 * Brings together the hands the clip's figures join at a frame. `entries[i]` is figure i's character ({ vrm }), null
 * or without a vrm when it is drawn as sticks. Call when every character's bones are posed (and the arms kept out of
 * the bodies, the mic and the head's places done), before the characters are updated.
 */
export function joinHands(entries, figures, frame) {
  let wants = null;
  figures.forEach((data, i) => {
    if (!data || !data.join || !entries[i] || !entries[i].vrm) return;
    for (const join of data.join) {
      const w = weightAt(join.weight, frame), j = figures.findIndex(f => f && f.id === join.with);
      if (w > 0 && j >= 0 && j !== i && entries[j] && entries[j].vrm) (wants ??= []).push({ i, side: join.hand, j, their: join.their, w });
    }
  });
  if (!wants) return;
  for (const entry of entries) if (entry && entry.vrm) entry.vrm.scene.updateMatrixWorld(true);
  const done = new Set();
  for (const want of wants) {
    if (done.has(want)) continue;
    const back = wants.find(o => o !== want && o.i === want.j && o.side === want.their && o.j === want.i && o.their === want.side);
    if (back) done.add(back);
    const mine = armOf(entries[want.i].vrm, want.side), theirs = armOf(entries[want.j].vrm, want.their);
    if (!mine || !theirs) continue;
    if (back) {
      // Both reach: half-way between the two wrists as the turns left them — or, where one of the two arms does not
      // get there (a small character beside a tall one, their hands lifted above the heads: the small one's arm
      // ended 0.17 m under the place, DESIGN.md 138), the nearest place both arms reach: the tall one's hand
      // comes down to the small one's, as a grown-up's does to a child's.
      const place = within(mine.w.clone().lerp(theirs.w, 0.5), mine, theirs);
      bring(mine, place, want.w);
      bring(theirs, place, back.w);
    } else {
      bring(mine, theirs.w, want.w);
    }
    if (globalThis.shosaJoinDebug) {
      const at = arm => arm.hand.getWorldPosition(new THREE.Vector3()).toArray();
      globalThis.shosaJoinDebug({ a: want.i, hand: want.side, b: want.j, their: want.their, both: !!back, was: [mine.w.toArray(), theirs.w.toArray()], got: [at(mine), at(theirs)] });
    }
  }
}
