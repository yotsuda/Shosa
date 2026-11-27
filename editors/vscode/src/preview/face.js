// The face of a character (a `face` row, and the eyes' own blinking; DESIGN.md 131). The server sends, at every
// frame, how much of each of VRM's looks the face has and how far each eye is shut (`face`, Vocabulary.FaceShapes).
//
// A look moves the lids too: most characters' happy face narrows the eyes, and all of it shuts them. A wink or a
// blink put on top of that shuts a shut eye again, and the lid goes through the cheek (seen: smile(50).winkRight
// on a VRoid-made model). So a character is measured once: how much of an eye's own shutting each look already
// does, from the shapes themselves (the look's movement of the face laid along the blink's). An eye is then shut
// no further than what the looks leave of it. VRM has a switch for this on each look (overrideBlink), but it is
// all or nothing or missing, as the character's maker left it: measured, it is the same for every character, and
// the switch is put off.

/** VRM's expressions of the face, in the order the server sends their weights. */
export const FACE = ['happy', 'angry', 'sad', 'relaxed', 'surprised', 'blinkLeft', 'blinkRight'];
const LOOKS = 5;

/**
 * For each look, how much of the left eye's shutting and of the right's it does (0..1 each): [[l, r], …] in FACE's
 * order. Zeros for a character without the shapes (nothing is held back).
 */
export function measureLids(vrm) {
  const manager = vrm.expressionManager;
  const lids = FACE.slice(0, LOOKS).map(() => [0, 0]);
  if (!manager) return lids;
  const morphs = expression => (expression ? expression.binds : []).filter(b => b.primitives && Number.isInteger(b.index));
  for (let eye = 0; eye < 2; eye++) {
    const blink = morphs(manager.getExpression(FACE[LOOKS + eye]));
    for (let look = 0; look < LOOKS; look++) {
      const expression = manager.getExpression(FACE[look]);
      if (!expression) continue;
      let along = 0, whole = 0;
      for (const shut of blink) {
        for (const mesh of shut.primitives) {
          const moved = mesh.geometry.morphAttributes.position;
          const a = moved && moved[shut.index];
          if (!a) continue;
          const others = morphs(expression).filter(b => b.primitives.includes(mesh) && moved[b.index]);
          for (let i = 0; i < a.count; i++) {
            const x = a.getX(i) * shut.weight, y = a.getY(i) * shut.weight, z = a.getZ(i) * shut.weight;
            if (x === 0 && y === 0 && z === 0) continue;
            whole += x * x + y * y + z * z;
            for (const other of others) {
              const b = moved[other.index];
              along += (x * b.getX(i) + y * b.getY(i) + z * b.getZ(i)) * other.weight;
            }
          }
        }
      }
      lids[look][eye] = whole > 0 ? Math.min(1, Math.max(0, along / whole)) : 0;
    }
  }
  // Measured here: the character's own switch would take the blink away a second time.
  for (let look = 0; look < LOOKS; look++) {
    const expression = manager.getExpression(FACE[look]);
    if (expression) expression.overrideBlink = 'none';
  }
  return lids;
}

/** Puts a face on a character: `weights` in FACE's order, `lids` from measureLids. vrm.update applies it. */
export function setFace(vrm, lids, weights) {
  const manager = vrm.expressionManager;
  if (!manager) return;
  const shut = [0, 0];
  for (let look = 0; look < LOOKS; look++) {
    manager.setValue(FACE[look], weights[look]);
    shut[0] += lids[look][0] * weights[look];
    shut[1] += lids[look][1] * weights[look];
  }
  for (let eye = 0; eye < 2; eye++) {
    manager.setValue(FACE[LOOKS + eye], Math.min(weights[LOOKS + eye], Math.max(0, 1 - shut[eye])));
  }
}
