# Lily# → Shosa: the recording beside a chart may be "silent" (2026-11-26)

From: the Lily# session (`C:\MyProj\LilySharp`, commit `6895373bd`, session 1397).
About: the fourth step of the way from a song to a dance, **Arrange an Accompaniment…**
(Lily#'s `docs/accompaniment-design.md`).

## What changed in Lily#

The owner decided this on 2026-11-26. When the musician accepts an accompaniment arranged from a
chart's chords, Lily# writes one item into the file beside the chart:

```json
{ "orecchiante": 2, "audio": "band.mp3", "silent": true, "bars": [ … ] }
```

`"silent": true` means **the recording is not heard, and its bars still keep the time**. The score,
the voices and the dance stay on the recording's seconds. Nothing else in the file changes. When the
musician turns the recording on again in Lily#'s preview mixer, Lily# removes the item.

- `renderStems` (stemsVersion 1, unchanged) already honours it: `timeline` stays `'recording'`, there
  is no `recording` or `recordingPart` stem, and `notes` says why.
- Lily#'s own exports (WAV, MP3, MP4) leave the recording out and say so.

## What Shosa may want to do

Shosa reads the file beside the chart itself (DESIGN.md 97). When the file says `"silent": true`:

1. **Timing**: keep reading its bars as now. The dance keeps the recording's seconds.
2. **The preview's sound**: when the stems are not there, `main.js` `sound()` falls back to
   `recorder.play(recording.audio, …)`. It should not play the recording then; the notes or nothing
   would play instead.
3. **The MP4 / audio export**: `recorder.buffer(recording.audio)` is mixed in today (`main.js` around
   line 699). It should be left out, as Lily#'s exports leave it out.
4. Optional: the preview could say "the recording is silent (an accompaniment plays)" where it names it.

Lily# never writes `"silent": false`: the item is either `true` or absent.
