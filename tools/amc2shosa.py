# Transcribes a CMU mocap clip (ASF + AMC) into a .shosa file, mechanically: one word per row every STEP beats,
# read off the recorded joints. For looking at a recorded dance in Shosa's own preview, and for finding what the
# language cannot say. Not choreography: nobody would write a dance this way.
# usage: amc2shosa.py SKELETON.asf MOTION.amc OUT.shosa [--from SEC] [--to SEC] [--tempo 120] [--step 1] [--title T]
#        [--no-legs] [--model PATH]
import sys, math, argparse, importlib.util, os
import numpy as np

_spec = importlib.util.spec_from_file_location('amc_sheet_lib', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'amc_lib.py'))
amc = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(amc)

UP = np.array([0.0, 1.0, 0.0])

def unit(v):
    n = np.linalg.norm(v)
    return v / n if n > 1e-9 else v

def deg(x): return math.degrees(x)

def heading_of(R):
    """The way the hips face, flattened to the floor, as an angle about up (0 = +z, positive = to the dancer's left)."""
    f = R @ np.array([0, 0, 1.0])
    return math.atan2(f[0], f[2])   # +x is the dancer's left when facing +z

def frame_of(heading):
    f = np.array([math.sin(heading), 0, math.cos(heading)])
    left = np.cross(UP, f)
    return f, left

GRAIN = 5
def r5(x): return int(round(x / GRAIN)) * GRAIN

def direction(d, f, left, side_sign, limb=True, plain_down=None):
    """A direction as Shosa writes it. side_sign: +1 for a left limb (its `side` is the dancer's left), -1 for a right limb.
    limb=False writes left/right (torso, head)."""
    d = unit(d)
    x, s, y = float(d @ f), float(d @ left), float(d @ UP)
    elev = r5(deg(math.asin(max(-1, min(1, y)))))
    if elev >= 85: return 'up'
    if elev <= -85: return plain_down or 'fwd.down(85)'
    az = deg(math.atan2(s, x))            # 0 fwd, +90 the dancer's left, 180 back
    if limb:
        az *= side_sign                   # now +90 is the limb's own side
        pos, neg = 'side', 'across'
    else:
        pos, neg = 'left', 'right'
    a = abs(az); way = pos if az >= 0 else neg
    if a <= 90:
        n = r5(a)
        h = 'fwd' if n == 0 else (way if n == 90 else f'fwd.{way}({n})')
    else:
        n = r5(180 - a)
        h = 'back' if n == 0 else (way if n == 90 else f'back.{way}({n})')
    if elev > 0: return f'{h}.up({elev})'
    if elev < 0: return f'{h}.down({-elev})'
    return h

def coarse(d, f, left, side_sign):
    """A pointing for elbow(...) / knee(...): one way, level, up or down."""
    d = unit(d)
    x, s, y = float(d @ f), float(d @ left) * side_sign, float(d @ UP)
    if abs(y) > 0.85: return 'up' if y > 0 else 'down'
    if abs(x) >= abs(s): h = 'fwd' if x > 0 else 'back'
    else: h = 'side' if s > 0 else 'across'
    if y > 0.4: return h + '.up'
    if y < -0.4: return h + '.down'
    return h

def bend(a, b, c):
    """How far the joint at b is bent: 0 straight."""
    u, v = unit(b - a), unit(c - b)
    return r5(deg(math.acos(max(-1, min(1, float(u @ v))))))

def torso_word(d, f, left):
    d = unit(d)
    x, s, y = float(d @ f), float(d @ left), float(d @ UP)
    tilt = deg(math.acos(max(-1, min(1, y))))
    n = r5(tilt)
    if n == 0: return 'up'
    if n <= 50:
        if abs(x) >= abs(s): return f'up.{"fwd" if x > 0 else "back"}({n})'
        return f'up.{"left" if s > 0 else "right"}({n})'
    return direction(d, f, left, 1, limb=False, plain_down='down')

def rows_to_text(rows, step, per_bar):
    """rows: {name: [word, ...]} one word per slot; equal neighbours of a gesture row become one `then`-held word."""
    out = []
    for name, words in rows.items():
        line, i, n = [], 0, len(words)
        while i < n:
            w, at, ann = words[i].partition('@')
            line.append(f'{w}:{step}{at}{ann}')
            i += 1
            if i % per_bar == 0: line.append('|')
        out.append(f'    {name:<9}{{ ' + ' '.join(line) + ' }')
    return '\n'.join(out)

def through(words):
    """A word between two others is passed without stopping (`@through`): a gesture otherwise slows to nothing at every word."""
    quiet = ('then', 'skip')
    out = list(words)
    return out   # (the `quality { through }` row says it for every word now: DESIGN.md 130)

def hold(words):
    """A word equal to the one before it says nothing new: `then`."""
    out, last = [], None
    for w in words:
        out.append('then' if w == last else w)
        if w != 'then': last = w
    return out

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('asf'); ap.add_argument('amc'); ap.add_argument('out')
    ap.add_argument('--from', dest='t0', type=float, default=0); ap.add_argument('--to', dest='t1', type=float, default=None)
    ap.add_argument('--tempo', type=float, default=120); ap.add_argument('--step', default='1')
    ap.add_argument('--fps', type=float, default=120); ap.add_argument('--title', default=None)
    ap.add_argument('--no-legs', action='store_true'); ap.add_argument('--model', default=None)
    ap.add_argument('--turn', type=float, default=10, help='the least turn the steps row writes, in degrees')
    ap.add_argument('--calm', type=float, default=0.5, help='seconds the facing is averaged over (each way): the hips of a walk swing, the facing does not')
    ap.add_argument('--grain', type=int, default=5, help='degrees are written to the nearest this many')
    ap.add_argument('--no-elbow', action='store_true')
    ap.add_argument('--smooth', type=float, default=1.0, help='the window the gestures are averaged over, in slots (0: the frame itself)')
    a = ap.parse_args()
    global GRAIN; GRAIN = a.grain
    num, _, den = a.step.partition('/'); step = float(num) / float(den or 1)
    bones, parent, scale = amc.read_asf(a.asf)
    frames = amc.read_amc(a.amc)
    dur = len(frames) / a.fps
    t1 = dur if a.t1 is None else min(a.t1, dur)
    dt = step * 60.0 / a.tempo
    per_bar = int(round(4 / step))
    n = int((t1 - a.t0) / dt) + 1
    n = ((n + per_bar - 1) // per_bar) * per_bar           # whole bars: the last pose is held to the bar's end
    S = []
    for k in range(n):
        t = min(a.t0 + k * dt, t1)
        i = min(len(frames) - 1, int(round(t * a.fps)))
        root, Rroot, R, seg = amc.pose(bones, parent, scale, frames[i], rotations=True)
        # The limbs and the trunk are read as the mean over the slot (half of it each way): a tremble of a few degrees is not a word.
        half = max(1, int(dt * a.fps * a.smooth / 2))
        near = [amc.pose(bones, parent, scale, frames[j], rotations=True) for j in sorted({max(0, min(len(frames) - 1, i + o)) for o in range(-half, half + 1, max(1, half // 3))})]
        soft = {b: (sum(p[3][b][0] for p in near) / len(near), sum(p[3][b][1] for p in near) / len(near)) for b in seg}
        look = unit(sum(p[2]['head'] @ np.array([0, 0, 1.0]) for p in near))
        S.append(dict(root=root, R=R, Rroot=Rroot, seg=seg, soft=soft, softroot=sum(p[0] for p in near) / len(near), look=look, t=t))
    G = lambda s, b: s['soft'][b][1]                        # the same, smoothed: for the gestures (the feet's landings read the frame itself)
    if True:
        pass
    J = lambda s, b: s['seg'][b][1]                         # the far end of a bone

    # The way the body faces at each slot, unwrapped, and what the steps row has turned so far (5 degrees at a time).
    def calm_heading(t):
        if a.calm <= 0: return None
        js = range(max(0, int((t - a.calm) * a.fps)), min(len(frames), int((t + a.calm) * a.fps) + 1), 6)
        hs = [heading_of(amc.pose(bones, parent, scale, frames[j], rotations=True)[1]) for j in js]
        for q in range(1, len(hs)):
            while hs[q] - hs[q - 1] > math.pi: hs[q] -= 2 * math.pi
            while hs[q] - hs[q - 1] < -math.pi: hs[q] += 2 * math.pi
        return sum(hs) / len(hs)
    hd = [calm_heading(s['t']) if a.calm > 0 else heading_of(s['Rroot']) for s in S]
    for k in range(1, n):
        while hd[k] - hd[k - 1] > math.pi: hd[k] -= 2 * math.pi
        while hd[k] - hd[k - 1] < -math.pi: hd[k] += 2 * math.pi
    turned = [0] * n                                        # degrees to the left since slot 0, as written
    for k in range(1, n):
        want = deg(hd[k] - hd[0])
        d = r5(want - turned[k - 1])
        turned[k] = turned[k - 1] + (d if abs(d) >= a.turn else 0)
    face = [frame_of(hd[0] + math.radians(turned[k])) for k in range(n)]

    stand = float(np.percentile([s['root'][1] for s in S], 80))   # the hips' height standing
    rows = {}
    # ---- steps: the feet that land, the turns ----
    floor = min(min(J(s, 'ltoes')[1], J(s, 'rtoes')[1], J(s, 'ltibia')[1], J(s, 'rtibia')[1]) for s in S)
    ankle0 = float(np.percentile([min(J(s, 'ltibia')[1], J(s, 'rtibia')[1]) for s in S], 20))
    def on(s, side):                                        # is this foot on the floor: its ankle or its toe low
        return J(s, side + 'tibia')[1] < ankle0 + 0.07 or J(s, side + 'toes')[1] < floor + 0.05 and J(s, side + 'tibia')[1] < ankle0 + 0.14
    L = [on(s, 'l') for s in S]; Rr = [on(s, 'r') for s in S]
    steps, notes = [], []
    for k in range(n):
        f, left = face[k]
        turn = turned[k] - turned[k - 1] if k else 0
        tw = (f'left({turn})' if turn > 0 else f'right({-turn})') if turn else None
        word = None
        if k > 0:
            for side, name, onn, other in (('l', 'leftFoot', L, 'r'), ('r', 'rightFoot', Rr, 'l')):
                moved = np.linalg.norm((J(S[k], side + 'tibia') - J(S[k - 1], side + 'tibia'))[[0, 2]])
                # Landed from the air; or slid to a new place while the other foot stood (a foot that turns under the body is not a step).
                landed = onn[k] and (not onn[k - 1] or (moved > 0.25 and (Rr if side == 'l' else L)[k]))
                if landed and word is None:
                    rel = J(S[k], side + 'tibia') - J(S[k], other + 'tibia')
                    x = float(rel @ f); s_ = float(rel @ left) * (1 if side == 'l' else -1)
                    parts = []
                    if abs(x) >= 0.05: parts.append(f'{"fwd" if x > 0 else "back"}({abs(x):.2f}m)')
                    if s_ >= 0: parts.append(f'side({max(s_, 0.05):.2f}m)')
                    else: parts.append(f'across({-s_:.2f}m)')
                    word = name + '.' + '.'.join(parts) + (f'@turn({tw})' if tw else '')
        if word is None:
            word = (f'turn.{tw}' if tw else 'then') if k else 'stand'
        steps.append(word)
    # A leg may lift only off a foot that does not carry the weight: after a step the weight is on the foot that stepped,
    # so before that foot lifts, the weight shifts to the other one (in the slot before, when the feet say nothing there).
    weight = None
    for k in range(n):
        w = steps[k]
        if w.startswith('leftFoot'): weight = 'l'
        elif w.startswith('rightFoot'): weight = 'r'
        elif w.startswith('shift.left'): weight = 'l'
        elif w.startswith('shift.right'): weight = 'r'
        if k + 1 < n and steps[k] == 'then':
            for side, onn, other, to in (('l', L, Rr, 'right'), ('r', Rr, L, 'left')):
                if onn[k] and not onn[k + 1] and other[k + 1] and weight != ('r' if side == 'l' else 'l'):
                    steps[k] = f'shift.{to}'; weight = 'r' if side == 'l' else 'l'
    rows['steps'] = steps
    # ---- height ----
    hw = []
    for k, s in enumerate(S):
        drop = (stand - s['root'][1]) * 100
        if not (L[k] or Rr[k]): hw.append(hw[-1] if hw else 'straight')
        elif drop >= 4: hw.append(f'low({min(60, int(round(drop)))}cm)')
        elif drop <= -5: hw.append('high')
        else: hw.append('straight')
    rows['height'] = through(hold(hw))
    # ---- legs in the air ----
    if not a.no_legs:
        for side, name, onn, sign in (('l', 'leftLeg', L, 1), ('r', 'rightLeg', Rr, -1)):
            w = []
            for k, s in enumerate(S):
                f, left = face[k]
                other = Rr if side == 'l' else L
                # In the air only while the other foot stands, and let go a slot before it lands (a `skip` lets go over its own time).
                if onn[k] or not other[k] or k + 1 >= n or onn[k + 1] or k == 0 or not other[k - 1]:
                    w.append('skip'); continue
                hip, knee, ank = G(s, side + 'hipjoint'), G(s, side + 'femur'), G(s, side + 'tibia')
                d = unit(ank - hip)
                if d[1] < -0.94: d = unit(np.array([d[0], 0, d[2]]) / (np.linalg.norm(d[[0, 2]]) or 1) * 0.342 + UP * -0.94)   # never straight down: that is the floor
                word = direction(d, f, left, sign)
                b = bend(hip, knee, ank)
                if b >= 15:
                    word += f'.bent({min(150, b)})'
                    perp = (knee - hip) - unit(ank - hip) * float((knee - hip) @ unit(ank - hip))
                    if np.linalg.norm(perp) > 0.05: word += f'.knee({coarse(perp, f, left, sign)})'
                w.append(word + '.pointed')
            rows[name] = through(w)
    # ---- torso, head, arms ----
    tw_, hw_ = [], []
    # The skeleton's head at zero angles is not a head held level: take the clip's usual nod as level (a guess, no more than 45 degrees).
    nods = [math.asin(max(-1, min(1, float((s['R']['head'] @ np.array([0, 0, 1.0])) @ UP)))) for s in S]
    nod = max(-math.pi / 4, min(math.pi / 4, float(np.median(nods))))
    def rot_about(axis, ang):
        axis = unit(axis); K = np.array([[0, -axis[2], axis[1]], [axis[2], 0, -axis[0]], [-axis[1], axis[0], 0]])
        return np.eye(3) + math.sin(ang) * K + (1 - math.cos(ang)) * (K @ K)
    for k, s in enumerate(S):
        f, left = face[k]
        tw_.append(torso_word(G(s, 'thorax') - s['softroot'], f, left))
        hw_.append(direction(rot_about(left, nod) @ s['look'], f, left, 1, limb=False, plain_down='fwd.down(85)'))
    rows['torso'] = through(hold(tw_)); rows['head'] = through(hold(hw_))
    for side, name, sign in (('l', 'leftArm', 1), ('r', 'rightArm', -1)):
        w = []
        for k, s in enumerate(S):
            f, left = face[k]
            sh, el, wr = G(s, side + 'clavicle'), G(s, side + 'humerus'), G(s, side + 'radius')
            word = direction(wr - sh, f, left, sign, plain_down='fwd.down(85)')
            b = bend(sh, el, wr)
            if b >= 15:
                word += f'.bent({min(150, b)})'
                perp = (el - sh) - unit(wr - sh) * float((el - sh) @ unit(wr - sh))
                if b >= 25 and np.linalg.norm(perp) > 0.04 and not a.no_elbow: word += f'.elbow({coarse(perp, f, left, sign)})'
            w.append(word)
        rows[name] = through(hold(w))

    title = a.title or os.path.splitext(os.path.basename(a.amc))[0]
    model = f' "{a.model}"' if a.model else ''
    text = (f'// Transcribed by tools/amc2shosa.py from the CMU Graphics Lab Motion Capture Database (mocap.cs.cmu.edu), {os.path.basename(a.amc)},\n'
            f'// {a.t0:.2f} s to {t1:.2f} s, a word every {a.step} beat at {a.tempo:g} (every {dt:.3f} s). A recording read off mechanically, not choreography.\n'
            f'title "{title}"\ntempo {a.tempo:g}\n\nrole solo\ndancer solo humanoid{model} on solo\n\nsection Main {{\n  solo {{\n'
            + '    quality  { through }\n' + rows_to_text(rows, a.step, per_bar) + '\n  }\n}\n\nform { Main }\nscore { dancer solo }\n')
    open(a.out, 'w', encoding='utf-8', newline='\n').write(text)
    print(f'{a.out}: {n} words a row, {n * dt:.1f} s; the hips stand {stand:.2f} m; turned {turned[-1]} degrees')

main()
