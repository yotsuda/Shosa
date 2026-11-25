# Shosa

Write human movement as text, compile it, and watch a figure perform it beside the source.
Shosa is to movement what [Lily#](https://yotsuda.github.io/LilySharp/) is to music: a small language
you can read, diff and mend, which compiles to animation files a game engine loads.

<!-- VIDEO: samples/hello.shosa in VS Code. Drop the .mp4 here in GitHub's editor; it writes the link. -->

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

A word stands where the body arrives, as a note stands where it sounds: `up` on beat 1 means the arm is up
on beat 1, and the way there is in the time before.

## What it is for

- **Characters' movement for small game teams with no animator, and for AI agents that build games.**
  An AI cannot write joint curves, but it can write and fix Shosa: the whole language is one text an agent
  reads ([docs/GRAMMAR_FOR_LLM.md](docs/GRAMMAR_FOR_LLM.md)), and the compiler checks what it wrote and draws
  it as a picture to look at ([docs/FOR_AGENTS.md](docs/FOR_AGENTS.md)).
- **Choreography to a song.** `music "song.lys"` takes the tempo, the time and the order of the sections from a
  Lily# song; the dance keeps to its beats, and the preview plays the song with it.
- **A dance notation that runs**: the same core with a vocabulary per genre.

It does not try to replace finished animation. Walking, running and jumping are well served by asset packs
and generative models; Shosa's worth is in movement fitted to the beat, a change to one part that leaves the
rest alone, readable diffs, and the same result every time.

## Output

- glTF (`.glb`), VRM Animation (`.vrma`) and BVH (`.bvh`), for Godot, Blender, Unity and the rest.
- MP4 of the preview, with the song (or a click on every beat and a clap at every mark).
- Marks for the engine (`!hit`) travel with the animation as events.

## Status

Early: 0.3.0 is the first release, as a
[VS Code extension](https://marketplace.visualstudio.com/items?itemName=yotsuda.shosa) for Windows x64.
Packages for Linux x64 and macOS arm64 can be built but have not been run, and are not published yet.
The design notes and the plan are in Japanese.

## The VS Code extension

A side preview that moves as you type, diagnostics, completion and hover for every word, export to the files
above, and tools that let Copilot Chat's agent write Shosa (`#shosaGrammar`, `#shosaCheck`, `#shosaSheet`).
The compiler `shosa` comes with it and is on the PATH of VS Code's terminals. See
[editors/vscode/README.md](editors/vscode/README.md).

Install **Shosa** from the Extensions view (Windows x64), open [samples/hello.shosa](samples/hello.shosa)
— the dance in the video above — and press Ctrl+K V.

To build and install it on this PC (Windows): `tools\deploy-extension.ps1`, then *Developer: Reload Window*.

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
| `samples` | Example files, among them dances to two Lily# songs |
| `docs` | The plan (`ROADMAP.md`), the design and its trials (`DESIGN.md`), the language for an AI (`GRAMMAR_FOR_LLM.md`), the guide for agents (`FOR_AGENTS.md`) |
| `trials` | Files AI agents wrote from the grammar alone, kept as evidence of what the language lets them write |
| `tools` | Packing, installing and checking (Blender, Godot, the extension host) |

## License

MIT (`LICENSE`).

What you write in Shosa, and the animations, videos and images made from it, are yours, with no licence of
Shosa's on them.
