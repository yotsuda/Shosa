// A hand at a place of the character's own head (DESIGN.md 122). A word can put a hand at a place of the body
// (`head`, `chin`, `forehead`); the solver finds that place on Shosa's own figure (a head 0.10 m round) and the preview
// puts the arm's turns on the character, whose head is not the figure's: a big head with much hair has the hand inside
// it (the user: the hand sunk into the head). The clip says how much each hand is at each such place at every frame
// (`touch`, Solver.Touches); here, by that much, the wrist is reached to that place on the character's own head,
// measured from its mesh (clearance.js): beside the head off its side's surface, under and before its chin, before
// its brow. Only the preview's character changes: the movement and what is exported stay as solved. A hand a word
// sends in a direction (`side.up(60)`) is left where the word put it.
import * as THREE from 'three';
import { reach } from './mic.js';

/** The places of the head. (The trunk's — `chest`, `hip`, `belly`, `shoulder` — are not done yet.) */
const OF_THE_HEAD = ['head', 'chin', 'forehead'];
/** The points of the skull that say where its surface is one way: those within this angle of it, and how far out of them. */
const CONE = Math.cos(25 * Math.PI / 180), OUTER = 0.85;
/**
 * Where the wrist is from the chin (its lowest point in front) and from the brow, metres on the figure (scaled to the
 * character's arm): to the hand's own side, up, forward. As the solver's places are from the figure's: the wrist under
 * the chin and before it, the hand rising in front of it; the wrist before the brow, a little to its side.
 */
const FROM_CHIN = [0.05, -0.19, 0.08], FROM_BROW = [0.09, 0.03, 0.03];
/** And from the head's surface at its side, level with its middle: off it by a hand's thickness, a little up. */
const FROM_SIDE = [0.03, 0.02, 0];

const qa = new THREE.Quaternion();

/** How far out the skull's surface is from its middle, the way `d` (a unit vector in the head bone's axes), unscaled. */
function surfaceAlong(head, d) {
  const points = head.points, along = [];
  if (points) {
    for (let i = 0; i < points.length; i += 3) {
      const x = points[i], y = points[i + 1], z = points[i + 2];
      const dot = x * d.x + y * d.y + z * d.z;
      if (dot > 0 && dot * dot >= CONE * CONE * (x * x + y * y + z * z)) along.push(dot);
    }
  }
  if (along.length < 8) return head.radius;
  along.sort((a, b) => a - b);
  return along[Math.min(along.length - 1, Math.floor(OUTER * along.length))];
}

/**
 * How much the clip's `touch` has a hand at each place of the head at a frame (between two frames, blended):
 * { head, chin, forehead }, or null when it is at none.
 */
export function headTouch(touch, hand, f0, k) {
  let found = null;
  for (const entry of touch) {
    if (entry.hand !== hand || !OF_THE_HEAD.includes(entry.place) || !entry.weight || !entry.weight.length) continue;
    const w0 = entry.weight[Math.min(entry.weight.length - 1, f0)], w1 = entry.weight[Math.min(entry.weight.length - 1, f0 + 1)];
    const w = w0 + (w1 - w0) * k;
    if (w > 0) (found ??= {})[entry.place] = Math.min(1, (found[entry.place] ?? 0) + w);
  }
  return found;
}

/**
 * Reaches each hand to its place on the character's own head, by `weights` ({ left, right }, each headTouch's).
 * `figure(name)` is where the figure's joint is at this frame; `body` is clearance.js's measure of the character;
 * `size` its scale. Call after the bones are posed and the arms kept out of the body: the hand at the head is where
 * it has to be.
 */
export function touchHead(vrm, body, size, figure, weights) {
  if (!body || !body.head) return;
  const node = name => vrm.humanoid.getNormalizedBoneNode(name);
  const headBone = node('head');
  if (!headBone) return;
  for (const [side, sign] of [['left', 1], ['right', -1]]) {
    const places = weights[side];
    if (!places) continue;
    const upper = node(side + 'UpperArm'), lower = node(side + 'LowerArm'), hand = node(side + 'Hand');
    if (!upper || !lower || !hand) continue;
    vrm.scene.updateMatrixWorld(true);
    const s = upper.getWorldPosition(new THREE.Vector3()), e = lower.getWorldPosition(new THREE.Vector3()), w = hand.getWorldPosition(new THREE.Vector3());
    const fs = figure(side + 'Shoulder'), fe = figure(side + 'Elbow'), fw = figure(side + 'Hand');
    const figureArm = fs.distanceTo(fe) + fe.distanceTo(fw);
    if (figureArm < 1e-6) continue;
    const k = (s.distanceTo(e) + e.distanceTo(w)) / figureArm;
    const turn = headBone.getWorldQuaternion(qa).clone(), base = headBone.getWorldPosition(new THREE.Vector3());
    // A point of the head (in its bone's axes, unscaled) and an offset from it (metres on the figure), in the world.
    const from = (point, [across, up, forward]) => new THREE.Vector3(point.x + sign * across * k / size, point.y + up * k / size, point.z + forward * k / size)
      .multiplyScalar(size).applyQuaternion(turn).add(base);

    const wrist = w.clone();
    for (const [place, weight] of Object.entries(places)) {
      let target;
      if (place === 'chin' && body.head.chin) target = from(body.head.chin, FROM_CHIN);
      else if (place === 'forehead' && body.head.brow) target = from(body.head.brow, FROM_BROW);
      else if (place === 'head') {
        // Beside the head: out to the hand's side from the middle of the head, off its surface there (the hair's).
        // (A fixed place, as the chin's: from where the figure's wrist is on its way there, the place found moved about.)
        const out = new THREE.Vector3(sign, 0, 0);
        const beside = body.head.local.clone().addScaledVector(out, surfaceAlong(body.head, out));
        target = from(beside, FROM_SIDE);
      } else continue;   // a place that could not be measured: as the turns put it
      wrist.addScaledVector(target.sub(w), weight);
    }
    // The elbow pointing as it does now.
    // The hand turned in the world as it was: turned on with the forearm, it lay up over the face from under the chin.
    const handWorld = hand.getWorldQuaternion(new THREE.Quaternion());
    reach(upper, lower, hand, wrist, e.clone().sub(s), 1);
    hand.quaternion.copy(hand.parent.getWorldQuaternion(qa).invert().multiply(handWorld));
    hand.updateMatrixWorld(true);
    if (globalThis.shosaTouchDebug) {
      const eye = node(side + 'Eye'), tip = node(side + 'MiddleDistal') ?? node(side + 'MiddleProximal');
      globalThis.shosaTouchDebug({ side, places, shoulder: s.toArray(), elbowWas: e.toArray(), was: w.toArray(), wrist: wrist.toArray(), head: base.toArray(),
        elbow: lower.getWorldPosition(new THREE.Vector3()).toArray(), got: hand.getWorldPosition(new THREE.Vector3()).toArray(),
        eye: eye?.getWorldPosition(new THREE.Vector3()).toArray(), finger: tip?.getWorldPosition(new THREE.Vector3()).toArray(),
        chin: body.head.chin?.toArray(), brow: body.head.brow?.toArray(), size, k });
    }
  }
}
