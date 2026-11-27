# Changelog

## Unreleased

- *Name:* the extension is **Shosa 3D Choreographer** in the Extensions view and the Marketplace (was Shosa), as its sister projects say what they are (Calamo Sheet Music Scanner, Orecchiante Music Transcriber). The commands, settings and files are still `shosa`.
- *Preview:* moving the position slider while it plays no longer stops the play: the music holds while the knob is held and plays on from where it is let go.
- *Settings:* named `shosa.<heading>.<setting>` like Lily#'s, Cantoria's and Bamen's, each under a heading of its own — **VRM Models** (`shosa.models.folder`) and **Developer** (`shosa.developer.languageServer`, was `shosa.server.path`) — where the Settings editor showed "Models: Folder" and "Server: Path" under one. A server path set under the old name is to be set again.
- *Preview:* **the sound font is Lily#'s**: `shosa.playback.soundFont` is gone, and a song plays with the font Lily# plays with — `lilysharp.playback.soundFont` (**Lily#: Choose Sound Font…**), else the Lily# Sound Font extension's, else the system's. One setting for Lily#, Shosa and Bamen: a song is read by Lily#, so where there is a song, Lily# is there.

## 0.3.0 — 2026-11-25

The first release, for Windows (x64).

### Writing

- The Shosa language for `.shosa` files: diagnostics, completion and hover for every word, from the same table the compiler checks against.
- **File > New File... > Shosa File** starts from a file that already compiles.
- A problem with a word of a `move` or a `pose` is shown once, at that word inside it, with the beats where it happens; it used to be shown at every place the move is used.
- **Sitting**: `height { sit(0.45m) }` sits down on a seat that high (a chair, a sofa, a stool), the feet on the floor, and any other height stands up again. Seated, the arms, the torso and the head say what they say standing.
- **Juggling**: `dancer … juggles balls` (or `clubs`, `rings`) and a `juggle` row in siteswap as jugglers write it: `juggle { every 1/2  3:4 | 531531 3 3 | }`. A pattern that cannot be juggled, or cannot follow the one before it, is said where it goes wrong. Tricks go on a pattern with `@`: `3:4@mills`, `@windmill`, `@tennis`, `@halfShower`, `423:4@takeouts`, catches behind the back and throws under the leg. The legs, the torso and the head dance their own rows meanwhile.
- Parts of the body you do not write are not frozen: breathing, the weight shifting from foot to foot, the trunk and head following the arms, and the arms swinging with a walk.

### Preview

- **Open Preview to the Side** (Ctrl+K V): the figures move as you type. The word being performed is lit in the source; put the cursor on a word to see its pose; click a limb to go to the word that put it there. An edit of a long dance does not hold the preview up: only the frames it moved are sent and worked out again.
- **Dancing to a song**: `music "song.lys"` takes the tempo, the time and the order of the sections from a [Lily#](https://yotsuda.github.io/LilySharp/) song, and the preview plays the song with the dance. The song is read by the Lily# extension, which is needed only for this.
- A score without a song is heard as a click on every beat and a clap at every mark (`!hit`).
- **Camera** (the button at the bottom of the preview): **Fit** (or F) frames the whole score, every dancer and everything they juggle from start to end, as close as it fits; **Zoom**, **Height** and **Angle** move the view by hand, and **Reset** puts it back. The view is kept, and the MP4 is filmed from it.
- *Preview:* **A VRM character for a dancer**: `dancer alice humanoid "girl.vrm" on lead` dresses the dancer in a VRM model in the preview and its MP4. The export of the movement does not change. `height 1.5m` draws the character that tall, to the top of its hair, so models made at different sizes stand side by side as you want them. A character's arms are kept out of its own body, which is not the shape the movement was solved on: the elbow finds its way round first, so a hand stays where the words put it. A hand a word puts at the head (`head`, `chin`, `forehead`) is put there on the character's own head, measured from its mesh, so it is not lost in a big head or its hair. Set `shosa.models.folder` to the folder of your models, and with the cursor on the file of a dancer line, Ctrl+Shift+Up/Down switch to the model before or after it. A dancer's file is looked for in the dance's own folder first, then in that folder, and the keys write it from that folder (`"girl.vrm"`).
- *Preview:* **Singing**: `dancer alice humanoid "girl.vrm" sings melody on lead` moves the character's mouth to the words that part of the Lily# song sings, and the `.vrma` export carries it. `mic right` puts a mic in the hand: it is at the mouth while the dancer sings, and dances with the arm between phrases.
- *Preview:* **Dancing to a recording**: when a song made from a recording has its `NAME.orecchiante.json` beside it, the dance keeps the recording's beat, and the preview and the MP4 play the recording. A recording that Lily# marks silent (`"silent": true`, after **Arrange an Accompaniment…**) still keeps the beat but is not played: the song's parts are heard instead.
- *Preview:* **Mixer**: with the Lily# extension 0.17.0 or later, which hands out a song one part at a time, the preview plays each part of the score, each singing voice and the recording (or its separated parts) at the level you give it, and the MP4 has the same mix.

### Export

- **Model**: glTF (`.glb`), VRM Animation (`.vrma`) or BVH (`.bvh`), with the marks for a game engine.
- **MP4**: a video of the preview with its sound, encoded by the editor itself (no ffmpeg). The shape is the view's, 16:9 (1920×1080) or 9:16 (1080×1920, for YouTube Shorts); a fixed shape is framed in the preview.
- *Preview:* **Audio**: the song as the Mixer has it, for as long as the dance, as an MP3 or a WAV (written by the Lily# extension).

### For AI assistants

- Copilot Chat's agent mode gets four tools: the language (`#shosaGrammar`), the compiler's verdict (`#shosaCheck`), a picture of the movement (`#shosaSheet`) and the song a dance is written to (`#shosaSong`: its sections with their bars and seconds, who sings, and the words bar by bar on their beats). `shosa song SONG.lys` prints the same in a terminal.
- **Add a Dance…** on a Lily# song (right-click a `.lys`): a dance file begun beside it with a dancer for each part that sings, opened with the preview, and Copilot's chat asked to choreograph it to the words.
- The compiler `shosa` comes with the extension and is on the PATH of VS Code's terminals (`shosa grammar` prints the whole language for an agent).
