// A mic in a character's hand (`dancer alice … sings melody mic right`, DESIGN.md 117). The server says which hand
// and, at every frame, how far the mic is up at the mouth (`mic: { hand, up }`): 0 in the hand, 1 at the mouth
// while the dancer sings. The solver already brings the figure's hand to its mouth; the character's head and arms
// are not the figure's (a child, a two-heads-tall mini), so here the arm is reached to the character's own mouth,
// as far as the mic is up. The hand is closed on the mic (the fingers, which the figure has not) and turned so
// the mic comes out of the fist on the thumb's side: up from a hand at rest, to the lips from one at the mouth.
// `mic both`: the other hand joins lower on the handle while singing. Only the preview's character: what is
// exported has the figure's arm at the mouth, and no mic or fingers.
import * as THREE from 'three';

const FINGERS = ['Index', 'Middle', 'Ring', 'Little'];
/** Degrees each joint of a finger and of the thumb bends round a handle about 3 cm thick. */
const CURL = [['Proximal', 70], ['Intermediate', 80], ['Distal', 50]];
const THUMB = [['Metacarpal', 25], ['Proximal', 35], ['Distal', 30]];
/**
 * The fist's place in the hand is for a hand on Shosa's figure, whose forearm is this long; a character's own forearm
 * scales it. The mic is the same size in every hand, in metres (the user: scaled to a mini's small hand it was as thin
 * as a bamboo skewer).
 */
const FOREARM = 0.31;
/** From the wrist to the middle of the closed fist, along the hand and out of the palm. */
const FIST_ALONG = 0.045, FIST_OUT = 0.025;
/**
 * The mic along +y from the fist: its foot, and the middle of its ball (21 cm from the foot to the top of the ball).
 * The fist holds it 1 cm above the middle of its length (the user: at 6 cm from the foot it was held too low).
 */
const FOOT = -0.115, TO_BALL = 0.065;
/** How far below the first fist the other hand of `mic both` holds. */
const LOWER_HAND = 0.075;

const X = new THREE.Vector3(1, 0, 0), Y = new THREE.Vector3(0, 1, 0), Z = new THREE.Vector3(0, 0, 1);

function makeMic() {
  const group = new THREE.Group();
  const body = new THREE.MeshStandardMaterial({ color: 0x2b2b2e, metalness: 0.6, roughness: 0.4 });
  const grille = new THREE.MeshStandardMaterial({ color: 0xb8bcc2, metalness: 0.8, roughness: 0.35 });
  // Along +y, the fist at 0: the handle from the foot to under the collar, the collar, the ball.
  const collarFoot = TO_BALL - 0.025;
  const handle = new THREE.Mesh(new THREE.CylinderGeometry(0.016, 0.012, collarFoot - FOOT, 20), body);
  handle.position.y = (FOOT + collarFoot) / 2;
  const collar = new THREE.Mesh(new THREE.CylinderGeometry(0.024, 0.017, 0.02, 20), body);
  collar.position.y = collarFoot + 0.01;
  const ball = new THREE.Mesh(new THREE.SphereGeometry(0.03, 24, 16), grille);
  ball.position.y = TO_BALL;
  group.add(handle, collar, ball);
  group.traverse(o => { if (o.isMesh) o.frustumCulled = false; });
  return group;
}

const worldPos = node => node.getWorldPosition(new THREE.Vector3());
const worldQuat = node => node.getWorldQuaternion(new THREE.Quaternion());

/** Turns a bone so that it is turned `q` in the world. */
function setWorld(bone, q) {
  bone.quaternion.copy(worldQuat(bone.parent).invert().multiply(q));
  bone.updateMatrixWorld(true);
}

/** Turns a bone in the world by the least turn that takes the way `from` to `to`. */
function swing(bone, from, to) {
  if (from.lengthSq() < 1e-12 || to.lengthSq() < 1e-12) return;
  const q = new THREE.Quaternion().setFromUnitVectors(from.clone().normalize(), to.clone().normalize());
  setWorld(bone, q.multiply(worldQuat(bone)));
}

/** The part of `v` square to the unit vector `axis`, of length 1; `fallback` when there is none. */
function square(v, axis, fallback) {
  const out = v.clone().addScaledVector(axis, -v.dot(axis));
  if (out.lengthSq() > 1e-8) return out.normalize();
  return fallback ? square(fallback, axis, null) ?? new THREE.Vector3() : new THREE.Vector3();
}

export function createMicHands(scene) {
  /** The character's own frame at rest: the normalized rig's bones all rest turned as its scene is. */
  function restOf(vrm) {
    return worldQuat(vrm.scene);
  }

  /** How the hand is turned in the world for a way of the hand (wrist to knuckles) and of the thumb. */
  function handTurn(rest, isLeft, along, thumb) {
    // At rest (the T-pose, facing +z) a left hand points +x, a right one -x, and the thumbs point forward (+z).
    const restAlong = isLeft ? X.clone() : X.clone().negate();
    const from = new THREE.Matrix4().makeBasis(restAlong, Z, restAlong.clone().cross(Z));
    const to = new THREE.Matrix4().makeBasis(along, thumb, along.clone().cross(thumb));
    const q = new THREE.Quaternion().setFromRotationMatrix(to.multiply(from.transpose()));
    return q.multiply(rest);
  }

  /** The fingers of a hand closed by `amount` (0 open, 1 round the handle). */
  function closeHand(vrm, side, rest, amount) {
    const inverse = rest.clone().invert();
    // In the rest frame the fingers point out to their side and close down toward the palm (-y); the thumb turns
    // about x, down under the palm, on both hands.
    const fingerAxis = (side === 'left' ? Z.clone().negate() : Z.clone()).applyQuaternion(inverse);
    const thumbAxis = X.clone().applyQuaternion(inverse);
    for (const finger of FINGERS) {
      for (const [joint, degrees] of CURL) {
        const bone = vrm.humanoid.getNormalizedBoneNode(side + finger + joint);
        if (bone) bone.quaternion.setFromAxisAngle(fingerAxis, amount * degrees * Math.PI / 180);
      }
    }
    for (const [joint, degrees] of THUMB) {
      const bone = vrm.humanoid.getNormalizedBoneNode(side + 'Thumb' + joint);
      if (bone) bone.quaternion.setFromAxisAngle(thumbAxis, amount * degrees * Math.PI / 180);
    }
  }

  /**
   * Reaches an arm so its wrist is at `wrist`, the elbow toward `pole`, from where it is now by `share`
   * (0 leaves it, 1 all the way): the upper arm and the forearm turned by the least turns that do it.
   */
  function reach(upper, lower, hand, wrist, pole, share) {
    if (share <= 0) return;
    const s = worldPos(upper), e = worldPos(lower), w = worldPos(hand);
    const a = s.distanceTo(e), b = e.distanceTo(w);
    const target = w.clone().lerp(wrist, share);
    const d = target.clone().sub(s);
    const length = Math.min(a + b - 1e-4, Math.max(Math.abs(a - b) + 1e-4, d.length()));
    const way = d.normalize();
    const now = square(e.clone().sub(s), way, pole);
    const bend = square(now.clone().lerp(square(pole, way, now), share), way, pole);
    const x = (a * a - b * b + length * length) / (2 * length);
    const elbow = s.clone().addScaledVector(way, x).addScaledVector(bend, Math.sqrt(Math.max(0, a * a - x * x)));
    swing(upper, e.clone().sub(s), elbow.clone().sub(s));
    const e2 = worldPos(lower), w2 = worldPos(hand);
    swing(lower, w2.clone().sub(e2), s.clone().addScaledVector(way, length).sub(e2));
  }

  return {
    /**
     * Puts the mic in the character's hand for this frame: `which` is right, left or both, `up` how far it is at
     * the mouth. `head` is the measured head (clearance.js), or null. Call after the bones are posed.
     */
    hold(entry, which, up, head, size) {
      const vrm = entry.vrm, node = name => vrm.humanoid.getNormalizedBoneNode(name);
      if (!entry.mic) {
        entry.mic = { mesh: makeMic() };
        scene.add(entry.mic.mesh);
      }
      entry.mic.mesh.visible = vrm.scene.visible;
      const rest = restOf(vrm);
      vrm.scene.updateMatrixWorld(true);
      const main = which === 'left' ? 'left' : 'right';
      const headBone = node('head');
      const upper = node(main + 'UpperArm'), lower = node(main + 'LowerArm'), hand = node(main + 'Hand');
      if (!upper || !lower || !hand || !headBone) {
        entry.mic.mesh.visible = false;
        return;
      }
      const k = worldPos(lower).distanceTo(worldPos(hand)) / FOREARM;

      // The body's frame now, from the chest (or the hips): the way it faces, its up, its left.
      const trunk = node('upperChest') || node('chest') || node('spine') || node('hips');
      const restInverse = rest.clone().invert();
      const turned = (bone, v) => v.clone().applyQuaternion(restInverse).applyQuaternion(worldQuat(bone));
      const bodyFwd = turned(trunk, Z), bodyUp = turned(trunk, Y), bodyLeft = turned(trunk, X);

      // At the mouth: the head's own front and up, the mouth on the face (measured from the mesh when it could be:
      // a big head has its mouth farther out), the mic pointing up and back to the lips.
      const front = turned(headBone, Z), headUp = turned(headBone, Y);
      let mouth;
      if (head && head.mouth) {
        mouth = head.mouth.clone().multiplyScalar(size).applyQuaternion(worldQuat(headBone)).add(worldPos(headBone));
      } else {
        mouth = worldPos(headBone).addScaledVector(front, 0.09 * k).addScaledVector(headUp, 0.02 * k);
      }
      // Steep, so the fist is under the chin and the mouth is seen above the ball (at 35° from level the fist was in front
      // of the mouth; at 53°, held near the middle of the mic, in front of the chin and the lips).
      const micWay = headUp.clone().multiplyScalar(0.9).addScaledVector(front, -0.44).normalize();
      // The ball just before the lips, the fist down the handle from it (under the chin: the mic is the same size for
      // every character, and a fist sized to a short forearm came up over the chin, DESIGN.md 117.4).
      const ball = mouth.clone().addScaledVector(front, 0.035);
      const fist = ball.clone().addScaledVector(micWay, -TO_BALL);

      // A hand on the mic: the thumb along it, the fingers round it, reaching from its own side across and forward.
      const placeHand = (side, at, amount) => {
        const isLeft = side === 'left', out = isLeft ? 1 : -1;
        const u = node(side + 'UpperArm'), l = node(side + 'LowerArm'), h = node(side + 'Hand');
        if (!u || !l || !h) return;
        if (amount > 0) {
          const along = square(front.clone().multiplyScalar(0.6).addScaledVector(bodyLeft, -0.6 * out).addScaledVector(headUp, 0.3), micWay, front);
          const palm = along.clone().cross(micWay).multiplyScalar(isLeft ? 1 : -1);
          const wrist = at.clone().addScaledVector(along, -FIST_ALONG * k).addScaledVector(palm, -FIST_OUT * k);
          const pole = bodyUp.clone().negate().addScaledVector(bodyLeft, 0.6 * out).addScaledVector(bodyFwd, -0.2);
          reach(u, l, h, wrist, pole, amount);
        }
        // The hand: at rest the thumb (and the mic) as near up as it goes, the wrist straight; at the mouth along the mic.
        const forearm = worldPos(h).sub(worldPos(l)).normalize();
        // Up and a little forward: square to a forearm held a little down, straight up leaned the mic back to the chest.
        const restTurn = handTurn(rest, isLeft, forearm, square(bodyUp.clone().addScaledVector(bodyFwd, 0.5), forearm, bodyFwd));
        const along = square(front.clone().multiplyScalar(0.6).addScaledVector(bodyLeft, -0.6 * out).addScaledVector(headUp, 0.3), micWay, front);
        const upTurn = handTurn(rest, isLeft, along, micWay);
        setWorld(h, restTurn.slerp(upTurn, amount));
        closeHand(vrm, side, rest, side === main ? 1 : amount);
      };

      placeHand(main, fist, up);
      if (which === 'both') placeHand('left', fist.clone().addScaledVector(micWay, -LOWER_HAND), up);
      else if (entry.mic.helped) closeHand(vrm, 'left', rest, 0);
      entry.mic.helped = which === 'both';

      // The mic in the main fist, out on the thumb's side.
      const h = worldQuat(hand), isLeft = main === 'left';
      const along = (isLeft ? X.clone() : X.clone().negate()).applyQuaternion(restInverse).applyQuaternion(h);
      const thumb = Z.clone().applyQuaternion(restInverse).applyQuaternion(h);
      const palm = along.clone().cross(thumb).multiplyScalar(isLeft ? 1 : -1);
      const mesh = entry.mic.mesh;
      mesh.position.copy(worldPos(hand).addScaledVector(along, FIST_ALONG * k).addScaledVector(palm, FIST_OUT * k));
      mesh.quaternion.setFromUnitVectors(Y, thumb);
    },

    /** Takes the mic away (the dancer line no longer has one) and opens the hands. */
    drop(entry) {
      if (!entry.mic) return;
      scene.remove(entry.mic.mesh);
      entry.mic.mesh.traverse(o => { if (o.isMesh) { o.geometry.dispose(); o.material.dispose(); } });
      if (entry.vrm) {
        const rest = restOf(entry.vrm);
        closeHand(entry.vrm, 'left', rest, 0);
        closeHand(entry.vrm, 'right', rest, 0);
      }
      entry.mic = null;
    },
  };
}
