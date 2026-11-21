r"""Read a dancer's .vrma written by `shosa vrma` as SMPL joint rotations, in EDGE's frame.

Shosa's skeleton and SMPL's both rest in a T-pose, y up, facing +z, with no rest rotations, so a
bone's local rotation means the same in both. Shosa has fewer bones: SMPL's middle spine (6), toes
(10, 11) and hand ends (22, 23) stay unturned. EDGE turns the root by +90 degrees about x (z up).

  python -I shosa2smpl.py out\edge\shosa\song.zundamon.vrma --start 22.5 --seconds 15
"""
import json
import struct

import numpy as np
from scipy.spatial.transform import Rotation as R

FPS = 30
# SMPL joint -> Shosa bone
SMPL_FROM_SHOSA = {
    0: "hips", 1: "leftUpperLeg", 2: "rightUpperLeg", 3: "spine", 4: "leftLowerLeg", 5: "rightLowerLeg",
    7: "leftFoot", 8: "rightFoot", 9: "chest", 12: "neck", 13: "leftShoulder", 14: "rightShoulder",
    15: "head", 16: "leftUpperArm", 17: "rightUpperArm", 18: "leftLowerArm", 19: "rightLowerArm",
    20: "leftHand", 21: "rightHand",
}
UPPER = [0, 3, 6, 9, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21]
ARMS = [13, 14, 16, 17, 18, 19, 20, 21]


def read_glb(path):
    b = open(path, "rb").read()
    n = struct.unpack_from("<I", b, 12)[0]
    g = json.loads(b[20 : 20 + n])
    off = 20 + n
    bn = struct.unpack_from("<I", b, off)[0]
    return g, b[off + 8 : off + 8 + bn]


def accessor(g, blob, i):
    a = g["accessors"][i]
    v = g["bufferViews"][a["bufferView"]]
    width = {"SCALAR": 1, "VEC3": 3, "VEC4": 4}[a["type"]]
    start = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    return np.frombuffer(blob, dtype=np.float32, count=a["count"] * width, offset=start).reshape(a["count"], width)


def load(path, start=0.0, seconds=None):
    """Returns (local axis-angle: frames x 24 x 3, root position: frames x 3), EDGE's z-up frame, 30 fps."""
    g, blob = read_glb(path)
    bones = {k: v["node"] for k, v in g["extensions"]["VRMC_vrm_animation"]["humanoid"]["humanBones"].items()}
    anim = g["animations"][0]
    rot, hips = {}, None
    time = None
    for ch in anim["channels"]:
        s = anim["samplers"][ch["sampler"]]
        if time is None:
            time = accessor(g, blob, s["input"])[:, 0]
        node, kind = ch["target"]["node"], ch["target"]["path"]
        if kind == "rotation":
            rot[node] = accessor(g, blob, s["output"])
        elif kind == "translation" and node == bones["hips"]:
            hips = accessor(g, blob, s["output"])
    rate = round(1 / (time[1] - time[0]))
    assert rate == FPS, f"{path} is at {rate} fps"
    first = int(round(start * FPS))
    count = len(time) - first if seconds is None else int(round(seconds * FPS))
    sl = slice(first, first + count)

    q = np.zeros((count, 24, 3))
    for j, name in SMPL_FROM_SHOSA.items():
        q[:, j] = R.from_quat(rot[bones[name]][sl]).as_rotvec()
    fix = R.from_euler("x", 90, degrees=True)
    q[:, 0] = (fix * R.from_rotvec(q[:, 0])).as_rotvec()
    y_up = hips[sl].astype(np.float64)
    pos = np.stack([y_up[:, 0], -y_up[:, 2], y_up[:, 1]], axis=1)
    return q, pos


if __name__ == "__main__":
    import argparse

    p = argparse.ArgumentParser()
    p.add_argument("vrma")
    p.add_argument("--start", type=float, default=0)
    p.add_argument("--seconds", type=float)
    a = p.parse_args()
    q, pos = load(a.vrma, a.start, a.seconds)
    print(q.shape, "hips height %.2f-%.2f" % (pos[:, 2].min(), pos[:, 2].max()))
