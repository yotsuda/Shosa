// Arms kept out of the character's body (DESIGN.md 116). The movement is solved on Shosa's own figure (1.7 m,
// a slim trunk) and only the bones' turns are put on the character: a character with a thicker trunk, a big
// head or puffed sleeves gets its arms through its body where the figure's were clear. Here, once when the
// character is read, its trunk and head are measured from its mesh (as it stands at rest); then every frame,
// after the turns are put on, an arm that is inside them is turned out at the shoulder, by as little as clears
// it. Only the preview's character changes: the movement and what is exported stay as solved.
//
// The trunk is a chain of capsules with an elliptic cross-section (wider side to side than front to back),
// from the hips up to the neck, one per bone; the head is a sphere. Each is fixed to its bone, so it bends,
// leans and turns with the body. The arm is tested along the elbow half of the upper arm (the half at the
// shoulder is joined to the trunk) and the forearm to the wrist, each with its own thickness. The hand is not:
// a hand is often where the words put it on the body (`chin`, `chest`).
import * as THREE from 'three';

const TRUNK = ['hips', 'spine', 'chest', 'upperChest', 'neck'];
const MARGIN = 0.005;          // metres of air left between arm and body
const PASSES = 4;
// The elbow and the forearm. Not the upper arm short of the elbow: the shoulder joint is itself inside the
// trunk as measured (the trunk's mesh runs out over it), so points near it are never clear, and turning the
// arm for them raised it ever higher (DESIGN.md 116.2).
const SAMPLES = [['upper', 1], ['lower', 0.25], ['lower', 0.5], ['lower', 0.75], ['lower', 1]];
/** The most an arm is turned out in a frame: past it the arm is left as it is rather than thrown about. */
const MOST = 30 * Math.PI / 180;

const v1 = new THREE.Vector3(), v2 = new THREE.Vector3(), v3 = new THREE.Vector3();
const qa = new THREE.Quaternion(), qb = new THREE.Quaternion();

/** The axes of a segment from a to b turned as its bone is: u along it, side across the body, front out of the chest. */
function frame(a, b, boneQuaternion) {
  const u = new THREE.Vector3().subVectors(b, a);
  const length = u.length();
  u.divideScalar(length || 1);
  const side = new THREE.Vector3(1, 0, 0).applyQuaternion(boneQuaternion);
  const front = new THREE.Vector3().crossVectors(side, u).normalize();
  side.crossVectors(u, front).normalize();
  return { u, side, front, length };
}

function percentile(values, p) {
  if (values.length === 0) return 0;
  const sorted = Float64Array.from(values).sort();
  return sorted[Math.min(sorted.length - 1, Math.floor(p * sorted.length))];
}

/**
 * The trunk, head and arms of a character, measured once as it stands at rest (its scene unscaled):
 * null when it has not the bones to do it with.
 */
export function measureBody(vrm) {
  const humanoid = vrm.humanoid;
  const node = name => humanoid.getNormalizedBoneNode(name);
  const trunk = TRUNK.filter(node);
  if (trunk.length < 2 || !node('head')) return null;
  vrm.scene.updateMatrixWorld(true);

  // Which humanoid bone each bone of the file belongs to: itself, or its nearest humanoid ancestor (a skirt to the
  // hips, a bust to the chest, hair to the head, a sleeve to the arm).
  const named = new Map();
  for (const name of [...trunk, 'head', 'leftUpperArm', 'leftLowerArm', 'leftHand', 'rightUpperArm', 'rightLowerArm', 'rightHand',
    'leftUpperLeg', 'rightUpperLeg', 'leftShoulder', 'rightShoulder']) {
    const raw = humanoid.getRawBoneNode(name);
    if (raw) named.set(raw, name);
  }
  const owner = bone => {
    for (let b = bone; b; b = b.parent) if (named.has(b)) return named.get(b);
    return null;
  };

  const at = name => node(name).getWorldPosition(new THREE.Vector3());
  const turned = name => node(name).getWorldQuaternion(new THREE.Quaternion());
  const segments = [];
  for (let i = 0; i + 1 < trunk.length; i++) {
    segments.push({ from: trunk[i], to: trunk[i + 1], ...frame(at(trunk[i]), at(trunk[i + 1]), turned(trunk[i])), a: at(trunk[i]), xs: [], zs: [] });
  }
  const arms = {};
  for (const side of ['left', 'right']) {
    const s = at(side + 'UpperArm'), e = at(side + 'LowerArm'), w = node(side + 'Hand') ? at(side + 'Hand') : null;
    if (!w) continue;
    arms[side] = { upper: { a: s, b: e, rs: [] }, lower: { a: e, b: w, rs: [] } };
  }
  const head = { members: [] };

  const p = new THREE.Vector3();
  vrm.scene.traverse(object => {
    if (!object.isSkinnedMesh) return;
    const geometry = object.geometry;
    const position = geometry.attributes.position, index = geometry.attributes.skinIndex, weight = geometry.attributes.skinWeight;
    if (!position || !index || !weight) return;
    const bones = object.skeleton.bones.map(owner);
    // Every few vertices is enough for a measure, and keeps a large model quick to read.
    const step = Math.max(1, Math.floor(position.count / 40000));
    for (let i = 0; i < position.count; i += step) {
      let best = 0, bestWeight = -1;
      for (let k = 0; k < 4; k++) {
        const wk = weight.getComponent(i, k);
        if (wk > bestWeight) { bestWeight = wk; best = index.getComponent(i, k); }
      }
      const name = bones[best];
      if (!name) continue;
      p.fromBufferAttribute(position, i).applyMatrix4(object.matrixWorld);
      if (TRUNK.includes(name)) {
        // The segment it lies along (projected inside it): its offset across the body and out of the chest.
        for (const segment of segments) {
          const t = v1.subVectors(p, segment.a).dot(segment.u);
          if (t < 0 || t > segment.length) continue;
          segment.xs.push(Math.abs(v1.dot(segment.side)));
          segment.zs.push(Math.abs(v1.dot(segment.front)));
          break;
        }
      } else if (name === 'head') {
        head.members.push(p.clone());
      } else {
        const m = /^(left|right)(UpperArm|LowerArm)$/.exec(name);
        const arm = m && arms[m[1]] && arms[m[1]][m[2] === 'UpperArm' ? 'upper' : 'lower'];
        if (!arm) continue;
        const axis = v2.subVectors(arm.b, arm.a), length = axis.length();
        axis.divideScalar(length || 1);
        const t = v1.subVectors(p, arm.a).dot(axis);
        if (t < 0.1 * length || t > 0.9 * length) continue;
        arm.rs.push(v1.addScaledVector(axis, -t).length());
      }
    }
  });

  // Most of the trunk is inside (a bust, a collar, a bow, a flared hem stand out): the 80th percentile, not the farthest.
  const trunkShapes = segments.filter(s => s.xs.length >= 20).map(s => ({
    from: s.from, to: s.to,
    rx: percentile(s.xs, 0.8), rz: percentile(s.zs, 0.8),
  }));
  if (trunkShapes.length === 0) return null;

  // The head: the vertices the head bone moves that are above the neck (face and skull). Much hair is often
  // moved by the head bone itself and hangs to the waist or below: below the neck it is left out, and above
  // it the middle (medians) and the 70th percentile are taken, which twin tails and ahoge do not pull out.
  let headShape = null;
  const neckTop = at('head').y;
  const skull = head.members.filter(q => q.y >= neckTop - 0.02);
  if (skull.length >= 20) {
    const middle = axis => percentile(skull.map(q => q[axis]), 0.5);
    const centre = new THREE.Vector3(middle('x'), middle('y'), middle('z'));
    const radius = percentile(skull.map(q => q.distanceTo(centre)), 0.7);
    // Where the centre is from the head bone, in the bone's own axes (it turns with the head).
    const local = centre.clone().sub(at('head')).applyQuaternion(turned('head').invert());
    headShape = { local, radius, mouth: measureMouth(node, at, turned, head.members) };
  }

  const armShapes = {};
  for (const [side, arm] of Object.entries(arms)) {
    armShapes[side] = {
      upper: Math.max(0.02, percentile(arm.upper.rs, 0.5)),
      lower: Math.max(0.015, percentile(arm.lower.rs, 0.5)),
    };
  }
  return { trunk: trunkShapes, head: headShape, arms: armShapes };
}

/**
 * Where the mouth is on the face, in the head bone's own axes (for a mic, DESIGN.md 117): null when it cannot be
 * found. At rest the character faces +z. Its height is 0.4 of the way from the head bone (the top of the neck) up
 * to the eyes; it is on the face's surface there, the front of the head's own vertices down the middle (the hair
 * of the fringe is above it, the locks at the sides are not in the middle).
 */
function measureMouth(node, at, turned, members) {
  const base = at('head');
  const eyes = ['leftEye', 'rightEye'].filter(node).map(at);
  const eyeY = eyes.length ? eyes.reduce((s, e) => s + e.y, 0) / eyes.length : base.y + 0.1;
  if (eyeY <= base.y) return null;
  const y = base.y + 0.4 * (eyeY - base.y);
  for (const width of [0.015, 0.03]) {
    const front = members.filter(q => Math.abs(q.y - y) < width && Math.abs(q.x - base.x) < width).map(q => q.z);
    if (front.length >= 3) {
      const point = new THREE.Vector3(base.x, y, percentile(front, 0.95));
      return point.sub(base).applyQuaternion(turned('head').invert());
    }
  }
  return null;
}

/**
 * Turns each arm out at the shoulder until it is clear of the trunk and head, by as little as does it.
 * The bones are the character's normalized ones, posed this frame; `size` is the scene's scale.
 */
export function keepArmsOut(vrm, body, size) {
  if (!body) return;
  const node = name => vrm.humanoid.getNormalizedBoneNode(name);
  for (const side of ['left', 'right']) {
    const thickness = body.arms[side];
    const upperArm = node(side + 'UpperArm'), lowerArm = node(side + 'LowerArm'), hand = node(side + 'Hand');
    if (!thickness || !upperArm || !lowerArm || !hand) continue;
    // First the elbow is swung round the line from the shoulder to the wrist: the hand stays where the words put
    // it (on the chin, on the chest, in the other hand), and only the elbow and forearm find their way round the body.
    swingElbow(vrm, body, size, side, thickness, upperArm, lowerArm, hand);
    // What that cannot clear (an arm hanging straight down has no elbow to swing), the arm turned out at the shoulder.
    let turnedBy = 0;
    for (let pass = 0; pass < PASSES && turnedBy < MOST; pass++) {
      vrm.scene.updateMatrixWorld(true);
      const shoulder = upperArm.getWorldPosition(new THREE.Vector3());
      const elbow = lowerArm.getWorldPosition(new THREE.Vector3());
      const wrist = hand.getWorldPosition(new THREE.Vector3());
      const deepest = deepestPoint(vrm, body, size, side, shoulder, elbow, wrist, thickness);
      if (!deepest) break;
      // Move that point of the arm out along the body's normal, turning the whole arm about the shoulder.
      const lever = v1.subVectors(deepest.point, shoulder);
      const reach = lever.length();
      const axis = v2.crossVectors(lever, deepest.normal);
      if (reach < 1e-4 || axis.lengthSq() < 1e-10) break;
      axis.normalize();
      const angle = Math.min(Math.atan2(deepest.depth + MARGIN * size, reach), MOST - turnedBy);
      turnedBy += angle;
      // The turn in the world, put on the bone as a turn relative to its parent.
      qa.setFromAxisAngle(axis, angle);
      const parent = upperArm.parent.getWorldQuaternion(qb);
      const world = upperArm.getWorldQuaternion(new THREE.Quaternion());
      upperArm.quaternion.copy(parent.invert().multiply(qa).multiply(world));
    }
  }
}

/** How far the elbow may be swung each way, and in what steps. */
const SWING_MOST = 90, SWING_STEP = 5;

/**
 * Swings the elbow round the shoulder–wrist line by the smallest angle that clears the arm (or, if none does,
 * the one that leaves it least inside), keeping the hand where it was and turned as it was.
 */
function swingElbow(vrm, body, size, side, thickness, upperArm, lowerArm, hand) {
  vrm.scene.updateMatrixWorld(true);
  const shoulder = upperArm.getWorldPosition(new THREE.Vector3());
  const elbow = lowerArm.getWorldPosition(new THREE.Vector3());
  const wrist = hand.getWorldPosition(new THREE.Vector3());
  const first = deepestPoint(vrm, body, size, side, shoulder, elbow, wrist, thickness);
  if (!first) return;
  const axis = new THREE.Vector3().subVectors(wrist, shoulder);
  if (axis.lengthSq() < 1e-8) return;
  axis.normalize();
  // An arm almost straight has nothing to swing: the elbow is on the line.
  if (v1.subVectors(elbow, shoulder).cross(axis).length() < 0.01 * size) return;

  const turn = new THREE.Quaternion(), swung = new THREE.Vector3();
  let best = { angle: 0, depth: first.depth };
  for (let k = 1; k <= SWING_MOST / SWING_STEP && best.depth > 0; k++) {
    for (const sign of [1, -1]) {
      const angle = sign * k * SWING_STEP * Math.PI / 180;
      turn.setFromAxisAngle(axis, angle);
      swung.subVectors(elbow, shoulder).applyQuaternion(turn).add(shoulder);
      const found = deepestPoint(vrm, body, size, side, shoulder, swung, wrist, thickness);
      const depth = found ? found.depth : 0;
      if (depth < best.depth - 1e-4) best = { angle, depth };
      if (depth === 0) break;
    }
  }
  if (best.angle === 0) return;
  // The swing is a turn of the upper arm in the world about the line; the hand is put back as it was turned.
  const handWorld = hand.getWorldQuaternion(new THREE.Quaternion());
  turn.setFromAxisAngle(axis, best.angle);
  const parent = upperArm.parent.getWorldQuaternion(new THREE.Quaternion());
  const world = upperArm.getWorldQuaternion(new THREE.Quaternion());
  upperArm.quaternion.copy(parent.invert().multiply(turn).multiply(world));
  upperArm.updateMatrixWorld(true);
  const handParent = hand.parent.getWorldQuaternion(new THREE.Quaternion());
  hand.quaternion.copy(handParent.invert().multiply(handWorld));
}

/** The point of the arm that is farthest inside the body, with the way out and how far: null when the arm is clear. */
function deepestPoint(vrm, body, size, side, shoulder, elbow, wrist, thickness) {
  const node = name => vrm.humanoid.getNormalizedBoneNode(name);
  let found = null;
  const consider = (point, depth, normal) => {
    if (depth > 0 && (!found || depth > found.depth)) found = { point: point.clone(), depth, normal: normal.clone() };
  };
  const point = new THREE.Vector3();
  for (const [part, t] of SAMPLES) {
    if (part === 'upper') point.lerpVectors(shoulder, elbow, t);
    else point.lerpVectors(elbow, wrist, t);
    const armRadius = thickness[part] * size;

    for (const shape of body.trunk) {
      const a = node(shape.from).getWorldPosition(v1.clone());
      const b = node(shape.to).getWorldPosition(v2.clone());
      const axes = frame(a, b, node(shape.from).getWorldQuaternion(qb));
      const offset = v3.subVectors(point, a);
      const along = offset.dot(axes.u);
      if (along < 0 || along > axes.length) continue;
      offset.addScaledVector(axes.u, -along);
      const rx = shape.rx * size + armRadius, rz = shape.rz * size + armRadius;
      const dx = offset.dot(axes.side), dz = offset.dot(axes.front);
      const f = Math.hypot(dx / rx, dz / rz);
      if (f >= 1) continue;
      const out = new THREE.Vector3();
      let depth;
      if (f < 1e-3) {
        // On the axis: out to the arm's own side.
        out.copy(axes.side).multiplyScalar(side === 'left' ? 1 : -1);
        depth = Math.min(rx, rz);
      } else {
        out.copy(axes.side).multiplyScalar(dx / (rx * rx)).addScaledVector(axes.front, dz / (rz * rz)).normalize();
        depth = offset.length() * (1 / f - 1);
      }
      consider(point, depth, out);
    }

    // The wrist end of the forearm is where a word puts the hand against the face (`chin`): for it the head is
    // smaller (70%) and the arm thin, so a hand on the chin stays, and only a hand sunk into the face is pushed out
    // (DESIGN.md 116.2).
    const wristEnd = part === 'lower' && t > 0.5;
    if (body.head) {
      const head = node('head');
      const centre = body.head.local.clone().multiplyScalar(size).applyQuaternion(head.getWorldQuaternion(qb)).add(head.getWorldPosition(v1.clone()));
      const offset = v3.subVectors(point, centre);
      const distance = offset.length();
      const reach = wristEnd ? 0.7 * body.head.radius * size : body.head.radius * size + armRadius;
      if (distance < reach && distance > 1e-4) consider(point, reach - distance, offset.divideScalar(distance));
    }
  }
  return found;
}
