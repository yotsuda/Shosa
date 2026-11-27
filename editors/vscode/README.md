# Shosa

Write movement as text, or have an AI write it, and watch characters dance, sing and juggle to a song beside the source.
Film it as an MP4, or export it for a game engine.

```
tempo 120

role solo
dancer solo humanoid on solo

section Wave {
  solo {
    steps    { leftFoot.side together rightFoot.side together }
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
  The song is read by the Lily# extension, which is needed only for this: without it, everything else works.
- **More than steps and arms**: `height { sit(0.45m) }` sits on a chair; `dancer … juggles balls` with
  `juggle { every 1/2  3:4 | 531531 3 3 | }` juggles in siteswap, with tricks (`3:4@mills`, `@windmill`, `423:4@takeouts`).
  Parts you do not write are not frozen: the body breathes, shifts its weight, and swings its arms as it walks.
- Marks for a game engine (`!hit`) are shown as they arrive. A score without a song is heard too: a click on every beat and a clap at every mark, so its timing can be judged.
- **Export** from the preview (the buttons at its top right, or *Shosa: Export…* in the command palette), the score shown:
  **Model** writes it for an engine — glTF (`.glb`), VRM Animation (`.vrma`) or BVH (`.bvh`), by the type picked in the save dialog;
  **MP4** writes a video of the preview as you have turned it, with the song (or the clicks and claps of a score without one). Nothing else has to be installed (no ffmpeg):
  the video is encoded by the editor itself. The box beside it picks the shape: **View** (the preview as it is), **16:9** (1920×1080) or
  **9:16** (1080×1920, for YouTube Shorts); a fixed shape is framed in the preview, so what you see is what is filmed.
- **Camera** (the button at the bottom of the preview): **Fit** (or the F key) brings the view as close as the whole score fits —
  every dancer and everything they juggle, from start to end, with no more floor than that. **Zoom**, **Height** and **Angle**
  move it by hand (so do a drag, the wheel and a right drag), **Reset** puts it back. The view is kept, and the MP4 is filmed from it.
- **An AI assistant can write it**: in Copilot Chat's agent mode, ask for a movement in Shosa. The extension gives the assistant
  the language (`#shosaGrammar`), the compiler's verdict (`#shosaCheck`), a picture of the movement to look at (`#shosaSheet`)
  and the song a dance is written to (`#shosaSong`: its sections with their bars, who sings, and the words bar by bar on their beats).
- **Add a Dance…** (right-click a Lily# song, `.lys`): asks which of the song's scores, a character for each part that sings,
  and what the dance should be like; writes `SONG.shosa` beside the song with a dancer for each singing part, opens it with the preview,
  and asks Copilot's chat to choreograph it to the words. The same is `shosa song SONG.lys` in a terminal.

The compiler (`shosa`) writes the movement as BVH, glTF and VRM Animation. It comes with the extension and is on the
PATH of VS Code's terminals: `shosa check FILE`, `shosa sheet FILE -o X.png`, `shosa gltf FILE --combined -o X.glb`
(`shosa --help` for the rest; `shosa grammar` prints the whole language for an AI agent that will write it).

## Preview features

These work, but are new and may still change.

- **A VRM character for a dancer**: `dancer alice humanoid "girl.vrm" on lead` dresses the dancer in a VRM model,
  in the preview and in its MP4. What **Model** exports does not change: the movement, for any character.
  `height 1.5m` on the line draws the character that tall (to the top of its hair): models are made at their own sizes,
  and dancers who should be the same size are given the same height. The character's arms are kept out of its
  own body (its trunk and head are measured from the model): the elbow goes round, and the hand stays where the words put it.
  A hand a word puts at the head (`head`, `chin`, `forehead`) goes to that place on the character's own head.
  Set `shosa.models.folder` to the folder you keep your models in; with the cursor on the file of a dancer line,
  **Ctrl+Shift+Up / Ctrl+Shift+Down** put the model before or after it there, and the preview changes with it.
  A dancer's file is looked for in the dance's own folder first, then in that folder: the keys write the short name
  (`"girl.vrm"`), so a dance does not say where on your machine the models are.
- **Singing**: `dancer alice humanoid "girl.vrm" sings melody on lead` moves the character's mouth to the words
  that part of the Lily# song sings (the `.vrma` export carries it; it needs the Lily# extension 0.18.0 or later,
  which tells the syllables and their beats, as `shosa song` and Add a Dance… need too). `mic right` puts a mic in the hand: at the mouth
  while the dancer sings, dancing with the arm between phrases.
- **Dancing to a recording**: when a song made from a recording has its `NAME.orecchiante.json` beside it,
  the dance keeps the recording's beat, and the preview and the MP4 play the recording.
- **Mixer** (the button at the bottom of the preview): each part of the score, each singing voice and the recording
  (or its separated parts) with a level of its own, 0 to 200 %. The preview and the MP4 play that mix.
  It needs the Lily# extension 0.17.0 or later, which hands out a song one part at a time; with an older one, the Mixer says so
  and the song plays as before.
- **Audio** (the button beside **MP4**, there with the Mixer): the song as the Mixer has it, for as long as the dance, as an MP3 or a WAV
  (written by the Lily# extension).

## Settings

- `shosa.models.folder` — the folder your VRM models are kept in.
- `shosa.developer.languageServer` — a language server of your own build.

The preview plays a song with the sound font Lily# plays with: choose it with **Lily#: Choose Sound Font…** (`lilysharp.playback.soundFont`).

## License

MIT.
The third-party software the package carries (three.js, the synthesiser, the .NET runtime and others) is listed,
each with its licence, in `THIRD-PARTY-NOTICES.txt`.

**What you write and export is yours.** The `.shosa` files you write, and the animations, videos and images
Shosa makes from them (glTF, VRM Animation, BVH, MP4, MP3, WAV, PNG), are your work: no licence of Shosa's applies to
them, and you may use them in a game or anything else, sold or not, without credit. (A song you dance to, and
heard in an MP4 or an MP3, keeps its own rights, as does a sound font it is played through.)
