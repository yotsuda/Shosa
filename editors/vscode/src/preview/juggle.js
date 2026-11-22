// The balls (clubs, rings) a dancer juggles (`dancer alice humanoid juggles balls`, DESIGN.md 118). The server
// sends, at every frame, where each one is (`juggle: { prop, count, frames }`, seven numbers a ball: x y z, the hand
// it left and the hand it goes to — 0 right, 1 left, the same while held — how far through its flight, and a club's
// turn), worked out from the hands of Shosa's figure. A character's hands are not where the figure's are (a child,
// a two-heads-tall mini), so a ball is moved by how far the character's palm is from the figure's: in a hand, by
// that hand's; in the air, from the thrower's to the catcher's as the flight goes on. The arc itself is left as it
// is: how high a ball goes is the time it flies, whoever throws it.
import * as THREE from 'three';

/** A beanbag is about 68 mm across; a club 50 cm long; a ring 32 cm across. */
const BALL = 0.034, CLUB = 0.5, RING = 0.16;
/** From the wrist to where a ball sits: along the hand, and up out of the palm (as the server puts it). */
const PALM_ALONG = 0.05, PALM_UP = 0.04;
const COLORS = [0xe8552f, 0x2f8fe8, 0xf2c22e, 0x3fbf6a, 0xb25fe0, 0xf07fb0, 0x5fd0d0, 0xf09a3e, 0x9aa0a8];

function makeProp(prop, color) {
  const material = new THREE.MeshStandardMaterial({ color, roughness: 0.55, metalness: 0.05 });
  let mesh;
  if (prop === 'clubs') {
    // Along +y, its middle at 0: the knob and the handle below, the fat body above.
    mesh = new THREE.Group();
    const handle = new THREE.Mesh(new THREE.CylinderGeometry(0.013, 0.011, CLUB * 0.45, 12), material);
    handle.position.y = -CLUB * 0.25;
    const body = new THREE.Mesh(new THREE.CylinderGeometry(0.022, 0.042, CLUB * 0.55, 16), material);
    body.position.y = CLUB * 0.2;
    const top = new THREE.Mesh(new THREE.SphereGeometry(0.022, 12, 8), material);
    top.position.y = CLUB * 0.475;
    const knob = new THREE.Mesh(new THREE.SphereGeometry(0.018, 10, 8), material);
    knob.position.y = -CLUB * 0.475;
    mesh.add(handle, body, top, knob);
  } else if (prop === 'rings') {
    mesh = new THREE.Mesh(new THREE.TorusGeometry(RING, 0.012, 8, 40), material);
  } else {
    mesh = new THREE.Mesh(new THREE.SphereGeometry(BALL, 18, 12), material);
  }
  mesh.traverse(o => { if (o.isMesh) o.frustumCulled = false; });
  return mesh;
}

const worldQuat = node => node.getWorldQuaternion(new THREE.Quaternion());

/**
 * Turns a character's hands palm up, the fingers along the forearm and the thumbs out, by `weight` (0 leaves them
 * as posed, 1 all the way): a juggler's hands carry the balls on the palm. Shosa's figure has no turn of the hand,
 * and the character's hands, posed from its forearms, faced each other edge up. Call after the bones are posed.
 */
export function palmsUp(vrm, weight) {
  if (!(weight > 0)) return;
  const node = name => vrm.humanoid.getNormalizedBoneNode(name);
  vrm.scene.updateMatrixWorld(true);
  const rest = worldQuat(vrm.scene), restInverse = rest.clone().invert();
  const trunk = node('upperChest') || node('chest') || node('spine') || node('hips');
  if (!trunk) return;
  const X = new THREE.Vector3(1, 0, 0), Z = new THREE.Vector3(0, 0, 1);
  const bodyLeft = X.clone().applyQuaternion(restInverse).applyQuaternion(worldQuat(trunk));
  for (const side of ['left', 'right']) {
    const lower = node(side + 'LowerArm'), hand = node(side + 'Hand');
    if (!lower || !hand) continue;
    const isLeft = side === 'left';
    const along = hand.getWorldPosition(new THREE.Vector3()).sub(lower.getWorldPosition(new THREE.Vector3())).normalize();
    // The thumb out to its own side, square to the hand: with the fingers forward that is the palm up, on both hands.
    const thumb = bodyLeft.clone().multiplyScalar(isLeft ? 1 : -1);
    thumb.addScaledVector(along, -thumb.dot(along));
    if (thumb.lengthSq() < 1e-8) continue;
    thumb.normalize();
    // At rest (the T-pose, facing +z) a left hand points +x, a right one -x, and the thumbs point forward (+z) (as mic.js).
    const restAlong = isLeft ? X.clone() : X.clone().negate();
    const from = new THREE.Matrix4().makeBasis(restAlong, Z, restAlong.clone().cross(Z));
    const to = new THREE.Matrix4().makeBasis(along, thumb, along.clone().cross(thumb));
    const target = new THREE.Quaternion().setFromRotationMatrix(to.multiply(from.transpose())).multiply(rest);
    const turned = worldQuat(hand).slerp(target, Math.min(1, weight));
    hand.quaternion.copy(worldQuat(hand.parent).invert().multiply(turned));
    hand.updateMatrixWorld(true);
  }
}

export function createJuggle(scene) {
  const shown = new Map();   // figure index -> { prop, meshes }

  function drop(i) {
    const entry = shown.get(i);
    if (!entry) return;
    for (const mesh of entry.meshes) {
      scene.remove(mesh);
      mesh.traverse(o => { if (o.isMesh) { o.geometry.dispose(); o.material.dispose(); } });
    }
    shown.delete(i);
  }

  const p = new THREE.Vector3(), left = new THREE.Vector3(), fwd = new THREE.Vector3(), axis = new THREE.Vector3();
  const offsets = [new THREE.Vector3(), new THREE.Vector3()];
  const Y = new THREE.Vector3(0, 1, 0), Z = new THREE.Vector3(0, 0, 1);

  /** The figure's palm, as the server puts a ball in it: past the wrist along the forearm, and up. */
  function figurePalm(points, joints, side, out) {
    const wrist = points[joints.indexOf(side + 'Hand')], elbow = points[joints.indexOf(side + 'Elbow')];
    if (!wrist || !elbow) return out.set(0, 0, 0);
    return out.subVectors(wrist, elbow).normalize().multiplyScalar(PALM_ALONG).add(wrist).addScaledVector(Y, PALM_UP);
  }

  return {
    /**
     * Puts each figure's balls where they are at a frame. `figures` are the preview's ({ data, points }); `palms(i)`
     * is the character's palms [right, left] for a figure shown as its character, or null for one shown as sticks.
     */
    pose(clip, figures, frame, palms, visible) {
      figures.forEach((figure, i) => {
        const juggle = figure.data && figure.data.juggle;
        if (!juggle || !juggle.frames || !juggle.frames.length) {
          drop(i);
          return;
        }
        let entry = shown.get(i);
        if (!entry || entry.prop !== juggle.prop || entry.meshes.length !== juggle.count) {
          drop(i);
          entry = { prop: juggle.prop, meshes: [] };
          for (let b = 0; b < juggle.count; b++) {
            const mesh = makeProp(juggle.prop, COLORS[b % COLORS.length]);
            scene.add(mesh);
            entry.meshes.push(mesh);
          }
          shown.set(i, entry);
        }
        const frames = juggle.frames;
        const f0 = Math.max(0, Math.min(frames.length - 1, Math.floor(frame))), f1 = Math.min(frames.length - 1, f0 + 1), k = Math.max(0, frame - f0);
        const a = frames[f0], c = frames[f1];

        // The character's palm from the figure's, for each hand (none for sticks).
        const own = palms ? palms(i) : null;
        offsets.forEach((o, hand) => {
          if (own && own[hand]) o.copy(own[hand]).sub(figurePalm(figure.points, clip.joints, hand === 0 ? 'right' : 'left', p));
          else o.set(0, 0, 0);
        });
        // The dancer's left and front, from its shoulders: a club turns end over end about the left; a ring faces the side.
        const ls = figure.points[clip.joints.indexOf('leftShoulder')], rs = figure.points[clip.joints.indexOf('rightShoulder')];
        if (ls && rs) left.subVectors(ls, rs).setY(0).normalize(); else left.set(1, 0, 0);
        fwd.crossVectors(left, Y).normalize();

        entry.meshes.forEach((mesh, b) => {
          mesh.visible = visible;
          const j = b * 7;
          // A ball that changes between the two frames (caught, thrown) is taken from the nearer one, not blended.
          const sameState = a[j + 3] === c[j + 3] && a[j + 4] === c[j + 4] && c[j + 5] >= a[j + 5];
          const src = sameState || k < 0.5 ? a : c, t = sameState ? k : 0;
          p.set(a[j] + (c[j] - a[j]) * (sameState ? k : (k < 0.5 ? 0 : 1)),
                a[j + 1] + (c[j + 1] - a[j + 1]) * (sameState ? k : (k < 0.5 ? 0 : 1)),
                a[j + 2] + (c[j + 2] - a[j + 2]) * (sameState ? k : (k < 0.5 ? 0 : 1)));
          const from = src[j + 3] | 0, to = src[j + 4] | 0;
          const u = src[j + 5] + ((sameState ? c[j + 5] : src[j + 5]) - src[j + 5]) * t;
          p.add(offsets[from].clone().lerp(offsets[to], u));
          mesh.position.copy(p);
          if (entry.prop === 'clubs') {
            // Held, it points forward and up out of the fist; thrown, it turns end over end, the knob toward the hand.
            const spin = src[j + 6] + ((sameState ? c[j + 6] : src[j + 6]) - src[j + 6]) * t;
            const way = fwd.clone().multiplyScalar(Math.cos(Math.PI / 3)).addScaledVector(Y, Math.sin(Math.PI / 3));
            axis.copy(left);
            way.applyAxisAngle(axis, -spin);
            mesh.quaternion.setFromUnitVectors(Y, way.normalize());
            // The middle of a held club is ahead of the fist, not in it.
            if (from === to) mesh.position.addScaledVector(way, CLUB * 0.22);
          } else if (entry.prop === 'rings') {
            // Thrown along the forearms: the ring in the plane of up and the front, its face to the side, its edge to
            // the audience (the user: face to the front, it was a quarter turn out as seen from above).
            mesh.quaternion.setFromUnitVectors(Z, left);
            if (from === to) mesh.position.addScaledVector(Y, RING * 0.8);
          }
        });
      });
      for (const i of [...shown.keys()]) if (i >= figures.length) drop(i);
    },

    clear() {
      for (const i of [...shown.keys()]) drop(i);
    },
  };
}
