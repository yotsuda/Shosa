# Shosa

Write human movement as text and watch it performed beside the source.

```
tempo 120

figure solo
dancer solo humanoid on solo

section Wave {
  solo {
    steps    { left.side together right.side together }
    rightArm { side up side down }
  }
}

form { Wave }
score { dancer solo  loop }
```

- **Preview to the side** (the button at the top right of a `.shosa` file, or Ctrl+K V): the figures move as you type.
  The word being performed is lit in the source; put the cursor on a word to see its pose; click a limb to go to the word that put it there.
- **File > New File... > Shosa File** starts from a file that already compiles; nothing of the skeleton above can be left out, and completion writes each item for you.
  One `score` is one file written, and the preview shows one score at a time.
- **Diagnostics, completion and hover** for every word of the language, from the same table the compiler checks against.
- **Dancing to a song**: `music "song.lys"` takes tempo, time and the order of the sections from a [Lily#](https://yotsuda.github.io/LilySharp/) song,
  and the preview plays the song with the dance — through the sound font Lily# plays with, when one is installed.
- Marks for a game engine (`!hit`) are shown as they arrive. A score without a song is heard too: a click on every beat and a clap at every mark, so its timing can be judged.
- **Export** from the preview (the two buttons at its top right, or *Shosa: Export…* in the command palette), the score shown:
  **Model** writes it for an engine — glTF (`.glb`), VRM Animation (`.vrma`) or BVH (`.bvh`), by the type picked in the save dialog;
  **MP4** writes a video of the preview as you have turned it, with the song (or the clicks and claps of a score without one). Nothing else has to be installed (no ffmpeg):
  the video is encoded by the editor itself.
- **An AI assistant can write it**: in Copilot Chat's agent mode, ask for a movement in Shosa. The extension gives the assistant
  the language (`#shosaGrammar`), the compiler's verdict (`#shosaCheck`) and a picture of the movement to look at (`#shosaSheet`).

The compiler (`shosa`) writes the movement as BVH, glTF and VRM Animation. It comes with the extension and is on the
PATH of VS Code's terminals: `shosa check FILE`, `shosa sheet FILE -o X.png`, `shosa gltf FILE --combined -o X.glb`
(`shosa --help` for the rest; `shosa grammar` prints the whole language for an AI agent that will write it).

## Settings

- `shosa.playback.soundFont` — the sound font the preview plays a song with. Empty: the one Lily# plays with.
- `shosa.server.path` — a language server of your own build.

## License

MIT. One program in the package is not: `shosa-music`, which reads Lily# songs and links Lily#, is under the
GPL (version 3 or later; `server/LICENSE-shosa-music.txt`). Its source is `Shosa.Music` in Shosa's repository.
