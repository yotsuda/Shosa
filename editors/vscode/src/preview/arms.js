// A juggling character's arms solved again on its own body (DESIGN.md 118.12). The movement is solved on Shosa's own
// figure (1.7 m, a slim trunk) and the preview puts its turns on the character; in Mills' Mess the figure's elbows are
// held side to side under the shoulders with the forearms going round (the user), but a character with shorter arms and
// a thicker trunk (a kimono) got its forearms 12-17 cm inside its body, and keeping them out (clearance.js) swung its
// elbows out and in, 0.24 m side to side. Here, while Mills' Mess has the arms, each arm is aimed again: the hand where
// the figure's is from the shoulder, scaled to the character's arm and brought forward by how much thicker its trunk is,
// and the elbow put as the solver puts the figure's (Solver.ElbowOut), but never inside the trunk side to side.
import * as THREE from 'three';

/** The figure's trunk and arm thickness (Solver.TrunkRadius, its arms as the sheet draws them), metres. */
const FIGURE_TRUNK = 0.11, FIGURE_ARM = 0.04;
/** As the solver (Solver.MillsElbowSide, MillsElbowSlack, MillsElbowTurn): metres in from the shoulder, scaled to the arm. */
// The turn is freer than the solver's (0.25): the way the figure's elbow points is not the character's, and held to it
// the elbow stayed in the trunk more often.
const ELBOW_SIDE = -0.12, ELBOW_SLACK = 0.03, ELBOW_TURN = 0.6;
/** Air left between the elbow and the trunk's side, metres (scaled). */
const ELBOW_CLEAR = 0.01;
/** How far in front of the trunk's front the hand is at the least, metres (scaled): room for the forearm to go round. */
const HAND_CLEAR = 0.08;
const TRUNK_PARTS = new Set(['spine', 'chest', 'upperChest']);

const qa = new THREE.Quaternion(), qb = new THREE.Quaternion();

/**
 * Aims a bone so that its child joint goes to `to` (world), turning it by the least that does, and puts its children back
 * as they were turned in the world except the one given (which follows).
 */
function aim(bone, child, to) {
  bone.updateWorldMatrix(true, false);
  const from = bone.getWorldPosition(new THREE.Vector3());
  const now = child.getWorldPosition(new THREE.Vector3()).sub(from);
  const want = to.clone().sub(from);
  if (now.lengthSq() < 1e-10 || want.lengthSq() < 1e-10) return;
  const turn = qa.setFromUnitVectors(now.normalize(), want.normalize());
  const world = bone.getWorldQuaternion(new THREE.Quaternion());
  const parent = bone.parent.getWorldQuaternion(qb);
  bone.quaternion.copy(parent.invert().multiply(turn.multiply(world)));
  bone.updateWorldMatrix(false, true);
}

/**
 * Solves the character's arms again on its body (see above), as much as `weight` (0..1). `figure(name)` is where the
 * figure's joint is (world) at this frame; `body` is clearance.js's measure of the character; `size` its scale.
 */
export function millsArms(vrm, body, size, figure, weight) {
  if (!body || weight <= 0) return;
  const node = name => vrm.humanoid.getNormalizedBoneNode(name);
  vrm.scene.updateMatrixWorld(true);
  // The character's trunk, side to side and front to back, at its widest about the chest.
  let rx = 0, rz = 0;
  for (const shape of body.trunk) {
    if (!TRUNK_PARTS.has(shape.from)) continue;
    rx = Math.max(rx, shape.rx * size);
    rz = Math.max(rz, shape.rz * size);
  }
  // The figure's frame: across to its left, up, forward (the character faces the same way).
  const fl = figure('leftShoulder'), fr = figure('rightShoulder');
  const left = fl.clone().sub(fr).setY(0).normalize(), up = new THREE.Vector3(0, 1, 0);
  const forward = new THREE.Vector3().crossVectors(left, up).normalize();
  const middle = node('upperChest') ?? node('chest') ?? node('spine');
  const trunkAt = middle ? middle.getWorldPosition(new THREE.Vector3()) : null;

  for (const [side, sign] of [['left', 1], ['right', -1]]) {
    const upper = node(side + 'UpperArm'), lower = node(side + 'LowerArm'), hand = node(side + 'Hand');
    const thickness = body.arms[side];
    if (!upper || !lower || !hand || !thickness) continue;
    const shoulder = upper.getWorldPosition(new THREE.Vector3());
    const elbowNow = lower.getWorldPosition(new THREE.Vector3());
    const wristNow = hand.getWorldPosition(new THREE.Vector3());
    const l1 = shoulder.distanceTo(elbowNow), l2 = elbowNow.distanceTo(wristNow);
    const fs = figure(side + 'Shoulder'), fe = figure(side + 'Elbow'), fw = figure(side + 'Hand');
    const f1 = fs.distanceTo(fe), f2 = fe.distanceTo(fw);
    if (f1 + f2 < 1e-6 || l1 + l2 < 1e-6) continue;
    const k = (l1 + l2) / (f1 + f2);
    const armRadius = thickness.lower * size;
    // How much further out in front the arm has to be than the figure's, for the thicker trunk.
    const ahead = Math.max(0, rz + armRadius - (FIGURE_TRUNK + FIGURE_ARM) * k);

    // The hand: the figure's from its shoulder, scaled, and forward by that much, and at least clear of the trunk's front
    // with room for the forearm; as the character's is, the rest.
    const target = fw.clone().sub(fs).multiplyScalar(k).add(shoulder).addScaledVector(forward, ahead);
    if (trunkAt) {
      const short = rz + armRadius + HAND_CLEAR * k - target.clone().sub(trunkAt).dot(forward);
      if (short > 0) target.addScaledVector(forward, short);
    }
    const wrist = wristNow.clone().lerp(target, weight);

    // The elbow: on its circle round the line from the shoulder to the wrist, side to side as near the solver's place
    // as it goes without turning far from where the figure's points, and never inside the trunk's side.
    const axisLine = wrist.clone().sub(shoulder);
    const distance = THREE.MathUtils.clamp(axisLine.length(), Math.abs(l1 - l2) + 1e-3, l1 + l2 - 1e-4);
    const axis = axisLine.normalize();
    const along = (l1 * l1 - l2 * l2 + distance * distance) / (2 * distance);
    const height = Math.sqrt(Math.max(0, l1 * l1 - along * along));
    const natural = fe.clone().sub(fs).sub(axis.clone().multiplyScalar(fe.clone().sub(fs).dot(axis)));
    if (natural.lengthSq() < 1e-10) natural.copy(up).negate();
    const u = natural.addScaledVector(axis, -natural.dot(axis)).normalize();
    const v = new THREE.Vector3().crossVectors(axis, u);
    const outward = left.clone().multiplyScalar(sign);
    // Side to side from the trunk's middle: the solver's place, but out of the trunk.
    const middleOut = trunkAt ? trunkAt.dot(outward) : 0;
    const wanted = Math.max(shoulder.dot(outward) + ELBOW_SIDE * k, middleOut + rx + armRadius + ELBOW_CLEAR * k);
    const c0 = shoulder.dot(outward) + axis.dot(outward) * along, a = height * u.dot(outward), b = height * v.dot(outward);
    // Inside the trunk (its cross-section an ellipse, the arm's thickness added) costs a great deal: the elbow goes to its
    // side or in front of it, whichever is less far.
    const centre = shoulder.clone().addScaledVector(axis, along), at = new THREE.Vector3();
    const sideR = rx + armRadius, frontR = rz + armRadius;
    let best = Infinity, theta = 0;
    for (let i = -180; i <= 180; i++) {
      const t = i * Math.PI / 180;
      const miss = (c0 + a * Math.cos(t) + b * Math.sin(t) - wanted) / (ELBOW_SLACK * k), turn = t / ELBOW_TURN;
      let cost = miss * miss + turn * turn;
      if (trunkAt) {
        at.copy(centre).addScaledVector(u, height * Math.cos(t)).addScaledVector(v, height * Math.sin(t)).sub(trunkAt);
        const f = Math.hypot(at.dot(outward) / sideR, at.dot(forward) / frontR);
        if (f < 1) cost += ((1 - f) * frontR / (0.005 * k)) ** 2;
      }
      if (cost < best) { best = cost; theta = t; }
    }
    const elbow = centre.clone().addScaledVector(u, height * Math.cos(theta)).addScaledVector(v, height * Math.sin(theta));
    elbow.lerp(elbowNow, 1 - weight);

    // The upper arm to the elbow, the forearm to the wrist; the hand turned in the world as it was.
    const handWorld = hand.getWorldQuaternion(new THREE.Quaternion());
    aim(upper, lower, elbow);
    aim(lower, hand, wrist);
    hand.parent.getWorldQuaternion(qb);
    hand.quaternion.copy(qb.invert().multiply(handWorld));
    hand.updateWorldMatrix(false, true);
  }
}
