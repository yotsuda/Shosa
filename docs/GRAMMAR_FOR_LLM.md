# Shosa (`.shosa`) — Language Spec for LLMs

Shosa is a text notation for human movement. A compiler turns the text into the movement
of a jointed body. You write WHAT each part of the body does and WHEN; the
compiler decides how the body gets there (where knees and elbows go, how the weight shifts).

This file is the whole language. A word that is not listed here does not exist: do not
invent one. If a movement cannot be said with these words, say so instead of guessing.

## A complete file

```shosa
// A comment runs to the end of the line.
title "Fighter"
tempo 120          // beats per minute, 10 to 400: here one beat is 0.5 s
time 4/4           // beats per bar / beat unit

role hero                      // who the choreography is written for
dancer hero humanoid on hero     // a body that dances it

// A pose: a named way of standing and holding the body.
pose guard {
  steps { leftFoot.fwd(0.3m) }
  height { low(8cm) }
  arms  { fwd.up(25).bent }          // the fists up in front of the face, the elbows down
  torso { up.fwd(10).round }         // leaning 10 degrees forward, the back rounded
  head  { fwd }                      // the face level, looking ahead over the lean
}

// A move: a named row of words. `!hit` names a moment for the game engine.
// The fist is out ON the beat the jab is written at, and back in guard a beat later.
// The same direction as the guard, straight: the fist goes out along one line. (To another
// direction it would swing: from `fwd.up(25).bent` to `fwd` it comes down like a chop.)
move jab { leftArm { fwd.up(25)@sharp!hit guard } }

// A section: what each role does over a stretch of time.
section Idle {
  hero {
    body { guard:8 }
    head { fwd.left:2 fwd:2 fwd.right:2 fwd:2 }
  }
}

section Punch {
  hero {
    body { guard jab:1 jab:2 | }       // in guard on beat 0; the jabs land on beats 1 and 2
  }
}

// A form: the sections that are played, in order.
form Idle  { Idle }
form Punch { Punch }

// A score: ONE file written — who dances, and which form.
score Idle  { dancer hero  form Idle  loop }
score Punch { dancer hero  form Punch }
```

**Nothing of this skeleton can be left out.** Every file has at least one `role`, one
`dancer`, one `section`, one `form` and one `score`. The least a file can be:

```shosa
role solo
dancer solo humanoid on solo

section Main {
  solo {
    steps { leftFoot.fwd rightFoot.fwd leftFoot.fwd stand }
  }
}

form { Main }
score { dancer solo }
```

## Document skeleton (top level)

| Item | Meaning |
|---|---|
| `title "…"` | Optional. |
| `tempo N` | Beats per minute. Optional; 90 if not written. Also `tempo 4. = 60` (see "A tempo: three spellings"). |
| `time N/N` | Beats per bar / beat unit. Optional; 4/4 if not written. One beat is the note of the lower number. It is the time of every section that writes none of its own. |
| `fps N` | Frames per second of the output, and what a length in frames counts. Optional; 30. |
| `using "parts.shosa"` | Another file joins this one (see "Files of parts: `using`"). Optional; as many as you like. |
| `music "song.lys"` | The song this is danced to (see "Dancing to a song"). Optional. |
| `role NAME` | An owner of choreography. Required: at least one (see "Roles and dancers"). |
| `dancer NAME humanoid on ROLE …` | A body that dances a role. Required: at least one. |
| `pose NAME { TRACK { target } … }` | A named pose. |
| `move NAME { TRACK { events } … }` | A named move. |
| `section NAME { ROLE { TRACK { … } … } … }` | A stretch of time: what each role does in it. It may begin with lines of its own — `tempo N`, `time N/N`, `upbeat N` — before its first block (see "What a section begins with"). |
| `form { A B A }` | The order the sections are played in. Required: every score plays one. The unnamed score plays this one; `form NAME { … }` is for the score that names it (a file whose scores all name their form has no unnamed one). Repeat signs: see "Doing it again". A `tempo N` or `time N/N` before a name: see "What a section begins with". |
| `score { dancer NAME … }` | One file written: who dances, and which form. Required: at least one. `score NAME { … }` is one more (see "Scores"). |
| `formation NAME { DANCER at X Z … }` | Where each dancer stands in a formation; `go.NAME` in `steps` walks there (see "Formations"). Optional. |

Names are letters, digits and `_`. The words in the left column are reserved, and a pose
or a move cannot take the name of a word of the language — any word of any table here,
so not `low`, `side`, `center`, `stand` either (SH1005 says so); nor can a role or a dancer
(SH1003). (A mark's name, after its `!`, can be any word at all.) Definitions can be used before
or after the line that defines them. A role and its dancer may have the same name.

## A tempo: three spellings

Wherever a `tempo` is written — at the top of the file, at the start of a section,
before a name in a form, in a score — it is one of:

| Written | Meaning |
|---|---|
| `tempo 120` | 120 beats a minute. A beat is the note of the lower number of the time in force: a crotchet in 4/4, a quaver in 6/8. |
| `tempo 4. = 60` | 60 of that NOTE a minute, as Lily# writes it: `4` a crotchet, `8` a quaver, `2` a minim, a dot after it for a dotted one. For the pulse a dancer feels when it is not the beat: in 6/8, `tempo 4. = 60` (two dotted crotchets to the bar) is the same as `tempo 180`. Only the tempo is said this way: a bar of 6/8 still has 6 beats, and lengths are still in them. |
| `tempo 50%` | Half the tempo there would be here WITHOUT this line. In a score: of the tempo the score would begin at, so `tempo 50%` is half speed whatever the sections write. At the start of a section: of the tempo in force before it (`tempo 80%`: a little slower from here). Before a name in a form: of the tempo that play would go at — the section's own, if it writes one. At the top of a file danced to a song: of the song's. |

A percentage is worked out where it stands, and what it comes to is a tempo like any
other from there on. At the top of a file without a song there is nothing for it to be
of (SH1038). Whatever is written must come to 10 to 400 beats a minute (SH1001).
The number may have a decimal point (`tempo 133.1`, `tempo 66.67%` for two thirds);
a fraction is not written.

Percentages in a form do not build up, since what a form writes is for one play:
`form { Drill tempo 110% Drill tempo 120% Drill Bow }` is 100, 110, 120 and Bow at 100
again, whether the 100 is Drill's own `tempo 100` or the tempo in force before it.
Each time 10% faster than the time before is written out: `tempo 110%`, `tempo 121%`, `tempo 133.1%`.
(A section that itself begins with `tempo 110%` does build up, every time it is played —
it changes the tempo in force — and what follows it stays that fast until a section writes a tempo.)
The other spellings stand in a form too: `form { Jig Slip tempo 4. = 112 Jig }`.

## Scores: what a file writes

**One `score` is one file written, and one movement from beginning to end.** It says who
dances (`dancer NAME`, at least one) and plays ONE form: the unnamed `form { … }`, or
the one it names with `form NAME`.

```
form { Verse Chorus Verse }                      // the unnamed form: what the unnamed score plays
form short { Chorus }                            // a named form, for a score that names it

score          { dancer alice  dancer bob }      // written to dance.glb
score practice { dancer alice  form short  tempo 80 }   // written to dance-practice.glb
```

- The unnamed score is written to the input's name (`dance.glb`), `score NAME` to
  `dance-NAME.glb`. Every command writes every score; `--score NAME` writes one.
  `shosa gltf FILE --combined` puts every score into ONE file, an animation each,
  named after its score: what a game engine wants of a character with several movements.
- Inside a score: `dancer NAME` (required, one for each dancer who appears), `form NAME`,
  `loop`, `inPlace`, `tempo N` (or `tempo N%`), `title "…"`, `music "…"`. A dancer the score does not
  list is not in it.
- **A score starts IN the first word of each row**: at beat 0 every part is where its
  row's first word says, and nothing is seen going there. A part no row writes is
  neutral: arms hanging, torso upright, head straight, feet side by side. Within the
  score, where a role is, which way it faces and how it is posed carry over from one
  section of the form to the next.
- **A game character's file has one form and one score per movement** — `Idle`, `Walk`,
  `Attack` …, as in the complete file above: `form Idle { Idle }` and
  `score Idle { dancer hero  form Idle  loop }`.
- `loop` in a score — the end joins the start. The score is solved as if it followed
  itself: the way to the first word of each row is made at the END of the loop, in its
  last word, so a looping walk's first step lands on beat 0 every time round, and
  `body { guard:8 }` in a loop simply stays in guard. The feet must end as
  they start (same stance, same facing; travelling is fine) or SH2005 says so.
  A bounce in that guard is one more row, `height { { low(8cm) low } q q q }`: the
  height is a row of its own and leaves the stance alone (see "Height"). (That row is
  8 beats, like the guard beside it: with bar lines, `{ low(8cm) low } q | % |`.)
- `inPlace` in a score — the sections are written the same (`leftFoot.fwd` still steps), but
  the even travel over the floor is taken out of the output and reported as a velocity,
  for an engine that moves the character itself. Usually with `loop`. Only the travel from
  start to end is taken out: a patrol that goes out and comes back ends where it began, its
  velocity is 0, and the walk out and back stays in the movement (write each way as a score of its own to hand both to the engine).
- A section lasts as long as its longest track. A part no track writes stays as it is.

## Tracks: one row per part of the body

A section holds a block for each role that does something in it, and the block holds
tracks: `section Main { solo { steps { … }  arms { … } } }`. The examples below show
the tracks alone; in a file each of them stands inside a role's block.

| Track | Meaning |
|---|---|
| `body` | The whole body. Takes the poses and moves you have defined, `then` and `skip`; a narrower track written beside it wins for its own part. |
| `steps` | The feet on the floor: steps, turns, jumps. Where the weight is. |
| `height` | How high the body stands on its feet: `low` (the knees bent), `low(30cm)`, `mid`, `high` (on the toes); and `sit(0.45m)`, on a seat that high. Kept until it is changed. |
| `arms` | Both arms at once, mirrored: `side` is away from the midline for each. |
| `leftArm` | The left arm: where the hand points from the shoulder, or a place on the body it goes to. |
| `rightArm` | The right arm. |
| `leftLeg` | The left leg when it carries no weight: where it points from the hip. |
| `rightLeg` | The right leg when it carries no weight. |
| `legs` | Both legs at once, mirrored, when neither carries the weight: in the air of a jump, and off the feet (`steps { hands }`, `{ seat }` …): `legs { side.up(30) }` is a straddle. |
| `turnout` | Both legs turned in their hip sockets: `out(80)` (degrees each), `in(20)`, `parallel`. The feet and the knees point that way; the hips do not turn. |
| `hips` | The hips turned over feet that stay where they stand: `left(30)` (degrees), `right(20)`, `center`. The shoulders and the head do not go round with them. |
| `torso` | The spine: `up` is upright, `up.fwd(10)` leans 10 degrees forward, `fwd` is folded level. |
| `shoulders` | The line of the shoulders turned from the feet, as seen from above: `left(30)` (degrees), `right(85)`, `center`. The chest turns on the hips to put it there; the feet stay. |
| `head` | Where the face looks: `fwd` is straight ahead. It nods with the chest's lean, but does not turn with the hips or the shoulders. |
| `hands` | Both hands at once, mirrored: the shape of the fingers (`fist`, `open`, `point`, `peace` …), and which way the fingers and the palm face (`open.fingers(up).palm(fwd)`). |
| `leftHand` | The left hand: the shape of its fingers, and which way they and the palm face. |
| `rightHand` | The right hand. |
| `face` | The face of a character: its look (`smile`, `sad`, `angry`, `relaxed`, `surprised`, `neutral`), and the eyes (`closed`, `winkLeft`, `winkRight`): `face { smile:4 | smile.winkRight:1 smile:3 | }`. It changes in a moment just before its beat. The eyes blink by themselves, written or not. |
| `quality` | How the parts go to their words from here on, for every word that does not say so itself: the names of the `@` marks, without the `@`. `quality { through }` passes every word without stopping, one flowing line (as `@through` on each); `quality { sharp }` hits every word at the last moment (as `@sharp` on each); `smooth` is the default again. Timed like any row (`quality { through:8 | sharp:4 smooth:4 | }`), and kept until the next word of the row. A word's own `@` wins. Not for `steps`. |
| `juggle` | What the hands throw (`juggles balls` on the dancer line), in siteswap as jugglers write it: `juggle { every 1/2  3:4 | 531531 3 3 | }`. A word with no length is its throws as written, one throwing beat each (`531` once through); with a length, the pattern over and over for that long (`3:4`); `every` says how many beats from one throw to the next. While a pattern is thrown, the arms are the juggle row's. |

Tracks may overlap: `body` covers everything, `arms` covers both arms. **Where two tracks
write the same part at the same time, the narrower one wins**; when the narrower one has
ended, or says `skip`, the part follows the wider one again (it is where the wider
track's word says at that moment, having gone there through the narrower track's last
word). So `body { guard:8 }` with
`head { fwd.left:2 … }` is a guard whose head looks around, and `arms { side:4 }` with
`leftArm { up:4 }` puts the right arm to the side and the left arm up.

Where the feet stand is the `steps` row; how high the body stands on them is a row of its
own, `height` (see "Height"). So beside a wider track a `height` row takes the height
and leaves the feet: `body { guard:8 }` with `height { { low(8cm) low } q q q }`
is a guard that bounces, `body { walk:4 }` with `height { low:4 }` the same walk
with bent knees. A step, a turn or a jump writes the feet, and **a step is taken or it
is not**: a wider track's step (a pose's stance included) is taken only if that track
has the feet at the beat the step is written at. One whose beat falls where a narrower
track has the feet is not taken at all — a `shift` has the feet too: `body { wide:8 }` with
`steps { shift.left:4 shift.right:4 }` never takes `wide`'s stance. Write the stance
as the first step of `steps` instead.

## Events: `target:length@annotation!mark`

A track is a row of events (words). Each is a target, then optionally `:` and a length,
`@` annotations and one `!` mark — glued together, no spaces, and **in that order**
(SH1040): `kick:2@mirror!hit`, never `kick@mirror:2`. The target says WHERE the part
arrives and in what shape; `@` says only HOW it gets there.

```
side.up              there for one beat (the default)
side.up:2            for two beats
side.up:1/2          for half a beat      (also :1.5, :3/4)
side.up:0.3s         for 0.3 seconds
side.up:12f          for 12 frames (at the file's fps)
side.up:2@sharp      gone to at the last moment: an accent
fwd@sharp!hit        … and the beat it is there is named `hit`
then:3               nothing new for three beats
skip:3               say nothing for three beats (a wider track may)
skip:3bar            say nothing for three whole bars (also then:3bar)
```

**A word is written AT the beat the part is THERE. Its length is how long it is that
part's word: until the beat of the next one.** It is like a note, which sounds from
where it is written: a step written on beat 1 LANDS on beat 1, a clap written at the
start of a bar is heard at the start of the bar. The position of a word is the sum of
the lengths before it in the row. Beats are counted from 0, here, on the sheet and in
`shosa check`: beat 2 is the moment the third beat of the bar begins, so a clap that a
dancer counts "on 3" is the word AT beat 2: `arms { side.down:2 clap … }`.

**The way to a word is made BEFORE its beat, in the time of the word before it in the row.**
How much of that time depends on the part:

| Part | When it sets out |
|---|---|
| The feet — in `steps`: a step, `stand` | A step straight after a step (or a `stand`, a `together`) WALKS: its foot leaves the floor soon after the other lands and swings through 80% of the word before, no more than 0.6 s, and the body goes on over the feet without stopping at each landing. In `leftFoot.fwd rightFoot.fwd leftFoot.fwd` at 120 the right foot is off the floor from beat 0.2 to beat 1. After anything else (a `then`, a shift, a turn, a jump, the start) a step's FOOT sets out in the second half of the word before, no more than 0.5 s before its beat: `leftFoot.side then stand` closes between beat 1.5 and beat 2; the body sets off as long again before the foot, so a walk starts without a jerk. A walk that ends (a `stand`, a `then`, the last step) slows over its last step and is at rest on the last beat. A step with `@sharp` waits, body and all, and goes in its last 0.15 s: the stamp of a hit. A foot always STANDS on its beat. |
| A turn in `steps` (`turn.right(90)`, …) | Through ALL of the word before, as a gesture: it is the body that turns over the foot. `together:2 turn.right(90):2` turns from beat 0 to beat 2. A quick pivot is a short word: `together:3/2 turn.right(90):1/2` turns in the half beat before beat 2. |
| A shift in `steps` (`shift.right`, …) | Through ALL of the word before, as a gesture: the weight is the body's, not a foot's. `leftFoot.side(0.5m):2 shift.right:2` stands wide on beat 0 and moves the weight over from beat 0 to beat 2. To move it later, stand first: `leftFoot.side(0.5m):1 then:1 shift.right:2` (`then`, below) moves it from beat 1 to beat 2. Unlike a gesture it does not slow at a word on its way: `shift.right:2 shift.center:2 shift.left:2` goes on through the middle at one speed, and slows only where it turns back (so `@through` is not needed on a shift, and is not taken). |
| A jump, a hop, a leap | It LANDS on its beat. The bend and the flight are in the word before it (in up to a second of it). |
| Arms, legs, torso, head, and the `height` | Through ALL of the word before. `arms { side:4 up:4 }` is at the side on beat 0 and travels the whole four beats to be up on beat 4. A gesture sets out for the next word as soon as it has arrived, unless told to stay (`then`, below); it eases in and out, so it slows at each word (to pass a word without stopping — a point on an arc — `@through` on that word: `down side:1@through up:1` goes from down over the side to up in one sweep). A back-and-forth (`fwd:1 back:1 fwd:1`, a breath, a sway) already moves as a sine wave does: it slows to nothing only at the ends, as a swing does. |
| Any word with `@sharp` | At the last moment: it waits, then goes in no more than 0.15 s before its beat. A punch, a clap, a pointing finger. |

- **To stay, and then go: `then`.** `then` is a word that brings nothing new on its
  beat. The part stays where it is through the word before the `then`, and goes to the
  NEXT word through the `then`: `arms { up:2 then:2 side }` is up on beat 0, still up
  on beat 2, and at the side on beat 4, moving only from 2 to 4. Read it as "up for
  two beats, then in two beats to the side": the length of the
  `then` is how long the way to the next word takes. A long rest and a quick move is
  `up:7 then:1 side`. Without the `then`, `up:8 side` drifts to the side for eight beats.
  Of several `then` in a row only the LAST is the way to the next word
  (`up:2 then:2 then:1 side` is still up on beat 4 and moves in the last beat), and a
  `then` with nothing after it simply stays. For a step and a `@sharp` word a
  `then` only waits: they still set out late in it (its second half; its last 0.15 s).
  A `shift` and a `turn` go through all of the `then`, as a gesture does.
  A slow way that crosses a bar line is ONE tied `then`: `shift.right:2 then:2~ | :2 shift.center:2`
  goes back to the middle over beats 2 to 6; `then:2 | then:2 shift.center:2` would go in
  the last two beats only (the first `then` stays).
- **The first word of a row is where the part IS when the score starts.** Nothing is
  seen going there: `body { guard jab }` starts in guard, and the jab lands on beat 1.
  To show the body going to its first target, write first where it comes from:
  `arms { down side:3 }` raises them through beat 0 to 1; `arms { then side:3 }` the same
  from wherever they are. There is no length 0 (SH1009): every word takes time —
  so a score cannot end on the instant its last word arrives. "Back in the stance at
  2 s" in a score of 2 s is the stance arriving earlier and standing to the end, and
  a role that comes back "on the next bar's 1" has to come back inside the last bar.
  The same for the feet: a step written on beat 0 of a score that does not loop is a
  foot already there. To SEE it land, give the score a lead-in (`steps { then:4 | leftFoot.fwd … }`).
  `shosa check` says it under the score (``solo starts with `leftFoot.fwd` already made``; a `leftFoot.place` or a `shift.left` too): count that
  step out of how far the dancer goes — "two steps forward, two back" from beat 0 ends one step behind.
  **A step already made is a way of standing, so the weight is on both feet**:
  `steps { rightFoot.fwd(0.08m).across(0.13m):4 }` stands in fifth on both legs, with no
  `shift.center`. To begin on the foot it stepped onto, shift to it next
  (`rightFoot.fwd shift.right:1/4 hop:3/4`; a `leap` straight after does not know its foot, SH2012, nor a `hop` unless a leg row throws the other leg out),
  and a `together` or `stand` straight after closes the left foot to the right (the weight goes over the right first; `shift.left` before it to close onto the left).
  `shosa check` also says where each dancer ends (``solo ends 0.5 m back of where it began``,
  ``solo ends where it began``): read it instead of adding the steps up.
  In a `loop` the first step is already made too: when the feet end standing so, it is not
  taken again at the end (no foot lifts), and the weight goes back between the feet over
  the last word: `steps { leftFoot.side(0.4m):2 shift.right:4 shift.left:2 }` looping is a
  stance 0.4 m wide whose weight drifts right, left and back to the middle, never still.
- **The last word of a row stays for its length**: the score ends with the body there.
  `arms { down side }` is two beats: down, then out to the side on beat 1 and still there when it ends.
- After a `skip` nothing had the part, so the next word sets out when the `skip`
  begins: `arms { skip:4 side }` rises through all four beats. To go late, end the
  silence with a `then`: `skip:3 then side` rises in the last beat only.
- In a later section, the first word of a row is on the section's first beat, and is
  gone to at the END of the section before: no earlier than the last word that
  dancer has there (in any row), and no earlier than its last bar. A dancer who
  stands through an intro stands, and moves to the verse's first words in the intro's last bar.
  A row may begin with `then` there too (`rightArm { then fwd@sharp … }` keeps the arm as it was one more beat).
- Seconds and frames are turned into beats by the tempo, so they can be mixed with beats.
  Use them where no beat matters (a hit reaction, a gesture).
- `|` is a bar line. It is optional, but when written **the bar it closes must have exactly
  the beats of the `time`** (error SH1002). Write them: they catch miscounted lengths, and
  they line the tracks up. In a row that has bar lines, the events after the last one must
  fill a bar too (warning SH1002): pad with `skip` or `then`. A movement that is not
  counted in bars (a game animation of 2.5 s, a reaction written in seconds) is
  written without bar lines.
- **A bar with nothing in it is a bar in which the track says nothing** (a `skip` of one
  bar, whatever the time): `arms { | | clap:1 then:3 | }` claps at the start of the third bar. So a
  line is never decoration: one at the very start of a row is an empty bar too.
  For a longer wait count the bars: `skip:10bar` says nothing
  for ten bars, `then:2bar` brings nothing new for two. A length in bars is for `skip`
  and `then` only, starts at the start of a bar, and the next `|` closes it (SH1033):
  `arms { skip:10bar | clap:1 then:3 | }`.
- **An event that runs across a bar line is tied over it**: `~`, the line, then the rest
  of its length alone. In 4/4, `body { jab:2 kick:2~ | :1 then:3 | }` is a kick of three
  beats written at beat 2 (the third beat of the bar). It is ONE event (annotations and the mark go on the
  first part, after the tie: `kick:2~@mirror!hit | :1`; the `~` is the length's own, straight after it — SH1040).
  Its length is the sum of the parts as written, for a defined move too:
  a 4-beat `kick` that starts on the last beat of a 3/4 bar is `kick:1~ | :3`
  (never `kick~ | :3`: the tie needs a length before it). One that crosses several lines is tied over each:
  a 12-beat `drill` is `drill:4~ | :4~ | :4 |`. Staying still needs no tie: `then:4 | then:4 |`.
- `{ … } & { … }` — rows that happen together, inside one track. All start at the same
  time; the whole is as long as the longest row. The braces are required on both sides.
  Where two rows write the same part, the LATER row wins:
  `body { { walk:4 } & { wave:4 } }` walks while one arm waves.
  A word in front of a group has its length written (`guard:1 { jab } & { stepIn }`):
  a bare word followed by `{` would read as the name of a track. **This is so before
  any group** — `{ … } q` and `{ … } & { … }` alike: `stand { leftFoot.fwd hop.fwd } q` is
  read as a track named `stand` (SH1004), `stand:1 { leftFoot.fwd hop.fwd } q` is right.
  (A word with a dot in it, `fwd.down.bent { … }`, cannot be a track's name and needs no length.)
- `!name` marks the beat its word is written at: the moment the hand is there, the
  foot lands, the jump comes down. (On a move it is the beat the move is written at:
  the beat of its first word.) It does nothing to the movement; it is written to the output for the engine.
  `shosa check` lists every mark with its beat and its second, and the sheet writes
  it in green over the first pose at or after it. Any event that moves something can
  carry one (a step, a turn, a hand going to a place), and a name may be used as often
  as you like: an engine gets one event for each. A bar done again by `%` gives its
  marks again, and so does a section the form plays again: each event says the
  section that wrote it and which play of it this is (`shosa check`: `!hit at beat 12 = 6 s   Attack, verse 2`),
  so an engine can tell them apart under one name. For a name of its own, write the
  event per play: `verse 1 { fwd@sharp!first } verse 2 { fwd@sharp!second }`.

## What a section begins with

Three lines can stand at the start of a section, before its first block, each on a line
of its own. (In a file danced to a song only `upbeat` can: the tempo and the time are the song's, SH1035.)

```
section Waltz {
  tempo 90
  time 3/4
  upbeat 1
  solo {
    arms  { up:1 | side:3 | down:3 | }                     // begins one beat BEFORE the section: up on that beat, at the side on the section's first
    steps { leftFoot.side rightFoot.side stand | % | }          // begins at the section's first bar line
  }
}
```

- **The `time` is the section's own. A section that writes none is in the `time` at
  the top of the file, WHATEVER IS PLAYED BEFORE IT.** A section's bars are counted
  in one time, so they hold the same beats wherever the form plays it: after
  `form { March Waltz Coda }`, a Coda that is in three writes `time 3/4` itself, as
  Waltz does (without it its bars are the file's 4/4, and SH1002 says so).
- **The `tempo` goes on. `tempo N` is in force from where the section is played UNTIL
  A SECTION THAT WRITES ANOTHER IS PLAYED**, and does not go back to the top of the
  file at the next section: after Waltz's `tempo 90`, a Coda that writes no tempo is
  at 90 too. So how fast a section goes depends on what the form plays before it; a
  section that must go at one tempo wherever it is played writes it.
  The `tempo` at the top of the file is what the score begins at.
- **One play of a section in another tempo or time: write it in the `form`, before the
  section's name.** `form { March Waltz Coda tempo 132 March }` plays the last March at
  132 and leaves the section as it is; `form { tempo 100 Drill tempo 120 Drill tempo 140 Drill }`
  plays the same bars at three tempos. That play goes by what the form writes, whatever
  the section writes itself (a `tempo` alone leaves its time alone, and the other way
  round). **It is for THAT PLAY ONLY: the next name is played as if the form had written
  nothing.** In `form { tempo 144 March March Coda }` the second March and the Coda are
  at what they would be without the `tempo 144` — March's own `tempo 120`, or the tempo
  in force before the form's line if March writes none. (What a section writes itself
  still counts for what follows it: after `tempo 144 March`, a section that writes no
  tempo goes on at March's own 120, not at 144.) So one play is changed in one
  place, and "back to the usual tempo" needs no number. What is to last is written by
  a section (the rule above); a name with nothing before it that follows no section's
  tempo is at the `tempo` at the top of the file.
  In a repeat it belongs to the name it stands before, on every pass. It
  must have a name after it (SH1037). Not in a section: `verse 2 { tempo 132 }` is an error.
- A bar has the beats of the section's time (bar lines, `%`, `skip:2bar` all follow it),
  and a beat is the note of its lower number. Where that note changes from one section
  to the next (4/4 to 6/8) and
  no `tempo` is written with it, the music keeps its speed: 120 becomes 240, since the
  beat is now a quaver (a crotchet lasts as long as before, so six counts of 6/8 take
  half the seconds of six counts of 6/4); the other way, 6/8 at 288 into 3/4 is 144, the
  quavers running on as they were — also into a section that writes no time in a file
  whose own is 3/4. Where the note stays the same (6/8 to 9/8) nothing
  changes but the length of the bar: a section with `time 9/8` alone after a jig at `4. = 100` is a
  slip jig at `4. = 100`. A section that writes both may write them in either
  order: a plain `tempo 100` counts the beats of the section's own `time`. In 6/8 the tempo is easier said by the note
  that is felt: `tempo 4. = 60` with `time 6/8` (see "A tempo: three spellings").
  `:0.3s` and `:12f` are counted at the section's tempo.
- A score's own `tempo N` is the tempo the score BEGINS at — its first section really
  goes at N, also when that section writes a tempo of its own or the form writes one before it — and the tempos after
  it keep their proportion (`tempo 60` on a score that would begin at 120 halves every
  one of them; a practice score of a section that writes `tempo 160` is at half speed with `tempo 80`).
  **Half speed without looking the number up is `tempo 50%`**: a score's percentage is of
  the tempo it would begin at, and every tempo after it keeps its proportion too.
  A score at another speed is the same beats in other seconds, and the body answers to
  seconds: a jump has the seconds of the word before it to fly in (up to one), so at
  half speed it is a longer, higher flight, and a
  movement that was just possible can be too fast (SH2003) in the faster score only (a `@sharp` word takes its 0.15 s at any tempo: if it is too fast, it is too far). The
  warning then names the score it is in.
- `shosa check` says, for every score, how long it is in beats and in seconds, and for each
  section it plays: its beats, its bars, its seconds, the tempo and the time it is played in.
  ```
  score: 14 beats, 8 s, 1 dancer
    March   0-8    2 bars   4 s   at 120   4/4
    Waltz   8-14   2 bars   4 s   at 90    3/4
  ```
  In 6/8, 9/8 and 12/8 the tempo is said by the beat and by the dotted crotchet that
  is felt: `at 288 (4. = 96)   6/8`.
  Read it for each score: a score that plays a section alone does not hear what the
  whole piece plays before it, so a Coda that writes no tempo is at the file's own
  there (its time is its own, as everywhere). Its form says it: `form coda { tempo 90 Coda }`.
- **`upbeat N`: the section's rows may begin N beats before its first bar line** (`upbeat 1`,
  `upbeat 1/2`; shorter than a bar, SH1036). It is for a word that is THERE before the
  section's first beat — the hands apart on the beat before the clap, a step that
  lands on the last beat of what comes before — without writing it at the end of
  whatever section that is. (To be somewhere ON the first beat needs no upbeat: the
  first word of a row is on the section's first beat, and is gone to in the section
  before.) A row uses the upbeat by beginning with a
  bar of exactly N beats (`arms { up:1 | side:3 | … }`); any other row begins at the
  first bar line as usual. The section is as long as it is from that bar line: the
  upbeat adds nothing to it.
- The upbeat is done over the last N beats of whatever is played before the section,
  and takes from it only the parts it writes (a `skip` in the upbeat takes nothing):
  what the earlier section writes for those parts on those beats is not done.
  Those beats are the earlier section's: they go at ITS tempo, and the section's own
  `tempo` and `time` begin at its first bar line. The upbeat bar can hold anything a
  bar can (a `verse 2 { … }`, a mark); a `%` after it does the bars after it again, never the upbeat.
- Before the first section of a score there is nothing: what an upbeat writes before
  beat 0 is not seen, and the score starts in the words at its first bar line. In a
  `loop` what is before the start is the end, so the upbeat is done over the last N
  beats of the loop (a mark in an `upbeat 1` is at beat 7 of an 8-beat loop).

## Doing it again

Three signs, by how much is done again. (There is no `repeat`.)

- **A few events: `{ … }` and `q`.** Put the unit in braces; each `q` after it does it
  again: `rightArm { { up.bent:1/2 side.up.bent:1/2 } q q }` waves three times.
  `q` is the nearest group in braces written before it in the same track (plain events
  in between do not matter: `{ a b } then:1 q`). Without a length it lasts as long as
  the group; `q:2` stretches or squeezes the group into 2 beats; `q@mirror` swaps left
  and right (`steps { { leftFoot.side stand } q@mirror }` goes left, then right). A `q`
  with no group before it is an error (SH1026): one event is simply written twice.
- **A bar: `%`.** Alone between bar lines, `%` is the bar before it again; `%2` is the
  two bars before it (any number: `%4`, `%8`; one bar sixteen times is `a | % | %2 | %4 | %8 |`): `steps { leftFoot.side stand rightFoot.side stand | % | % | % | }`,
  `steps { vine:4 | vine:4@mirror | %2 | }`. The bars must be whole (no event tied into
  them from before, SH1027; ties between the bars of a `%4` are fine) and in the same
  row of the same section. **To change one bar of a run, write that bar out instead of its `%`**:
  `a b c clap | % | % | a b c jump |`.
- **Sections: repeat signs in the `form`.** `form { |: A :| B }` plays A A B;
  `|: A :|*4` four times; numbered endings `form { |: A [1. B] :| [2. C] }` play
  A B A C (`[1-2. B]` is the ending of passes 1 and 2; every pass needs exactly one).
- **A section that is played more than once, and differs one of the times: `verse N { … }`**
  in the row, where the difference is. `verse 2 { … }` is done only the second time the
  score plays that section; `verse 1,3 { … }` the first and third, `verse 1-2 { … }` the
  first two. What is written outside a verse is done every time:
  `arms { clap:1 side:3 | % | % | verse 1 { % | } verse 2 { up:4 | } }` — three bars the
  same, the fourth different the second time. Verses written next to each other share
  ONE place in the row (as long as the longest of them), so what follows starts on the
  same beat every time; on a play that no verse names, the row says nothing there.
  A row only some plays have is all verse: `head { verse 2 { fwd.left:4 | fwd:4 | } }`.
  "The second time" counts the plays in the score being written (with a song: the
  times the song plays the section); going round a `loop` is not another play.
  Not in a `move` (SH1034).
  A verse may be whole bars, with their lines inside its braces as above, or a part
  of a bar, with the line after it: `steps { leftFoot.side stand rightFoot.side verse 1-2 { stand } verse 3 { jump.stand } | }`
  changes the last beat only. A `%` in a verse is the bar written before it — in
  the same verse, or before the verses (never a bar of the verse beside it).
- A change INSIDE a longer run is written over it, in a narrower track that says `skip`
  up to that moment: `body { vine:4 | rock:4 | %2 | }` with `arms { skip:3bar | skip:2 then side | }`
  has the arms out on the last beat only, raised through the `then` (see "Tracks": the narrower wins).

## Poses and moves

```
pose guard { steps { leftFoot.fwd(0.3m) }  height { low(8cm) }  arms { fwd.up(25).bent } }
move kick  { rightLeg { fwd.down.bent fwd@sharp!hit fwd.down.bent down } }   // knee up, kick, knee, down
```

- **A move is written at the beat of its FIRST word**, and that word is gone to through
  the word before the move, like any other. `kick` at beat 4 has the knee up on beat 4
  and kicks on beat 5 (its `!hit`). The words inside keep to the rule too: each is
  there on its own beat, counted from the beat the move is written at.

- A `pose` holds, per track, ONE target and no length. A `move` holds rows of events.
  Either may hold any track (`leftArm`, `head`, `torso` …), and a move's rows are written
  like a section's: bar lines, `%`, `{ } q`, marks.
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
  `guard` again moves nothing. (A step written directly in `steps` always steps.) Stepping
  into it, the body stands on both feet, the weight between them, as with a first step
  already made: the same guard stands the same in a loop and out of one. A `hop` straight
  after it needs a `shift` to the foot it hops on, or the other leg thrown out in its row (SH2012).
- **A shape held by several rows at once is a pose, so that its length is in one place.**
  An attitude held for three bars, written row by row, is three lengths to change when
  it should be held for two:
  ```
  rightLeg { back.bent:4 then:12 }
  arms     { up.bent(30):4 then:12 }
  head     { fwd.left(20):4 then:12 }
  ```
  As a pose it is one: `pose attitude { rightLeg { back.bent } arms { up.bent(30) } head { fwd.left(20) } }`
  and `body { attitude:4 then:12 }`. A change of the hold is one number.
  One part of the pose a little behind the rest: a row for that part beside the `body` row,
  holding it where it was and then waiting, gives it back to the pose late. With
  `body { closed:1 attitude:3 }`, `arms { closed:1/2 then:1 }` keeps the arms where they are
  to beat 1/2 and has them arrive in the attitude's arms at 1 1/2, half a beat after the leg
  (`arms { closed:3/2 }` would have them arrive at 1 1/2 too, but setting out at once).
- A pose takes `@mirror`, `@free`, `@smooth`, `@sharp`, `@linear`, `@through`; a move takes `@mirror`
  and `@free`. Definitions take no arguments: a variation is the same word with the
  change written over it (a narrower track, or a later `&` row). A move may hold a
  `body` row of other moves, so a whole pass can be one word:
  `move pass { body { vine:4 vine:4@mirror } }`.

## Files of parts: `using`

Poses and moves used by several files — a character's guard and punches, used by its
`Idle`, `Combo` and `Hit` files — are written once, in a file of their own, and brought
in with `using`, as in Lily#:

```shosa
// hero.parts.shosa: poses and moves only. It needs no role, section, form or score.
pose guard { steps { leftFoot.fwd(0.3m) }  height { low(8cm) }  arms { fwd.up(25).bent } }
move jab   { leftArm { fwd.up(25)@sharp!hit guard } }
```

```shosa
// combo.shosa
using "hero.parts.shosa"

role hero
dancer hero humanoid on hero
section Combo { hero { body { guard jab:1 jab:2 | } } }
form { Combo }
score { dancer hero }
```

- `using "FILE"` is a top-level line: a path from this file's folder (`"parts/hero.shosa"`,
  `"../moves.shosa"`). **The file joins this one**: everything it writes is as if written
  here, after this file's own lines — its poses and moves, and anything else it has. A file
  brought in may itself have `using` lines (a path from ITS folder). Each file comes in once,
  however many lines name it, and a file that names one that names it back is no error.
- **A name is defined once in all of them** (SH1005): a pose in the file of parts cannot be
  defined again here. Write a variation under a name of its own.
- **A file of parts** — poses, moves and `using` lines, with no role, dancer, section,
  form or score — is not an error on its own (it has no SH1030), and writes nothing out:
  it is written out by the files that bring it in. `shosa check` on it lists what it offers
  (`a file of parts, for using: pose guard, move jab (2 beats)`); on a file that brings
  files in, it lists them (`using hero.parts.shosa`).
- What is wrong in a file brought in is said on the `using` line that brought it in, with
  the file and the line: `In hero.parts.shosa, line 3: …`. A file that is not there is SH1039.
- A move counts its bars in the time of the file that uses it, and its seconds in that
  file's tempo, wherever it is written.
- **Shosa has files of parts of its own**, a style's poses and moves each: `using "shosa:basics"`,
  `"shosa:idol"`, `"shosa:pop"`, `"shosa:jazz"` (and, experimental, `"shosa:ballet"` and `"shosa:break"`) bring them in as any file of parts, from any
  folder. What each holds, with a line on every pose and move, is in the choreography guide
  (`shosa guide`; in the editor, the tool `shosa_guide`).

## Gesture targets (arms, legs, torso, head)

(The `shoulders` and `hips` tracks have words of their own: see "The shoulders and the hips" below.)

A target is a direction, written as words joined by dots, and then, for a part that takes
one, the shape it arrives in (see "Shapes" below).

| Direction | Meaning |
|---|---|
| `fwd` | Forward: the way the dancer faces. |
| `back` | Backward. |
| `side` | Away from the body's midline: left for a left limb, right for a right limb. |
| `across` | Toward and past the midline: the opposite of `side`. |
| `left` | To the dancer's left. For the torso and the head; a limb says `side` or `across`. |
| `right` | To the dancer's right. For the torso and the head. |

| Up and down | Meaning |
|---|---|
| `up` | Straight up. After a direction, toward up: `fwd.up(30)` is 30 degrees above `fwd` (`fwd.up` 45). Before one, from straight up: `up.fwd(10)` is 10 degrees forward of upright (a torso's slight lean). |
| `down` | Straight down; for an arm, hanging as an arm nobody wrote does (the hand by the thigh, a little forward); for a free leg, back on the floor. After a direction, toward down: `side.down(20)`; `fwd.down` 45. |
| `floor` | For an arm, after its direction: the hand on the floor that way, as far out as the straight arm reaches from the shoulder (`side.floor`, `fwd.floor`; `down.floor` under the shoulder; `back.floor.bent(60)` nearer). For a leg, after its direction instead of up or down: the leg straight that way, the foot pointed and its toe on the floor (a tendu): `rightLeg { side.floor }`, `fwd.floor`, `back.floor`. How far down follows from how high the hip is (deeper in a plié). Out from the floor and back to it, the toe slides along the floor; a brush up (a dégagé) passes through it: `side.floor:1/2 side.down(45):1/2`. |

- One word is one of the four ways, level: `fwd`, `side`, `back`.
- **A number in brackets belongs to the word before it: how far toward that word.** Two
  directions make a diagonal, 45° between them (`fwd.side`); with degrees on the second,
  that far from the first toward it, 0 to 90: `head { fwd.left(30) }` looks 30° to the left
  of straight ahead, `rightArm { fwd.across(18) }` reaches a little across
  (with square shoulders the fist ends in front of the middle of the body: on the face of someone who stands
  straight ahead. A shoulder that has come round (`shoulders { left(45) }`) is already nearer the middle, so
  the number gets smaller — still about 18 with the shoulders 45° round, about 5 with them side-on, 90°;
  read the fist in `shosa clip`, and again after changing the turn), `fwd.side(70)` is
  nearly out to the side. `fwd.left(0)` is `fwd`, `fwd.left(90)` is `left`.
- **Up or down comes last, with the degrees above or below level**: `fwd.up(30)`,
  `side.down(20)`, `fwd.left(30).up(10)` (30° left, 10° up). Without brackets 45:
  `fwd.up` is half-way between ahead and straight up. An arm swinging while walking is
  `fwd.down(70)` / `back.down(70)`: 20° from hanging. Level is the direction alone.
- `up` or `down` alone is straight up or down. **Written first, they are measured from
  upright** (or from straight down): `up.fwd(10)` is 10° forward of upright. That is how a
  torso's lean is said, from upright as a coach says it: `torso { up.fwd(30) }` is a bow of
  30°, `torso { up.back(20) }` leans back 20°, `torso { fwd }` is folded level.
  (`up.fwd(10)` and `fwd.up(80)` are the same direction.) After `up.` or `down.` comes one way only.
- With `@mirror` the sides swap as they do without brackets. The brackets need two
  directions square to each other, and stand on the word that is turned toward: `left(30)`
  (from where?), `fwd.back(30)` and `fwd(30).left` are errors (SH1013), and so is a
  number after a dot (`fwd.30`): degrees are always in brackets.
- `low`, `mid` and `high` are heights, not directions (see "Height").
- **Arms and legs say `side` / `across`, never `left` / `right`** (error SH1011). That is
  what lets `arms { side }` be right for both arms at once.
- The torso and the head say `left` / `right`, never `side` / `across`.
- For an arm the direction is where the HAND is from the shoulder; for a leg, where the
  foot is from the hip; for the torso, where the spine points (`up` is upright,
  `up.fwd(45)` leans 45° forward, `fwd` is level); for the head, where the face looks
  (`fwd` straight ahead, `fwd.left` turned, `fwd.down(25)` looking down).
- **Every written direction is measured the same way: from the way the feet face, level
  with the floor** — for the arms, the legs, the torso and the head alike. `down` is toward
  the floor even when the torso leans, and `head { fwd }` looks straight ahead over a bow
  (to look along the bowed chest, write the degrees — over `torso { up.fwd(30) }` the face in
  line with the chest is `fwd.down(30)` — or measure from the chest: `head { fwd.body }`).
  The head's left and right is from where the feet point, so it does not follow the
  shoulders or the hips (see `shoulders` below).
  **What nobody writes is not measured**: unwritten arms hang (in a bow, in front of the
  thighs), and an unwritten head looks along the chest, so it bows with a bow.
  **A body is never quite still**: in the rows a dancer writes nothing in (`torso`, `head`,
  `hips`, and `steps` while both feet stand), it breathes, its weight drifts a few centimetres
  from foot to foot, and the chest and the head go a little with the arms (back and up as they
  rise). Unwritten arms swing while the feet walk, each against the other foot (forward as
  it is ahead), and hang again when the feet stand; unwritten hips go a few degrees round
  with the leg in front and unwritten shoulders the other way. Do not write the swing of a plain walk.
  Always the same for the same text, and a loop still joins. A row you write is exactly
  as written: to keep the torso straight up, write `torso { up }`; to walk with the arms
  still at the sides, write `arms { down }`.
- **An arm at `down` hangs as an arm nobody wrote does**: the elbow a little bent, the
  hand by the thigh and a little forward — not a straight line under the shoulder
  joint, which on a character is inside the hip. So `down` is how an arm is put back,
  and how a row says "hanging" in its first word (`arms { down:3 then side }`).
- **A part follows the torso only when its target says `.body`** (an arm, a leg, the head).
  Arms left at `up` stay vertical while the torso leans to the side. For a side bend with
  the arms overhead, write the lean and, over the same beats, the arms again with `.body`:
  `torso { left.up:2 up:2 }` with `arms { up.body:2 up:2 }`.
- Bending: `bent` brings the hand or foot closer along the same direction (a right angle
  at the elbow or the knee; `bent(40)` for 40°, see "Shapes" below). A raised knee is
  `fwd.bent` (thigh about horizontal) or `fwd.down.bent` (lower); a guard with the fists
  up in front of the face is `fwd.up(25).bent`.
  **A hand travels from where it is to the target, so a straight punch is the guard's own
  direction without `bent`**: from `fwd.up(25).bent`, `fwd.up(25)@sharp` goes out along one line
  (to `fwd` it would come down like a chop). Both hands at a place near the middle of the
  body (`arms { chin }`, `arms { chest }`) bring the elbows across each other: for two arms
  side by side use a direction.
- **Never send a part to the exact opposite of where it is** (`down` straight to `up`,
  `fwd` straight to `back`): the way round is then unwritten (warning SH2001). Go through
  a target in between — `arms { fwd up }` raises them through the front, `arms { side up }`
  through the side.
- **A leg swings about the hip**: lowered from `fwd` to `down` it stays straight all the way.
  From behind to the front (or the front to behind) it passes under the hip, through `down`
  (a leg brushing through first position), never over the top. An arm goes the shortest way
  round: from `back.up(20)` to `fwd` it goes over the top of the shoulder.


An arm can also go to a place on the own body instead of a direction:

| Arm target | Meaning |
|---|---|
| `hip` | The hand on the hip of its own side. |
| `chest` | The hand in front of the chest. |
| `shoulder` | The hand on its own shoulder. |
| `head` | The hand beside the head. |
| `chin` | The hand in front of the chin: one hand in thought, one fist held close. (Both arms there cross at the elbows: a guard is `fwd.up(25).bent`.) |
| `forehead` | The hand at the forehead, the elbow out to the side: a salute, a hand shading the eyes, wiping the brow. It goes with the head (a head turned, tilted or looking up takes it along). |
| `belly` | The hand on the belly. |
| `shoulder.across` | The hand on the opposite shoulder. With `arms`: arms folded. |
| `clap` | The hand at the midline in front of the chest. With `arms`: the two hands meet. |
| `thigh` | The hand on the front of the thigh of its own side, half-way down it, wherever the leg is (standing, lifted, sitting). |
| `knee` | The hand on the knee of its own side, wherever the leg is: `arms { knee }` with a bow and bent knees is hands on the knees. |

`arms { clap!clap }` is a clap on its beat (add `@sharp` for a crisp one); `arms { shoulder.across }` folds the arms.
`thigh` and `knee` are on the leg of the hand's own side, wherever that leg is: `arms { knee }` with
`torso { up.fwd(40) }` and `height { low(20cm) }` is hands on the knees (an arm reaches 0.6 m: standing upright
the knee is out of reach, SH2006 does not say so — read the hand in `shosa clip`).

**A hand that stays where it is in the room: `stay`.** Every target above is measured from the
shoulder, so a hand goes with the body when it leans, bows or sinks. `stay` keeps the hand where its
word before put it, in the room: the elbow bends and unbends to follow it (a hand on a counter, on a
rail, on a wall). **It is timed exactly as `then`: it holds through the time of the word BEFORE it,
and its own length is the way to the word after it.**
`rightArm { fwd.down(40).bent(30):4 stay:1 down }` with `torso { up:2 up.fwd(30):2 up }`: the hand is
put on the counter at beat 0, stays there in the room through beats 0–4 (the 4 of the word before
`stay`, while the torso bows and comes up), and goes down in beat 4–5 (the 1 of `stay`).
So in a row that loops, a last `stay` is the way back to the first word — its length is NOT held:
to keep the hand still all the way round, give the length to the word before and keep the `stay`
short (`arms { counter:15/4 stay:1/4 }` in a loop of 4 beats; the body is back where it began, so
the way back goes nowhere). A body that goes farther from the hand than the arm is long is a
warning (SH2015). `stay` is for an arm only.

An arm or the head can take a place on ANOTHER dancer, by its role:
`ROLE.PLACE`: `rightArm { b.hand:2 }` reaches for b's right hand, `head { b.head }`
looks at b.

| Place | Meaning |
|---|---|
| `hand` | The other dancer's hand of the same side. If it reaches back, the two hands meet half-way, at the height of a handshake. |
| `leftHand` | The other dancer's left hand, whichever arm reaches: `leftArm { b.rightHand.low }` beside b is hand in hand. If that hand reaches back for this one, the two meet half-way. |
| `rightHand` | The other dancer's right hand. |
| `shoulder` | The other dancer's shoulder of the same side. |
| `head` | The other dancer's head. |
| `chest` | The other dancer's chest. |

The role named must be danced by exactly one dancer (SH1021). The arm only reaches as
far as it is long (about 0.6 m from the shoulder): bring the two bodies within reach with
their `at` (where the dancer stands: the middle of the feet) and their steps. A hand
that does not get there is a warning that says how far short it is (SH2006).
Two facing dancers whose hands meet in front of them stand about a metre apart; for a
meeting above the heads or beside the hips (`.high`, `.low`) the arms slant, so about 0.8 m.
Hands of the same side (both left hands) are diagonal to each other between two who face,
which costs a little more: about 0.7 m for those `.low`. Two who touch low and also
bend their knees deep stand 0.8 m apart and lean in a little (`torso { up.fwd(20) }`):
at 0.7 m the bent knees meet.
Two bodies are in the same place (SH2007) when their trunks come within about 0.2 m of
each other: nearer than that in the picture is only one standing behind the other.
So are two whose legs go through each other, of the legs they stand on (two who face
at 0.7 m and both bend their knees deep); a leg in the air is not looked at, since a kick is meant to land.

Two hands that reach for each other can meet at another height, when BOTH say the same
level: `a { rightArm { b.hand.high } }` with `b { rightArm { a.hand.high } }` is a high
five above the heads; `.mid` meets at shoulder height, `.low` beside the hips.

**Hand in hand.** `hand` is the hand of the SAME side (a handshake: right to right). Two who
stand side by side hold their INNER hands, a left in a right: name the hand, `leftHand` or
`rightHand`, whichever arm reaches. `a { leftArm { b.rightHand.low:2 then:6 } }` with
`b { rightArm { a.leftHand.low:2 then:6 } }`: the hands meet beside the hips on beat 2 and stay
joined through the `then` — while the two walk, sway or bow, the hands go with them (a hand that
holds another's follows it at every moment, until the row's next word takes the arm away).
Two who face take both hands straight across the same way: `leftArm { b.rightHand }` and
`rightArm { b.leftHand }` for each. Under `@mirror` and for a mirrored dancer the hand named is
the other one, as the arm is.

**`neighbor`: whoever stands next to the dancer.** In an arm's row, `neighbor` in place of a role is
the dancer nearest on THAT ARM'S side when the word arrives, of any role (within 2.5 m, and more
beside than before or behind). So one role danced by a whole line can say it:
`line { arms { neighbor.hand:2 then:2 neighbor.hand.high:2 then:2 down:2 } }` — everybody takes the
hands of both neighbours beside the hips, the line lifts them above the heads, and lets go.

| Place | Meaning |
|---|---|
| `hand` | The neighbour's hand on this side (the inner hands of two side by side). If it reaches back (`neighbor.hand` too, or this dancer's hand by its role), the two meet half-way, beside the hips; `.mid` at the shoulders, `.high` above the heads. |
| `shoulder` | The neighbour's shoulder on this side: a hand on it. |

- Unwritten, the neighbours' hands meet beside the hips (`.low`); `.mid` and `.high` as above.
  The hand meets a neighbour's that reaches back by `neighbor.hand` too, or by this dancer's role
  and hand (`a.leftHand`): either way of naming it is the same hand.
- **With nobody on that side (the two ends of a line) there is no hand to take**: the arm hangs as an
  arm nobody wrote (and swings in a walk); with `.high` it goes up too, open and out, as the ends of a line lifting its hands do. Nothing is said. One arm only
  (`leftArm { neighbor.hand }`) when only that side is meant.
- Who the neighbour is is found once, when the word arrives, and kept while it holds: walk to
  the formation first (`go.line`), then take hands.
- Hand in hand, two stand 0.6 to 1.1 m apart (the middles of their feet; at 1.1 m the arms are
  straight); farther is SH2006.
- **A hand in another's turns to it by itself, palm to palm**: lifted hands (`.mid`, `.high`, a
  high five) face the other dancer; hands held beside the hips stay as the arm has them, the
  palms to the thighs. No `hands` row is needed for it, and a row that says the palm
  (`palm(fwd)`) wins.
- A character's own arms are brought together in the preview (a child beside a grown-up still
  holds hands); the fingers are as the `hands` row says, or relaxed.

### Legs

`leftLeg` / `rightLeg` are for a leg that carries NO weight. Make the gesture, then put
the leg back with `down`.

- From standing on both feet nothing else is needed: the weight goes over the other
  foot by itself as the leg lifts, and comes back as it is put down. `steps` can be
  left out, or `then`.
- After a step the weight is on the foot that stepped, so the OTHER leg is free
  (after `leftFoot.fwd`, the right leg). After a `hop` or a `leap` the foot that did not
  land is already off the floor, held low behind: a leg row can shape it (`rightLeg { back.up(10) }`
  is a landing in arabesque), and the leg's `down` or the next word of `steps` that moves it
  puts it on the floor (a leg's row wins over `steps`: while the row holds it up, a step does not, SH2013).
- While either foot is in the middle of a step, or the other leg is also off the
  floor, a leg gesture is an error (SH2004) — except in the air of a jump, a hop or a
  leap, where both legs may take a shape (a split leap: `leftLeg { fwd }` with `rightLeg { back }`). The leg lifts through the word BEFORE its
  gesture, so keep the steps out of that word: give the leg a `then` of the beat it
  may lift in (`rightLeg { skip:3 then fwd.bent down }` lifts in beat 3 only).
- A leg put `down` lands where it stood before the gesture. A foot held in the air after a
  `hop` or a `leap` is put on the floor by `down` beside the foot it stands on, at standing
  width; the weight stays on the standing foot.
- **`skip` in a leg's row lets the leg go back to `steps`** over the time of the `skip`, not
  in the word before it (`rightLeg { side:4 skip:4 }` holds the leg out for four beats, still,
  and lets it go in the next four): its
  foot goes to where the steps have it, and a step of that foot in that time carries it there
  straight from the air. So a foot closes from the ankle into fifth without passing out to the
  side (where `down` would put it): `leftLeg { otherAnkle.back:4 skip:1 }` with
  `steps { … leftFoot.fwd(0.08m).across(0.13m) }` landing as the `skip` ends. A `skip` that goes
  on past the landing of a step or a jump is all let go when the foot lands. With no step, the
  foot goes back to where it stood before the leg lifted (or, after a hop, stays held in the
  air until a step or a `down` puts it on the floor). To keep a leg as it is, write `then`.
  A foot along the floor (a tendu) is the leg's direction closed by `floor` instead of up or down:
  `rightLeg { side.floor }` (also `fwd.floor`, `back.floor`, `fwd.side(30).floor`): the leg straight,
  the foot pointed, the toe on the floor, as far out as the leg reaches from the hip (farther in a
  plié). Out from the floor and back the toe slides; a dégagé brushes through it, `side.floor:1/2
  side.down(70):1/2 side.floor:1/2 down:1/2`, and a rond de jambe is `fwd.floor side.floor back.floor down`.
  **With `bent` the knee bends and the toe still rests on the floor**, nearer the body, the heel up:
  `rightLeg { back.floor.bent(40) }` is the foot resting on its toe behind (a dancer waiting, a "B-plus"),
  `fwd.floor.bent(30)` a toe tapped in front (`knee(…)` says where the knee points). The floor is the place: a
  knee bent further than still reaches the floor from where the hip is bends as far as does.
  Seen from above, a foot going between the floor and a shape by the standing foot goes round
  the standing ankle the shorter way, no nearer to it than its two ends: from the back of the
  ankle to fifth in front it passes beside the standing foot (about 12 cm out), not through it.

A leg in the air can also send its toe to a place on the other leg:

| Leg target | Meaning |
|---|---|
| `otherKnee` | The toe at the front of the other leg's knee: a retiré (passé). The knee points where the foot does (`turnout`), or as `knee(...)` says: `otherKnee.knee(fwd)`. |
| `otherKnee.back` | The toe at the back of the other leg's knee. |
| `otherAnkle` | The toe at the front of the other leg's ankle: a coupé (sur le cou-de-pied). |
| `otherAnkle.back` | The toe at the back of the other leg's ankle. |
| `otherShin` | The toe at the front of the other leg's shin, half-way between the ankle and the knee (a low retiré). |
| `otherShin.back` | The toe at the back of the other leg's calf. |
| `otherThigh` | The toe at the front of the other leg's thigh, half-way up it (a high retiré; the foot of a tree pose). |
| `otherThigh.back` | The toe at the back of the other leg's thigh. |

`rightLeg { otherKnee:2 down }` is a retiré (passé) held for two beats; `rightLeg { otherKnee fwd.up(15):2 down }`
a développé that goes up through it and unfolds to the front. How far the knee bends follows
from the place (so no `bent`); it points where the foot does — forward with the legs
parallel, to the side turned out (`turnout`) — or as `knee(...)` says: `otherKnee.knee(fwd)`.
The place is found on the other leg wherever it is: in a plié, turned out, or in the air.
The foot is pointed on along the shin, its toe at the place. `otherShin` and `otherThigh` are half-way
along the shin and the thigh (their fronts; `.back` their backs): a retiré at half height, and one above the knee.

**The foot of a leg in the air: `pointed`, `flexed`.** A lifted foot lies as it stood, level, unless its
word says: `rightLeg { fwd.down(45).pointed }` stretches it on along the shin (a dégagé, a battement, the back
foot of an arabesque: `back.up(10).pointed`), `fwd.flexed` pulls the toes up toward the shin, the heel
leading (a heel dig, a flexed kick). They are shape words (after `bent` and `knee(…)`), and go evenly from
word to word. A toe sent to the floor or to the other leg is pointed already, and takes neither; in the air
of a jump every foot points by itself.

### Shapes: `bent`, `elbow`, `knee`, `pointed`, `flexed`, `round`, `tilt`, `body`

After the direction a target may say the shape the part arrives in, with words of their
own joined on by dots, **each once and in this order** (SH1012 otherwise):

| Shape | Meaning |
|---|---|
| `bent` | For an arm or a leg: bend the elbow or the knee, the hand or foot staying in its direction and coming closer. `bent` is a right angle; `bent(40)` 40 degrees (0 straight, up to 150): `fwd.up(25).bent`. |
| `elbow` | For an arm: which way the elbow points, as an arm's direction: `fwd.across(15).down(25).bent(50).elbow(side)` holds it out to the side (ballet's rounded arms; the `across` brings the hands together). Without it the elbow hangs down, back and a little out. Only the part square to the arm counts; on a straight arm it turns the arm about its length. |
| `knee` | For a leg in the air: which way the knee points, as a leg's direction: `back.up(20).bent.knee(side.up)` (an attitude, the knee above the foot). Without it the knee points where its foot does (`turnout`). |
| `pointed` | For a leg in the air: the foot stretched on along the shin, the toes pointed: `rightLeg { fwd.down(45).pointed }` (a dégagé, a battement, an arabesque's back foot). Without it (and without `flexed`) a lifted foot lies square, as it stood. |
| `flexed` | For a leg in the air: the foot pulled up, the toes toward the shin and the heel pushed out: `rightLeg { fwd.flexed }` (a flexed-foot kick, a heel dig, a character step). |
| `facing` | For the torso, off the feet: which way the chest faces, as a direction: `torso { down.facing(fwd) }` (a handstand facing the front; a cartwheel's upside-down). Without it the chest faces as the shortest tip from upright leaves it: `fwd` face down, `back` face up, `down` (tipped over forward) looking back. |
| `round` | For the torso: the back is rounded. The direction still says where the chest is from the hips; the spine bows back from that line, and the chest, the shoulders and the head lean further forward (`torso { up.fwd(10).round }` is a fighter's guard, `up.round` a slouch). Without it the back is straight, as in a bow from the hips. |
| `tilt` | For the head: the line of the eyes tilted from level, an ear toward its shoulder: `fwd.tilt(left(15))` (up to 45; `tilt(left(0))` holds the eyes level on a leaning torso). Without it the head sits square on the neck and leans with the torso. |
| `body` | For an arm: the direction is measured against the torso instead of the room, so the arm leans when the torso leans: `up.body`. |

```shosa
arms     { fwd.across(30).down(70).bent(40).elbow(side) }   // ballet's low rounded arms (bras bas): the hands 19 cm apart in front of the hips
arms     { fwd.across(15).down(25).bent(50).elbow(side) }   // rounded in front of the body (first): the hands 12 cm apart, at the diaphragm
arms     { fwd.up(70).bent(40).elbow(side) }                // overhead (fifth, en haut)
arms     { side.fwd(20).down(15).bent(20) }                 // open to the sides (second)
arms     { fwd.down(15).bent(40).elbow(side) }              // NOT rounded: the hands go forward, a shoulder's width apart
rightLeg { back.bent.knee(side.up) }             // an attitude: the thigh 30° above level, the knee out and above the foot
torso    { up.fwd(10).round }                    // a boxer's hunch
```

- **How far an elbow or a knee bends**: `bent` is a right angle (90°); `bent(N)` bends it
  N degrees, from 0 (straight) to 150 (folded tight). The hand or foot stays in the
  direction written and comes closer: `arms { fwd.down(15).bent(40) }` is a little bent,
  `rightLeg { fwd.down.bent(130) }` a knee drawn right up before a kick.
  **The direction is the FOOT's (from the hip), not the thigh's**: bending lifts the
  knee above that line, so a bent leg's thigh is higher than its direction says. Turned
  out with the knee to the side and up (`knee(side.up)`): `back.bent` has the thigh 30°
  above level and the foot at hip height, `back.down(20).bent` the thigh 12° up,
  `back.up(20).bent` 44° up with the foot above the hip, `back.down(35).bent` level (the
  knee at hip height). Turned out (`out(80)`), the thigh of a leg behind points about 30° out to its side
  (as a turned-out hip lets it). To give a thigh's angle, write
  it and read it off with `shosa clip` (the hip and the knee).
- **Rounded arms bring the HANDS toward the middle**: the direction is where each hand is from its
  own shoulder, so `fwd` keeps the hands a shoulder's width apart however the elbows are held. Round
  them with `across` (15 to 30, see the examples above), and read the gap in `shosa clip`: past about
  `across(30)` with a deeper bend the hands cross (`across(35).down(25).bent(60)` overlaps them 16 cm).
- **Which way it points**: `elbow(DIRECTION)` / `knee(DIRECTION)`, the direction written
  as an arm's or a leg's (`side`, `side.up`, `back`, `fwd.down`; `side` is the limb's own
  side, so `@mirror` and `arms` are right for both). Without it an elbow hangs down, back
  and a little out, and a knee points where its foot does. Only the part of the pointing
  square to the limb counts: pointed along the limb itself (a hand out to the side with
  `side.elbow(side)`) it says nothing, warning SH2011. On a straight arm `elbow` turns the
  arm about its own length (`side.elbow(back)` turns the palm forward); a model it is put
  on shows it. Where the joint points is set at each word and carried with the limb
  between them, so it does not spin on the way.
- **`round`: the back is rounded.** Without it the torso leans with a straight back, as a
  bow folds at the hips. `torso { up.fwd(10).round }` is a boxer's hunch, `up.round` a
  slouch. The direction still says where the chest is from the hips; the spine bows back
  from that line, so the chest and the shoulders lean about 15° further forward than the
  word says. A written head is measured from level, so the rounding does not tip it; an
  unwritten head leans with the chest. The back always rounds forward, whichever way the
  torso leans; there is no word for arching it back.
- **`tilt`: the head tilted to the side**, an ear toward its shoulder: `head { fwd.tilt(left(15)) }`
  (the side, then the degrees in brackets, up to 45). It is the line of the eyes **from level, in the
  room**, as every written direction is, so it is the same on an upright and on a leaning torso. It does
  not change where the face looks: `fwd.right(30).tilt(right(10))` looks 30° right and tilts 10°.
  Without `tilt` the head sits square on the neck and leans with the torso (a side bend tips the eyes
  with it); `tilt(left(0))` holds them level over it. The tilt goes evenly from word to word.
- **`body`: measured from the chest**, not the room, so the part leans when the torso
  leans (an arm, a leg or the head; `elbow(…)` is measured so too). See "Every written
  direction is measured the same way" above.


### Turnout: `turnout`

How far the legs are turned in their hip sockets, about their own length, is a row of its
own: **`turnout` says how far each foot, and its knee, points out from the way the body faces**,
in degrees as seen from above. A ballet dancer stands turned out; a hip-hop dancer may stand turned in.

| Turnout | Meaning |
|---|---|
| `out` | Both legs turn outward from the hips: each foot, and its knee, N degrees out from the way the body faces: `out(N)` (up to 90: a dancer's full turnout, the feet in one line; 45 to 60 for one who has not trained). |
| `in` | Both legs turn inward: each foot N degrees in: `in(N)` (up to 45). Toes toward each other. |
| `parallel` | The legs are not turned: the feet and the knees point the way the body faces. |

```shosa
turnout { out(80) }                 // a dancer's turnout from the first beat: each foot 80° out
turnout { parallel:4 out(45):4 }       // parallel, turned out 45° by beat 4
```

- The number is each leg's own, and always written: `out(90)` puts the two feet in one line (180° between them).
  It is not mirrored: both legs turn out alike, also under `@mirror`.
- **The knees bend the way the feet point**: turned out, a plié (`height { low }`)
  takes the knees out over the toes instead of forward, and a raised knee (`rightLeg { side.down(20).bent }`)
  points to the side. The hips, the torso and the steps are not changed: a step still goes
  where its word says, only the foot on it is turned.
- **`together` closes the feet touching, and turned out the heels touch**: at `out(90)`
  heel to heel in one line (a first position); at `out(45)` the ankles are 0.09 m apart;
  parallel or turned in, side by side (0.1 m). The turnout at the beat `together` lands is
  the one that counts, so write it first: `turnout { out(80) }` with `steps { together }`.
  (`stand` closes to standing width, 0.2 m, however the legs are turned.) A row whose
  first word is turned out begins with the heels drawn in as far as it is turned out.
- It goes like a gesture (through all of the word before, `@sharp`, `then`), and is kept
  until a word changes it, across sections too. A row nobody wrote is `parallel`.
  In a pose it is the pose's own (`pose first { turnout { out(80) }  steps { together } }`),
  and a pose's name in the row is that pose's turnout.

## Off the feet: `hands`, `head`, `back`, `seat`, `knees` … `feet`

The `steps` row says where the weight is, and it need not be on the feet. A word of the row puts it on
another part of the body, on the floor, and `feet` stands up again:

| Carries the weight | Meaning |
|---|---|
| `hands` | The weight on both hands, a shoulder's width apart on the floor, the shoulders over them: a handstand (`torso { down }`), a plank (`torso { fwd.up(15) }`), a freeze, the hands of floor footwork. `hands.fwd(0.5m)` puts them half a metre ahead of where the weight was. The arms are straight, or bent as their row says (`arms { down.bent(90) }`). |
| `leftHand` | The weight on the left hand alone, the left shoulder over it; the right arm is its row's. |
| `rightHand` | The weight on the right hand alone. |
| `head` | The weight on the top of the head (a headstand, a headspin: `torso { down }`); the hands may help (`arms { side.floor }`). |
| `back` | Lying on the upper back: `torso { back }` flat on the back, `torso { back.down(25) }` rolled up onto the shoulders, the hips in the air (a backspin). |
| `front` | Lying on the chest, face down: `torso { fwd }` flat (the worm's low point), `torso { fwd.down(20) }` the hips up. |
| `seat` | Sitting on the floor: the hips on it, the torso upright or as its row says (`torso { back }` lies down), the legs out in front unless their rows say (`legs { fwd.floor.bent(90) }`: the knees up). |
| `knees` | Kneeling on both knees, the shins on the floor behind, the hips over the knees; the `height` row sits back toward the heels (`low(30cm)`). |
| `leftKnee` | Kneeling on the left knee, the right foot on the floor in front (a knee drop, a proposal). |
| `rightKnee` | Kneeling on the right knee, the left foot on the floor in front. |
| `feet` | Back on the feet, standing where the weight was (`feet.back(0.5m)`: half a metre behind it), at standing width. |

```shosa
// A handstand: fold, the hands down 0.6 m ahead, the body over them; and down again where it stood.
steps { stand:2 then:2 | hands.fwd(0.6m):4 | then:2 feet.back(0.6m):2 | }
torso { up:2 fwd.down(30):2 | down:4 | then:2 fwd.down(30):1 up:1 | }

// Sitting on the floor, the knees up and the hands behind; then lying back.
steps { stand:2 seat.back(0.3m):2 | then:4 | }
torso { up:4 | up:2 back:2 | }
legs  { down:4 | fwd.floor.bent(90):4 | }
arms  { down:4 | back.floor:2 down:2 | }

// A headstand that spins once, the legs in a V, the hands on the floor beside the head.
steps { stand:2 head.fwd(0.4m):2 | then:2 turn.left(360):2 | }
torso { up:1 fwd.down(40):1 down:2 | then:4 | }
arms  { down:2 side.floor:2 | then:4 | }
legs  { down:4 | side.up(40):4 | }
```

- **It is timed as a `shift` is: the body goes there through ALL of the word before**, and is there on its
  beat. `stand:2 then:2 hands:4` stands for two beats and goes onto the hands over the next two (the `then`).
  Give it time: a second down to the floor or up from it reads as a person; a handstand in half a second does not.
- **Where it is put: from where the weight was**, as a jump says where it lands: `hands.fwd(0.6m)` puts the
  hands 0.6 m ahead of the middle of the feet, `seat.back(0.3m)` sits down 0.3 m behind them,
  `feet.back(0.6m)` stands up 0.6 m behind where the hands were (`left`, `right` too; nothing: on the same
  spot). `shosa check` says where the dancer ends. The feet come back at standing width, facing as the body does.
- **`hands`, `head`, `back`, `front`: the whole body is tipped as the `torso` row says**, hips and all.
  The torso's word is where the spine points, from the hips to the chest, in the room as always: `down` is
  upside down (a handstand, a headstand), `fwd.up(15)` a plank (on `hands`), `back` flat on the back,
  `back.down(25)` rolled up onto the shoulders with the hips in the air, `fwd` flat on the front. An
  unwritten torso is `up`: on `hands` that is sitting between the arms. **Which way the chest faces** is
  what the shortest tip from upright leaves: face down for `fwd`, face up for `back`, looking back for
  `down` (tipped over forward). To say it, `facing(…)` after the direction: `down.facing(fwd)` (a handstand
  seen from the front; the upside-down of a cartwheel, `up left:1@through down.facing(fwd):1@through right:1@through up:1`),
  `back.facing(down)` (rolled over onto the front, the head still pointing back: half of a windmill's roll).
  Never straight from `up` to `down` (which way round?): through `fwd`, `back`, `left` or `right` (SH2001).
  **The head of a tipped body is measured from its chest**, as with `.body`: `head { fwd }` is the face square
  on the body (looking up, lying on the back), `fwd.down(30)` the chin toward the chest.
- **`seat` and `knees` (and `leftKnee`, `rightKnee`) stand on level hips**, as the feet do: the torso's row
  leans the body from them as it does standing (`torso { back }` lies down from sitting), and `shoulders`
  and `hips` turn it. On the knees the `height` row sits the hips back toward the heels (`low(30cm)`; `mid` kneels up).
- **The legs are free off the feet, and their rows say where they point, in the room as always**
  (`legs` writes both alike, mirrored; `leftLeg` / `rightLeg` one): upside down on the hands,
  `legs { side.up(40) }` is a straddle and `leftLeg { fwd.up(20) }` with `rightLeg { back.up(20) }` a split.
  A leg nobody wrote, or sent `down`, lies as the body does: on along the trunk (straight up in a handstand,
  out behind in a plank), out in front of a seat. `DIR.floor` puts a toe on the floor (`legs { back.floor }`
  under a plank, `fwd.floor.bent(90)` the knees up before a seat, `side.floor.bent(60)` a foot of floor
  footwork), and `.body` measures from the trunk (`legs { fwd.bent(120).body }`: tucked to the chest,
  however the body lies). Kneeling, a leg is the floor's.
- **The arms: a hand that carries the weight is on the floor at its place**, both a shoulder's width apart
  (`hands`), and its arm's row says only how far the elbow bends (`arms { down.bent(90) }`: a push-up's low
  point; the body comes that much lower) and where it points (`elbow(side)`). `leftHand` / `rightHand`
  carry on one hand, the other arm free. Any other arm is its row's: `DIR.floor` puts a hand on the floor
  that way (`arms { side.floor }` beside a headstand, `back.floor` behind a seat; on the feet too, when
  the shoulder is low enough: a crouch), and an arm nobody wrote lies along the body.
- **`turn.left(720)` turns the body on what carries it**: a headspin, a backspin (`back` with
  `torso { back.down(25) }` and the legs tucked), a turn on the hands. Nothing else of `steps` is done off the
  feet: a step, a jump, a shift, `stand` want the feet under the body (error SH2025: write `feet` first).
- **On the way from one to another the old one holds until the body has set off, and the new one is there
  before the body arrives**: the feet stay on the floor through the first part of the way onto the hands,
  and the hands reach the floor in the first half. So fold first (`torso { fwd.down(30) }` in the word
  before `hands`), as a person does: the hands are then near the floor they go to.
- The floor stops a hand and a foot (a limb sent below it rests on it), and the body lies as high as its
  lowest part of the trunk allows. Elbows and knees are not kept out of the floor or of the body: read the
  sheet. A character (`"girl.vrm"`) is posed by the same angles on its own proportions, so its hands may not
  be exactly on the floor.
- Not said (yet): the weight shared between two parts by a number (a tripod's three points are the head and
  two hands on the floor, the weight written on the `head`); walking on the hands (put them again:
  `hands.fwd(0.2m)`, both at once); a roll that travels; lifting or leaning on another dancer.

## The hands: `hands`, `leftHand`, `rightHand`

An arm's row says where the wrist is. What the hand does there is a row of its own:
**the shape of the fingers, then which way the fingers point and the palm faces.**
`hands` writes both hands alike (mirrored, as `arms` does); `leftHand` / `rightHand` one.

```shosa
hands     { fist }                                // both fists, from the first beat
rightHand { relaxed:3 then:1 point:4 }            // hanging, then pointing by beat 4
hands     { spread.fingers(up).palm(fwd) }        // jazz hands: the fingers up, the palms to the front
rightHand { open.palm(up):2 fist.palm(up):2 }     // a hand held out, closing
hands     { fist.index(0).middle(0) }             // a finger of its own after the shape: 0 straight, 90 closed
```

| Hand shape | Meaning |
|---|---|
| `relaxed` | The hand as it hangs: the fingers a little curled, more toward the little finger. What a hand nobody wrote is. |
| `open` | The hand flat: the fingers straight and together, the thumb beside them. A blade of a hand, a stop sign, a salute. |
| `spread` | The hand flat and the fingers spread wide, the thumb out: jazz hands, a high five, paper. |
| `fist` | The fingers closed into the palm, the thumb across them: a punch, a guard, rock. |
| `point` | The first finger straight, the others closed, the thumb across them: pointing; one. |
| `peace` | The first and the middle finger straight and apart, the others closed: a V, scissors; two. |
| `thumbUp` | A fist with the thumb straight out of it: with the palm turned in (`thumbUp.palm(across)`) the thumb points up. |
| `gun` | The first finger straight and the thumb up from it, the others closed: a finger gun, an L. |
| `rock` | The first and the little finger straight, the middle two closed under the thumb: horns. |
| `shaka` | The thumb and the little finger out, the middle three closed: hang loose, a telephone. |
| `ok` | The tips of the thumb and the first finger together in a ring, the other three straight and a little spread. |
| `pinch` | The tips of the thumb and the first finger together, the other three closed: holding something small, a pinch of salt. |
| `heart` | The four fingers curved over together and the thumb curved under them: half a heart; the two hands side by side, fingertips and thumb tips touching, make a whole one. |
| `claw` | Every finger half bent and spread, the thumb too: a cat's paw, a monster's hand. |
| `soft` | A ballet dancer's hand: the fingers long and barely curved, the middle finger a little in toward the thumb, the first finger a little lifted. |
| `count` | Fingers held up to count, the rest closed: `count(3)` is three (first, middle and ring); `count(0)` a fist, `count(1)` is `point`, `count(2)` `peace`, `count(5)` the whole hand, spread. |

| Finger | Meaning |
|---|---|
| `thumb` | The thumb alone, after the hand's shape: `thumb(0)` straight out to its side, `thumb(90)` folded across the palm. |
| `index` | The first finger alone, after the hand's shape: how far it is curled, `index(0)` straight, `index(90)` closed into the palm: `fist.index(0)` points. |
| `middle` | The middle finger alone: `middle(0)` straight to `middle(90)` closed. |
| `ring` | The ring finger alone: `ring(0)` straight to `ring(90)` closed. |
| `little` | The little finger alone: `little(0)` straight to `little(90)` closed. |

| The wrist | Meaning |
|---|---|
| `fingers` | Which way the fingers point, as an arm's direction: `open.fingers(up)` (a stop sign on an arm held forward). The wrist bends that way, up to 80 degrees from the forearm; without it the fingers go on along the forearm. |
| `palm` | Which way the palm faces, as an arm's direction: `open.palm(up)`, `spread.fingers(up).palm(fwd)`. The hand turns about its fingers to face it there as nearly as it can (only the part square to the fingers counts); without it the palm lies as the arm has it. |

- **A word is the whole hand, in this order**: one shape; then any finger of its own (`thumb`, `index`,
  `middle`, `ring`, `little`, each with how far it is closed in brackets, 0 to 90); then `fingers(…)`;
  then `palm(…)`. What a word does not say is the relaxed hand, turned as the arm has it:
  `palm(up)` alone is a relaxed hand with the palm up, and `fist` after `open.palm(up)` lets the palm go.
  Write the palm again to keep it: `open.palm(up) fist.palm(up)`.
- **`fingers(…)` and `palm(…)` take an arm's direction** (`fwd`, `up`, `side`, `across`, `back`, `down`,
  `fwd.up(30)` …), measured in the room from the way the feet face, as every direction is; `side` is the
  hand's own side, so `hands { open.palm(across) }` turns the two palms toward each other.
  The fingers go on along the forearm unless `fingers(…)` bends the wrist, and a wrist bends no more than
  80° from the forearm: `arms { fwd }` with `hands { open.fingers(up).palm(fwd) }` is a stop sign
  (the fingers 10° short of straight up); with the arm hanging, `fingers(up)` cannot be (the hand gets as
  near as the wrist lets it). The palm faces square to the fingers: only that part of `palm(…)` counts
  (`fingers(up).palm(up)` says nothing of the palm, warning SH2011).
  Without `palm(…)` the palm lies as the arm has it: down with the arm out to the side or forward,
  toward the thigh with the arm hanging.
- **It moves as a gesture does: through ALL of the word before**, so `hands { open:4 fist }` closes slowly
  over four beats. A hand that snaps shut on its beat is `@sharp` (`fist@sharp`), or a `then` before it
  (`open:3 then:1 fist`). It is kept until a word changes it, across sections too. A row nobody wrote is `relaxed`.
- **The hand's words are the hand's row's only, and not reserved**: a file may name a move `point` or a pose
  `open`. In a hand's row the hand's own word is meant.
- A pose may hold the hands (`pose guard { arms { fwd.up(25).bent }  hands { fist } }`), and so may a move;
  `body { guard }` then closes the fists with the rest.
- Two hands make one shape by where the arms put them (a heart of two `heart` hands, fingertips together):
  bring the wrists near with the arms' row, read their places in `shosa clip`, and look at a character.
  `shosa check --at` reads a hand back: how far each finger is closed, where the fingers point and the palm faces.
- The figure of sticks has no fingers; a character (`dancer … "girl.vrm"`) shows them, and a file written
  from a dance with a hand's row (`.glb`, `.vrma`, `.bvh`) has the fifteen bones of each hand
  (`leftThumbMetacarpal` … `rightLittleDistal`, VRM's names). A dance that writes no hand has none.
  A hand that holds a mic or juggles is the mic's or the juggling's while it does.

## The face: `face`

A character (`dancer … "girl.vrm"`) has a face, and its row says **a look, then the eyes** (either alone too):

```shosa
face { smile(50) }                                  // a smile held through the whole dance
face { smile(50):3 smile(60).winkRight:1/2 smile(50):1/2 | surprised:2 smile:2 | }
face { neutral:4 | relaxed.closed:4 | sad(40):4 }   // at rest, eyes shut in a soft smile, a little sad
```

| Look | Meaning |
|---|---|
| `neutral` | The face at rest: no look, the eyes open. What a face nobody wrote is. |
| `smile` | A happy face (VRM's `happy`): `smile` is the whole of it — on most characters a wide smile, the eyes narrowed or shut with it; `smile(50)` half of it, a smile with the eyes open, which is what a dancer holds through a song. |
| `sad` | A sad face (VRM's `sad`): the brows up in the middle, the mouth down. `sad(40)` a little. |
| `angry` | An angry face (VRM's `angry`): the brows down, the mouth tight. |
| `relaxed` | A soft, pleased face (VRM's `relaxed`): on most characters the eyes closed in a gentle smile. |
| `surprised` | A surprised face (VRM's `surprised`): the eyes and the brows up, the mouth a little open. |

| Eyes | Meaning |
|---|---|
| `closed` | Both eyes shut (`closed(50)`: half shut, sleepy). Alone or after a look: `relaxed.closed`. |
| `winkLeft` | The dancer's left eye shut, the right open: a wink. Alone or after a look: `smile(60).winkLeft`. |
| `winkRight` | The dancer's right eye shut: `smile(60).winkRight`. |

- **A word is the whole face**: one look (or none: the face at rest), then `closed`, `winkLeft` or
  `winkRight`. A bracket is how much, in percent: `smile(50)`, `closed(50)`. `smile` alone is all of the
  character's happy face, which on most characters narrows or shuts the eyes: **a smile to dance in is
  `smile(40)` to `smile(60)`**, the full `smile` for an accent (the last pose, a "yay").
- **It is there on its beat, and changes in a moment**: a look takes the last 0.2 s before its beat, an
  eye 0.08 s — not the whole of the word before, as a gesture does. A wink on beat 3 for half a beat is
  `smile(50):3 smile(50).winkRight:1/2 smile(50)` (the look is written again to keep it: the word is the
  whole face). It is kept until a word changes it, across sections too. No `@` but `@mirror`, which
  winks the other eye (as a mirrored dancer does).
- **The eyes blink by themselves**, every few seconds, each dancer at its own moments, with a `face` row
  or without; not while the row has an eye shut.
- The mouth of a dancer who sings (`sings PART`) goes on singing under any look.
- The face's words are the face's row's only, and not reserved. A pose may hold a face
  (`pose cheer { arms { up } face { smile } }`), and so may a move.
- **A `face` row beside `body` wins over the face a pose or a move brings**, as every narrower row does:
  where the row has a word, the wink of `body { peaceEye }` is not seen. Leave the pose its face with
  `skip`, and write the look again after it, for a pose's face stays as any face does (its wink too):
  `body { bounce:4 | then:2 peaceEye:2 | bounce:4 | }  face { smile(50):4 | skip:4 | smile(50):4 | }`.
  A row of looks written through every bar hides every face the vocabulary files wrote.
- The figure of sticks has no face: the preview shows it on a character, and a `.vrma` keys VRM's
  expressions (`happy`, `angry`, `sad`, `relaxed`, `surprised`, `blinkLeft`, `blinkRight`). What a look
  is, exactly, is the character's own; `.glb` and `.bvh` have no face.
- Not said: where the eyes look (they look where the head faces), the brows or the mouth on their own,
  a look coming slowly.

## The shoulders and the hips: `shoulders`, `hips`

The torso's word says where the spine points (its lean). How the body is turned over
feet that stay where they stand is two rows of their own, because they are other axes
and move at other times — a guard keeps its lean through every punch:
**`hips` says where the line of the hips points, `shoulders` where the line of the
shoulders points, each in degrees from the way the feet face**, as seen from above.
The two are written the same way:

| Shoulders | Meaning |
|---|---|
| `left` | The shoulders turn to the dancer's left, the right shoulder comes forward: `left(N)`, N degrees from the feet (up to 90). |
| `right` | The shoulders turn to the dancer's right, the left shoulder comes forward: `right(N)`. |
| `center` | The shoulders square over the feet. |

| Hips | Meaning |
|---|---|
| `left` | The hips turn to the dancer's left, the right hip comes forward: `left(N)`, N degrees from the feet (up to 45). |
| `right` | The hips turn to the dancer's right, the left hip comes forward: `right(N)`. |
| `center` | The hips square over the feet. |

```shosa
shoulders { center:2 left(30):2 center:2 right(20):2 }   // square, 30° left on beat 2, square on 4, 20° right on 6
hips      { center:4 left(15):4 }                     // the hips 15° left on beat 4: under square shoulders
```

- `left` is the way `turn.left` goes: the shoulders (or the hips) face left, the RIGHT
  shoulder (hip) comes forward. A right-handed punch that lands is `left`; drawing that
  fist back is `right`.
- The number is degrees from the feet, and is always written (a turn of the hips has no
  size of its own): `left(30)`; it may have a decimal point (`left(7.5)`). The hips turn up to 45, the shoulders up to 90 (side-on);
  more is an error (SH1013): to face another way the feet turn (`steps { turn.left(90) }`).
- **Each row is from the feet, not one on the other.** `hips { left(20) }` with
  `shoulders { left(45) }` is a punch from the hip: hips 20°, shoulders 45°, as a coach
  says it. `hips { left(30) }` alone turns the hips under shoulders that stay square
  (a twist dance is `hips { left(30) right(30) }` and nothing else); to take the shoulders round
  with the hips, write both. These are the numbers `shosa check --at` reads back.
- The chest turns on the hips, about the spine, to put the shoulders where their row says:
  upright that is the difference of the two rows. **It turns at most 60° on the hips**:
  `shoulders { right(90) }` over square hips gets to 60° and is warned (SH2010, saying
  how far they get); over `hips { right(30) }` it is side-on.
- **Under a lean the number is still what is seen from above**, but a turn of the chest
  about a slanting spine shows less from above, so it may have to turn further: bent forward
  30° (`torso { up.fwd(30) }`), shoulders 90° over hips at 40° are in reach (a golfer's top);
  bent 60° (`torso { up.fwd(60) }`) over square hips the shoulders get to about 41°, not 60
  (SH2010 says how far they get, and what to change: the hips further that way, or less
  lean). The head, which sits on the spine, shifts a few centimetres sideways.
  **Bent level (`torso { fwd }`), a turn about the spine does not show from above at all**:
  the shoulders stay square to the feet whatever the row says. There, point the bend itself
  toward that side, `torso { fwd.right(60) }` with `shoulders { right(60) }` (the spine
  points 60° right and the shoulders lie across it); SH2010 says so when it happens.
- What the shoulders take round: the arms that hang on them. A hand comes 19 cm
  further forward between `right` and `left` (30° each way), on the same line: **an arm's direction is
  still measured against the room** (`fwd` is where the feet point, however the shoulders are turned).
  An arm that says `.body` goes round with the chest: `arms { side.body }` with the
  shoulders turning swings the arms round like a turnstile. A hand on the `hip` or the
  `belly` goes with the hips.
- What does not turn: the feet (they stay planted; the knees give), the legs (`fwd`
  is still where the feet point), and **the head: the face looks where the `head` row says**
  (ahead, if there is none) — a fighter's eyes stay on the target. To look over the
  shoulder, turn the head as well: `head { fwd.left }` is 45° from where the feet point,
  `head { left }` 90°, and `head { fwd.left(60) }` any number between.
- They go like a gesture: through all of the word before, `@sharp` for the snap of a
  punch, `then` to stay. A row nobody wrote is `center` from the start.
- In a pose it is the stance's own (`shoulders { right(20) }`: the left shoulder
  ahead), and a move that turns says where it comes back to with the pose's name in
  the same row — it does not need to know how the torso leans (`guard@mirror` in a
  `shoulders` row is the mirrored stance's shoulders, `left(20)`). The pose needs a row
  of that name to be named in it (SH1015):

```shosa
pose guard { arms { fwd.up(25).bent }  torso { up.fwd(10).round }  hips { right(10) }  shoulders { right(20) }  head { fwd } }
move cross {
  rightArm  { fwd.down.bent fwd.up(25)@sharp!hit guard }   // elbow back, fist out, back to guard
  hips      { right(20)     left(15)@sharp       guard }   // the hip back, and through
  shoulders { right(40)     left(35)@sharp       guard }   // the shoulder back, round with the fist, back
}
```

- Two rows of an `&` that both turn the shoulders at the same time: the later row has
  them (as with any part), and the earlier one is warned that its turn is not seen
  (SH2009) — two punches look apart, but they share the one pair of shoulders. Give the turn to
  one of them, or write them one after the other. What counts is what is lost: an earlier
  row that only stands in a pose's turn (`guard` above) loses nothing and is not warned;
  an earlier row's whip is lost, and warned, whatever the later row has there — a pose's
  name too (a move that begins `loose:1 left(30)@sharp …` holds the shoulders for that first beat).
  A `skip` there leaves them to the other row. The same for the hips; one warning
  names both when a move loses both.
- A turn is kept until a word changes it, across sections too: hips or shoulders turned at
  the end of the Verse are still turned in the Chorus unless its rows say `center`
  (`shoulders { center:4 | … }`). `check --at` at the first beat of a section shows it.
- The face's left or right is the line of the ears on the floor, as the hips and the
  shoulders are their lines: over an upright torso it is the bracket of the `head` row
  however far up or down the face looks; under a lean it is less. A face
  steeply down (a bow, a golfer over the ball) still turns a few degrees when the chest
  turns under it, and `check --at` says those few degrees (bent forward 30°, the
  face 70° down: about 3° each way through a swing).
- On the contact sheet the turn is read under each pose, where the role is drawn from
  above: the grey line of the hips turns away from where the feet point, and the shoulder
  line (left half blue, right half red) lies across it when the two are turned apart, and
  hides it when they are not (15° shows). The hips are narrower than the shoulders: the
  dark dot at each end of their line is what shows them under the shoulders (a dot above
  the shoulder line on one side, below it on the other). In the pose itself the turn
  shows only from the side (`--view side`: the two shoulders come apart); from the front
  the shoulder line only gets shorter. For the number, `shosa check FILE --at` (below).
  `shosa check` still says which way the dancer faces by its feet: hips turned at the
  end are not "turned 30° to its left".

## The feet: `steps`

The `steps` track says where the weight is. Its events are:

**A step** — `FOOT.DIRECTION`: `leftFoot.fwd`, `rightFoot.side`, `leftFoot.back`. The foot
LANDS on the word's beat and carries the weight from then on. How far it goes is a bracket
on its direction, in metres: `leftFoot.fwd(0.3m)` (a small step), `leftFoot.side(0.4m)`.

| Foot | Meaning |
|---|---|
| `leftFoot` | The left foot steps and takes the weight: `leftFoot.fwd`, `leftFoot.side(0.4m)`. |
| `rightFoot` | The right foot steps and takes the weight. |

| Step direction | Meaning |
|---|---|
| `fwd` | One stride (0.5 m) ahead of the supporting foot, or as far as its bracket says: `fwd(0.3m)`. |
| `back` | One stride behind the supporting foot: `back(0.3m)`. |
| `side` | Apart from the supporting foot, to the stepping foot's own side: the feet 0.55 m apart, or `side(0.4m)`. |
| `across` | Crossing over the supporting foot's line, 0.08 m past it (`across(0.13m)`: how far the feet cross, as `side` says how far apart), just in front of it (0.12 m; with `back`, just behind). How far in front is said by `fwd`: `fwd(0.08m).across(0.13m)`. |
| `place` | Beside the supporting foot, at standing width: a step on the spot. After a step across or behind it is beside THAT foot, so the stance moves (a balancé's third step is `rightFoot.fwd(0.08m)` after `leftFoot.back(0.08m)`, not `place`). |

- `left` and `right` are never feet: a foot is `leftFoot` / `rightFoot`. `left` and `right`
  are always the dancer's own left and right (`jump.left`, `turn.left(90)`, `shift.left`).
- **A step is placed from the OTHER foot, not from where the stepping foot was.**
  `leftFoot.fwd` puts the left foot one stride (0.5 m) ahead of the right foot. So
  `leftFoot.fwd rightFoot.fwd leftFoot.fwd` is a walk, and `leftFoot.fwd rightFoot.side` puts
  the right foot level with the left one and apart from it: "forward, side", as a dancer counts it.
- So **a foot already ahead does not advance by stepping "ahead" again**: from a stance
  with the left foot 0.3 m in front, `leftFoot.fwd(0.3m)` puts it where it is. To
  move the stance forward, reach with the front foot and close up with the back one:
  `leftFoot.fwd rightFoot.back(0.3m)` (the right foot ends 0.3 m behind the left).
- `side` is 0.55 m apart (`side(0.4m)`: the feet 0.4 m apart); standing width is 0.2 m.
  Directions combine: `leftFoot.fwd.side`. A step with `side` in it carries the body
  0.35 m that way once the other foot has followed, and
  the next plain step lands at standing width beside it: walking with every left step
  `leftFoot.fwd.side` drifts 0.35 m to the left each time. `back` is the same 0.5 m as `fwd`:
  `leftFoot.fwd stand rightFoot.back stand` goes out and comes home in one bar, and
  `leftFoot.fwd rightFoot.fwd leftFoot.back rightFoot.back stand` does it in walking steps (the
  fourth step leaves the left foot a stride ahead; `stand` brings it home).
- `across` puts the foot crossed over the other one's line, 0.08 m past it, and just in front of it
  (0.12 m); `back.across` just behind it — the cross-behind of a grapevine. Each word measures its
  own way: `across(0.13m)` is how far the feet CROSS (as `side(0.4m)` is how far apart they are),
  and how far in front or behind is said by `fwd` / `back`: `fwd(0.08m).across(0.13m)` is a fifth
  position turned out (the front heel at the back toes), `back(0.07m).across` crosses 0.07 m behind,
  `fwd(0.5m).across` is a stride that crosses (`fwd.across` too). A cross is not a
  stride. Since the next step is placed from the crossed foot, a line of
  side / cross / side drifts by that 0.12 m; alternate front and behind to cancel it. A grapevine
  crosses behind both ways and so drifts back 0.24 m over two; to keep it level, give the next
  side step the way back: `rightFoot.side.fwd(0.12m)`… or cross just behind, `back(0.03m).across`.
- A step that turns (`leftFoot.place@turn(left(45))`) swivels on the standing foot, not
  on the middle of the body, so turning on the spot wanders a little and comes back:
  a whole turn ends where it began, a part of one does not. A quarter turn in two
  of 45° ends 0.04 m back and 0.04 m to the side; half round in two of 90° ends
  0.2 m back. To be on the spot after half a turn, make it a whole one, or
  step it back (`check` says where the dancer ends).
- A step says nothing of how high the body stands: that is the `height` row (below),
  kept until it is changed — a walk in a low stance is a walk with `height { low }` beside it.
- `leftFoot.fwd@turn(left(90))` turns DURING the step: the standing foot swivels, and the
  step lands where it would after the turn. `@turn(SIDE(DEGREES))`.
- **A walk along a curve is a walk whose every step turns a little**:
  `steps { stand:2 { leftFoot.fwd@turn(left(15)) rightFoot.fwd@turn(left(15)) } q q q q stand:2 }`
  is ten steps round an arc to the left, 150° in all. The arc's radius is the stride over the
  turn of one step in radians: 0.5 m steps of 15° go round a circle of 1.9 m, of 30° one of
  about 1 m (a table), of 5° one of 5.7 m (a wide bend). Fewer degrees on the first and the
  last steps ease into the bend and out of it. To end at a place (in front of someone, at a
  door) count it out: `check` says where the dancer ends and how far it has turned
  (`ends 0.46 m forward and 3.68 m to its left of where it began, turned 150° to its left`),
  and a last straight step or two, or a shorter stride (`fwd(0.3m)`), makes up the rest.
- The same foot twice in a row is allowed (it moves again from the other foot).

**Other things the feet do:**

| Event | Meaning |
|---|---|
| `together` | The free foot closes to the supporting foot, touching (turned out, the heels touch); the weight is on both. |
| `stand` | The free foot closes to standing width (0.2 m, ankle to ankle); the weight is on both. |
| `turn` | A pivot on the supporting foot: `turn.left(90)`, `turn.right(180)` (degrees, up to 1080). |
| `jump` | Off the floor, landing on both feet: `jump`, `jump.fwd`, `jump.left(0.6m)`, `jump.apart`, `jump.stand`, `jump.together`. |
| `hop` | Off the floor from the foot that carries the weight, landing on the same foot: `hop`, `hop.fwd`. The other foot is held off the floor. From both feet, it lands on the foot whose leg its row does not throw out (a sissonne). |
| `leap` | Off the floor from the foot that carries the weight, landing on the other: `leap.fwd` (a running step), `leap.left` (a small jump to the side). The foot it took off from is held off the floor. |
| `shift` | The weight moves and neither foot leaves the floor: `shift.left`, `shift.right`, `shift.center`. |
| `go` | Walk to the dancer's own place in a formation, and stand there on the beat: `go.vee` (`formation vee { alice at -1 0  bob at 1 0 }`), `go.home` (its `at`). The steps are made for it, and it stands facing as it faced. |
| `lift` | Only for a free body (`@free`, or `free` on the dancer): the feet hang at a height, `lift(0.4m)`. `lift(0.4m).fwd` also travels (0.4 m, or `fwd(0.6m)`). |
| `land` | Only for a free body: back down to the floor. |

- `together` and `stand` move the foot that does not carry the weight, so after a step or
  a `shift.left`: `together` closes it touching (turned out, heel to heel), `stand` to
  standing width. Where the weight is on both feet (after `jump.apart`, after
  `shift.center`, after a first step already made) neither is the free one: the weight goes
  toward the foot it is nearer first (the RIGHT foot when it is between them), from a little
  before the word sets out and as far as a `shift` goes (no more than 3 cm lower), and the other
  foot closes to it alone (two feet do not slide in at once): `jump.apart:1 then:1 stand:2` ends
  with the feet together where the right foot stood. To close onto the left foot, shift to it
  first: `jump.apart:1 shift.left:1 stand:2`. The closing foot is lifted when the weight got
  over the other foot (a stance up to about 0.3 m), and SLIDES in along the floor when it could
  not (a wide second: the weight comes over as the foot slides in).
  To come in to the middle, jump them in: `jump.stand`. To STAND with the feet apart from the start, the first
  word of `steps` is `leftFoot.side` (or `jump.apart`): a first word is already made, and
  stands on both feet.
- `shift.left`, `shift.right`, `shift.center` — the weight moves over that foot (or
  between them); no foot leaves the floor. The basis of standing and waiting.
  **The hips sink no more than 3 cm for it**: with the feet apart, the other leg is all but
  straight already, and every cm toward the foot costs height. A shoulder-wide stance
  (0.3 m) goes over the foot; 0.5 m apart the hips go half of the way from the middle to
  the foot, 0.7 m apart a quarter of it.
  To go over the foot in a wide stance is a lunge: write its depth in `height`. The hips
  stay at that depth and go on toward the foot as far as the other leg reaches, straightening
  out of the bend: `steps { leftFoot.side(0.7m) shift.right:1 }` with
  `height { mid low(15cm):1 }` is a side lunge over the right foot, the hips 15 cm below the
  wide stance straight, the left leg straight and the right knee at about 75°.
  Deeper, the other leg bends too: with `low(25cm)` it is a plié in second with the weight
  over the right foot, the knees at about 50° (left) and 95° (right).
- `turn.left(90)`, `turn.right(180)` — side, then degrees in brackets (up to 1080: a
  double turn is `turn.right(720)`).
  A turn pivots on the foot that carries the weight: the one that took the last step or
  that a `shift` went to (on both, if the feet are together). A free leg may gesture during a turn.
  After it "forward" is the new facing. `turn.b` turns to face the dancer of role b.
  It turns through all of the word before it (see "When it sets out"): `then:1 turn.right(360):1`
  is a pirouette in one beat, `together:2 turn.right(90):2` a slow quarter turn.
- **A jump: `jump`, `hop`, `leap`** — each word says which foot it LANDS on; which it
  takes off from is how the feet stood before it (a jump from one foot or from both):

  | Word | Lands on | From both feet | From one foot |
  |---|---|---|---|
  | `jump` | both feet | a jump in place, a sauté, an échappé (`jump.apart`) | an assemblé |
  | `hop` | the foot it stands on; from both feet, the one whose leg its row does not throw out | a sissonne (the other leg in a shape as it lands; neither: SH2012) | a hop, a temps levé |
  | `leap` | the foot that carried no weight | (an error, SH2012: which foot?) | a running step, a jeté |

  From both feet, a `hop` pushes off both together and lands on one: the foot whose leg
  its row does NOT hold in a shape as it lands (a sissonne: `steps { together:2 then:1 hop.left(0.2m):1 }`
  with `rightLeg { skip:2 then:1 side.down(45):1 }` springs off both feet and lands on the left,
  the right leg out to the side; a `skip` or `down` in its row after that closes it). With no leg
  thrown out (or both), it does not know its foot (SH2012); put the weight on one first instead
  (`shift.left`, then `hop` lands on the left foot and `leap` on the right). After a `hop` or a `leap` the foot that did not land
  is held low behind it, off the floor: the next word of `steps` that moves it puts it
  down (`leftFoot.place`, `stand`), and a leg row may shape it. Running is
  `rightFoot.fwd leap.fwd leap.fwd leap.fwd`; skipping is `leftFoot.fwd hop.fwd rightFoot.fwd hop.fwd`.
  After the word, where it lands:

| Jump word | Meaning |
|---|---|
| `place` | Land where you took off (the default). |
| `fwd` | Land ahead: 0.4 m for a jump, 0.25 m for a hop, 0.8 m for a leap, or `fwd(0.6m)`. |
| `back` | Land behind, as far as `fwd` goes ahead: `back(0.3m)`. |
| `left` | Land to the dancer's left (0.4 m; a leap 0.6 m): a dodge is `jump.left(0.25m)`. |
| `right` | Land to the dancer's right. |
| `up` | How high the body rises, always with the height: `hop.up(5cm)`, `jump.fwd(0.3m).up(20cm)`. It takes the time that height needs and waits on the floor through the rest of the word before. Not written, the time before it decides. |
| `apart` | For a jump: land with the feet 0.6 m apart, or `apart(0.8m)`. |
| `stand` | For a jump: land with the feet at standing width. |
| `together` | For a jump: land with the feet touching (turned out, the heels). |
| `change` | For a jump: land with the feet as they stood, mirrored left for right: the foot that was behind lands in front, as far ahead and as crossed (a changement: fifth, right foot front, lands in fifth, left foot front). |
| `beat` | For a jump: the legs beat in the air. They close crossed the other way round from how they land, and open again to land: `jump.beat` from fifth is an entrechat quatre (it lands as it stood), `jump.change.beat` a royale. `beat(2)` beats twice, each time the other way round (`jump.change.beat(2)`: an entrechat six); up to 3. |
| `leftFoot` | For a jump, last: the stance it lands in, as a step of that foot from the other would stand: `jump.rightFoot.fwd(0.08m).across(0.13m)` lands in fifth with the right foot in front, from wherever the feet were (an échappé closing from second). Where it lands (`fwd`, `left`, `up(…)`) is written before the foot. |
| `rightFoot` | For a jump, last: the stance it lands in, named by the right foot (see `leftFoot`). |

  A jump LANDS on its beat; the bend and the flight are in the word before it, and the
  knees give a little after. How high it goes follows from the time that
  word gives it (the longer, up to a second, the higher; a `then` counts as that word, so
  `then:2 jump` is a high one and `then:1/2 jump` a little one).
  The word before is the jump's time, not the standing: at 120 `together:3 jump` lands on beat 3
  and takes a whole second of the word before it, bending from beat 1.5 and off the floor near beat 2.
  To stand still 2 beats and then jump, give the jump only its own time: `together:2 then:1 jump`
  (bends after beat 2, off the floor near 2.5), or write its height (`together:3 jump.up(10cm)`
  stands until about beat 2.2). To see when the feet leave the floor, `shosa check --at 2,2.25,2.5` (it says each foot `on the floor` or `N cm up`), or `shosa sheet --step 0.25`.
  A quarter of a second gives 3 cm of air and half a second 10 cm: a leap that is to be SEEN in the air needs half a second or more before it.
  **The height of a jump is how far the toes clear the floor.** In the air the feet point (soon after
  the take-off, and back under the legs to land, toes first), and the body has risen onto its toes
  to leave the floor, so the hips go higher than that by what the pointed foot reaches down: about
  4 cm more for a little jump, up to 9 cm for 12 cm and more (`jump.up(5cm)` lifts the hips about 9 cm,
  the toes 5 cm). A foot swinging through in a leap or a run clears the floor by more.
  **To make jumps alike whatever comes before them, write the height**: `hop.up(5cm)`,
  `jump.fwd(0.3m).up(20cm)`. A written height takes the time it needs (5 cm about 0.28 s, 10 cm
  0.44 s, 20 cm 0.69 s, bend and air) and the body stands through the rest of the word before:
  `shift.right:3/2 hop.up(5cm):1 hop.up(5cm):1 hop.up(5cm):1` is three hops of the same
  5 cm, where without `up` the first, after the longer word, would go twice as high. With
  less time than that before it, it rises what the time gives (warning SH2016, which says how
  high it gets): jumps every half beat at 120 go up 4 cm at most, every beat 12 cm. It lands at the
  height it took off from (the `height` row), and a plain `jump` lands with the feet as they stood: as far apart, one as far ahead, crossed
  if they were crossed (a sauté in fifth lands in fifth); `jump.change` lands with them mirrored, the foot
  that was behind now in front (a changement: fifth with the right foot front lands in fifth with the left
  front; the feet open past each other in the air). From one foot, `jump` lands with the
  feet centred under where the two of them were, the one held in the air counted (after a
  `hop`, about 6 cm behind the foot it stood on). An assemblé from fifth: the back leg brushes
  out in its row and is let go (`skip`) over the landing, and `jump.change` brings it down in front:
  `steps { rightFoot.fwd(0.08m).across(0.13m):2 then:1 jump.change.up(12cm):1 }` with
  `leftLeg { skip:1 then:1 side.down(45):1/2 skip:3/2 }` (arms in second for it: `side.fwd(20).down(15).bent(20)`).
  The jump bends its own knees before it takes off, as deep as it will go high, measured down
  from standing straight (5 cm below it for a little jump of 3 cm, 7 cm for 6 cm of air, 12 cm for
  16 cm and more; a little one bends quickly and is soon off the floor). A plié the `height` row
  already stands in is part of that bend: the jump sinks only as much deeper as it needs, pushes
  up onto straight legs and goes up from there, and lands on straight legs and gives back into the
  row's plié. So the plié before a jump may be written or not, and there is one bend either way:
  `height { straight:1 low(8cm):1 }` with `steps { together:2 jump.up(10cm):1 }` goes down into
  the plié through beat 1, stays, sinks 1 cm more, jumps 10 cm, lands at beat 2 and gives back into the plié.
  `jump@turn(right(180))` turns in the air. Jumps in a row, each on its beat: `jump jump jump`.
  **A jump lands in any stance a step can stand in: the foot and its step, last.**
  `jump.rightFoot.fwd(0.08m).across(0.13m)` lands in fifth with the right foot in front, wherever the feet
  were — out of second (an échappé closing: `jump.apart(0.5m) then jump.leftFoot.fwd(0.08m).across(0.13m)`),
  out of first. The words after the foot are a step's (`fwd`, `back`, `side`, `across`, each with its metres),
  and say where that foot stands from the other; the two land about the middle of where they took off (plus
  `jump.fwd(0.3m)…`, written before the foot). `up(8cm)` and `beat` may stand before the foot or after its step.
  **`beat`: the legs beat in the air** (from a crossed stance, a fifth). They close crossed the other way
  round from how they will land, then open and land: `jump.beat.up(12cm)` from fifth with the right foot in
  front shows the left in front in the air and lands as it stood (an entrechat quatre); `jump.change.beat`
  beats as it stood and lands changed (a royale); `beat(2)` beats twice, the first as it lands and the
  second the other way round (`jump.change.beat(2).up(18cm)`: an entrechat six), up to `beat(3)`. Give it
  air: 12 cm for one beat, 18 cm and more for two (a beat in less is a flicker).
  Between two jumps close together (no more than 0.15 s standing between the give of one and the
  bend of the next: jumping jacks, a skipped rope) the knees go down and up ONCE: the give of the
  landing is the bend of the next take-off.
  `left` / `right` go to the dancer's own left and right (a sidestep or a
  dodge: `jump.right(0.25m)`; a dancer `facing stageLeft` has upstage on its left, so two who
  face each other and both write `jump.left` go to opposite walls); with `fwd` or `back` they go
  diagonally (`jump.back.left`). In the air both legs may take a shape: `leap.fwd(1.2m)` with
  `leftLeg { fwd }` and `rightLeg { back }` over its flight is a split leap. **A leg's row
  wins over `steps`**, so the leg the leap lands on must be put `down` in its row on the
  landing beat, or its foot stays in the air (warning SH2013); the leg it took off from
  stays as its row holds it (a landing in arabesque) until its row says `down`, which puts
  it on the floor beside the foot it landed on:
  ```shosa
  steps    { stand:1 shift.left:1 then:2 | leap.fwd(1.2m):2 then:2 | }  // off the left foot, onto the right at beat 4
  leftLeg  { skip:3 then:1/2 back:1/2 | then:2 down:2 | }              // behind in the air, on the floor beside the right at beat 6
  rightLeg { skip:3 then:1/2 fwd:1/2 | down:4 | }                      // in front in the air, down to land on at beat 4
  ```
  The same holds after a `hop` or a `leap` for any word of `steps` that moves a foot
  its leg's row still holds up (`together`, `stand`, a step): the row says `down` first.
- `lift(0.4m)` — ONLY for a free body (`@free` on the event, or `free` on the dancer line;
  otherwise SH1022): the feet hang 0.4 m above the floor until told otherwise.
  `lift(0.4m).fwd`, `.back`, `.left`, `.right` also travel (0.4 m, or `fwd(0.8m)`).
  `land` comes back to the floor.
  A double jump: `steps { then:0.3s lift(0.4m):0.3s@free lift(0.9m):0.3s@free land:0.4s@free }`
  (0.4 m up at 0.3 s, 0.9 m at 0.6 s, down at 0.9 s).
- `then` — nothing new: the feet stay, and the next word's foot sets out in its second half.

## Height: `height`

How high the body stands on its feet is a row of its own, and **it is kept until it is
changed**, as a turn of the hips is — across steps, jumps and sections:

| Height | Meaning |
|---|---|
| `low` | The knees bent, the hips 16 cm lower than standing straight in the same place; `low(30cm)` 30 cm lower (a deep crouch is `low(40cm)`). |
| `mid` | Standing, the knees soft (bent 12°): the height a row nobody wrote stands at. |
| `straight` | Standing on straight legs (a ballet dancer's, a soldier at attention): the hips half a centimetre higher than `mid`. |
| `high` | On the toes, the legs straight. |
| `pointe` | On the tips of the toes, the feet upright under the legs (pointe shoes): the hips higher than `high` by most of the foot's length. |
| `sit` | Sitting on a seat, the feet on the floor: `sit(0.45m)` on a seat 0.45 m above the floor (a chair, and what `sit` alone means; a sofa about 0.35 m, a bar stool 0.7 m; 0.1 m to 0.8 m). The hips go back and down onto it and carry the weight there; any other height stands up again. |

```shosa
height { straight:3 low(12cm):3 straight:3 }  // a demi-plié from straight legs: down through beats 0-3, up through 3-6
height { { low(8cm) low } q q q }   // a bounce in a low stance
height { mid high:2 mid }           // up onto the toes by beat 1, down again through beats 1-3
height { straight }                 // a soldier at attention, a ballet dancer: straight legs, kept until changed
```

- **A height moves as a gesture does: through ALL of the word before** (a plié or a rise
  as slow as you write it). To stay, then go, `then`: `height { mid:2 then:1 low:1 }`
  stands until beat 2 and is down at 3.

- `mid` stands on soft knees (12°), as a character stands about; `straight` on straight legs
  (about 4° in `check`), half a centimetre higher. Ballet and a soldier at attention write
  `height { straight }` once; it is kept until another height is written (after a plié, go
  back to `straight`, not `mid`). `high` is on the toes, the legs straight (demi-pointe: the ankles 7 cm up).
  `pointe` is on the tips of the toes, the feet upright under the shins (the ankles 12 cm up): pointe work,
  written as `high` is (`height { straight:2 pointe:2 straight }`, a relevé; steps and turns on it as on
  `high`). Both feet rise: for one foot on its toe beside a flat one, rest it there in its leg's row (`back.floor.bent(40)`).
- `low` bends the knees, the hips 16 cm lower than standing straight on the same feet (on wide feet, lower than the wide stance straight): a fighter's stance, a plié — not a squat.
  `low(8cm)` is a slight bend, `low(40cm)` a deep crouch (up to `low(60cm)`).
- The weight stays where it is, and the feet: a `height` row beside a pose or a walk
  changes only how high it stands.
- A row nobody wrote is `mid` (soft knees). A pose may hold a height (`pose guard { … height { low(8cm) } }`),
  and a pose's name in the row is that pose's height.

### Sitting: `sit(0.45m)`

`sit` is a height too: the hips on a seat, the feet on the floor. The bracket is **how high
the seat is above the floor** (a chair 0.45 m, which is what `sit` alone means; a sofa about
0.35 m, a bar stool 0.7 m; from 0.1 m to 0.8 m). The seat itself is not in the language: whoever
sets the scene puts a chair there.

```shosa
steps  { leftFoot.fwd rightFoot.fwd stand:2 | then:4 | then:4 | then:4 | }      // two steps to the chair, the feet closed by beat 2
height { mid:2 then:2 | sit(0.45m):4 | then:4 | then:2 mid:2 | }                 // down over beats 2-4, sitting until 12, up over beats 12-14
torso  { up:4 | up:2 up.fwd(15):2 | then:1 up:3 | up:4 | }                       // sitting, a lean toward the other
```

- **It moves as every height does: through all of the word before.** The `then:2` before
  `sit` is the two beats of sitting down; the last `then` before `mid` is getting up
  (`sit(0.45m):4 then:4 then:2 mid` sits for eight beats and rises in two). Without a
  `then`, `mid:4 sit(0.45m)` sinks slowly through all four beats. A second or so each way reads as a person.
- **The feet stay where they stand**, and the hips go back and down from over them: the
  shins end upright, the knees over the ankles, the thighs level on a chair. On the way
  down and up the torso leans forward over the feet by itself (up to 28°, on top of what
  its row says) and is back as its row says when seated.
- **Where the seat has to be**: seated, the hips (the `pelvis` joint of `shosa clip`) are
  0.09 m above the seat and behind the ankles — behind the dancer's place, which is the middle
  of the feet — by 0.45 m on a chair (seat 0.45 m), 0.44 m on seats of 0.35 m and 0.5 m,
  0.43 m at 0.3 m, 0.42 m at 0.6 m, 0.38 m at 0.7 m. The back of the body is about 0.12 m
  behind that joint. So to sit on a chair, stand with the ankles about 0.45 m in front of
  the middle of where the hips are to be, the back to the chair.
- **Sitting, every other row says what it says standing**: `torso`, `head`, `shoulders`,
  the arms (a hand to the other's hand, `b.hand`; a point; `chin`). An arm nobody wrote, or
  sent `down`, rests its hand on the thigh instead of hanging; the weight does not drift from foot to foot.
- **Do not move the feet while seated** (a step, a jump, `stand`: warning SH2024): the seat is
  measured from the feet and would slide with them. Stand up first. A free leg may still take a
  shape in its row (`rightLeg { fwd.down(30) }` stretches it out).
- `sit` is kept until another height is written: `mid`, `straight` or `low` stands up again.
  `check --at` reads the knees about 89° on a chair. Not said: leaning on the back of the
  chair or an armrest, crossed legs, feet off the floor on a high stool.

## Annotations

| Annotation | Meaning |
|---|---|
| `@smooth` | The way TO this word eases in and out (the default). |
| `@sharp` | Go at the last moment: the part waits, then gets there in no more than 0.15 s before its beat. An accent — a punch, a clap. Fast, so it can be too fast (SH2003). |
| `@linear` | The way TO this word is at constant speed: no slowing as it arrives. |
| `@through` | Pass this word without stopping: the way to it arrives at speed and the way on to the next word leaves at that speed, the two one curve (no corner at the word) — a point an arm goes through on an arc (`rightArm { down side:1@through up:1 }`), a wave. On a word the part stops at (the last, or before `then`) it says nothing (SH2017). Not for steps. |
| `@mirror` | Left and right swapped: `kick:2@mirror` kicks with the other leg. |
| `@free` | The rules of weight, gravity and speed do not apply to this event. Bones still keep their length. |
| `@turn` | For a step or a jump: turn while doing it, `leftFoot.fwd@turn(left(90))`, `jump@turn(right(180))`. The step lands where it would after the turn. |

**`@` says how a part gets to its word, and what is done to the word — never the shape it
arrives in** (that is the target's: `bent`, `elbow`, `round` …). `@turn(…)` is for steps
and jumps. Several can follow one event, after its length and before its mark:
`fwd.up(25):1@sharp!hit`, `kick:2@mirror@free`.

### A whole passage one way: the `quality` row

To make a passage flow, or hit, do not mark every word: say it once in a `quality` row
beside the body's rows. Its words are the names of the marks, without the `@`:

```
lead {
  quality  { through:8 | sharp:4 smooth:4 | }
  rightArm { down:1 side:1 up:1 fwd.up(30):1 … }
  torso    { up:2 up.fwd(15):2 … }
}
```

- `through`: every word of the arms, legs, torso, head, hands and height in that time is passed
  without stopping, as if each had `@through`: one flowing line through the places written
  (legato). The part still stops where it has to: at the last word, before a `then`.
- `sharp`: every word is hit at the last moment, as if each had `@sharp` (staccato, popping).
- `linear`: every way at constant speed (a robot). `smooth`: the default again.
- It is in force from its beat **until the next word of the row**, across sections: one
  `quality { through }` at the top of a dance makes all of it flow. Its lengths only say when the
  next word comes; `then` and `skip` fill time and change nothing.
- A word's own mark wins: in a `through` passage, `clap@sharp` still hits, and `up@smooth` still stops.
- It says nothing to `steps` (a foot lands where it goes), and is not written in a pose or a
  move: there the marks go on the words. A move danced in a `through` passage flows, for the
  words of it that have no mark of their own.

Write the words as densely as the shape needs — one every half beat is fine in a `through`
passage: the part passes each on its beat and does not stop at any.


## Roles and dancers

A `role` owns choreography; a `dancer` is a body that performs it. A file for one body
has one of each (`role solo`, `dancer solo humanoid on solo`). Several bodies:

```
role lead
role chorus
dancer alice humanoid on lead   at 0 1
dancer bob   humanoid on chorus at -1.5 0
dancer carol humanoid on chorus at 1.5 0  mirror

section Verse {
  lead   { steps { leftFoot.side stand rightFoot.side stand } }
  chorus { arms  { side.up:2 down:2 } }
}

form { Verse }
score { dancer alice  dancer bob  dancer carol }
```

- EVERY track sits inside a role's block (`lead { steps { … } }`); a track written
  directly in a section is an error (SH1003).
- `dancer NAME humanoid on ROLE`, then any of, on the same line:
  `at X Z` (metres; x is stage left — the dancers' left, so the RIGHT of the sheet,
  which looks from the audience; z toward the audience), `facing front | back |
  stageLeft | stageRight` (or `left(30)` / `right(30)`: degrees turned from the front),
  `mirror` (left and right swapped), `delay N` (starts N beats late: a canon),
  `free` (weight, gravity and speed limits never apply), `in SECTION …` (dances only there;
  in the other sections the dancer is still on the floor, standing as they were left).
- Not written, a dancer stands at the origin (`at 0 0`) and faces the audience (`facing front`).
- A character for the preview: a VRM file in quotes right after `humanoid`,
  `dancer alice humanoid "girl.vrm" on lead` (from the folder of the .shosa file, or else from the editor's
  models folder, `shosa.models.folder`). The preview
  shows the dancer as that character instead of sticks; it changes nothing of the movement or
  of what is exported. Without it, the dancer is drawn as sticks.
- `height N m`: how tall the character is drawn, from the floor to the top of its hair, ears and all
  (0.1 m to 5 m; `150cm` also does): `dancer alice humanoid "girl.vrm" height 1.5m on lead`. Models are
  made at their own heights (a child, a two-heads-tall mini): give dancers who should be the same size the
  same height. Without it, a character is as tall as its file makes it. Only the preview's character
  changes: the movement and what is exported stay the same. Without a character it does nothing.
  (Not the `height` track of a section, which lowers or raises the body.)
- `sings PART`: the dancer's mouth sings the words of that part of the song (`music`), as the
  song's `singer … on PART` line sings them: `dancer alice humanoid "girl.vrm" sings melody on lead`.
  PART is the song's name for the part. Each syllable opens the mouth to its vowel (あ い う え お;
  ん and っ close it) on the beat it is sung, for as long as it is sung. The preview's character
  and the `.vrma` export move their mouth expressions; the movement is the same. A part nobody
  sings in the song is SH1908; without `sings`, the mouth stays shut.
- `mic right | left | both`: the dancer holds a mic in that hand: `dancer alice humanoid "girl.vrm"
  sings melody mic right on lead`. While it sings (by the phrase: up a little before the first
  syllable, still up through a breath, down after the last) the hand is at the mouth, whatever
  that arm's row says: the mic wins, and a word of that arm that arrives while singing is
  SH2018 (it is not seen). Between phrases the arm dances its row with the mic in its hand;
  where the row says nothing it holds the mic before the belly, the elbow bent (it does not
  swing). `both`: both hands on the mic while singing (both arms' words wait), and between
  phrases the right hand holds it and the left is free. Without `sings` the mic just stays in
  the hand. The preview draws the mic and closes the hand on it; exported files have the arm
  at the mouth, but no mic and no fingers.
- `juggles balls | clubs | rings`: what the dancer's hands throw in its `juggle` row (next
  section). How many is not written: the pattern says (`3` is three balls). A dancer cannot
  hold a mic and juggle (SH1043).
- `at` is where the feet stand TOGETHER before anything is written, and a first step
  already made moves one foot off it: `rightFoot.fwd(0.3m)` from `at 0 0` stands with the
  middle of the feet at 0.15 m forward, `leftFoot.side(0.6m)` with it 0.3 m to the left. To
  line up dancers who begin in different stances, move each one's `at` back by half its stance.
- `delay N` makes the score N beats longer: the others stand still while the late dancer
  finishes. `mirror` mirrors turns as well, so after a quarter turn a mirrored dancer faces
  the opposite way to the others.
- Several dancers can dance one role. A dancer appears in the scores that list it
  (`score solo { dancer bea }`: bea alone).
- Two bodies face to face, 1 m apart:
  `dancer ann humanoid on a at -0.5 0 facing stageLeft` and
  `dancer ben humanoid on b at 0.5 0 facing stageRight`.
  `at` is where the feet stand together: a stance with a foot forward (`leftFoot.fwd(0.3m)`)
  brings each of them nearer, and at 1 m the front legs of two such fighters meet (SH2007).
  Start those 1.1 m apart or more.

## Formations: `formation` and `go`

Where everybody stands is a **formation**, written once at the top level. A dancer walks to
its own place in it with one word of `steps`: `go.NAME`.

```shosa
role lead
role wing
dancer alice humanoid on lead at 0 0
dancer bob   humanoid on wing at -1.2 0
dancer carol humanoid on wing at 1.2 0

formation vee  { alice at 0 0.8    bob at -1.2 -0.4   carol at 1.2 -0.4 }
formation swap { alice at 0 -0.8   bob at 1.2 0       carol at -1.2 0 }

section Verse {
  lead { steps { stand:4 | go.vee:4 | then:4 | go.swap:4 | then:4 | go.home:4 | } }
  wing { steps { stand:4 | go.vee:4 | then:4 | go.swap:4 | then:4 | go.home:4 | } }
}
```

- `formation NAME { DANCER at X Z … }` names dancers (not roles), each with its place as on a
  dancer's line: metres, x to stage left, z toward the audience. A dancer who never goes to a
  formation may be left out of it. **`home` is the formation every file has**: each dancer's own `at`.
- `go.NAME` is a word of `steps`, and like every word it ARRIVES on its beat: on the beat it is
  written at the dancer stands on its place, the feet at standing width, and it walks there
  through the time of the word before it. `stand:4 | go.vee:4 |` leaves at beat 0, is there
  at beat 4 and stands there 4 beats. To stand 4 beats first and walk the next 4:
  `stand:4 | then:4 | go.vee:4 |`.
- **One row serves every dancer of a role: each goes to its OWN place.** A row whose dancer the
  formation does not name is SH1045.
- **The steps are made for it, and it stands facing as it faced.** A place near enough is
  walked facing so, a step a beat: forward, backward (up to 0.45 m a step), or sideways with
  the feet crossing (a grapevine; about half a metre a step), the last step closing the feet.
  **A place too far for that is walked as people walk**: the first step turns the body the
  way it goes, it walks there forward (two steps a beat when one a beat would be too long),
  and the last steps turn it back to face as it faced. At 120 beats a minute, four beats
  walk about 1.5 m sideways or 2 m forward facing the front, and about 2.5 m turned.
- `steps(N)` says how many steps, the last one the close: `go.vee.steps(8)`. A step is no
  longer than 0.7 m, and no longer than a fast walk (1.8 m a second) covers in its time: a
  step of a quarter of a second reaches 0.45 m. Longer is SH2026: give the walk more time
  (a longer word before it). To face another way on arriving, a `turn` after it.
- The other rows go on meanwhile: the arms, the head and the face dance their words through
  the walk (on a body that turns the way it walks, when the place is far), and arms nobody
  wrote swing as in any walk.
- **Dancers whose ways cross go round each other by themselves**: each keeps to its own right,
  as people passing in a corridor do, and one who walks goes round one who stands (the way
  leaves and arrives along the straight line and bows aside in the middle; 0.7 m between
  two dancers' middles where there is room for it). To say it yourself, bow the way:
  `go.swap.front` passes on the audience's side, `go.swap.back` upstage (0.5 m aside at the
  middle of the way, or `front(0.8m)`): "alice passes in front, bob behind". A way written so
  is left as written, and the others go round it. **`go.NAME.right` and `.left` bow the way
  to the walker's own side** (its right or left as it walks there, not the stage's;
  `right(0.8m)`): when every dancer of a formation walks on to the next one's place with
  the same word, they all go round ONE circle — three on a triangle, each to the next
  corner, `go.turned.right(0.8m)` — where straight ways would cut across it as a small
  triangle. The wider the ring and the bow, the rounder: give it 8 beats.
  A bowed way is a longer one: it needs the
  time. Where two still meet, `check` says so (SH2007): bow one of them, or let them go at
  different times.
- As the FIRST word of its row (`steps { go.vee:4 … }`) it is already made, as any first
  word: the score begins with the dancer on that place, whatever its `at`.
- It needs the weight on the feet (off them: SH2025), and it takes no `@turn`.
| After the name | Meaning |
|---|---|
| `front` | The way there bows toward the audience, to pass in front of someone: 0.5 m at its middle, or `front(0.8m)`. |
| `back` | The way there bows upstage, to pass behind someone: `back(0.8m)`. |
| `right` | The way there bows to the walker's own right as it walks (not the stage's): 0.5 m at its middle, or `right(0.8m)`. Everybody of a formation walking to the next place with the same word goes round one circle. |
| `left` | The way there bows to the walker's own left: `left(0.8m)`; the circle the other way round. |
| `steps` | How many steps it takes, the last one closing the feet: `go.vee.steps(8)` (2 to 64). Not written: one a beat, or two a beat to a place far away. |

## Juggling: `juggle`

A dancer whose line says `juggles balls` (or `clubs`, `rings`) throws them in a `juggle` row,
written in **siteswap** exactly as jugglers write it. `every` says how many beats from one throw
to the next, and is written before the first pattern of the row (SH1042); it holds until it is
written again.

- **A word with no length is its throws as written**, one throwing beat each: at `every 1/2`,
  `531` is 3 throws and 3/2 beats, `3 3 3 531 531 4 3` (or `333531531 43`, the same) is 11 throws.
  One `531` is the pattern once through, not over and over: write it twice, `531531`, or give it
  a length.
- **A word with a length is the pattern over and over for that long**: `3:8` is sixteen throws,
  `531:3` six (531 531).

```
dancer alice humanoid juggles balls on solo

section Show {
  solo {
    juggle { every 1/2  3:4 | 531531 3 3 | 441441 3 3 | skip:2 (4,2x)* | }
    steps  { then:4 | leftFoot.side:2 together:2 | then:4 | then:4 | }
  }
}
```

- Siteswap: each digit is one throw, and the number is how many throwing beats until that ball
  is thrown again (a = 10, b = 11 …). The hands take turns, the right first (`@mirror`: the
  left); an odd number goes across to the other hand, an even one comes back to the same hand.
  `0` is an empty hand, `1` hands a ball straight across, `2` holds it. The number of balls is
  the average of the numbers: `3` and `531` are three, `4` and `(4,4)` four.
- `(a,b)`: both hands throw at once, the right hand's number first; a pair takes two throwing
  beats, its numbers are even, and `x` sends one across (`(4,2x)`). `*` after pairs plays them
  again with the hands swapped: `(4,2x)*` is `(4,2x)(2x,4)`. `[ab]` throws two balls from one
  hand at once (`[32]1`).
- A pattern that cannot be juggled (two balls coming down together, `54`, `543`; an odd number
  in a pair) is SH1041, which says the beat where it goes wrong. A pattern of another number of
  balls than the row began with is SH2019.
- **The patterns of a row written one against the other are one run of throws**: the hands keep
  taking turns through it, and a ball thrown in one pattern comes down in the next. A pattern can
  follow another only when what is in the air at the change is what it would have thrown itself
  before (`3` → `531`, `531` → `3`, `3` → `441` are direct; `3` → `51` is not). When it cannot, it is
  SH2019, which says the throws that make the change when there are some: `3:3/2 4 51:2`.
  A word thrown once through or less is such a passage: it need not be a pattern on its own (`4`
  once is not four balls; `5314` is no pattern), and is judged only by what is in the air before
  it. A plain pattern and a pattern of pairs need throws of their own between them; `skip`
  between them always works.
- A length need not be a whole number of the pattern: at `every 1/2`, `531:3` is six throws (531
  531) and `531:2` four (5 3 1 5). The next pattern takes over from what is in the air then, so a
  cut in the middle of a pattern is a change like any other.
- The throws go on evenly through a run, from one word into the next (`every 3/4  3:4 | 3:4 |`
  throws every 3/4 over the bar line): a word whose length is not a whole number of throws
  hands the rest of its last throwing beat to the next word.
- Bar lines count the throws as they count any beats: at `every 1/2` in 4/4, eight throws a bar
  (`33333333 | 531531 33 |`). Throws written out that run across a bar line are tied over it as
  any word is, or written without bar lines.
- `skip:N`: no throwing for N beats. The balls in the air are caught, the hands keep them, and
  the arms dance their rows; the next pattern starts again from the hands.
- The beats are the score's: `{ 531:3/2 3:1/2 } q q`, `%`, `verse 2 { … }` and bar lines work as
  in any row, and a pattern can start on the beat of the song where it changes.
- While a pattern is thrown, both arms are the juggle row's: a word of `arms`, `leftArm` or
  `rightArm` that arrives then is SH2021 (it is not seen). Before and after, the hands come to
  the juggling and go back over a third of a second. The legs, the hips, the torso and the head
  dance their rows: walk, turn or bow while juggling.
- How high a ball goes is the time it flies: at tempo 120 and `every 1/2`, a 3 rises about
  0.2 m, a 5 about 1 m, a 7 about 2.5 m. Higher than 3 m is SH2020, and so is a throw every less
  than 0.12 s (the hands cannot keep up). Rings (32.4 cm across, flat bands) meet in the air below
  0.6 m, clubs below 0.45 m (SH2020): throw them less often (`every 1` at tempo 120 sends a 3 up 0.9 m). A ring is
  thrown along the forearm, the hand an edge (the thumb up), going up and down in one place.
  A throwing hand sinks, swings up and lets go at the speed the ball or ring leaves at; a
  catching hand gives with it.
- **Tricks**, put on a pattern with `@`, change how the hands throw and catch it, never what is
  thrown: `3:4@mills` (Mills' Mess: six throws round, three from the left and three from the right, the arms crossing and uncrossing),
  `@windmill` (the first hand's arm always on top, every ball thrown on its side and caught on the other, all one way round),
  `@penguinCatch` (caught behind the hip, the arm straight and the palm turned back, and thrown in front, palm up, above the shoulders: at tempo 120 `every 1`, else SH2020), `@clawCatch` (the palm down, the
  ball thrown up to the face and snatched from above there near its top; at `every 1/2` it suits a tempo
  of about 113 to 197, else SH2023 says the tempo it suits), `@reverse` (thrown from outside over the top, caught inside),
  `@tennis` (one ball thrown from well outside and higher, over the top of the others, to and fro like a tennis ball over the net; the rest a lower cascade nearer the middle),
  `@halfShower` (the first hand always throws from well outside, higher, over the top; the other always from inside, lower, under it),
  `423:4~@takeouts | :2 …` (a hand throws its 3 to the other as it sets off on a quick swipe across in front, takes its own 4 from above near the middle as it falls and carries it to the far side, stops once and carries it round a big circle, the ball just before the forehead, out past its own side and below, up into its next 4 from inside, as the 2, while the other hand throws its own 4 under it; written on in more words of the same pattern, `423:4@takeouts 423:4@takeouts`, the pattern goes on through them, not from its first throw again),
  `@backThrow` (thrown from behind the back over the other shoulder), `@backCatch` (caught behind the
  back, the ball come down over the shoulder it was thrown on), `@underThrow` (from under the thigh of the
  throwing hand's side); behind the back, the shoulders turn well round, that hand's side back.
  A throwing trick is every throw of the word; a catching one (`@penguinCatch`, `@clawCatch`, `@backCatch`) every
  ball caught in it, to be thrown again there. One throw is a word of one throw, which a
  cascade allows anywhere (`3:2 3:1/2@underThrow 3:3/2`, `3:2 3:1@backCatch 3:1`; at `every 1/2` a word on a whole beat of a
  run begun on one is the right hand's). Under the leg, lift that leg in its row by then
  (`rightLeg { skip:1 fwd.bent:1 then:1 down:1 }`, and the weight on the other foot in `steps`),
  or it is SH2022; behind the back, throw high enough to clear the shoulder (0.55 m, a throw
  every 1 at tempo 120), or it is SH2022. `@penguinCatch` goes with none of `@clawCatch`, `@backThrow`, `@backCatch`
  and `@underThrow`, nor `@backThrow` with `@underThrow`, nor `@backCatch` with `@clawCatch` or `@mills`, nor `@windmill`
  with `@mills`, `@penguinCatch`, `@backThrow`, `@backCatch` or `@underThrow`, nor `@tennis`, `@halfShower` and `@takeouts` with
  each other, `@mills`, `@windmill` or `@reverse`, nor `@takeouts` with a catching trick (SH1010).
- The preview draws the balls (clubs turning end over end, rings along the forearms, edge to the front) and turns a
  character's palms up while it throws. Exported files have the arms; the balls are not in them.

## Dancing to a song

`music "song.lys"` names a song written in Lily#: a path from this file's folder (`"song.lys"` beside it, `"../song.lys"` one folder up). The dance
then takes its tempo, its time and the order of its sections from the song: **the song's
own form is the form, so this is the one kind of file that writes none.**

```
music "song.lys"                  // or: music "song.lys" score karaoke  (one of the song's scores)

role solo
dancer solo humanoid on solo

section Verse  { solo { arms { side:4 | up:4 | } } }           // danced while the song plays its Verse
section Chorus { solo { arms { clap:1 side:3 | % | % | % | } } }

score { dancer solo }             // the whole song
```

- **A section is danced during the song's section of the same name**, every time the
  song plays it. The order is the song's. There is one text per name:
  a section played twice is danced the same both times (a second `section Chorus`
  is an error), except for what it does not write, which stays as the section before left it.
  What the second chorus does differently is a `verse 2 { … }` in its row (see "Doing it again").
- A section shorter than the song's stands still for the rest of it. A longer one is an
  error (`SH1903` says how many beats and bars the song's section has). A section of
  the song that this file does not have is stood through.
- A section whose name the song does not have is never danced (`SH1905` lists the
  song's sections).
- **Do not write `time`**: the bars are the song's (`SH1902`). Count the bars of each
  section in the song's time; `shosa check` prints where each section falls.
- **Do not work the song's shape out of the `.lys`: write `music` and one short section,
  and run `shosa check`.** It prints
  ```
  score to song.lys: 97 beats, 48.5 s, 1 dancer
    Verse    0-49    upbeat of 1, then 8 bars from beat 1   24.5 s   at 120   6/8
    Chorus   49-97   8 bars                                 24 s     at 120   6/8
  ```
  The beat is the note of the lower number of the song's time, and the tempo is counted
  in that beat — it need not be the number after `tempo` in the `.lys` (a 6/8 song
  marked 60 is 120 here), and a song that writes none is 120. A `tempo N` here counts the same beat
  (`tempo 4 = 60` says it by the note, `tempo 80%` without the number).
- If the song's section begins with an upbeat, the dance of it begins at its first bar
  line, and the upbeat is stood through. Every beat number after it (in `check`, on the
  sheet, of a mark) is later by the upbeat: with an upbeat of 1 in 6/8 the bars start
  at 1, 7, 13 …. The sheet's bold numbers follow the song's bar lines.
- **If the song changes tempo or time on the way, the dance follows it**, and nothing
  is written for that here. `shosa check` says where, on the line of the section it happens in:
  `March   0-16   4 bars   8.67 s   at 120, 90 from beat 12   4/4`.
  After a change of time a bar has the new number of beats — the bar lines are held
  to it, `%` does a bar of it again — and a beat is the note of the new time's lower
  number (in 6/8 a bar is 6 beats). A change of tempo moves no beat: only the
  seconds of the beats after it change (`check` gives a mark's beat and its second).
  `:0.3s` and `:12f` are counted at the tempo their section begins in.
- To change what the song gives: `tempo N` dances (and plays the song) at that tempo
  (the tempo the song BEGINS at, if it changes: the later ones keep their proportion),
  and `tempo 75%` at three quarters of the song's own, whatever that is;
  `form { Chorus Verse Chorus }` plays the song's sections in that order — its names
  are the song's, whether this file dances them or not. A score can have its own, so
  one file has the whole song and a practice cut:
  `form short { Chorus }` and `score practice { dancer solo  form short  tempo 75% }`.
  A form over the song's sections takes the repeat signs too (`form twice { |: Chorus :| }`)
  but no `tempo` or `time` of its own (SH1035: they are the song's),
  and the unnamed score of such a file still needs no form: it follows the song, beside
  the named scores that play their cuts.
- A score danced to a song may `loop` (a practice cut that goes round) and be `inPlace`,
  like any other.
- Do not write `music` unless you were given the song: without its file the line is an
  error (`SH1901`).

### Choreographing the words

(How to plan and write a whole dance — a song's sections as roles, variety, the vocabulary
files — is the choreography guide: `shosa guide`, in the editor the tool `shosa_guide`.)

**Before writing a dance to a song, read the song with `shosa song song.lys`** (in the
editor, the tool `shosa_song`). It prints the sections as played, who sings, and the
words bar by bar:
```
song hello.lys: 4/4 at 128 (a beat is a crotchet), 16 bars, 64 beats, 30 s
  sections as played (…):
    Intro    bars 1-4     4 bars   beats 0-16    0-7.5 s
    Chorus   bars 5-12    8 bars   beats 16-48   7.5-22.5 s
  sung by: zunda (Zundamon), metan (Metan). …
    Intro: no words
    Chorus
      bar 2 (beat 20)  zunda+metan: 1い 1.5は 2じ 2.5ま 3る 3.5よ~1.5   "いはじまるよ"
      bar 7 (beat 40)  zunda+metan: / 1.5よ 2ろ 2.5し 3く 3.5ね~1.5   "よろしくね"
      bar 8 (beat 44)  zunda: 1な 1.5の 2だ~1   "なのだ"
      bar 8 (beat 44)  metan: 1で 1.5す 2わ~1   "ですわ"
```
- Write one `section` for each section it lists, under the same name, as many bars long.
  A section the song plays again (`Chorus, verse 2`) is danced from the same text: what
  the second time does differently is a `verse 2 { … }` (see "Doing it again").
- `bar 2` is the second bar of that section, and `(beat 20)` the song's beat its bar line
  is on — the beats `check`, the sheet and `--from`/`--to` count, so the よ at 3.5 of
  that bar is beat 20 + 3.5 − 1 = 22.5 (`shosa sheet … --from 22 --to 23 --step 0.25`).
  The number before a syllable is the beat of the bar it is sung on, from 1; `~1.5`
  holds it a beat and a half; a `/` before a syllable says there is a rest of a beat or
  more before it (counted from where the last syllable ended, which may be in the bar before).
- **A word stands where the body arrives, so a gesture for a syllable is a word placed
  on its beat.** The words before it in the bar add up to its beat less 1: for the arm
  up on the よ above (beat 3.5 of bar 2), `rightArm { … | side:5/2 up:3/2 | }` — `side`
  stands on beat 1, `up` on 1 + 5/2 = 3.5.
- **An arm leaves a shape for the next word as soon as it gets there**, so a shape for
  a held syllable drifts through the hold unless a `then` follows it (see `then` above):
  `up:3/2 then:1 side` is up on the syllable, stays the beat and a half it is held, and
  goes to `side` in the beat of the `then`. The last word of a section is left for the
  next section's first word; to hold a last pose over the section's end, begin the
  next section's row with `then` (its first word then lands that much later).
- What reads as dancing to the words: the big shapes on the held syllables (`~`) and on
  the first syllable after a rest (`/`); the steps and the travelling in the bars without
  words; a sung line's last syllable as a pose to hold. Material used again — a step, a
  gesture on a word that comes back — is a `move` (or `pose`), and a move can be used in
  each row it covers (`steps { run:4 | … }` and `arms { run:4 | … }`).
- The dancer who sings a part is given `sings PART` (the part names it lists), so the
  mouth moves to the words. Parts joined with `+` sing together: two dancers on them
  suit a mirrored unison (`@mirror`). Where one part sings alone (bar 8 above), its
  dancer leads, and the other answers it or turns the head to it (`head { zunda.head }`).

## What the language cannot say (yet)

Do not try to express these; mention them as a limitation instead.

- Where the eyes look, and the brows or the mouth on their own (the `face` row has whole looks; the mouth of a dancer who sings is the song's); the shoulders on their own (a shrug). A finger's joints one by one, or a finger bent sideways: a hand's row closes a whole finger.
- "As far as it goes" (write the limit: 45 for the hips; the shoulders say when they do not get there, SH2010), a place on the other dancer's face (`b.chin`), the way an elbow travels in a hook.
- Holding an object, but a mic (`mic` on the dancer line). Contact other than a hand reaching a place (hand in hand is `b.rightHand`, `neighbor.hand`; an arm round a waist or linked elbows are not there: put the hand on the `shoulder`).
- Walking up to another dancer, or to a thing ("up to b", "to the door"): a dancer walks by name
  only to its own place in a formation (`go.vee`, see "Formations"); other ways are steps written
  and counted (a curve is steps that turn, see "The feet"; `check` says where they end). Turning
  to face the way it walks on the way to a formation, or running there.
- Weight on an elbow, a forearm, a shoulder alone or a hip (the body lies on its `back`, its `front`, a
  `seat`, the `knees`, the `head` or the `hands`: see "Off the feet"); leaning on the back of a chair
  (`height { sit(0.45m) }` sits on one), crossed legs.
- A change of tempo or of time in the middle of a section (a section says them where it begins; a dance to a song follows its song).
- A toe at a place on the other leg but its ankle, its shin, its knee and its thigh; a hand on the other side's leg, or on a foot.
- A foot that points or flexes while it stands on the floor; a jump landing on one foot from a beat (an entrechat trois, cinq), a beat of one leg against the other held out (a cabriole); a foot that slides while it carries the weight (a moonwalk).

## Checking your work

```
shosa check FILE            diagnostics, or "no problems"; every score with its length, and each section in it with its beats, bars, seconds, tempo and time, a step or jump the score starts with already made, and where each dancer ends from where it began; then every mark
shosa check FILE --score Rage   one score only (`--score ""` the unnamed one)
shosa check FILE --at 3,4.5,0.75s   also how the body is turned at those moments (beats; seconds with an `s`): hips, shoulders, face (and its tilt, when the eyes are off level), in degrees, how far each elbow and knee is bent (as `bent(N)` counts), and whether each foot is on the floor
shosa sheet FILE -o X.png   the movement on one image per score (X.png for the unnamed score, X-Idle.png, X-Punch.png …)
shosa sheet FILE -o X.png --score Punch --step 0.5 --from 0 --to 4   one score, closer poses (in beats; --from and --to also in seconds with an `s`, as --at: --from 0.5s --to 1s; --step 0.1 for a punch)
shosa sheet FILE -o X.png --view side   seen from stage left: a role facing front is in profile (one facing stageLeft or stageRight is in profile WITHOUT it)
shosa sheet FILE -o X.png --score solo  with `score solo { dancer bea }`, bea alone and larger (with `--score`, the file is X.png itself)
shosa clip FILE -o X.json   the solved movement as numbers: every joint at every frame (ONE file: with several scores an array of them, each with its `name`; `--score NAME` for that one alone)
```

What `check` prints for a file with a looping `score Idle` and a `score Patrol`:

```
guard.shosa: no problems
  Idle: 8 beats, 4 s, 1 dancer
    Idle   0-8   2 bars   4 s   at 120   4/4
    guard loops on the spot
  Patrol: 12 beats, 6 s, 1 dancer
    March   0-8   2 bars   4 s   at 120   4/4
    Turn   8-12   1 bar   2 s   at 120   4/4
    guard ends 0.96 m to its left of where it began, turned 90° to its right
  Patrol !step at beat 1 = 0.5 s
```

A score is headed by its name (the unnamed one by `score`), then one line for each section
played, then one line for each dancer: `ends where it began`, or how far `forward` / `back` /
`to its left` / `to its right` of there, by the way it faced at the start. `, turned N° to its …`
is added when it ends facing another way: **without it, the dancer faces as it began**.
Both ways at once read `ends 2.25 m forward and 0.42 m to its left of where it began`.
The place is the middle of the feet; a foot held up at the end (a passé, a coupé) counts
where it last stood, so ending on one foot in place says `ends where it began`.
A looping score says `loops on the spot` or `travels 1 m forward each time round`.
A score danced to a song is headed `Patrol to song.lys: …`. The marks come last, each with
its score in front (`score !clap at beat 1 = 0.556 s` for the unnamed one), and with
`   Attack, verse 2` behind when the score plays that section more than once, and with
the dancer behind (`score !hit at beat 1 = 0.5 s (a)`) when the score has more than one.
The line of a first step already made is longer than quoted above (``guard starts with
`leftFoot.fwd` already made: the first step is not seen (right for a stance, …)``); a foot
that a pose places says nothing. A section not written in bars is counted in whole
bars all the same (5 beats in 4/4 read `2 bars`).

How far something is turned is not read off the sheet (the view from above is good to
about 10°): ask for it. `shosa check FILE --at 3,0.75s` adds a line for each moment, score
and dancer:

```
  Punch at beat 3 = 0.75 s: hips 20° left, shoulders 45° left, face 30° right and 10° up; bent: elbows 90° left, 15° right, knees 12° left, 10° right; feet: left on the floor, right on the floor
```

Each foot is `on the floor`, `on its toes` (the heel up: a rise, a pointed foot touching
the floor) or `N cm up` (the lowest of its heel and toe): a jump's take-off and landing are
found by asking a few moments.

The turns are **from the way the feet face**, measured on the floor, more to the dancer's
own left or right: `hips` and `shoulders` are the numbers written in their rows (over a
leaning torso too; less only where SH2010 said they do not get there), `face` is
the bracket of `head { fwd.right(30) }` (the line of the ears; less over a leaning torso)
and `up` / `down` its height above or below level. A dancer whose feet
do not face the front has `(the feet face 90° left of the front)` behind; from the front
of the room the hips are then that plus their own number (`clip`'s `angles` has both).
With several dancers, a line for each, the name in front of `hips`
(`score at beat 4 = 2.5 s: a: hips 20° left, …`). These lines come last, after the marks.
After `bent:` each elbow and knee is how far it is bent, 0 straight: the number `bent(N)` writes.
A straight limb reads a few degrees (a leg standing at `straight` or stretched in the air about 4°, an arm `bent(0)` about 5°; at `mid` the standing knees read 12°):
the joint is kept a hair short of its full length so it knows which way it bends. An arm written
`down` (or not written) hangs with the elbow 10° bent; `down.bent(0)` holds it straight.
Between two words a part eases out of the one and into the other (slow, fast, slow), and
the head moves as a direction, so the halfway moment is near, not at, the halfway angle:
ask for the moment rather than working it out.

When the sheet cannot settle the rest — whether the head moved, how high a fist is,
whether it is in front of the middle of the body — read the numbers. `shosa clip` writes `fps` (30), `joints` (their names
in order: `pelvis`, `chest`, `head`, `nose`, `leftShoulder`, `rightHand`, `leftAnkle` …)
and, for each of `figures`, `frames`: one row for every frame, x y z for every joint in
that order, in metres. The frame of a moment is its seconds × `fps` (0.75 s is frame 22.5: a `@sharp` word
is still on its way at frame 22 and there at 23, so take the later one). y is up, z is
toward the audience, x toward stage left — for a dancer facing front, z is forward and
+x its own left. Beside `frames`, `angles` has a row for every frame too: the ten numbers
named in `angleNames` (`feet`, `hips`, `shoulders`, `face`, `faceUp`, `leftElbow`, `rightElbow`,
`leftKnee`, `rightKnee`, `tilt`), the degrees `check --at`
says, more to the left (or up) counted positive (`tilt` with the left ear down); `feet` is from the front of the room; an
elbow or a knee is how far it is bent, 0 straight. (Standing upright, this role's head has its middle at 1.72 m and its shoulders
are at 1.45 m. In the guard of "The shoulders and the hips" the head is at 1.67 m: a punch at `fwd.up(25)`
arrives at 1.64 m, nose height, one at `fwd.up(18)` at 1.57 m, the chin.)

Diagnostics are `FILE(line,column): severity CODE: message`. SH0xxx is syntax, SH1xxx is
a word or a count that does not make sense, SH2xxx is a movement the body cannot do
(SH2002 a step longer than the leg, SH2003 too fast: a hand over 10 m/s (a fast punch), a foot
over 12 m/s (a snap kick), at the fastest moment, which in an eased word is about 1.5 times its
mean (a hand going 1 m needs 0.15 s at least; `@linear` goes at its mean all the way, 0.1 s), SH2004 both feet off the floor,
SH2005 a loop that does not join, SH2006 a hand that cannot reach the other dancer,
SH2007 two dancers whose heads, bodies or standing legs are in the same place,
SH2009 two rows of an `&` that turn the shoulders, or the hips, at the same time,
SH2010 shoulders the chest cannot turn to on the hips, SH2011 an elbow or a knee pointed
along its own limb, SH2012 a hop (with no leg thrown out) or a leap from both feet, SH2013 a foot that `steps` puts
down while its leg's row still holds it up, SH2014 a bend or a knee written on a leg sent
down, which stands on the floor, SH2024 a foot moved while the body sits). The message says what to change.

The sheet shows poses numbered by beat from 0, read left to right, then down. How far
apart they are depends on the length shown: every half beat up to 8 beats, every beat up
to 32, every 2 beats beyond (every 3 in a section whose bars have 3, 6, 9 or 12 beats, so
that the first beat of every bar is among them; each section's poses are counted from its first bar line). **Anything shorter than that gap is invisible** — a half-beat
punch on a per-beat sheet shows nothing — so before concluding that a movement is missing,
look closer with `--step` and `--from` / `--to`. A movement that comes round as often
as the poses are taken (a march of two beats on a sheet of every 2 beats) looks like
standing still: look closer there too. Bold numbers start a bar.
In a score of several sections, a section's name stands in purple beside the first pose in it.
Under the first beat are the tempo, by the note that is felt, and the time (`4=120 4/4`; in 6/8 `4.=96 6/8`), and again
under the first pose of a section played at another tempo or in another time: two scores that differ only in speed differ there.
**Under each pose the role is drawn again from above**, audience at the bottom (so a role facing front
has its nose down and its left, blue, on the right): the grey line is the hips, the blue and red line over it the
shoulders, the ring the head with its nose. Read there which way the role faces, how far the shoulders are
turned from the hips (the two lines cross; in a lean they lie one ahead of the other, so compare their slopes), where the face looks, and a lean (the shoulders ahead of the hips).
A dark dot ends the hips' line on each side. Which way, and which shoulder or hip is ahead, reads at any angle; the degrees read to about ±10, and a face's up and down not at all (`--view side`).
With more than one dancer each has its own place in that row, left to right as in the pose, with its name under it (and at its ring on the floor);
how far apart they stand is on the floor plan, not there.
**Left limbs are blue, right limbs are red**; the torso and shoulder line are dark. The camera stands
in front and a little to stage left, so a role facing front shows its left on the right
of the picture; the short line on the head is the nose. From there a punch or a kick
straight ahead is foreshortened: judge those, bows and nods with `--view side`.
A bent knee can look straighter than it is too (in a deep plié in second, a knee bent 60° looked
about 30°): read the angles in `shosa check --at`. And from a little to the side, the two arms of
a symmetric pose do not look the same.
Under the poses is the floor from above,
audience at the bottom: the grey line is the path of the hips, the blue and red marks are
footprints, the ring is where the role started. Each pose is drawn in the middle of its
own place in the row, so **travel is seen only on the floor**, not from pose to pose. Always look at the sheet: a file with
no diagnostics can still be the wrong movement (a mirrored arm, a step the wrong way).

## Rules and gotchas

- Never leave out `role`, `dancer`, `form` or `score`; every track is inside a role's block.
- A word is AT its target ON its beat, and stays its length. A step written on beat 1 lands on beat 1.
- The way there is in the word BEFORE: `arms { down:4 side }` takes four beats to get there.
  To stay and then go, a `then` before the next word: `down:3 then side`.
- A score starts IN its first words: `body { guard jab }` starts in guard, the jab lands on beat 1. There is no `:0`.
- A definition without a length takes its defined length; write the length when counting.
- Limbs: `side` / `across`. Torso and head: `left` / `right`.
- `torso` leans; `hips` and `shoulders` turn the hips and the shoulders over the feet, each from the feet (`left(20)`: degrees of turn, as seen from above). Neither turns the feet, the head, or the other.
- `turnout` turns the feet and the knees out (`out(80)`) or in, both legs alike; the hips stay square.
- **A number is always in brackets, after the word it belongs to: how far toward that word.** `fwd.left(30)`, `fwd.up(30)`, `up.fwd(10)` (a lean from upright), `left(20)`, `turn.left(90)`, `bent(40)`, `leftFoot.fwd(0.3m)`, `lift(0.4m)`. Metres carry their unit.
- `left` and `right` are always the dancer's left and right; a foot is `leftFoot` / `rightFoot`.
- Every written direction is measured from the way the feet face, level — the head's too. `.body` measures from the chest.
- The target is where a part arrives and in what shape (`bent`, `elbow(…)`, `knee(…)`, `round`, `body`, in that order); `@` is only how it gets there. Write a word as target, length, `@`, mark.
- A step is measured from the other foot.
- Opposite targets need one in between.
- A leg gesture ends with `down`, and never overlaps a step.
- How high the body stands is the `height` row, kept until it is changed.
- `jump` lands on both feet, `hop` on the foot it stands on, `leap` on the other.
- A part does not lean with the torso unless its target says `.body`.
- `body` takes only your poses and moves, `then` and `skip`.
- Count every bar and write the `|`. An event across a bar line is tied: `kick:2~ | :1`.
- Again: `{ … } q` for a few events, `%` for a bar, `|: A :|` in the form for sections.
