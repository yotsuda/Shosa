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
            | `music "song.lys"` | The song this is danced to (see "Dancing to a song"). Optional. |
            | `figure NAME "label"` | An owner of choreography. Only for a file with several bodies (see "Figures and dancers"). |
            | `dancer NAME humanoid …` | A body that dances a figure. Optional. |
            | `pose NAME { TRACK { target } … }` | A named pose. |
            | `move NAME { TRACK { events } … }` | A named move. |
            | `section NAME [loop] [inplace] { TRACK { … } … }` | A stretch of time: what each part does in it. |
            | `form { A B A }` | The order the sections are performed in, as ONE clip. Repeat signs: see "Doing it again". |
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
              A bounce in that guard is one more row, `steps { { both.low@small both.low } q q q }`:
              `both` changes the height and leaves the stance alone (see "Tracks").
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
            `head { fwd.left:2 … }` is a guard whose head looks around, and `arms { side:4 }` with
            `leftArm { up:4 }` puts the right arm to the side and the left arm up.

            The `steps` row holds two parts: where the feet stand, and how high the body stands on
            them. `both.low` writes only the height, so beside a wider track it takes the height
            and leaves the feet: `body { guard:8 }` with `steps { { both.low@small both.low } q q q }`
            is a guard that bounces, `body { walk:4 }` with `steps { both.low:4 }` the same walk
            with bent knees. A step, a turn or a jump writes the feet, and **a step cannot be cut
            short**: where a narrower track moves the feet, a wider track's step that overlaps it
            in time is not taken at all (a pose's stance included).

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
            it with `hold`. Beats are counted from 0, here, on the sheet and in `shosa check`: beat 2
            is the moment the third beat of the bar begins. A clap that a dancer counts "on 3" is an
            event that ENDS at beat 2: `arms { side.low:3/2 clap:1/2 … }`.

            - Seconds and frames are turned into beats by the tempo, so they can be mixed with beats.
              Use them where no beat matters (a hit reaction, a gesture).
            - **`:0` is the pose a clip STARTS in**: no time passes, the body is simply there.
              It can only be written before anything else in the clip: `body { guard:0 jab … }`.
              Not needed in a `loop` (it starts where it ends).
            - `|` is a bar line. It is optional, but when written **the bar it closes must have exactly
              the beats of the `time`** (error SH1002). Write them: they catch miscounted lengths, and
              they line the tracks up. In a row that has bar lines, the events after the last one must
              fill a bar too (warning SH1002): pad with `skip` or `hold`.
            - **An event that runs across a bar line is tied over it**: `~`, the line, then the rest
              of its length alone. In 4/4, `body { jab:2 kick:2~ | :1 hold:3 | }` is a kick of three
              beats that starts on beat 3. It is ONE event (annotations and the mark go on the
              first part). Its length is the sum of the parts as written, for a defined move too:
              a 4-beat `kick` that starts on the last beat of a 3/4 bar is `kick:1~ | :3`
              (`kick~ | :3` would be 4 + 3 beats). Staying still needs no tie: `hold:4 | hold:4 |`.
            - `{ … } & { … }` — rows that happen together, inside one track. All start at the same
              time; the whole is as long as the longest row. The braces are required on both sides.
              Where two rows write the same part, the LATER row wins:
              `body { { walk:4 } & { wave:4 } }` walks while one arm waves.
            - `!name` marks the moment the part arrives (half-way through a `@sharp` event, the landing
              of a jump). It does nothing to the movement; it is written to the output for the engine.
              `shosa check` lists every mark with its beat and its second, and the sheet writes
              it in green over the first pose at or after it.

            ## Doing it again

            Three signs, by how much is done again. (There is no `repeat`.)

            - **A few events: `{ … }` and `q`.** Put the unit in braces; each `q` after it does it
              again: `rightArm { { up:1/2@bent side.high:1/2@bent } q q }` waves three times.
              `q` is the nearest group in braces written before it in the same track (plain events
              in between do not matter: `{ a b } hold:1 q`). Without a length it lasts as long as
              the group; `q:2` stretches or squeezes the group into 2 beats; `q@mirror` swaps left
              and right (`steps { { left.side together } q@mirror }` goes left, then right). A `q`
              with no group before it is an error (SH1026): one event is simply written twice.
            - **A bar: `%`.** Alone between bar lines, `%` is the bar before it again; `%2` is the
              two bars before it: `steps { left.side together right.side together | % | % | % | }`,
              `steps { vine:4 | vine@mirror:4 | %2 | }`. The bars must be whole (no event tied into
              them from before, SH1027; ties between the bars of a `%4` are fine) and in the same
              row of the same section. A `:0` in them is not done again. **To change one bar of a run, write that bar out instead of its `%`**:
              `a b c clap | % | % | a b c jump |`.
            - **Sections: repeat signs in the `form`.** `form { |: A :| B }` plays A A B;
              `|: A :|*4` four times; numbered endings `form { |: A [1. B] :| [2. C] }` play
              A B A C (`[1-2. B]` is the ending of passes 1 and 2; every pass needs exactly one).
            - A change INSIDE a longer run is written over it, in a narrower track that says `skip`
              up to that moment: `body { vine:4 | rock:4 | %2 | }` with `arms { skip:4 | % | % | skip:3 up:1 | }`
              raises the arms on the last beat only (see "Tracks": the narrower wins).

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
            - **It writes only the parts its own rows hold, and is as wide as the track it is written
              in.** `body { kick:4 }` (a move of the right leg) leaves the arms to any other track,
              `arms` included; a `rightLeg` track beside it is narrower than `body` and wins over the kick.
            - `kick@mirror` swaps left and right: the other leg kicks. Only the move is mirrored, not
              the body it starts from: out of a one-sided stance, first free the other leg
              (`steps { shift.right }` puts the weight on the right foot).
            - **A pose is a shape, not a movement.** A body already in it does not move. Its `steps`
              line means "standing as after this step": `guard` from neutral steps into the stance,
              `guard` again moves nothing. (A step written directly in `steps` always steps.)
            - A pose takes `@mirror`, `@free`, `@smooth`, `@sharp`, `@linear`; a move takes `@mirror`
              and `@free`. Definitions take no arguments: a variation is the same word with the
              change written over it (a narrower track, or a later `&` row). A move may hold a
              `body` row of other moves, so a whole pass can be one word:
              `move pass { body { vine:4 vine@mirror:4 } }`.

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
            far as it is long (about 0.6 m from the shoulder): bring the two bodies within reach with
            their `at` (where the dancer stands: the middle of the feet) and their steps. A hand
            that does not get there is a warning that says how far short it is (SH2006).

            Two hands that reach for each other can meet at another height, when BOTH say the same
            level: `a { rightArm { b.hand.high } }` with `b { rightArm { a.hand.high } }` is a high
            five above the heads; `.mid` meets at shoulder height, `.low` beside the hips.

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
              `hold` and a leg gesture keep the height they find: after a `.low` step the knees
              stay bent until a step without a level, or `both.mid`.
            - `@small` and `@large` scale a step (0.6 and 1.4): how far it goes, not its height.
            - `left.fwd@turn(left.quarter)` turns DURING the step: the standing foot swivels, and the
              step lands where it would after the turn. `@turn(SIDE.AMOUNT)`, amounts below.
            - The same foot twice in a row is allowed (it moves again from the other foot).

            **Other things the feet do:**


            """.ReplaceLineEndings("\n"));

        Table("Event", Vocabulary.SupportActions);

        sb.Append("""
            - `together` takes a level like a step: `together.high` closes on the toes.
            - `both.low`, `both.mid`, `both.high` — no foot moves; the height changes (a knee bend,
              a rise onto the toes). The weight stays where it was too. Here `@small` / `@large` scale the bend:
              `both.low@small` is a slight bend, `both.low@large` a deep crouch, so a light bounce in
              a low stance is `both.low@small both.low`, and a duck is `both.low@large`.
            - `shift.left`, `shift.right`, `shift.center` — the weight moves over that foot (or
              between them); no foot leaves the floor, and the height stays as it is. The basis
              of standing and waiting.
            - `turn.left.quarter`, `turn.right.half` — side, then amount:


            """.ReplaceLineEndings("\n"));

        Table("Turn amount", Vocabulary.TurnAmounts);

        sb.Append("""
              A turn pivots on the foot that carries the weight: the one that took the last step or
              that a `shift` went to (on both, if the feet are together). A free leg may gesture during a turn.
              After it "forward" is the new facing. In a file with figures, `turn.b` turns to face
              figure b.
            - `jump`, optionally with one or two of these words (`jump.fwd`, `jump.apart`):


            """.ReplaceLineEndings("\n"));

        Table("Jump word", Vocabulary.JumpWords);

        sb.Append("""
              The figure is in the air in the middle of the event and has landed, on both feet, when it
              ends. How high it goes follows from the time it has; it is not written. It lands at the
              height it took off from (low stays low), and a plain `jump` keeps the feet as far apart as they were.
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
            for steps, jumps and `both` / `together`; `@turn(…)` for steps and jumps. Several can
            follow one event, and the length, the annotations and the mark may come in any order:
            `fwd:1@bent@sharp`, `kick@mirror:2`.

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
              `at X Z` (metres; x is stage left — the dancers' left, so the RIGHT of the sheet,
              which looks from the audience; z toward the audience), `facing front | back |
              stageLeft | stageRight` (or degrees; positive turns to the dancer's own left),
              `mirror` (left and right swapped), `delay N` (starts N beats late: a canon),
              `free` (weight, gravity and speed limits never apply), `in SECTION …` (dances only there;
              in the other sections the dancer is still on the floor, standing as they were left).
            - `delay N` makes the clip N beats longer: the others stand still while the late dancer
              finishes. `mirror` mirrors turns as well, so after a quarter turn a mirrored dancer faces
              the opposite way to the others.
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

            Inside: `form NAME`, `tempo N`, `title "…"`, `dancer NAME`, `music "…"`. What is not
            written is the file's own.

            ## Dancing to a song

            `music "song.lys"` names a song written in Lily#, in a file beside this one. The dance
            then takes its tempo, its time and the order of its sections from the song, and the
            whole song is ONE clip.

            ```
            music "song.lys"                  // or: music "song.lys" score karaoke  (one of the song's scores)

            section Verse  { arms { side:4 | up:4 | } }           // danced while the song plays its Verse
            section Chorus { arms { clap:1 side:3 | % | % | % | } }
            ```

            - **A section is danced during the song's section of the same name**, every time the
              song plays it. Write no `form`: the order is the song's. There is one text per name:
              a section played twice is danced the same both times (a second `section Chorus`
              is an error), except for what it does not write, which stays as the section before left it.
            - A section shorter than the song's stands still for the rest of it. A longer one is an
              error (`SH1903` says how many beats and bars the song's section has). A section of
              the song that this file does not have is stood through.
            - A section whose name the song does not have is never danced (`SH1905` lists the
              song's sections).
            - **Do not write `time`**: the bars are the song's (`SH1902`). Count the bars of each
              section in the song's time; `shosa check` prints where each section falls.
            - **Do not work the song's shape out of the `.lys`: write `music` and one short section,
              and run `shosa check`.** It prints
              `to song.lys at 120 beats a minute, 6 to the bar: Verse 0-49 (upbeat of 1, then 8 bars from beat 1), Chorus 49-97 (8 bars)`.
              The beat is the note of the lower number of the song's time, and the tempo is counted
              in that beat — it need not be the number after `tempo` in the `.lys` (a 6/8 song
              marked 60 is 120 here), and a song that writes none is 120. A `tempo N` here counts the same beat.
            - If the song's section begins with an upbeat, the dance of it begins at its first bar
              line, and the upbeat is stood through. Every beat number after it (in `check`, on the
              sheet, of a mark) is later by the upbeat: with an upbeat of 1 in 6/8 the bars start
              at 1, 7, 13 …. The sheet's bold numbers follow the song's bar lines.
            - **If the song changes tempo or time on the way, the dance follows it**, and nothing
              is written for that here. `shosa check` says where:
              `at 120 beats a minute (90 from beat 12, 120 from beat 16), 4 to the bar (3 from beat 16)`.
              After a change of time a bar has the new number of beats — the bar lines are held
              to it, `%` does a bar of it again — and a beat is the note of the new time's lower
              number (in 6/8 a bar is 6 beats). A change of tempo moves no beat: only the
              seconds of the beats after it change (`check` gives a mark's beat and its second).
              `:0.3s` and `:12f` are counted at the tempo their section begins in.
            - To change what the song gives: `tempo N` dances (and plays the song) at that tempo
              (the tempo the song BEGINS at, if it changes: the later ones keep their proportion);
              `form { Chorus Verse Chorus }` plays the song's sections in that order — its names
              are the song's, whether this file dances them or not. Both can be written in a
              `score`, so one file has the whole song and a practice cut:
              `form short { Chorus }` and `score practice { form short  tempo 80 }`.
            - `loop` and `inplace` do nothing in a file that is danced to a song.
            - Do not write `music` unless you were given the song: without its file the line is an
              error (`SH1901`).

            ## What the language cannot say (yet)

            Do not try to express these; mention them as a limitation instead.

            - Hands and fingers, the face, the shoulders on their own, twisting the torso.
            - Holding an object. Contact other than a hand reaching a place.
            - Walking along a curve. Running (both feet off the floor between steps); hopping on one leg.
            - Sitting, kneeling, lying down, or anything else where a part other than the feet
              carries weight. Standing on one leg is possible (a leg gesture).
            - Arm swing while walking is not added for you: unwritten arms hang still.
            - A change of tempo or of time written in this file (a dance follows the changes of its song; see "Dancing to a song").
            - Where an elbow or a knee points.

            ## Checking your work

            ```
            shosa check FILE            diagnostics, or "no problems" and the clips with their lengths; then every mark
            shosa sheet FILE -o X.png   the movement on one image per clip (X.Idle.png, X.Punch.png …; just X.png for one clip)
            shosa sheet FILE -o X.png --clip Punch --step 0.5 --from 0 --to 4   one clip, closer poses
            shosa sheet FILE -o X.png --view side   seen from stage left: a figure facing front is in profile
            shosa sheet FILE -o X.png --score solo  one score: with `score solo { dancer bea }`, bea alone and larger
            ```

            Diagnostics are `FILE(line,column): severity CODE: message`. SH0xxx is syntax, SH1xxx is
            a word or a count that does not make sense, SH2xxx is a movement the body cannot do
            (SH2002 a step longer than the leg, SH2003 too fast, SH2004 both feet off the floor,
            SH2005 a loop that does not join, SH2006 a hand that cannot reach the other dancer,
            SH2007 two dancers whose heads or bodies are in the same place). The message says what to change.

            The sheet shows poses numbered by beat from 0, read left to right, then down. How far
            apart they are depends on the length shown: every half beat up to 8 beats, every beat up
            to 32, every 2 beats beyond. **Anything shorter than that gap is invisible** — a half-beat
            punch on a per-beat sheet shows nothing — so before concluding that a movement is missing,
            look closer with `--step` and `--from` / `--to`. Bold numbers start a bar.
            In a clip of several sections, a section's name stands in purple beside the first pose in it.
            With more than one dancer, each one's name is under where it stands, and at its ring on the floor.
            **Left limbs are blue, right limbs are red**; the torso and shoulder line are dark. The camera stands
            in front and a little to stage left, so a figure facing front shows its left on the right
            of the picture; the short line on the head is the nose. From there a punch or a kick
            straight ahead is foreshortened: judge those, bows and nods with `--view side`.
            Under the poses is the floor from above,
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
            - Count every bar and write the `|`. An event across a bar line is tied: `kick:2~ | :1`.
            - Again: `{ … } q` for a few events, `%` for a bar, `|: A :|` in the form for sections.

            """.ReplaceLineEndings("\n"));

        return sb.ToString();
    }
}
