r"""How fast the upper body's joints turn, frame to frame, in EDGE's motion.pkl files (DESIGN.md 111).

  python -I measure.py runs\chorus\motion.pkl runs\pin-z\motion.pkl --beats 1 3 6.5 --bpm 128

For each file: the fastest upper-body joint in each frame (degrees per second), its median, 99th
percentile and maximum, and the same split into frames near a pin's edge (within --near frames of
where a pin, as gen.py makes it, starts or ends) and the rest. A snap shows as thousands of
degrees per second at the edges.
"""
import argparse
import pickle

import numpy as np
from scipy.spatial.transform import Rotation as R

UPPER = [3, 6, 9, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("pkl", nargs="+")
    p.add_argument("--beats", type=float, nargs="*", default=[])
    p.add_argument("--bpm", type=float, default=128)
    p.add_argument("--before", type=int, default=2)
    p.add_argument("--after", type=int, default=3)
    p.add_argument("--near", type=int, default=1)
    a = p.parse_args()
    edges = set()
    for b in a.beats:
        f = int(round(b * 60 / a.bpm * 30))
        edges |= {f - a.before, f + a.after + 1}
    for path in a.pkl:
        q = pickle.load(open(path, "rb"))["smpl_poses"].reshape(-1, 24, 3)
        speed = np.stack([(R.from_rotvec(q[1:, j]) * R.from_rotvec(q[:-1, j]).inv()).magnitude() for j in UPPER], 1)
        top = np.degrees(speed.max(1)) * 30
        line = f"{path}: p50 {np.median(top):5.0f} p99 {np.percentile(top, 99):5.0f} max {top.max():5.0f} deg/s"
        if edges:
            near = np.array([min(abs(f - e) for e in edges) <= a.near for f in range(len(top))])
            worst = UPPER[int(speed[near].max(0).argmax())]
            line += f" | pin edges max {top[near].max():5.0f} (joint {worst}), elsewhere p99 {np.percentile(top[~near], 99):5.0f}"
        print(line)


if __name__ == "__main__":
    main()
