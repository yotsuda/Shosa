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

/** Reaches an arm's wrist toward `place` by `share`, a little short of it (`short`, a share of the arm), the elbow pointing as it does. */
function bring(arm, place, share, short = SHORT) {
  const back = arm.s.clone().sub(place);
  const wrist = short > 0 && back.lengthSq() > 1e-10 ? place.clone().addScaledVector(back.normalize(), short * arm.length) : place.clone();
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

/** A figure's two wrists nearer than CLOSE are together (a clap, a prayer, a heart): all of it; further than APART, not at all. Metres. */
const CLOSE = 0.07, APART = 0.16;

/**
 * A dancer's own two hands together (DESIGN.md 139). Where the figure's wrists are close — a clap, hands pressed, a
 * heart — the character's are brought as close: the turns alone leave them as far apart as its own shoulders and arms
 * make of the figure's angles (a small character with short arms clapped over its head with its hands 0.2 m apart).
 * The two wrists are put about the middle of where the turns left them, on the line between them, as far apart as the
 * figure's. Not a juggler's hands, nor a hand with a mic. Call as joinHands, after it.
 */
export function meetOwnHands(entries, figures, clip, frame) {
  const left = clip.joints.indexOf('leftHand'), right = clip.joints.indexOf('rightHand'), head = clip.joints.indexOf('head');
  if (left < 0 || right < 0) return;
  figures.forEach((data, i) => {
    const entry = entries[i];
    if (!data || !entry || !entry.vrm || data.juggle || data.mic) return;
    const frames = data.frames, f0 = Math.max(0, Math.min(frames.length - 1, Math.floor(frame))), f1 = Math.min(frames.length - 1, f0 + 1), k = Math.max(0, frame - f0);
    const gap = f => Math.hypot(f[left * 3] - f[right * 3], f[left * 3 + 1] - f[right * 3 + 1], f[left * 3 + 2] - f[right * 3 + 2]);
    const apart = gap(frames[f0]) + (gap(frames[f1]) - gap(frames[f0])) * k;
    if (apart >= APART) return;
    const t = Math.min(1, (APART - apart) / (APART - CLOSE)), share = t * t * (3 - 2 * t);
    entry.vrm.scene.updateMatrixWorld(true);
    const l = armOf(entry.vrm, 'left'), r = armOf(entry.vrm, 'right');
    if (!l || !r) return;
    const between = r.w.clone().sub(l.w), was = between.length();
    if (was < 1e-6) return;
    // The middle: as high as the turns left the hands, and as far before and beside the head as the figure's are
    // (a clap over the head is over the head: left to the turns, one arm kept out of the body put it over a shoulder).
    const place = l.w.clone().lerp(r.w, 0.5), headBone = entry.vrm.humanoid.getNormalizedBoneNode('head');
    if (head >= 0 && headBone) {
      const top = headBone.getWorldPosition(new THREE.Vector3());
      const off = j => {
        const at = f => (f[left * 3 + j] + f[right * 3 + j]) / 2 - f[head * 3 + j];
        return at(frames[f0]) + (at(frames[f1]) - at(frames[f0])) * k;
      };
      place.x = top.x + off(0);
      place.z = top.z + off(2);
    }
    const middle = within(place, l, r), half = between.multiplyScalar(0.5 * Math.min(apart, was) / was);
    bring(l, middle.clone().sub(half), share, 0);
    bring(r, middle.clone().add(half), share, 0);
  });
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
