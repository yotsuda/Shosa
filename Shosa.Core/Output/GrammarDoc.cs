using System.Text;
using Shosa.Core.Motion;
using Shosa.Core.Semantics;

namespace Shosa.Core.Output;

/// <summary>
/// The language in one file, for an AI that is about to write it: docs/GRAMMAR_FOR_LLM.md.
/// Every word list in it is printed from <see cref="Vocabulary"/>, the same table the binder
/// checks against, so the description cannot name a word the compiler does not know.
/// A test keeps the file in docs/ identical to what this prints.
/// </summary>
public static class GrammarDoc
{
    public static string Render()
    {
        var sb = new StringBuilder();

        void Table(string heading, Vocabulary.Word[] words)
        {
            sb.Append("| ").Append(heading).Append(" | Meaning |\n|---|---|\n");
            foreach (var word in words)
                sb.Append("| `").Append(word.Name).Append("` | ").Append(word.Doc).Append(" |\n");
            sb.Append('\n');
        }

        sb.Append("""
            # Shosa (`.shosa`) — Language Spec for LLMs

            Shosa is a text notation for the movement of human figures. A compiler turns the text into
            the movement of a jointed figure. You write WHAT each part of the body does and WHEN; the
            compiler decides how the body gets there (where knees and elbows go, how the weight shifts).

            This file is the whole language. A word that is not listed here does not exist: do not
            invent one. If a movement cannot be said with these words, say so instead of guessing.

            ## A complete file

            ```shosa
            // A comment runs to the end of the line.
            title "Fighter"
            tempo 120          // beats per minute, 10 to 400: here one beat is 0.5 s
            time 4/4           // beats per bar / beat unit

            // A pose: a named way of standing and holding the body.
            pose guard {
              steps { left.fwd.low@small }
              arms  { fwd.high@bent }
            }

            // A move: a named row of events. `!hit` names a moment for the game engine.
            move jab { leftArm { fwd:1@sharp!hit guard:1 } }

            // No `form` in this file, so each section is a clip of its own.
            section Idle loop {
              body { guard:8 }
              head { fwd.left:2 fwd:2 fwd.right:2 fwd:2 }
            }

            section Punch {
              body { guard:0 | jab:1 jab:1 hold:2 }
            }
            ```

            ## Document skeleton (top level)

            | Item | Meaning |
            |---|---|
            | `title "…"` | Optional. |
            | `tempo N` | Beats per minute. Optional; 90 if not written. |
            | `time N/N` | Beats per bar / beat unit. Optional; 4/4 if not written. One beat is the note of the lower number. |
            | `fps N` | Frames per second of the output, and what a length in frames counts. Optional; 30. |
            | `figure NAME "label"` | An owner of choreography. Only for a file with several bodies (see "Figures and dancers"). |
            | `dancer NAME humanoid …` | A body that dances a figure. Optional. |
            | `pose NAME { TRACK { target } … }` | A named pose. |
            | `move NAME { TRACK { events } … }` | A named move. |
            | `section NAME [loop] [inplace] { TRACK { … } … }` | A stretch of time: what each part does in it. |
            | `form { A B*2 A }` | The order the sections are performed in, as ONE clip; `*N` repeats one. |
            | `score NAME { … }` | A unit of output (see "Scores"). Optional. |

            Names are letters, digits and `_`. The words in the left column are reserved, and a pose
            or a move cannot take the name of a word of the language. Definitions can be used before
            or after the line that defines them.

            ## Clips: what a file produces

            - **Without a `form`, every section is a clip of its own**, and every clip starts from
              neutral: arms hanging, torso upright, head straight, feet side by side. This is the
              shape of a game character's file: `Idle`, `Walk`, `Attack` …
            - **With a `form`, the form is the one clip.** Where a figure is, which way it faces and
              how it is posed carry over from one section to the next. Two sections meant to be
              danced one after the other NEED a form.
            - `section NAME loop` — the end joins the start. The section is solved as if it followed
              itself, so it begins already in the state it ends in: a looping walk needs no starting
              pose, and `body { guard:8 }` in a loop simply stays in guard. The feet must end as
              they start (same stance, same facing; travelling is fine) or SH2005 says so.
            - `section NAME inplace` — written the same (`left.fwd` still steps), but the even travel
              over the floor is taken out of the output and reported as a velocity, for an engine
              that moves the character itself. Usually with `loop`.
            - A section lasts as long as its longest track. A part no track writes stays as it is.

            ## Tracks: one row per part of the body

            A section holds tracks. In a file without `figure` lines they sit directly in the section.


            """.ReplaceLineEndings("\n"));

        Table("Track", Vocabulary.Tracks);

        sb.Append("""
            Tracks may overlap: `body` covers everything, `arms` covers both arms. **Where two tracks
            write the same part at the same time, the narrower one wins**; when the narrower one has
            ended, or says `skip`, the part follows the wider one again. So `body { guard:8 }` with
            `head { fwd.left:2 … }` is a guard whose head looks around.

            ## Events: `target:length@annotation!mark`

            A track is a row of events. Each event is a target, then optionally `:` and a length,
            `@` annotations and one `!` mark — glued together, no spaces:

            ```
            side.high            one beat (the default)
            side.high:2          two beats
            side.high:1/2        half a beat          (also :1.5, :3/4)
            side.high:0.3s       0.3 seconds
            side.high:12f        12 frames (at the file's fps)
            side.high:2@sharp    two beats, with an accent
            fwd:1@sharp!hit      … and the moment it arrives is named `hit`
            hold:3               stay as you are for three beats
            skip:3               say nothing for three beats (a wider track may)
            ```

            **The part moves during the event and is AT its target when the event ENDS.** To be
            somewhere on beat 3, write the event that ends on beat 3. To arrive and then stay, follow
            it with `hold`.

            - Seconds and frames are turned into beats by the tempo, so they can be mixed with beats.
              Use them where no beat matters (a hit reaction, a gesture).
            - **`:0` is the pose a clip STARTS in**: no time passes, the body is simply there.
              It can only be written before anything else in the clip: `body { guard:0 jab … }`.
              Not needed in a `loop` (it starts where it ends).
            - `|` is a bar line. It is optional, but when written the bar it closes must have exactly
              the beats of the `time` (error SH1002). Write them: they catch miscounted lengths.
            - `repeat N { … }` performs the events inside N times; bar lines may be inside.
            - `{ … } & { … }` — rows that happen together, inside one track. All start at the same
              time; the whole is as long as the longest row. The braces are required on both sides.
              Where two rows write the same part, the LATER row wins:
              `body { { walk:4 } & { wave:4 } }` walks while one arm waves.
            - `!name` marks the moment the part arrives (half-way through a `@sharp` event, the landing
              of a jump). It does nothing to the movement; it is written to the output for the engine.

            ## Poses and moves

            ```
            pose guard { steps { left.fwd.low@small }  arms { fwd.high@bent } }
            move kick  { rightLeg { fwd.low:1@bent fwd:1@sharp!hit fwd.low:1@bent down:1 } }
            ```

            - A `pose` holds, per track, ONE target and no length. A `move` holds rows of events.
            - A defined name is written in a track like any word. **Without a length it lasts as long
              as it was defined** (a pose: one beat; a move: its longest track — `kick` above is 4
              beats). **With a length the whole of it is stretched or squeezed to fit**: `kick:2`.
              When counting bars, write the length.
            - **Written in a track, a definition does only the part that track covers.**
              `body { guard }` is the whole guard; `leftArm { guard }` is the guard's left arm (this
              is how `jab` returns to guard). A track that shares no part with it is an error (SH1015).
            - `kick@mirror` swaps left and right: the other leg kicks.
            - **A pose is a shape, not a movement.** A body already in it does not move. Its `steps`
              line means "standing as after this step": `guard` from neutral steps into the stance,
              `guard` again moves nothing. (A step written directly in `steps` always steps.)
            - A pose takes `@mirror`, `@free`, `@smooth`, `@sharp`, `@linear`; a move takes `@mirror`
              and `@free`. Definitions take no arguments. To vary one repeat, put the common part in a
              move and write the differing parts side by side.

            ## Gesture targets (arms, legs, torso, head)

            A target is a direction, written as words joined by dots: any of the direction words, and
            at most one level.


            """.ReplaceLineEndings("\n"));

        Table("Direction", Vocabulary.Directions);
        Table("Level", Vocabulary.Levels);
        Table("Alone", Vocabulary.Poles);

        sb.Append("""
            - Two direction words make a diagonal: `fwd.side`, `back.side.low`, `fwd.left.high`.
            - No level means `mid`. A level with no direction is not a target; use `up` or `down`.
            - **A number in the place of the level is degrees above the horizontal**, from -90 to 90:
              `fwd.30`, `side.-30`, `back.70`. (`high` is 45, `low` is -45.) An arm swinging while
              walking is `fwd.-70` / `back.-70`: 20° from hanging.
            - The number is always measured from the HORIZONTAL, also for the torso: `torso { fwd.60 }`
              leans 30° forward of upright (a bow), `fwd.80` is a nod of the whole body, `back.70`
              leans back 20°.
            - `up` and `down` stand alone: `fwd.up` is not a word — write `fwd.high`.
            - **Arms and legs say `side` / `across`, never `left` / `right`** (error SH1011). That is
              what lets `arms { side }` be right for both arms at once.
            - The torso and the head say `left` / `right`, never `side` / `across`.
            - For an arm the direction is where the HAND is from the shoulder; for a leg, where the
              foot is from the hip; for the torso, where the spine points (`up` is upright,
              `fwd.high` leans 45° forward, `fwd` is horizontal); for the head, where the face looks
              (`fwd` straight ahead, `fwd.left` turned, `fwd.-25` a nod down).
            - Arms, legs and torso are measured against the room: `down` is toward the floor even
              when the torso leans. The head is measured against the chest, so it follows a bow.
              So in a forward fold `arms { down }` hangs toward the floor, and in a bow unwritten
              arms hang in front of the thighs.
            - **An arm follows the torso only when its event says `@body`.** Arms left at `up` stay
              vertical while the torso leans to the side. For a side bend with the arms overhead,
              write the lean and, over the same beats, the arms again with `@body`:
              `torso { left.high:2 up:2 }` with `arms { up:2@body up:2 }`.
            - Bending: `@bent` brings the hand or foot closer along the same direction. A raised
              knee is `fwd@bent` (thigh about horizontal) or `fwd.low@bent` (lower); a guard with
              the fists near the chin is `fwd.high@bent`.
            - **Never send a part to the exact opposite of where it is** (`down` straight to `up`,
              `fwd` straight to `back`): the way round is then unwritten (warning SH2001). Go through
              a target in between — `arms { fwd up }` raises them through the front, `arms { side up }`
              through the side.

            An arm can also go to a place on the own body instead of a direction:


            """.ReplaceLineEndings("\n"));

        Table("Arm target", Vocabulary.Anchors);

        sb.Append("""
            `arms { clap:1/4!clap }` is a clap; `arms { across.shoulder }` folds the arms.

            In a file with figures, an arm or the head can take a place on ANOTHER figure,
            `FIGURE.PLACE`: `rightArm { b.hand:2 }` reaches for b's right hand, `head { b.head }`
            looks at b.


            """.ReplaceLineEndings("\n"));

        Table("Place", Vocabulary.Parts);

        sb.Append("""
            The figure named must be danced by exactly one dancer (SH1021). The arm only reaches as
            far as it is long: bring the two bodies within reach with their `at` and their steps.

            ### Legs

            `leftLeg` / `rightLeg` are for a leg that carries NO weight. Make the gesture, then put
            the leg back with `down`.

            - From standing on both feet nothing else is needed: the weight goes over the other
              foot by itself as the leg lifts, and comes back as it is put down. `steps` can be
              left out, or `hold`.
            - After a step the weight is on the foot that stepped, so the OTHER leg is free
              (after `left.fwd`, the right leg).
            - While either foot is in the middle of a step, or the other leg is also off the
              floor, a leg gesture is an error (SH2004).
            - A leg put `down` lands where it stood before the gesture.

            ## The feet: `steps`

            The `steps` track says where the weight is. Its events are:

            **A step** — `FOOT.DIRECTION`, optionally with a level: `left.fwd`, `right.side`,
            `left.back.low`. The foot moves and then carries the weight.


            """.ReplaceLineEndings("\n"));

        Table("Foot", Vocabulary.Feet);
        Table("Step direction", Vocabulary.StepDirections);

        sb.Append($$"""
            - **A step is placed from the OTHER foot, not from where the stepping foot was.**
              `left.fwd` puts the left foot one stride ({{Rig.Stride}} m) ahead of the right foot. So
              `left.fwd right.fwd left.fwd` is a walk, and `left.fwd right.side` puts the right foot
              level with the left one and apart from it: "forward, side", as a dancer counts it.
            - So **a foot already ahead does not advance by stepping "ahead" again**: from a stance
              with the left foot a small step in front, `left.fwd@small` puts it where it is. To
              move the stance forward, reach with the front foot and close up with the back one:
              `left.fwd right.back@small` (the right foot ends a small step behind the left).
            - `side` is {{(Rig.Stance + Rig.SideStep).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}} m apart; standing width is {{Rig.Stance}} m.
              Directions combine: `left.fwd.side`. `back` is the same {{Rig.Stride}} m as `fwd`, so
              two steps forward and two back end where they began.
            - `across` puts the foot just in front of the other one (0.12 m), crossed over;
              `back.across` just behind it — the cross-behind of a grapevine. A cross is not a
              stride. Since the next step is placed from the crossed foot, a line of
              side / cross / side drifts by that 0.12 m; alternate front and behind to cancel it.
            - A level on a step is the height you arrive at: `low` knees bent, `high` on the toes
              (only these words: degrees are not for the feet).
              **A step with no level arrives at normal height**, so after `right.side.high` a plain
              `left.fwd` comes down again; to stay up, write `.high` on every step.
            - `@small` and `@large` scale a step (0.6 and 1.4).
            - `left.fwd@turn(left.quarter)` turns DURING the step: the standing foot swivels, and the
              step lands where it would after the turn. `@turn(SIDE.AMOUNT)`, amounts below.
            - The same foot twice in a row is allowed (it moves again from the other foot).

            **Other things the feet do:**


            """.ReplaceLineEndings("\n"));

        Table("Event", Vocabulary.SupportActions);

        sb.Append("""
            - `together` takes a level like a step: `together.high` closes on the toes.
            - `both.low`, `both.mid`, `both.high` — both feet stay; the height changes (a knee bend,
              a rise onto the toes). `both` never travels.
            - `shift.left`, `shift.right`, `shift.center` — the weight moves over that foot (or
              between them); no foot leaves the floor. The basis of standing and waiting.
            - `turn.left.quarter`, `turn.right.half` — side, then amount:


            """.ReplaceLineEndings("\n"));

        Table("Turn amount", Vocabulary.TurnAmounts);

        sb.Append("""
              A turn pivots on the foot that took the last step (on both, if the feet are together).
              After it "forward" is the new facing. In a file with figures, `turn.b` turns to face
              figure b.
            - `jump`, optionally with one or two of these words (`jump.fwd`, `jump.apart`):


            """.ReplaceLineEndings("\n"));

        Table("Jump word", Vocabulary.JumpWords);

        sb.Append("""
              The figure is in the air in the middle of the event and has landed, on both feet, when it
              ends. How high it goes follows from the time it has; it is not written.
              `jump@turn(right.half)` turns in the air.
            - `lift(0.4)` — ONLY for a free body (`@free` on the event, or `free` on the dancer line;
              otherwise SH1022): the feet hang 0.4 m above the floor until told otherwise.
              `lift.fwd(0.4)` / `lift.back(0.4)` also travel. `land` comes back to the floor.
              A double jump: `steps { lift(0.4):0.3s@free lift(0.9):0.3s@free land:0.4s@free }`.
            - `hold` — stay.

            ## Annotations


            """.ReplaceLineEndings("\n"));

        Table("Annotation", Vocabulary.Annotations.Select(w => new Vocabulary.Word("@" + w.Name, w.Doc)).ToArray());

        sb.Append("""
            `@bent` is for arm and leg directions; `@body` for arm directions; `@small` / `@large`
            for steps and jumps; `@turn(…)` for steps and jumps. Several can follow one event:
            `fwd:1@bent@sharp`.

            ## Figures and dancers

            A file for ONE body needs neither: its tracks sit directly in the sections, and the body
            is a humanoid at the origin, facing front. To change that, one line, without `on`:
            `dancer mage humanoid free`.

            Several bodies:

            ```
            figure lead
            figure chorus
            dancer alice humanoid on lead   at 0 1
            dancer bob   humanoid on chorus at -1.5 0
            dancer carol humanoid on chorus at 1.5 0  mirror

            section Verse {
              lead   { steps { left.side together right.side together } }
              chorus { arms  { side.high:2 down:2 } }
            }
            ```

            - A `figure` owns choreography; a `dancer` is a body that performs it. In a file with
              figures, EVERY track sits inside a figure's block (`lead { steps { … } }`).
            - `dancer NAME humanoid on FIGURE`, then any of, on the same line:
              `at X Z` (metres; x is stage left, z toward the audience), `facing front | back |
              stageLeft | stageRight` (or degrees; positive turns to the dancer's own left),
              `mirror` (left and right swapped), `delay N` (starts N beats late: a canon),
              `free` (weight, gravity and speed limits never apply), `in SECTION …` (dances only there).
            - Several dancers can dance one figure. A figure with no dancer line is danced by a
              default body at the origin.
            - Two bodies face to face, 1 m apart:
              `dancer ann humanoid on a at -0.5 0 facing stageLeft` and
              `dancer ben humanoid on b at 0.5 0 facing stageRight`.

            ## Scores

            A `score` is a unit of output; the file's default score exists without being written.

            ```
            form short { Chorus }                    // a named form is used by a score
            score practice { form short  tempo 80 }  // shosa … --score practice
            score solo     { dancer alice }          // only alice appears
            ```

            Inside: `form NAME`, `tempo N`, `title "…"`, `dancer NAME`. What is not written is the
            file's own. `music "song.lys"` (dancing to a song written in Lily#) is accepted but not
            read yet: tempo, time and form are still the ones in this file.

            ## What the language cannot say (yet)

            Do not try to express these; mention them as a limitation instead.

            - Hands and fingers, the face, the shoulders on their own, twisting the torso.
            - Holding an object. Contact other than a hand reaching a place.
            - Walking along a curve. Running (both feet off the floor between steps); hopping on one leg.
            - Sitting, kneeling, lying down, or anything else where a part other than the feet
              carries weight. Standing on one leg is possible (a leg gesture).
            - Arm swing while walking is not added for you: unwritten arms hang still.
            - A change of tempo or of time inside a file.
            - Where an elbow or a knee points.

            ## Checking your work

            ```
            shosa check FILE            diagnostics, or "no problems" and the clips with their lengths
            shosa sheet FILE -o X.png   the movement on one image per clip (X.Idle.png, X.Punch.png …)
            shosa sheet FILE -o X.png --clip Punch --step 0.5 --from 0 --to 4   one clip, closer poses
            ```

            Diagnostics are `FILE(line,column): severity CODE: message`. SH0xxx is syntax, SH1xxx is
            a word or a count that does not make sense, SH2xxx is a movement the body cannot do
            (SH2002 a step longer than the leg, SH2003 too fast, SH2004 both feet off the floor,
            SH2005 a loop that does not join). The message says what to change.

            The sheet shows poses numbered by beat from 0, read left to right, then down. How far
            apart they are depends on the length shown: every half beat up to 8 beats, every beat up
            to 32, every 2 beats beyond. **Anything shorter than that gap is invisible** — a half-beat
            punch on a per-beat sheet shows nothing — so before concluding that a movement is missing,
            look closer with `--step` and `--from` / `--to`. Bold numbers start a bar.
            **Left limbs are blue, right limbs are red**; the torso and shoulder line are dark. The camera stands
            in front and a little to stage left, so a figure facing front shows its left on the right
            of the picture; the dot on the head is the nose. Under the poses is the floor from above,
            audience at the bottom: the grey line is the path of the hips, the blue and red marks are
            footprints, the ring is where the figure started. Always look at the sheet: a file with
            no diagnostics can still be the wrong movement (a mirrored arm, a step the wrong way).

            ## Rules and gotchas

            - No `form`: each section is its own clip and starts from neutral. Start an action clip
              with its pose: `body { guard:0 … }`.
            - An event ends AT its target. `arms { up:4 }` takes four beats to get there.
            - A definition without a length takes its defined length; write the length when counting.
            - Limbs: `side` / `across`. Torso and head: `left` / `right`.
            - Degrees are from the horizontal, for the torso too.
            - A step is measured from the other foot.
            - Opposite targets need one in between.
            - A leg gesture ends with `down`, and never overlaps a step.
            - A step with no level comes back to normal height.
            - Arms do not lean with the torso unless the event says `@body`.
            - `body` takes only your poses and moves, `hold` and `skip`.
            - Count every bar and write the `|`.

            """.ReplaceLineEndings("\n"));

        return sb.ToString();
    }
}
