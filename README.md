# Shosa

Write movement as text, or have an AI write it, and watch characters dance, sing and juggle to a song.
Shosa is to movement what [Lily#](https://yotsuda.github.io/LilySharp/) is to music: a small language
you can read, diff and mend. It keeps to the beats of a song, and compiles to a video or to animation files
a game engine loads.

**A dance to a song** (30 s, with sound) — [samples/hello.shosa](samples/hello.shosa)

https://github.com/user-attachments/assets/d7ae6452-aa83-461f-b32f-29bd82b1691d

**Juggling, written in siteswap** (52 s) — [samples/juggle.shosa](samples/juggle.shosa)

https://github.com/user-attachments/assets/0feaac58-2745-4985-ac92-d5753ad17819

From the first video:

```
music "hello.lys"

role zunda
role metan
dancer zundamon humanoid "models/zundamon.vrm" height 150cm sings zunda on zunda at -0.8 0
dancer zunko    humanoid "models/zunko.vrm"    height 150cm sings metan on metan at  0.8 0

// Step out and close, both ways: the hands meet as the feet do.
move touchClap {
  steps  { rightFoot.side(0.45m) together leftFoot.side(0.45m) together }
  arms   { side.down(40) clap@sharp side.down(40) clap@sharp }
}

section Chorus {
  zunda { body { touchClap:4 | point:4 | meAndYou:4 | together2:4 | … } }
  metan { body { touchClap:4@mirror | point:4@mirror | youAndMe:4 | … } }
}
```

And from the second, one juggler's first bars:

```
juggle { every 1/2
         3:4 | 3:4@reverse |          // a cascade, then thrown over the top
         531:3 3:1 | 441:3 3 4 |      // a tower; a box of 4s; a 4 into the shower
         …
         3:4@mills | 3:4@windmill |   // Mills' Mess; a windmill
```

A word stands where the body arrives, as a note stands where it sounds: `clap` on beat 2 means the hands meet
on beat 2, and the way there is in the time before. The song's tempo, time and sections come from the Lily# song;
the mouths move to the words it sings.

## What it is for

- **Videos of characters performing to music.** VRM characters dance, sing with a mic in hand, and juggle
  (balls, clubs and rings, in siteswap, with tricks), to a Lily# song or to a recording. The preview films its
  own MP4, with the song: 16:9, or 9:16 for YouTube Shorts.
- **Movement an AI can write.** An AI cannot write joint curves, but it can write and fix Shosa: the whole
  language is one text an agent reads ([docs/GRAMMAR_FOR_LLM.md](docs/GRAMMAR_FOR_LLM.md)), and the compiler checks
  what it wrote and draws it as a picture to look at ([docs/FOR_AGENTS.md](docs/FOR_AGENTS.md)). With Lily# writing
  the song, the whole video can come from text an agent wrote.
- **The same movement in a game.** It exports as glTF, VRM Animation and BVH, with marks (`!hit`) as events.
  Shosa helps where a game needs movement of its own: dances fitted to a song, gestures, juggling, a fighter's
  combo. Walking, running and jumping are better taken from asset packs and generative models.
- **A dance notation that runs**: the same core, with a vocabulary per genre.

Its strengths are movement fitted to the beat, several dancers who see each other, a change to one part that
leaves the rest alone, readable diffs, and the same result every time. It does not capture movement, it builds
it from poses on beats: clear and on time, but not yet as natural as motion capture.

So it is best at **dances that hit a pose on the beat**: pop and idol choreography, gestures fitted to the words
of a song, several dancers in unison or mirrored. Dances whose point is technique are beyond it today: the
vocabularies `shosa:ballet` and `shosa:break` are experimental (ballet reads as a beginner at the barre, breaking
has the shapes without the momentum).

## Trying it

Install **Shosa 3D Choreographer** from VS Code's Extensions view (Windows x64), open [samples/hello.shosa](samples/hello.shosa),
and press Ctrl+K V. The preview moves as you type.

- It dances to a song, so it also wants the
  [Lily#](https://marketplace.visualstudio.com/items?itemName=yotsuda.lilysharp) extension, which reads the song
  and sings it (the voices are NEUTRINO's). [samples/wave.shosa](samples/wave.shosa) needs nothing.
- **To dance to a recording of your own** (an MP3 of any song): install
  [Orecchiante](https://marketplace.visualstudio.com/items?itemName=yotsuda.orecchiante), right-click the recording
  and choose **Convert Recording to Lily# (.lys)…**. It writes `song.lys` beside it: the tempo, the bar lines,
  the chords and the sections, with no notes to write. `music "song.lys"` then dances to the recording's own
  beats, with the sections the chart names, and the preview and the MP4 play the recording.
- The characters of the videos (Zundamon and Tohoku Zunko, © SSS LLC.) are not in the repository:
  [samples/models/README.md](samples/models/README.md) says where they come from and on what terms. Without them
  the dancers are drawn as sticks. Any VRM model will do: the extension's
  [README](editors/vscode/README.md#preview-features) lists places to download one.
- [samples/juggle.shosa](samples/juggle.shosa) is the second video: four jugglers for fifty seconds. Three balls
  in siteswaps (531, 441, 52512, 7131, 45141, a synchronous box) and tricks (Mills' Mess, a windmill, takeouts),
  five balls (645, 744, 97531), five clubs and seven rings.

To have an AI write it, ask Copilot Chat's agent for a movement in Shosa: the extension gives it the language
(`#shosaGrammar`), the compiler's verdict (`#shosaCheck`) and a picture of the movement (`#shosaSheet`).
Other agents start from [docs/FOR_AGENTS.md](docs/FOR_AGENTS.md).

## The VS Code extension

A side preview that moves as you type, diagnostics, completion and hover for every word, a camera, a mixer for
the song's parts, and export: **MP4** (the preview with the song), **Audio** (MP3 or WAV), and **Model** (`.glb`,
`.vrma`, `.bvh`). The compiler `shosa` comes with it and is on the PATH of VS Code's terminals. See
[editors/vscode/README.md](editors/vscode/README.md).

To build and install it on this PC (Windows): `tools\deploy-extension.ps1`, then *Developer: Reload Window*.

## Status

Early: 0.3.0 is the first release, as a
[VS Code extension](https://marketplace.visualstudio.com/items?itemName=yotsuda.shosa) for Windows x64.
Packages for Linux x64 and macOS arm64 can be built but have not been run, and are not published yet.
The design notes and the plan are in Japanese.

## Help wanted, most of all from people who dance

The movement is only as good as its vocabulary, and that was written by a programmer and an AI. Pull requests
are welcome:

- **A pose or a move, corrected or added**, in the built-in vocabularies (`Shosa.Core/Library/*.shosa`: basics,
  pop, jazz, ballet, break). They are plain Shosa, a few lines each.
- **A dance** for [samples/dances](samples/dances), in any style.
- **A quality of movement.** A `quality` row knows `through`, `sharp` and `smooth`; swinging, suspended and
  collapsing movement are not there yet.
- **A comparison with the real thing.** `tools/amc2shosa.py` writes motion capture (ASF/AMC, as in CMU's
  database) out as Shosa, to see what the language cannot say.

An issue that says "this does not look like a chassé, because …" helps as much as a pull request.

## Building

Needs the .NET 10 SDK, and Node.js for the extension.

```
dotnet build Shosa.slnx
dotnet test Shosa.slnx
Shosa.Cli\bin\Debug\net10.0\shosa.exe check samples\dance.shosa
Shosa.Cli\bin\Debug\net10.0\shosa.exe sheet samples\walk.shosa -o out\walk.png
Shosa.Cli\bin\Debug\net10.0\shosa.exe gltf samples\walk.shosa -o out\walk.glb
cd editors\vscode; npm run build; npm test
```

Songs are read by the compiler of [Lily#](https://github.com/yotsuda/LilySharp), `lysc` (`lysc timeline`,
`notes`, `midi`, `wav`): Shosa builds alone, and needs Lily# installed only to dance to a song. `shosa` looks
for `lysc` in `SHOSA_LYSC`, then on the PATH, then in the Lily# extension installed in VS Code
([docs/DESIGN.md](docs/DESIGN.md), section 92).

## The repository

| Folder | What is in it |
|---|---|
| `Shosa.Core` | The language: syntax tree, binder, the movement it compiles to, the writers of BVH, glTF and VRM Animation |
| `Shosa.Cli` | `shosa`, the compiler: `check`, `sheet`, `preview`, `bvh`, `gltf`, `vrma`, `midi`, `wav`, `grammar` |
| `Shosa.Lsp` | The language server the extension runs |
| `Shosa.Tests` | The tests |
| `editors/vscode` | The VS Code extension |
| `samples` | Example files, among them dances to Lily# songs |
| `docs` | The plan (`ROADMAP.md`), the design and its trials (`DESIGN.md`), the language for an AI (`GRAMMAR_FOR_LLM.md`), the guide for agents (`FOR_AGENTS.md`) |
| `trials` | Files AI agents wrote from the grammar alone, kept as evidence of what the language lets them write |
| `tools` | Packing, installing and checking (Blender, Godot, the extension host) |

## License

MIT (`LICENSE`).

What you write in Shosa, and the animations, videos and images made from it, are yours, with no licence of
Shosa's on them.
