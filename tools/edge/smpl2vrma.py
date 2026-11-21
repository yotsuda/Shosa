r"""Turn EDGE's SMPL motion (motion.pkl from gen.py) into a VRM Animation (.vrma) for three-vrm.

  venv\Scripts\python.exe smpl2vrma.py runs\chorus\motion.pkl runs\chorus\edge.vrma [--x 0.6] [--mirror]

EDGE writes SMPL in a z-up frame (it turned AIST++'s y-up root by +90 degrees about x). SMPL's rest
pose is a T-pose, y up, facing +z, its left at +x: the same as a VRM 1.0 humanoid and the same as
what `shosa vrma` writes (no rest rotations, offsets as translations), so the local rotations go
across as they are once the root is turned back.
"""
import argparse
import json
import pickle
import struct
import sys
from pathlib import Path

import numpy as np
from scipy.spatial.transform import Rotation as R

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "out" / "edge" / "EDGE"))
from vis import smpl_offsets, smpl_parents  # noqa: E402

FPS = 30
VRM_NAMES = [
    "hips", "leftUpperLeg", "rightUpperLeg", "spine", "leftLowerLeg", "rightLowerLeg",
    "chest", "leftFoot", "rightFoot", "upperChest", "leftToes", "rightToes",
    "neck", "leftShoulder", "rightShoulder", "head", "leftUpperArm", "rightUpperArm",
    "leftLowerArm", "rightLowerArm", "leftHand", "rightHand", None, None,
]
# SMPL's mirror: left <-> right joints
MIRROR = [0, 2, 1, 3, 5, 4, 6, 8, 7, 9, 11, 10, 12, 14, 13, 15, 17, 16, 19, 18, 21, 20, 23, 22]


def load(path):
    m = pickle.load(open(path, "rb"))
    q = np.asarray(m["smpl_poses"], dtype=np.float64).reshape(-1, 24, 3)
    trans = np.asarray(m["smpl_trans"], dtype=np.float64)
    joints = np.asarray(m["full_pose"], dtype=np.float64)  # frames x 24 x 3, z up
    return q, trans, joints


def to_y_up(q, trans, joints):
    fix = R.from_euler("x", -90, degrees=True)
    root = fix * R.from_rotvec(q[:, 0])
    rots = [root] + [R.from_rotvec(q[:, j]) for j in range(1, 24)]
    pos = np.stack([trans[:, 0], trans[:, 2], -trans[:, 1]], axis=1)
    height = joints[..., 2]
    # the sole: toe joints sit about 2 cm above it, ankle joints about 7.5 cm
    sole = np.minimum(height[:, [10, 11]].min(axis=1) - 0.02, height[:, [7, 8]].min(axis=1) - 0.075)
    pos[:, 1] -= np.percentile(sole, 5)
    pos[:, [0, 2]] -= pos[0, [0, 2]]
    return rots, pos


def mirror(rots, pos):
    # reflect x: a rotation (x, y, z, w) becomes (x, -y, -z, w); left and right swap
    out = []
    for j in range(24):
        quat = rots[MIRROR[j]].as_quat().copy()
        quat[:, 1] *= -1
        quat[:, 2] *= -1
        out.append(R.from_quat(quat))
    pos = pos.copy()
    pos[:, 0] *= -1
    return out, pos


def write_vrma(rots, pos, path):
    frames = len(pos)
    rest_hips = float(-(smpl_offsets[1][1] + smpl_offsets[4][1] + smpl_offsets[7][1] + smpl_offsets[10][1]) + 0.02)

    blob = bytearray()
    views, accessors = [], []

    def add(data, kind, minmax=False):
        data = np.ascontiguousarray(data, dtype=np.float32)
        while len(blob) % 4:
            blob.append(0)
        offset = len(blob)
        blob.extend(data.tobytes())
        views.append({"buffer": 0, "byteOffset": offset, "byteLength": data.nbytes})
        acc = {"bufferView": len(views) - 1, "componentType": 5126, "count": len(data), "type": kind}
        if minmax:
            acc["min"] = [float(data.min())]
            acc["max"] = [float(data.max())]
        accessors.append(acc)
        return len(accessors) - 1

    time = add(np.arange(frames) / FPS, "SCALAR", minmax=True)

    nodes, human = [], {}
    for j in range(24):
        node = {"name": VRM_NAMES[j] or ("leftHandEnd" if j == 22 else "rightHandEnd")}
        off = smpl_offsets[j]
        node["translation"] = [0.0, rest_hips, 0.0] if j == 0 else [float(v) for v in off]
        kids = [k for k in range(24) if smpl_parents[k] == j]
        if kids:
            node["children"] = kids
        nodes.append(node)
        if VRM_NAMES[j]:
            human[VRM_NAMES[j]] = {"node": j}

    samplers, channels = [], []
    for j in range(22):
        quat = rots[j].as_quat()  # x, y, z, w as glTF wants
        # keep each quaternion on the same side as the last, so interpolation takes the short way
        for f in range(1, frames):
            if np.dot(quat[f], quat[f - 1]) < 0:
                quat[f] = -quat[f]
        samplers.append({"input": time, "output": add(quat, "VEC4"), "interpolation": "LINEAR"})
        channels.append({"sampler": len(samplers) - 1, "target": {"node": j, "path": "rotation"}})
    samplers.append({"input": time, "output": add(pos, "VEC3"), "interpolation": "LINEAR"})
    channels.append({"sampler": len(samplers) - 1, "target": {"node": 0, "path": "translation"}})

    while len(blob) % 4:
        blob.append(0)
    gltf = {
        "asset": {"version": "2.0", "generator": "Shosa smpl2vrma (EDGE)"},
        "extensionsUsed": ["VRMC_vrm_animation"],
        "extensions": {"VRMC_vrm_animation": {"specVersion": "1.0", "humanoid": {"humanBones": human}}},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": nodes,
        "animations": [{"name": "EDGE", "samplers": samplers, "channels": channels}],
        "buffers": [{"byteLength": len(blob)}],
        "bufferViews": views,
        "accessors": accessors,
    }
    text = json.dumps(gltf, separators=(",", ":")).encode()
    text += b" " * (-len(text) % 4)
    total = 12 + 8 + len(text) + 8 + len(blob)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, total))
        f.write(struct.pack("<II", len(text), 0x4E4F534A) + text)
        f.write(struct.pack("<II", len(blob), 0x004E4942) + bytes(blob))


def main():
    p = argparse.ArgumentParser()
    p.add_argument("pkl")
    p.add_argument("vrma")
    p.add_argument("--x", type=float, default=0.0, help="where on the floor it starts (metres, +x is the dancer's left)")
    p.add_argument("--mirror", action="store_true")
    a = p.parse_args()
    q, trans, joints = load(a.pkl)
    rots, pos = to_y_up(q, trans, joints)
    if a.mirror:
        rots, pos = mirror(rots, pos)
    pos[:, 0] += a.x
    write_vrma(rots, pos, a.vrma)
    print(f"{len(pos)} frames, hips height {pos[:,1].min():.2f}-{pos[:,1].max():.2f} m, "
          f"travel x {pos[:,0].min():.2f}..{pos[:,0].max():.2f}, z {pos[:,2].min():.2f}..{pos[:,2].max():.2f} -> {a.vrma}")


if __name__ == "__main__":
    main()
