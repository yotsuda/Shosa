# Reads a CMU mocap clip (ASF skeleton + AMC motion) and draws a contact sheet: the figure from the front (top row) and
# from its right side (bottom row), N frames evenly over a time range. Left limbs red, right limbs blue, trunk black.
# usage: amc_sheet.py SKELETON.asf MOTION.amc OUT.png [--from SEC] [--to SEC] [--n 12] [--fps 120]
import sys, math, argparse
import numpy as np
from PIL import Image, ImageDraw

import os, importlib.util
_spec = importlib.util.spec_from_file_location('amc_lib', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'amc_lib.py'))
_lib = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(_lib)
read_asf, read_amc, pose = _lib.read_asf, _lib.read_amc, _lib.pose

def color(name):
    if name in ('lhipjoint', 'rhipjoint', 'lowerback', 'upperback', 'thorax', 'lowerneck', 'upperneck', 'head', 'lclavicle', 'rclavicle'):
        return (30, 30, 30)
    return (210, 40, 40) if name.startswith('l') else (40, 80, 220)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('asf'); ap.add_argument('amc'); ap.add_argument('out')
    ap.add_argument('--from', dest='t0', type=float, default=0); ap.add_argument('--to', dest='t1', type=float, default=None)
    ap.add_argument('--n', type=int, default=12); ap.add_argument('--fps', type=float, default=120)
    a = ap.parse_args()
    bones, parent, scale = read_asf(a.asf)
    frames = read_amc(a.amc)
    dur = len(frames) / a.fps
    t1 = dur if a.t1 is None else min(a.t1, dur)
    idx = [min(len(frames) - 1, int(round((a.t0 + (t1 - a.t0) * i / max(1, a.n - 1)) * a.fps))) for i in range(a.n)]
    poses = [pose(bones, parent, scale, frames[i]) for i in idx]
    # The dancer's own front: the root's facing at the first shown frame, flattened to the floor. The views stay fixed, so turns show.
    f = poses[0][1] @ np.array([0, 0, 1.0]); f[1] = 0
    f = f / (np.linalg.norm(f) or 1) if np.linalg.norm(f) > 0.2 else np.array([0, 0, 1.0])
    right = np.cross(np.array([0, 1.0, 0]), f)   # the dancer's left (+x when facing +z): screen x of the front view, as the audience sees it
    cell, px = 250, 105.0  # pixels per cell, pixels per meter
    W, H = cell * a.n, cell * 2 + 22
    img = Image.new('RGB', (W, H), (255, 255, 255)); d = ImageDraw.Draw(img)
    for c, (i, (rootp, _, segs)) in enumerate(zip(idx, poses)):
        for row, (ux, sign) in enumerate(((right, 1.0), (f, 1.0))):
            ox, oy = c * cell + cell / 2, row * cell + cell - 14 + 20
            d.line([(c * cell + 4, oy), (c * cell + cell - 4, oy)], fill=(190, 190, 190))
            def to(p):  # each cell is centred on the root, so travel does not leave the cell
                q = p - rootp
                return (ox + sign * float(q @ ux) * px, oy - float(p[1]) * px)
            for name, (s, e) in segs.items():
                d.line([to(s), to(e)], fill=color(name), width=3)
            hs, he = segs['head']; hc = to(he); d.ellipse([hc[0] - 6, hc[1] - 6, hc[0] + 6, hc[1] + 6], outline=(30, 30, 30), width=2)
        d.text((c * cell + 6, 4), f'{i / a.fps:.2f}s  root y {rootp[1]:.2f}m', fill=(0, 0, 0))
    d.text((6, 22), 'front (red = dancer\'s left)', fill=(120, 120, 120)); d.text((6, cell + 22), 'from the side (facing right)', fill=(120, 120, 120))
    img.save(a.out)
    print(f'{a.amc}: {len(frames)} frames, {dur:.1f}s; drew {a.n} frames {a.t0:.1f}-{t1:.1f}s -> {a.out}')

main()
