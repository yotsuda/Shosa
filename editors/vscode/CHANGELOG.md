# Changelog

## 0.3.0 — Unreleased

The first release, for Windows (x64).

### Writing

- The Shosa language for `.shosa` files: diagnostics, completion and hover for every word, from the same table the compiler checks against.
- **File > New File... > Shosa File** starts from a file that already compiles.
- Parts of the body you do not write are not frozen: breathing, the weight shifting from foot to foot, and the trunk and head following the arms.

### Preview

- **Open Preview to the Side** (Ctrl+K V): the figures move as you type. The word being performed is lit in the source; put the cursor on a word to see its pose; click a limb to go to the word that put it there.
- **Dancing to a song**: `music "song.lys"` takes the tempo, the time and the order of the sections from a [Lily#](https://yotsuda.github.io/LilySharp/) song, and the preview plays the song with the dance. The song is read by the Lily# extension, which is needed only for this.
- A score without a song is heard as a click on every beat and a clap at every mark (`!hit`).
- *Preview:* **A VRM character for a dancer**: `dancer alice humanoid "girl.vrm" on lead` dresses the dancer in a VRM model in the preview and its MP4. The export of the movement does not change. `height 1.5m` draws the character that tall, to the top of its hair, so models made at different sizes stand side by side as you want them. A character's arms are kept out of its own body, which is not the shape the movement was solved on: the elbow finds its way round first, so a hand stays where the words put it. Set `shosa.models.folder` to the folder of your models, and with the cursor on the file of a dancer line, Ctrl+Shift+Up/Down switch to the model before or after it.
- *Preview:* **Dancing to a recording**: when a song made from a recording has its `NAME.orecchiante.json` beside it, the dance keeps the recording's beat, and the preview and the MP4 play the recording.
- *Preview:* **Mixer**: with the Lily# extension 0.17.0 or later, which hands out a song one part at a time, the preview plays each part of the score, each singing voice and the recording (or its separated parts) at the level you give it, and the MP4 has the same mix.

### Export

- **Model**: glTF (`.glb`), VRM Animation (`.vrma`) or BVH (`.bvh`), with the marks for a game engine.
- **MP4**: a video of the preview with its sound, encoded by the editor itself (no ffmpeg). The shape is the view's, 16:9 (1920×1080) or 9:16 (1080×1920, for YouTube Shorts); a fixed shape is framed in the preview.
- *Preview:* **Audio**: the song as the Mixer has it, for as long as the dance, as an MP3 or a WAV (written by the Lily# extension).

### For AI assistants

- Copilot Chat's agent mode gets three tools: the language (`#shosaGrammar`), the compiler's verdict (`#shosaCheck`) and a picture of the movement (`#shosaSheet`).
- The compiler `shosa` comes with the extension and is on the PATH of VS Code's terminals (`shosa grammar` prints the whole language for an agent).
