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
            title "Walk and wave"
            tempo 100          // beats per minute, 10 to 400
            time 4/4           // beats per bar / beat unit

            figure a "Walker" { rig humanoid }

            phrase Walk {
              a {
                steps    { left.fwd right.fwd left.fwd together | hold:4 }
                rightArm { hold:4 | side.high:1@bent up:1/2@bent side.high:1/2@bent down:2 }
              }
            }

            form { Walk }
            ```

            ## Document skeleton (top level, in this order)

            1. `title "…"`, `tempo N`, `time N/N` — each optional (defaults: tempo 90, time 4/4).
            2. One or more figures: `figure NAME "label" { rig humanoid }`. The label is optional.
               Inside the braces, also optional: `at X Z` (start position in metres; x is stage left,
               z is toward the audience) and `facing front | back | stageLeft | stageRight` (or a number
               of degrees; positive turns to the figure's own left). Default: at 0 0, facing front.
               The properties are separated by spaces, in any order. Two figures 3 m apart, face to face:
               `figure a { rig humanoid at -1.5 0 facing stageLeft }`
               `figure b { rig humanoid at 1.5 0 facing stageRight }`
            3. One or more phrases: `phrase NAME { FIGURE { TRACK { … } TRACK { … } } … }`.
               A track ALWAYS sits inside a figure's block, even when there is only one figure.
            4. `form { A B*2 A }` — the order the phrases are performed in; `*N` repeats one.
               Without a form, every phrase is performed once, in the order written.

            Names are letters, digits and `_`. `title tempo time figure phrase form` are reserved.

            ## Tracks: one row per part of the body


            """.ReplaceLineEndings("\n"));

        Table("Track", Vocabulary.Tracks);

        sb.Append("""
            - A track a phrase does not write keeps doing what it did last. At the start every part is
              neutral: arms hanging, torso upright, head straight, feet side by side.
            - Where a figure is, which way it faces and how it is posed all carry over from one phrase
              to the next. Nothing resets.
            - A phrase lasts as long as its longest track. Shorter tracks hold for the rest.
            - `arms` and `leftArm`/`rightArm` cannot both be written in one phrase for one figure.

            ## Events: `target:beats@annotation`

            A track is a row of events. Each event is a target, then optionally `:` and a length in
            beats, then optionally one or more `@` annotations — glued together, no spaces:

            ```
            side.high          one beat (the default)
            side.high:2        two beats
            side.high:1/2      half a beat        (also :1.5, :3/4)
            side.high:2@sharp  two beats, with an accent
            hold:3             stay as you are for three beats
            ```

            **The part moves during the event and is AT its target when the event ENDS.** To be
            somewhere on beat 3, write the event that ends on beat 3. To arrive and then stay, follow
            it with `hold`.

            - `|` is a bar line. It is optional, but when written the bar it closes must have exactly
              the beats of the `time` (error SH1002). Write them: they catch miscounted lengths.
            - `repeat N { … }` performs the events inside N times.

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
            - `up` and `down` stand alone: `fwd.up` is not a word — write `fwd.high`.
            - **Arms and legs say `side` / `across`, never `left` / `right`** (error SH1011). That is
              what lets `arms { side }` be right for both arms at once.
            - The torso and the head say `left` / `right`, never `side` / `across`.
            - For an arm the direction is where the HAND is from the shoulder; for a leg, where the
              foot is from the hip; for the torso, where the spine points (`up` is upright,
              `fwd.high` leans 45° forward, `fwd` is horizontal); for the head, where the face looks.
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
            - The torso leans 45° (`.high`) or 90° (`.mid`); no angle in between can be written.
            - **Never send a part to the exact opposite of where it is** (`down` straight to `up`,
              `fwd` straight to `back`): the way round is then unwritten (warning SH2001). Go through
              a target in between — `arms { fwd up }` raises them through the front, `arms { side up }`
              through the side.

            An arm can also go to a place on the body instead of a direction:


            """.ReplaceLineEndings("\n"));

        Table("Arm target", Vocabulary.Anchors);

        sb.Append("""
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
            - `side` is {{(Rig.Stance + Rig.SideStep).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}} m apart; standing width is {{Rig.Stance}} m.
              Directions combine: `left.fwd.side`. `back` is the same {{Rig.Stride}} m as `fwd`, so
              two steps forward and two back end where they began.
            - `across` puts the foot just in front of the other one (0.12 m), crossed over;
              `back.across` just behind it — the cross-behind of a grapevine. A cross is not a
              stride. Since the next step is placed from the crossed foot, a line of
              side / cross / side drifts by that 0.12 m; alternate front and behind to cancel it.
            - A level on a step is the height you arrive at: `low` knees bent, `high` on the toes.
              **A step with no level arrives at normal height**, so after `right.side.high` a plain
              `left.fwd` comes down again; to stay up, write `.high` on every step.
            - `@small` and `@large` scale a step (0.6 and 1.4).
            - The same foot twice in a row is allowed (it moves again from the other foot).

            **Other things the feet do:**


            """.ReplaceLineEndings("\n"));

        Table("Event", Vocabulary.SupportActions);

        sb.Append("""
            - `together` takes a level like a step: `together.high` closes on the toes.
            - `both.low`, `both.mid`, `both.high` — both feet stay; the height changes (a knee bend,
              a rise onto the toes). `both` never travels.
            - `turn.left.quarter`, `turn.right.half` — side, then amount:


            """.ReplaceLineEndings("\n"));

        Table("Turn amount", Vocabulary.TurnAmounts);

        sb.Append("""
              A turn pivots on the foot that took the last step (on both, if the feet are together).
              After it "forward" is the new facing. A turn is its own event: a step cannot turn.
            - `jump`, optionally with one or two of these words (`jump.fwd`, `jump.apart`):


            """.ReplaceLineEndings("\n"));

        Table("Jump word", Vocabulary.JumpWords);

        sb.Append("""
              The figure is in the air in the middle of the event and has landed, on both feet, when it
              ends. How high it goes follows from the tempo; it is not written.
            - `hold` — stay.

            ## Annotations


            """.ReplaceLineEndings("\n"));

        Table("Annotation", Vocabulary.Annotations.Select(w => new Vocabulary.Word("@" + w.Name, w.Doc)).ToArray());

        sb.Append("""
            `@bent` is for arm and leg directions; `@body` for arm directions; `@small` / `@large`
            for steps and jumps. Every event starts from rest where the last one ended; no event
            begins before its own beats.

            ## What the language cannot say (yet)

            Do not try to express these; mention them as a limitation instead.

            - Contact: a clap, holding hands, a hand on another figure, holding an object.
            - Hands and fingers, the face, the shoulders on their own, twisting the torso.
            - Turning while stepping or jumping; walking along a curve.
            - Sitting, kneeling, lying down, or anything else where a part other than the feet
              carries weight. Standing on one leg is possible (a leg gesture); hopping on one is not.
            - Arm swing while walking is not added for you: unwritten arms hang still.
            - A change of tempo or of time inside a file.
            - A starting pose: every figure starts neutral, and getting anywhere takes an event.
            - One repeat that differs from the others. `A*2` and `repeat` play the same thing; to
              change the second time, write a second phrase and name both in the form.
            - Where an elbow or a knee points, and any angle other than the 45° steps.

            ## Checking your work

            ```
            shosa check FILE            diagnostics, or "no problems"
            shosa sheet FILE -o X.png   the movement on one image
            shosa sheet FILE -o X.png --step 0.5 --from 8 --to 16   closer poses, part of the piece
            ```

            Diagnostics are `FILE(line,column): severity CODE: message`. SH0xxx is syntax, SH1xxx is
            a word or a count that does not make sense, SH2xxx is a movement the body cannot do
            (SH2002 a step longer than the leg, SH2003 too fast for the tempo, SH2004 both feet off
            the floor). The message says what to change.

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

            - A track goes inside a figure block: `phrase P { a { steps { … } } }`.
            - An event ends AT its target. `arms { up:4 }` takes four beats to get there.
            - Limbs: `side` / `across`. Torso and head: `left` / `right`.
            - A step is measured from the other foot.
            - Opposite targets need one in between.
            - A leg gesture ends with `down`, and never overlaps a step.
            - A step with no level comes back to normal height.
            - Arms do not lean with the torso unless the event says `@body`.
            - Count every bar and write the `|`.

            """.ReplaceLineEndings("\n"));

        return sb.ToString();
    }
}
