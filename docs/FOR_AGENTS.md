# Shosa for AI agents

How an AI agent that builds a game (or anything with a character in it) writes the
character's movement with Shosa. The language itself is in
[GRAMMAR_FOR_LLM.md](GRAMMAR_FOR_LLM.md); this file is the work around it: what to set up,
the loop to follow, and what comes out for the engine.

## What Shosa is for

You write a `.shosa` text file; the compiler turns it into an animation of a humanoid
skeleton (glTF, VRM Animation, BVH). Use it for movement that is the character's own —
an attack combo, an idle, a gesture, a dance to the beat — and that you want to correct
one word at a time and keep in git. It is a rough, readable movement, not final-quality
animation; for a stock walk or run, an asset library is the better source.

## Set up

The compiler is the program `shosa`. It comes with the Shosa extension for VS Code, which
puts it on the PATH of VS Code's terminals: in one, `shosa` runs by its name. (The
extension is not published yet. Until it is, or outside VS Code, build it from the
repository with the .NET 10 SDK: `dotnet build Shosa.Cli` →
`Shosa.Cli/bin/Debug/net10.0/shosa(.exe)`.)

```
shosa grammar -o SHOSA_GRAMMAR.md # the language, as this build of the compiler knows it
```

Take the grammar from `shosa grammar` rather than from a copy: it is generated from the
table the compiler checks against, so the two cannot disagree. Dancing to a Lily# song
(`music "song.lys"`) also needs `shosa-music` beside `shosa`: the extension carries it;
from the repository it is built by `dotnet build Shosa.slnx` with a checkout of LilySharp
beside the repository.

**In VS Code, an assistant that uses the editor's tools (Copilot Chat in agent mode) needs
none of this file's setup**: the extension gives it three tools — `shosa_grammar` (the
language), `shosa_check` (what `shosa check` prints, for the text as the editor has it; `score` and
`at` as `--score` and `--at`)
and `shosa_sheet` (the sheet as an image). The rest of this file is for an agent that
works through the command line.

## For the project's agent instructions

Put this in the project's `CLAUDE.md` / `AGENTS.md`, with the paths as they are there:

```markdown
## Character movement (Shosa)

Movement is written in `anim/*.shosa` and compiled; never edit the `.glb` files.

- Before writing or changing a `.shosa` file, read `docs/SHOSA_GRAMMAR.md` in full. A word
  that is not in it does not exist. If a movement cannot be said, say so; do not guess.
- After every change: `shosa check anim/hero.shosa`. Fix until it says `no problems`.
- Then `shosa sheet anim/hero.shosa -o out/hero.png` and LOOK at the image of each score
  you changed (left limbs blue, right limbs red). No diagnostics does not mean the right
  movement. For anything shorter than a beat: `--score NAME --step 0.25`; for a punch,
  kick or bow straight ahead: `--view side`.
- Then `shosa gltf anim/hero.shosa --combined -o assets/hero.glb`.
- One movement of the character = one `section`, one `form` and one `score` of the same
  name. The score's name is the animation's name in the engine.
```

## The loop

1. **Read the grammar in full**, once per session. It is written to be enough on its
   own: in eleven trials, agents given nothing else wrote 109 files, and none was left
   with a diagnostic.
2. **Write**, starting from "A complete file" in the grammar. A character's file has one
   `score` per movement (`Idle`, `Walk`, `Attack` …).
3. **`shosa check FILE`** prints `FILE(line,column): severity CODE: message` for each
   problem — the message says what to change — or `no problems`, then every score with
   its length and each section's beats, bars, seconds, tempo and time, then every mark
   with its beat and second. Take lengths and times from here; do not compute them.
   A line ``solo starts with `left.fwd` already made`` under a score is not a problem: it
   says the score begins with that foot already there, so that first step is not seen
   (right for a stance or a dance to a song; where the step must be seen, a `then` before it).
   A line for each dancer says where it is at the end: ``solo ends 1 m forward and 0.35 m to
   its left of where it began, turned 90° to its left``, ``solo ends where it began``; for a
   loop, how far it travels each time round (``loops on the spot`` when it does not).
   Forward and left are the dancer's own at the start. Check "comes back to where it began" here.
4. **`shosa sheet FILE -o X.png`** and look. How to read the image, and what it cannot
   show at its default spacing, is under "Checking your work" in the grammar.
   Under each pose the figure is drawn from above (hips grey with a dot at each end, shoulders
   blue and red, the nose): which way it faces and which shoulder or hip is ahead in a
   `twist` or under `hips` are read there — the direction, not the degrees.
   The degrees are in **`shosa check FILE --at 3,0.75s`** (beats; seconds with an `s`): a line
   for each moment, ``Punch at beat 3 = 0.75 s: hips 20° left, shoulders 45° left, face 30°
   right and 10° up``, all from the way the feet face.
   What is left (by how many centimetres, how high a fist
   arrives, whether the head moved) is in **`shosa clip FILE -o X.json`**: every joint's
   x y z at every frame, and the same angles at every frame (the layout is in the same place
   of the grammar; one file, an array of the scores when there are several, `--score NAME` for one).
5. **Export** (below). To change the movement later, change the words and run 3 to 5
   again; never touch the output.

## What comes out

```
shosa gltf FILE --combined -o hero.glb   every score in one file, an animation each: for a game engine
shosa gltf FILE -o hero.glb              a file per score: hero.glb (the unnamed score), hero-Idle.glb …
shosa vrma FILE -o hero.vrma             VRM Animation, a file per score and per dancer: goes on any VRM model
shosa bvh  FILE -o hero.bvh              for Blender and motion tools
```

- **Skeleton**: 19 bones with the VRM humanoid names — `hips spine chest neck head`,
  `left`/`right` + `Shoulder UpperArm LowerArm Hand UpperLeg LowerLeg Foot` (the
  shoulders never turn; they are there because characters have them). Metres, y up, the
  figure faces +z, 1.7 m tall, rest pose a T-pose. There is no mesh: the animation is
  put on your character by the engine's humanoid retargeting (a `.vrma` on a VRM model
  with three-vrm needs none of it).
- **Animation names**: the score's name; for the unnamed score, the file's `title`
  (`shosa` if it has none).
- **Sampling**: every bone at every frame, 30 a second unless the file says `fps N`.
- **`extras` of each glTF animation** — what the engine needs besides the bones:

  ```json
  { "loop": true,
    "velocity": { "hero": [0, 0, 1.0] },
    "marks": [ { "time": 0.5, "beat": 1, "name": "step", "figure": "hero", "section": "Walk", "play": 1 } ] }
  ```

  - `loop` — the score says `loop`: the end joins the start.
  - `velocity` — only with `inplace`: metres a second, [x, y, z], for each dancer. The
    travel is taken out of the animation; the engine moves the character by this.
  - `marks` — the `!name` of the file, only if there are any: `time` in seconds from
    the start of the animation, the `beat`, the figure that wrote it, the section it is
    in and which play of that section this is (from 1). Fire the hit, the footstep
    sound, the effect from these. A mark is at the beat its word is written at — the
    moment the fist is out, the foot lands, the jump comes down — so a looping walk's first
    footstep is at `time` 0: fire it at the start and again each time the loop wraps.
- **`.vrma` and `.bvh`** have nowhere to keep that, so it is written beside them as
  `NAME.events.json`: `score` (its name) and `seconds` (its length), then the same
  `loop`, `velocity` and `marks`, field for field.

Reading the extras in three.js (the loader keeps the JSON it parsed):

```js
const gltf = await new GLTFLoader().loadAsync('hero.glb');
gltf.parser.json.animations.forEach((a, i) => {
  const clip = gltf.animations[i];            // same order
  const { loop, velocity, marks = [] } = a.extras;
  // mixer.clipAction(clip).setLoop(loop ? THREE.LoopRepeat : THREE.LoopOnce)
  // each frame: fire every mark whose time was passed since the last frame
});
```

**Godot 4** reads the file (a `Skeleton3D` for each dancer, one `AnimationPlayer`, an
animation for each score), but two of its import defaults need changing:

- It **drops the `extras`**, `loop` included: every animation comes in not looping.
  Copy `tools/godot_shosa_import.gd` from the Shosa repository into the project and name
  it as the `.glb`'s import script. It sets each animation's `loop_mode`, keeps the
  extras as `animation.get_meta("shosa")` (`{ loop, velocity, marks }`), and makes the
  marks a signal: a node `ShosaMarks` beside the `AnimationPlayer` emits
  `mark_reached(mark, figure, section, play)` when a playing animation passes one.

  ```gdscript
  $Hero/ShosaMarks.mark_reached.connect(func(mark, figure, section, play):
      if mark == "hit": deal_damage())
  ```
- Its **animation optimizer thins the keys**: bones end up to 6 mm off, and a quick foot
  was 108 mm off for one frame. Turn it off for exact movement (0.13 mm).

Both, in the `.glb.import` file (or in the Import dock and the Advanced Import Settings):

```
import_script/path="res://godot_shosa_import.gd"
_subresources={
"nodes": {
"PATH:AnimationPlayer": {
"optimizer/enabled": false
}
}
}
```

**On a character with a mesh (Godot's retargeting).** Give both files a `BoneMap`: the
animation's `.glb` and the character's. Then Godot names both skeletons
`GeneralSkeleton` and the bones by its own humanoid names (`SkeletonProfileHumanoid`:
Shosa's names with the first letter in upper case, `hips` → `Hips`), and the animations
of one play on the other.

```
godot --headless --path PROJECT -s tools/godot_bonemap.gd -- hero.glb shosa_bonemap.tres
godot --headless --path PROJECT -s tools/godot_bonemap.gd -- character.glb character_bonemap.tres
```

(Or by hand in the editor: open the `.glb`, select `Skeleton3D`, put a new `BoneMap` in
Retarget > Bone Map and choose `SkeletonProfileHumanoid` as its Profile. The editor
guesses the bones from their names at that moment, all 19 of Shosa's.)

`tools/godot_bonemap.gd` (in the Shosa repository) writes the map: for Shosa's file all
19 bones, for a VRM model (renamed to `.glb`; Godot reads it without an add-on, the toon
materials become plain ones) the bones its `VRMC_vrm` / `VRM` extension names. It prints
the line to add to each `.glb.import`, beside the optimizer's under `"nodes"`:

```
"PATH:hero/Skeleton3D": {
"retarget/bone_map": Resource("res://shosa_bonemap.tres")
}
```

In the game, give the character an `AnimationPlayer` with the animation file's library,
and a copy of its `ShosaMarks` for the signal (the animations call a node of that name
beside the player's root node; without it Godot warns `couldn't resolve track:
'ShosaMarks'` and plays on):

```gdscript
var moves := preload("res://hero.glb").instantiate()       # not added to the tree
var player := AnimationPlayer.new()
$Character.add_child(player)
player.add_animation_library("", moves.get_node("AnimationPlayer").get_animation_library(""))
$Character.add_child(moves.get_node("ShosaMarks").duplicate())
$Character/ShosaMarks.mark_reached.connect(_on_mark)
player.play("Combo")
```

Godot scales the travel of the hips by the character's size (`motion_scale`, the height
of its hips: a character with shorter legs takes shorter steps and its feet do not
slide), so `velocity` of an `inplace` score is to be scaled the same way:
`velocity * character_skeleton.motion_scale / 0.98`. Limbs keep their angles, not their
reach: a hand that touches the head on Shosa's figure lands where the character's
proportions put it. Through an `AnimationTree` the marks are signalled for every
animation that has a weight in the blend.

`tools/godot_view.gd` draws frames of an animation as Godot plays it, the bones as
sticks and beside them the character: `godot --path PROJECT -s tools/godot_view.gd --
hero.glb C:\full\path\out.png --animation Combo --on character.glb [--view side]`.

Checked so far: Blender 5.3 reads the glTF and the BVH with every bone within 0.14 mm
of the solver; Godot 4.7.2 reads the glTF as said above (positions compared and the
signals counted headless; frames drawn and looked at, alone and retargeted onto a VRoid
model; the editor's import dialog opened and its guessed bone map read, Reimport not
pressed there); three-vrm plays the `.vrma`
on a VRoid model; the Khronos glTF Validator reports no errors and no warnings.
**Not checked: Unity (UniVRM).** If an engine drops the `extras`, read them from the
file's JSON at import, or export `--score NAME` with `vrma`/`bvh` for the `.events.json`.

## What to expect of the movement

- The solver is fast and exact about WHEN and WHERE (a mark is at its beat, a foot is
  where the step put it) and plain about HOW: no arm swing unless written, no fingers,
  no face. The grammar's "What the language cannot say (yet)" is the list; tell the user
  when a request falls in it instead of approximating in silence.
- The same text always gives the same movement, on every machine.
- A diff of a `.shosa` file reads as the change in the movement. Commit the `.shosa`;
  the exported files can be built again.
