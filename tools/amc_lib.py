# Reading CMU mocap (ASF skeleton + AMC motion): joints in metres, y up; a figure at rest faces +z with +x on its left.
import math
import numpy as np

def rot(axis, deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    if axis == 'x': return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    if axis == 'y': return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])

def euler(rx, ry, rz):  # the ASF convention: R = Rz Ry Rx
    return rot('z', rz) @ rot('y', ry) @ rot('x', rx)

def read_asf(path):
    bones, parent, scale = {}, {}, 1.0
    section, cur = None, None
    lines = open(path).read().splitlines()
    i = 0
    while i < len(lines):
        t = lines[i].split(); i += 1
        if not t or t[0].startswith('#'): continue
        if t[0].startswith(':'):
            section = t[0]; continue
        if section == ':units' and t[0] == 'length': scale = float(t[1])
        elif section == ':bonedata':
            if t[0] == 'begin': cur = {'dof': []}
            elif t[0] == 'end': bones[cur['name']] = cur
            elif t[0] == 'name': cur['name'] = t[1]
            elif t[0] == 'direction': cur['dir'] = np.array([float(v) for v in t[1:4]])
            elif t[0] == 'length': cur['len'] = float(t[1])
            elif t[0] == 'axis':
                c = euler(float(t[1]), float(t[2]), float(t[3])); cur['C'] = c; cur['Ci'] = c.T
            elif t[0] == 'dof': cur['dof'] = t[1:]
        elif section == ':hierarchy':
            if t[0] in ('begin', 'end'): continue
            for child in t[1:]: parent[child] = t[0]
    return bones, parent, scale

def read_amc(path):
    frames, cur = [], None
    for line in open(path):
        t = line.split()
        if not t or t[0].startswith('#') or t[0].startswith(':'): continue
        if t[0].isdigit() and len(t) == 1:
            cur = {}; frames.append(cur)
        elif cur is not None:
            cur[t[0]] = [float(v) for v in t[1:]]
    return frames

def pose(bones, parent, scale, frame, rotations=False):
    """World positions in meters: {bone: (start, end)}, plus the root."""
    k = 0.0254 / scale
    r = frame['root']
    rootp = np.array(r[0:3]) * k
    R = {'root': euler(r[3], r[4], r[5])}
    P = {'root': rootp}
    out = {}
    def solve(name):
        if name in R: return
        p = parent[name]; solve(p)
        b = bones[name]
        a = dict(zip(b['dof'], frame.get(name, [])))
        M = euler(a.get('rx', 0), a.get('ry', 0), a.get('rz', 0))
        R[name] = R[p] @ b['C'] @ M @ b['Ci']
        P[name] = P[p] + R[name] @ (b['dir'] * b['len'] * k)
        out[name] = (P[p], P[name])
    for name in bones: solve(name)
    if rotations: return rootp, R['root'], R, out
    return rootp, R['root'], out

