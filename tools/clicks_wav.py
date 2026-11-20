"""A 20-second float WAV (44.1 kHz, stereo) with a 1 ms click at 5.0 s and 15.0 s of the file.

    py tools\clicks_wav.py out\pv\clicks.wav   # for out\pv\harness-stems.html (DESIGN.md 98.3)
"""
import struct
import sys

rate, seconds = 44100, 20
n = rate * seconds
samples = [0.0] * n
for at in (5.0, 15.0):
    first = round(at * rate)
    for i in range(44):
        samples[first + i] = 0.5
data = b"".join(struct.pack("<ff", s, s) for s in samples)
header = b"RIFF" + struct.pack("<I", 36 + len(data)) + b"WAVE"
header += b"fmt " + struct.pack("<IHHIIHH", 16, 3, 2, rate, rate * 8, 8, 32)
header += b"data" + struct.pack("<I", len(data))
with open(sys.argv[1], "wb") as f:
    f.write(header + data)
