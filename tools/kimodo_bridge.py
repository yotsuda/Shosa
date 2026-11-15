"""Shosa -> Kimodo and back: an experiment (DESIGN.md 25).

Kimodo (NVIDIA) generates human motion from a text and constraints. Shosa knows the key poses
of a clip and when each is reached. This asks Kimodo to fill what is between them:

  export   a solved clip (`shosa clip FILE --score NAME -o clip.json`) -> a folder Kimodo's
           generator takes (`kimodo_gen --input_folder DIR`): constraints.json and meta.json.
           The poses where a word of the source arrives, and the marks (`!hit`), become
           full-body key frames; the path of the hips becomes the root path.
  compare  what Kimodo made (an .npz) against what was asked: how far each hand, foot and
           the head are from their targets at the key frames, and a picture of both.

Run with the Python that has Kimodo installed (it reads Kimodo's skeleton):
  python tools/kimodo_bridge.py export out/kimodo/combo.clip.json -o out/kimodo/combo --text "A person ..."
  kimodo_gen --input_folder out/kimodo/combo --output out/kimodo/combo/generated --bvh
  python tools/kimodo_bridge.py compare out/kimodo/combo out/kimodo/combo/generated.npz

Kimodo's body is SOMA: 30 joints, a T-pose at rest, x to its left, y up, z forward — Shosa's
axes. Its key frames are joint POSITIONS on that body, written as joint rotations (Kimodo runs
them through its own forward kinematics), so a Shosa pose is turned into the rotations that
point each SOMA bone the way the Shosa bone points. Bone lengths differ a little; directions
are what is kept, and the hips are set at the height that puts the lower foot on the floor.
"""

import argparse
import json
import math
import os
import warnings

import numpy as np
from scipy.spatial.transform import Rotation

warnings.filterwarnings("ignore")

SOMA30 = [
    ("Hips", None), ("Spine1", "Hips"), ("Spine2", "Spine1"), ("Chest", "Spine2"), ("Neck1", "Chest"),
    ("Neck2", "Neck1"), ("Head", "Neck2"), ("Jaw", "Head"), ("LeftEye", "Head"), ("RightEye", "Head"),
    ("LeftShoulder", "Chest"), ("LeftArm", "LeftShoulder"), ("LeftForeArm", "LeftArm"), ("LeftHand", "LeftForeArm"),
    ("LeftHandThumbEnd", "LeftHand"), ("LeftHandMiddleEnd", "LeftHand"),
    ("RightShoulder", "Chest"), ("RightArm", "RightShoulder"), ("RightForeArm", "RightArm"), ("RightHand", "RightForeArm"),
    ("RightHandThumbEnd", "RightHand"), ("RightHandMiddleEnd", "RightHand"),
    ("LeftLeg", "Hips"), ("LeftShin", "LeftLeg"), ("LeftFoot", "LeftShin"), ("LeftToeBase", "LeftFoot"),
    ("RightLeg", "Hips"), ("RightShin", "RightLeg"), ("RightFoot", "RightShin"), ("RightToeBase", "RightFoot"),
]
NAMES = [n for n, _ in SOMA30]
INDEX = {n: i for i, n in enumerate(NAMES)}
PARENT = [-1 if p is None else INDEX[p] for _, p in SOMA30]

# Shosa's joints (Rig.cs), and the SOMA joint each is compared with.
SHOSA = ["pelvis", "waist", "chest", "head", "nose", "leftShoulder", "leftElbow", "leftHand", "rightShoulder", "rightElbow", "rightHand",
         "leftHip", "leftKnee", "leftAnkle", "leftToe", "rightHip", "rightKnee", "rightAnkle", "rightToe"]
S = {n: i for i, n in enumerate(SHOSA)}
SAME = {"pelvis": "Hips", "chest": "Neck1", "leftShoulder": "LeftArm", "leftElbow": "LeftForeArm", "leftHand": "LeftHand",
        "rightShoulder": "RightArm", "rightElbow": "RightForeArm", "rightHand": "RightHand",
        "leftHip": "LeftLeg", "leftKnee": "LeftShin", "leftAnkle": "LeftFoot", "leftToe": "LeftToeBase",
        "rightHip": "RightLeg", "rightKnee": "RightShin", "rightAnkle": "RightFoot", "rightToe": "RightToeBase"}
TOE_ON_FLOOR = 0.015   # the height of SOMA's toe joint when the foot stands


def soma_rest():
    """The 30 joints at rest, metres, the hips at the origin."""
    import torch
    from kimodo.assets import skeleton_asset_path
    return torch.load(os.path.join(str(skeleton_asset_path("somaskel30")), "joints.p")).squeeze().double().numpy()


def unit(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v * 0.0


def basis(primary, secondary):
    """A rotation whose first axis is `primary` and whose second is `secondary`, made square to it."""
    x = unit(primary)
    y = unit(secondary - x * np.dot(secondary, x))
    return np.stack([x, y, np.cross(x, y)], axis=1)


def best(*candidates):
    """The first of the candidates that is a direction at all (the others are fallbacks)."""
    for c in candidates:
        if np.linalg.norm(c) > 1e-3:
            return unit(c)
    return np.array([0.0, 0.0, 1.0])


def retarget(pose, rest):
    """A Shosa pose (18 joints, metres) as SOMA: global rotation matrices [30, 3, 3]."""
    p = {n: pose[i] for n, i in S.items()}
    r = {n: rest[i] for n, i in INDEX.items()}
    up = unit(p["chest"] - p["pelvis"])
    hips_line = unit(p["leftHip"] - p["rightHip"])
    shoulders_line = unit(p["leftShoulder"] - p["rightShoulder"])
    forward = unit(np.cross(shoulders_line, up))
    R = [None] * 30

    def aim(joint, rest_primary, rest_secondary, primary, secondary):
        R[INDEX[joint]] = basis(primary, secondary) @ basis(rest_primary, rest_secondary).T

    # The trunk: every bone along the spine, turning from the line of the hips to that of the shoulders.
    rest_up, rest_left = unit(r["Neck1"] - r["Hips"]), np.array([1.0, 0.0, 0.0])
    for joint, share in (("Hips", 0.0), ("Spine1", 1 / 3), ("Spine2", 2 / 3), ("Chest", 1.0)):
        aim(joint, rest_up, rest_left, up, hips_line * (1 - share) + shoulders_line * share)
    neck = unit(p["head"] - p["chest"])
    for joint in ("Neck1", "Neck2"):
        aim(joint, unit(r["Head"] - r["Neck1"]), rest_left, neck, shoulders_line)
    aim("Head", np.array([0.0, 1.0, 0.0]), np.array([0.0, 0.0, 1.0]), neck, best(p["nose"] - p["head"], forward))
    for joint in ("Jaw", "LeftEye", "RightEye"):
        R[INDEX[joint]] = R[INDEX["Head"]]

    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        s = side.lower()
        R[INDEX[side + "Shoulder"]] = R[INDEX["Chest"]]
        # An elbow is a hinge: its axis is square to the upper arm and the forearm. At rest the
        # arm lies along x and bends forward; a straight arm takes the axis the trunk suggests.
        upper, fore = unit(p[s + "Elbow"] - p[s + "Shoulder"]), unit(p[s + "Hand"] - p[s + "Elbow"])
        rest_arm = np.array([sign, 0.0, 0.0])
        rest_axis = np.cross(rest_arm, np.array([0.0, 0.0, 1.0]))
        axis = best(np.cross(upper, fore), np.cross(upper, forward), np.cross(upper, up * -sign))
        aim(side + "Arm", rest_arm, rest_axis, upper, axis)
        aim(side + "ForeArm", rest_arm, rest_axis, fore, axis)
        for joint in ("Hand", "HandThumbEnd", "HandMiddleEnd"):
            R[INDEX[side + joint]] = R[INDEX[side + "ForeArm"]]

        # A knee bends the shin backward: at rest the leg hangs along -y and the axis is x.
        thigh, shin = unit(p[s + "Knee"] - p[s + "Hip"]), unit(p[s + "Ankle"] - p[s + "Knee"])
        rest_leg = unit(r[side + "Shin"] - r[side + "Leg"])
        axis = best(np.cross(thigh, shin), hips_line)
        aim(side + "Leg", rest_leg, rest_left, thigh, axis)
        aim(side + "Shin", unit(r[side + "Foot"] - r[side + "Shin"]), rest_left, shin, axis)
        foot = best(p[s + "Toe"] - p[s + "Ankle"], forward)
        aim(side + "Foot", unit(r[side + "ToeBase"] - r[side + "Foot"]), rest_left, foot, axis)
        R[INDEX[side + "ToeBase"]] = R[INDEX[side + "Foot"]]
    return np.stack(R)


def fk(global_rots, rest, root):
    """Joint positions [30, 3] from global rotations and where the hips are."""
    out = np.zeros((30, 3))
    out[0] = root
    for j in range(1, 30):
        out[j] = out[PARENT[j]] + global_rots[PARENT[j]] @ (rest[j] - rest[PARENT[j]])
    return out


def local_rotvecs(global_rots):
    out = np.zeros((30, 3))
    for j in range(30):
        local = global_rots[j] if PARENT[j] < 0 else global_rots[PARENT[j]].T @ global_rots[j]
        out[j] = Rotation.from_matrix(local).as_rotvec()
    return out


def soma_pose(pose, rest, origin, floor_scale):
    """A Shosa pose on the SOMA body: (global rotations, joint positions)."""
    R = retarget(pose, rest)
    at_origin = fk(R, rest, np.zeros(3))
    # Over the floor as in Shosa, in SOMA's stride; as high as puts the lower toe where Shosa's is.
    xz = (pose[S["pelvis"]] - origin) * floor_scale
    lowest = min(pose[S["leftToe"]][1], pose[S["rightToe"]][1])
    y = TOE_ON_FLOOR + max(0.0, lowest) - min(at_origin[INDEX["LeftToeBase"]][1], at_origin[INDEX["RightToeBase"]][1])
    root = np.array([xz[0], y, xz[2]])
    return R, at_origin + root


def read_clip(path, figure=0):
    with open(path, encoding="utf-8") as f:
        clip = json.load(f)
    frames = np.array(clip["figures"][figure]["frames"], dtype=float).reshape(-1, len(SHOSA), 3)
    # Kimodo's clips begin facing +z (its heading is the line of the hips, turned a quarter): the
    # whole clip is turned about the hips of its first frame until it begins so.
    left = frames[0][S["leftHip"]] - frames[0][S["rightHip"]]
    forward = np.cross(left, np.array([0.0, 1.0, 0.0]))
    yaw = math.atan2(forward[0], forward[2])
    c, s = math.cos(-yaw), math.sin(-yaw)
    turn = np.array([[c, 0.0, s], [0.0, 1.0, 0.0], [-s, 0.0, c]])
    pivot = frames[0][S["pelvis"]] * np.array([1.0, 0.0, 1.0])
    frames = (frames - pivot) @ turn.T + pivot
    clip["turned_degrees"] = -math.degrees(yaw)
    return clip, frames


def seconds_at(clip, beat):
    tempos = clip.get("tempos") or [{"beat": 0, "bpm": clip["tempo"]}]
    seconds = 0.0
    for i, t in enumerate(tempos):
        nxt = tempos[i + 1]["beat"] if i + 1 < len(tempos) else math.inf
        seconds += (min(beat, nxt) - t["beat"]) * 60 / t["bpm"]
        if beat <= nxt:
            break
    return seconds


def key_frames(clip, count, limit):
    """The frames a word of the source arrives at, and the marks; thinned evenly to `limit`."""
    figure = clip["figures"][0]["id"]
    beats = {s["t1"] for s in clip["spans"] if s["figure"] == figure} | {m["beat"] for m in clip.get("marks", []) if m["figure"] == figure}
    frames = sorted({min(count - 1, max(0, round(seconds_at(clip, b) * clip["fps"]))) for b in beats} | {0})
    if len(frames) > limit:
        keep = {round(i * (len(frames) - 1) / (limit - 1)) for i in range(limit)}
        frames = [f for i, f in enumerate(frames) if i in keep]
    return frames


def export(args):
    clip, frames = read_clip(args.clip)
    if clip["fps"] != 30:
        raise SystemExit(f"Kimodo runs at 30 frames a second; this clip has {clip['fps']} (write `fps 30`).")
    if len(frames) > 300:
        raise SystemExit(f"Kimodo makes 10 seconds at most (300 frames); this clip has {len(frames)}.")
    rest = soma_rest()
    shosa_leg = np.linalg.norm(frames[0][S["leftKnee"]] - frames[0][S["leftHip"]]) + np.linalg.norm(frames[0][S["leftAnkle"]] - frames[0][S["leftKnee"]])
    soma_leg = np.linalg.norm(rest[INDEX["LeftShin"]] - rest[INDEX["LeftLeg"]]) + np.linalg.norm(rest[INDEX["LeftFoot"]] - rest[INDEX["LeftShin"]])
    floor_scale = soma_leg / shosa_leg
    origin = frames[0][S["pelvis"]] * np.array([1.0, 0.0, 1.0])

    keys = key_frames(clip, len(frames), args.keys)
    rots, roots, worst = [], [], 0.0
    for f in keys:
        R, joints = soma_pose(frames[f], rest, origin, floor_scale)
        rots.append(local_rotvecs(R).tolist())
        roots.append(joints[0].tolist())
    # The hips over the floor at every frame: the one constraint Kimodo is trained to take densely.
    path = [((frames[f][S["pelvis"]] - origin) * floor_scale)[[0, 2]].tolist() for f in range(len(frames))]

    constraints = [
        {"type": "fullbody", "frame_indices": keys, "local_joints_rot": rots, "root_positions": roots},
        {"type": "root2d", "frame_indices": list(range(len(frames))), "smooth_root_2d": path},
    ]
    if args.no_path:
        constraints.pop()
    os.makedirs(args.output, exist_ok=True)
    with open(os.path.join(args.output, "constraints.json"), "w", encoding="utf-8") as f:
        json.dump(constraints, f)
    meta = {"text": args.text, "duration": len(frames) / 30, "num_samples": 1, "seed": args.seed, "diffusion_steps": 100,
            "cfg": {"enabled": True, "text_weight": args.text_weight, "constraint_weight": args.constraint_weight}}
    with open(os.path.join(args.output, "meta.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)
    with open(os.path.join(args.output, "shosa.json"), "w", encoding="utf-8") as f:
        json.dump({"clip": os.path.abspath(args.clip), "keys": keys, "floor_scale": floor_scale, "origin": origin.tolist()}, f, indent=2)
    print(f"{args.output}: {len(frames)} frames ({len(frames) / 30:.2f} s), {len(keys)} key frames at {keys}")
    print(f"  SOMA's leg is {floor_scale:.3f} of Shosa's: the path over the floor is scaled by that")
    print(f"  turned {clip['turned_degrees']:.0f} degrees so that the hips of the first frame face +z, as Kimodo's clips begin")
    print(f"  kimodo_gen --input_folder {args.output} --output {os.path.join(args.output, 'generated')} --bvh")
    if args.picture:
        picture(args.output, clip, frames, keys, rest, origin, floor_scale, None)


# ---- Looking at it.

BONES = [(a, b) for b, a in enumerate(PARENT) if a >= 0 and NAMES[b] not in ("Jaw", "LeftEye", "RightEye")]
# Shosa's bones (Rig.Bones), by the indices of SHOSA: the trunk through the waist, the shoulders from the chest.
SHOSA_BONES = [(0, 1), (1, 2), (2, 3), (3, 4), (2, 5), (5, 6), (6, 7), (2, 8), (8, 9), (9, 10), (0, 11), (11, 12), (12, 13), (13, 14), (0, 15), (15, 16), (16, 17), (17, 18)]


def picture(folder, clip, frames, keys, rest, origin, floor_scale, generated):
    """One cell per key frame, seen from the front-left like Shosa's sheet: Shosa's pose in grey,
    the pose asked of Kimodo in thin colour, and what Kimodo made (if given) in thick colour."""
    from PIL import Image, ImageDraw
    cell_w, cell_h, scale, columns = 230, 300, 115, min(8, len(keys))
    rows = (len(keys) + columns - 1) // columns
    image = Image.new("RGB", (columns * cell_w, rows * cell_h), "white")
    draw = ImageDraw.Draw(image)
    yaw = math.radians(40)

    def project(p, centre, ox, floor):
        q = p - centre
        right = q[0] * math.cos(yaw) - q[2] * math.sin(yaw)
        depth = -q[0] * math.sin(yaw) - q[2] * math.cos(yaw)
        return ox + right * scale, floor - (q[1] * math.cos(math.radians(12)) + depth * math.sin(math.radians(12))) * scale

    def side_colour(name):
        return (47, 111, 223) if name.startswith(("Left", "left")) else (217, 72, 43) if name.startswith(("Right", "right")) else (48, 52, 58)

    for i, f in enumerate(keys):
        ox, floor = (i % columns) * cell_w + cell_w / 2, (i // columns) * cell_h + cell_h - 30
        draw.line([(ox - cell_w / 2 + 8, floor), (ox + cell_w / 2 - 8, floor)], fill=(216, 220, 226))
        draw.text((ox - cell_w / 2 + 8, floor - cell_h + 36), f"frame {f}  ({f / 30:.2f} s)", fill=(48, 52, 58))
        _, asked = soma_pose(frames[f], rest, origin, floor_scale)
        centre = np.array([asked[0][0], 0.0, asked[0][2]])
        shosa = (frames[f] - origin) * np.array([floor_scale, 1.0, floor_scale])
        for a, b in SHOSA_BONES:
            draw.line([project(shosa[a], centre, ox, floor), project(shosa[b], centre, ox, floor)], fill=(200, 204, 210), width=5)
        for a, b in BONES:
            draw.line([project(asked[a], centre, ox, floor), project(asked[b], centre, ox, floor)], fill=side_colour(NAMES[b]), width=1 if generated is not None else 2)
        if generated is not None:
            made = generated[f]
            for a, b in BONES:
                draw.line([project(made[a], centre, ox, floor), project(made[b], centre, ox, floor)], fill=side_colour(NAMES[b]), width=3)
    path = os.path.join(folder, "compare.png" if generated is not None else "asked.png")
    image.save(path)
    print(f"  {path}")


def compare(args):
    with open(os.path.join(args.folder, "shosa.json"), encoding="utf-8") as f:
        info = json.load(f)
    clip, frames = read_clip(info["clip"])
    rest, origin, floor_scale, keys = soma_rest(), np.array(info["origin"]), info["floor_scale"], info["keys"]
    data = np.load(args.npz)
    posed = data["posed_joints"]
    if posed.ndim == 4:
        posed = posed[0]
    if posed.shape[1] == 77:
        from kimodo.skeleton import SOMASkeleton77
        names77 = SOMASkeleton77().bone_order_names
        posed = posed[:, [names77.index(n) for n in NAMES]]
    print(f"{args.npz}: {posed.shape[0]} frames, {posed.shape[1]} joints; asked for {len(frames)} frames")

    watched = ["LeftHand", "RightHand", "LeftFoot", "RightFoot", "Head", "Hips"]
    print("  how far from what was asked, in cm (key frames):")
    print("  frame  " + "  ".join(f"{n:>9}" for n in watched))
    worst = {n: 0.0 for n in watched}
    for f in keys:
        if f >= len(posed):
            continue
        _, asked = soma_pose(frames[f], rest, origin, floor_scale)
        d = {n: float(np.linalg.norm(posed[f][INDEX[n]] - asked[INDEX[n]])) * 100 for n in watched}
        worst = {n: max(worst[n], d[n]) for n in watched}
        print(f"  {f:5d}  " + "  ".join(f"{d[n]:9.1f}" for n in watched))
    print("  worst  " + "  ".join(f"{worst[n]:9.1f}" for n in watched))

    # Between the key frames nothing was asked but the path: how far Kimodo's hands are from Shosa's there.
    between = [f for f in range(min(len(frames), len(posed))) if f not in keys]
    if between:
        gaps = []
        for f in between:
            _, asked = soma_pose(frames[f], rest, origin, floor_scale)
            gaps.append([np.linalg.norm(posed[f][INDEX[n]] - asked[INDEX[n]]) * 100 for n in ("LeftHand", "RightHand", "LeftFoot", "RightFoot")])
        gaps = np.array(gaps)
        print(f"  between the key frames, against Shosa's own movement (cm, mean / worst): hands {gaps[:, :2].mean():.1f} / {gaps[:, :2].max():.1f}, feet {gaps[:, 2:].mean():.1f} / {gaps[:, 2:].max():.1f}")
    if "foot_contacts" in data:
        contacts = data["foot_contacts"]
        contacts = contacts[0] if contacts.ndim == 3 else contacts
        print(f"  Kimodo says a foot is on the floor in {100 * float((contacts > 0.5).any(axis=1).mean()):.0f}% of the frames")
    picture(args.folder, clip, frames, [f for f in keys if f < len(posed)], rest, origin, floor_scale, posed)


def main():
    parser = argparse.ArgumentParser(description="Shosa -> Kimodo constraints, and a comparison of what comes back.")
    commands = parser.add_subparsers(dest="command", required=True)
    e = commands.add_parser("export", help="a solved clip -> a folder for kimodo_gen --input_folder")
    e.add_argument("clip", help="the JSON `shosa clip` writes")
    e.add_argument("-o", "--output", required=True, help="the folder to write")
    e.add_argument("--text", required=True, help="what the movement is, in English: Kimodo needs a text")
    e.add_argument("--keys", type=int, default=19, help="at most this many key frames (Kimodo: fewer than 20)")
    e.add_argument("--seed", type=int, default=1)
    e.add_argument("--text-weight", type=float, default=2.0)
    e.add_argument("--constraint-weight", type=float, default=2.0)
    e.add_argument("--no-path", action="store_true", help="leave the path of the hips out")
    e.add_argument("--picture", action="store_true", help="draw what is asked (asked.png)")
    e.set_defaults(run=export)
    c = commands.add_parser("compare", help="what Kimodo made against what was asked")
    c.add_argument("folder", help="the folder `export` wrote")
    c.add_argument("npz", help="what kimodo_gen wrote")
    c.set_defaults(run=compare)
    args = parser.parse_args()
    args.run(args)


if __name__ == "__main__":
    main()
