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


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--wav", required=True)
    p.add_argument("--start", type=float, default=0)
    p.add_argument("--seconds", type=float, default=15)
    p.add_argument("--out", required=True)
    p.add_argument("--seed", type=int, default=1)
    p.add_argument("--features-only", action="store_true")
    a = p.parse_args()

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
    model.render_sample((None, torch.from_numpy(cond), names), "edge", str(out), render_count=-1, fk_out=str(fk), render=False)
    pkl = next(fk.glob("*.pkl"))
    motion = pickle.load(open(pkl, "rb"))
    pickle.dump(motion, open(out / "motion.pkl", "wb"))
    print("frames", motion["smpl_poses"].shape, "->", out / "motion.pkl")


if __name__ == "__main__":
    main()
