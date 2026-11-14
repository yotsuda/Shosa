# Shosa

Write human movement as text and watch it performed beside the source.

```
tempo 120
section Wave loop {
  steps    { left.side together right.side together }
  rightArm { side up side down }
}
```

- **Preview to the side** (the button at the top right of a `.shosa` file, or Ctrl+K V): the figures move as you type.
  The word being performed is lit in the source; put the cursor on a word to see its pose; click a limb to go to the word that put it there.
- **Diagnostics, completion and hover** for every word of the language, from the same table the compiler checks against.
- **Dancing to a song**: `music "song.lys"` takes tempo, time and the order of the sections from a [Lily#](https://yotsuda.github.io/LilySharp/) song,
  and the preview plays the song with the dance — through the sound font Lily# plays with, when one is installed.
- Marks for a game engine (`!hit`) are shown as they arrive.

The compiler (`shosa`) writes the movement as BVH, glTF and VRM Animation.

## Settings

- `shosa.playback.soundFont` — the sound font the preview plays a song with. Empty: the one Lily# plays with.
- `shosa.server.path` — a language server of your own build.
