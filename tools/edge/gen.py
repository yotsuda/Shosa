r"""Generate a dance with EDGE for a stretch of a song.

  venv\Scripts\python.exe gen.py --wav music\song.wav --start 22.5 --seconds 15 --out runs\chorus --seed 1

Cuts [start, start+seconds] out of the song, slices it into 5 s windows 2.5 s apart (EDGE's long mode),
extracts the Jukebox features of each window (cached in OUT/feats.npy), then lets EDGE generate the
windows together and stitch them. Writes OUT/motion.pkl (smpl_poses: frames x 72 axis-angle,
smpl_trans: frames x 3, full_pose: frames x 24 x 3; EDGE's z-up frame, 30 fps) and OUT/audio.wav.
"""
import argparse
import gc
import os
import pickle
import random
import sys
from pathlib import Path

import numpy as np
import soundfile as sf
import librosa as lr
import torch

EDGE_DIR = Path(__file__).resolve().parents[2] / "out" / "edge" / "EDGE"  # the clone, its checkpoint.pt and the patches (DESIGN.md 110.1)
sys.path.insert(0, str(EDGE_DIR))
sys.path.insert(0, str(Path(__file__).resolve().parent))

STRIDE, WINDOW, FPS = 2.5, 5.0, 30


def slice_song(wav, start, seconds, out):
    audio, sr = lr.load(wav, sr=None, offset=start, duration=seconds, mono=True)
    sf.write(out / "audio.wav", audio, sr)
    slices = out / "slices"
    slices.mkdir(exist_ok=True)
    names, idx, pos = [], 0, 0
    win, step = int(WINDOW * sr), int(STRIDE * sr)
    while pos + win <= len(audio):
        name = slices / f"song_slice{idx}.wav"
        sf.write(name, audio[pos : pos + win], sr)
        names.append(str(name))
        pos += step
        idx += 1
    return names


def features(names, out):
    cache = out / "feats.npy"
    if cache.exists():
        return np.load(cache)
    import jukemirlib

    reps = []
    for name in names:
        audio = jukemirlib.load_audio(name)
        rep = jukemirlib.extract(audio, layers=[66], downsample_target_rate=FPS)[66]
        print(name, rep.shape, flush=True)
        reps.append(rep)
    reps = np.array(reps)
    np.save(cache, reps)
    jukemirlib.lib.VQVAE = jukemirlib.lib.TOP_PRIOR = None
    gc.collect()
    torch.cuda.empty_cache()
    return reps


def pin_constraint(model, a, frames):
    """The Shosa poses to hold (EDGE's normalized vector) and where: windows x 150 x 151 each."""
    import shosa2smpl
    from dataset.preprocess import vectorize_many
    from dataset.quaternion import ax_to_6v

    q, pos = shosa2smpl.load(a.pin_vrma, a.start, a.seconds)
    q, pos = q[:frames], pos[:frames]
    pos[:, 2] += 0.93  # EDGE's floor sits about 0.93 below its origin (DESIGN.md 110.3)
    q6 = ax_to_6v(torch.tensor(q, dtype=torch.float32))[None]
    vec = vectorize_many([torch.zeros(1, frames, 4), torch.tensor(pos, dtype=torch.float32)[None], q6])
    value = model.normalizer.normalize(vec.clone())[0]

    joints = {"upper": shosa2smpl.UPPER, "arms": shosa2smpl.ARMS}[a.pin_joints]
    channels = [7 + 6 * j + k for j in joints for k in range(6)]
    beat = 60.0 / a.bpm
    mask = torch.zeros(frames, value.shape[1])
    pinned = []
    for b in a.pin_beats:
        f = int(round(b * beat * FPS))
        lo, hi = max(0, f - a.pin_before), min(frames, f + a.pin_after + 1)
        # held fully in [lo, hi), fading out over --pin-feather frames on each side
        for g in range(max(0, lo - a.pin_feather), min(frames, hi + a.pin_feather)):
            d = lo - g if g < lo else (g - hi + 1 if g >= hi else 0)
            w = 1.0 - d / (a.pin_feather + 1)
            mask[g, channels] = torch.maximum(mask[g, channels], torch.tensor(w))
        pinned.append((lo, hi))
    print("pinned frames", pinned, "joints", joints, flush=True)

    horizon, half = int(WINDOW * FPS), int(STRIDE * FPS)
    windows = (frames - horizon) // half + 1
    take = lambda t: torch.stack([t[w * half : w * half + horizon] for w in range(windows)])
    return take(value), take(mask)


@torch.no_grad()
def long_ddim_inpaint(diff, shape, cond, value, mask):
    """EDGE's long_ddim_sample, with the pinned channels replaced by the noised Shosa value after each step."""
    batch, device = shape[0], diff.betas.device
    steps, eta = 50, 1
    times = list(reversed(torch.linspace(-1, diff.n_timestep - 1, steps=steps + 1).int().tolist()))
    weights = np.clip(np.linspace(0, diff.guidance_weight * 2, steps), None, diff.guidance_weight)
    value, mask, cond = value.to(device), mask.to(device), cond.to(device)
    x = torch.randn(shape, device=device)
    half = shape[1] // 2
    for time, time_next, weight in zip(times[:-1], times[1:], weights):
        t = torch.full((batch,), time, device=device, dtype=torch.long)
        pred_noise, x_start, *_ = diff.model_predictions(x, cond, t, weight=weight, clip_x_start=diff.clip_denoised)
        if time_next < 0:
            x = x_start
            break
        alpha, alpha_next = diff.alphas_cumprod[time], diff.alphas_cumprod[time_next]
        sigma = eta * ((1 - alpha / alpha_next) * (1 - alpha_next) / (1 - alpha)).sqrt()
        c = (1 - alpha_next - sigma**2).sqrt()
        x = x_start * alpha_next.sqrt() + c * pred_noise + sigma * torch.randn_like(x)
        x[1:, :half] = x[:-1, half:]
        noisy = diff.q_sample(value, torch.full((batch,), time_next, device=device, dtype=torch.long))
        x = noisy * mask + (1 - mask) * x
    return value * mask + (1 - mask) * x


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--wav", required=True)
    p.add_argument("--start", type=float, default=0)
    p.add_argument("--seconds", type=float, default=15)
    p.add_argument("--out", required=True)
    p.add_argument("--seed", type=int, default=1)
    p.add_argument("--features-only", action="store_true")
    p.add_argument("--pin-vrma", help="a dancer's .vrma from `shosa vrma`: hold its poses at --pin-beats")
    p.add_argument("--pin-beats", type=float, nargs="*", default=[], help="beats from --start")
    p.add_argument("--bpm", type=float, default=128)
    p.add_argument("--pin-joints", choices=["upper", "arms"], default="upper")
    p.add_argument("--pin-before", type=int, default=2, help="frames held before each beat")
    p.add_argument("--pin-after", type=int, default=3, help="frames held after each beat")
    p.add_argument("--pin-feather", type=int, default=6, help="frames over which a pin fades out on each side")
    a = p.parse_args()
    if a.pin_vrma:
        a.pin_vrma = str(Path(a.pin_vrma).resolve())

    out = Path(a.out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    names = slice_song(a.wav, a.start, a.seconds, out)
    cond = features(names, out)
    print("features", cond.shape, flush=True)
    if a.features_only:
        return

    random.seed(a.seed)
    np.random.seed(a.seed)
    torch.manual_seed(a.seed)

    os.chdir(EDGE_DIR)  # EDGE resolves nothing relative, but keep its habits
    from EDGE import EDGE

    model = EDGE("jukebox", str(EDGE_DIR / "checkpoint.pt"))
    model.eval()
    fk = out / "fk"
    cond = torch.from_numpy(cond)
    if a.pin_vrma:
        frames = int(round(a.seconds * FPS))
        value, mask = pin_constraint(model, a, frames)
        shape = (len(cond), model.horizon, model.repr_dim)
        samples = long_ddim_inpaint(model.diffusion, shape, cond, value, mask).cpu()
        model.diffusion.render_sample(samples, cond.to(model.accelerator.device), model.normalizer, "edge", str(out),
                                      fk_out=str(fk), name=names, sound=True, mode="long", render=False)
    else:
        model.render_sample((None, cond, names), "edge", str(out), render_count=-1, fk_out=str(fk), render=False)
    pkl = next(fk.glob("*.pkl"))
    motion = pickle.load(open(pkl, "rb"))
    pickle.dump(motion, open(out / "motion.pkl", "wb"))
    print("frames", motion["smpl_poses"].shape, "->", out / "motion.pkl")


if __name__ == "__main__":
    main()
