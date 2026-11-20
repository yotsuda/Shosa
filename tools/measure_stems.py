"""Where the clicks and the recording are in the mix and in the MP4's sound (DESIGN.md 98).

    C:\MyProj\Orecchiante\.venv\Scripts\python.exe -I tools\measure_stems.py out\pv\stems-run out\pv\ito.mp3 1.82
RUN_FOLDER is where tools\frame_server.py kept what out\pv\harness-stems.html posted: 00001.png the MP4, 00002.png the mix (raw floats).
The clicks are at 10.0 s and 20.0 s of the timeline; the dance's 0 is OFFSET s of it.
"""
import subprocess
import sys
import numpy as np

run, recording, offset = sys.argv[1], sys.argv[2], float(sys.argv[3])
rate = 48000


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-af", "pan=mono|c0=c0", "-ar", str(rate), "-f", "f32le", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32)


rec = decode(recording)
for name, sound in (("mix", np.fromfile(run + "/00002.png", dtype=np.float32)), ("mp4", decode(run + "/00001.png"))):
    print(f"{name}: {len(sound) / rate:.3f} s")
    jump = np.abs(np.diff(sound))
    for click in (10.0, 20.0):
        expected = round((click - offset) * rate)
        lo, hi = expected - rate // 2, expected + rate // 2
        found = lo + int(np.argmax(jump[lo:hi])) + 1
        print(f"  click at {click} s of the timeline: expected sample {expected}, found {found} ({(found - expected) / rate * 1000:+.2f} ms), jump {jump[found - 1]:.3f}")
    # The recording: the lag (within ±30 ms) at which a stretch of the sound matches it best.
    for at in (40.0, 150.0, 260.0):
        a = round(at * rate)
        seg = sound[a:a + rate * 4]
        best, best_lag = -2.0, 0
        for lag in range(-1440, 1441):
            r = rec[a + round(offset * rate) + lag: a + round(offset * rate) + lag + rate * 4]
            c = float(np.dot(seg, r) / (np.linalg.norm(seg) * np.linalg.norm(r) + 1e-12))
            if c > best:
                best, best_lag = c, lag
        print(f"  recording at {at} s of the dance: lag {best_lag / rate * 1000:+.2f} ms, correlation {best:.4f}")
