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

figure hero                      // who the choreography is written for
dancer hero humanoid on hero     // a body that dances it

// A pose: a named way of standing and holding the body.
pose guard {
  steps { left.fwd.low@small }
  arms  { fwd.25@bent }          // the fists up in front of the face, the elbows down
  torso { fwd.80@round }         // leaning 10 degrees forward, the back rounded
  head  { fwd.25 }               // the face up again, looking ahead
}

// A move: a named row of words. `!hit` names a moment for the game engine.
// The fist is out ON the beat the jab is written at, and back in guard a beat later.
// The same direction as the guard, straight: the fist goes out along one line. (To another
// direction it would swing: from `fwd.high@bent` to `fwd` it comes down like a chop.)
move jab { leftArm { fwd.25@sharp!hit guard } }

// A section: what each figure does over a stretch of time.
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

**Nothing of this skeleton can be left out.** Every file has at least one `figure`, one
`dancer`, one `section`, one `form` and one `score`. The least a file can be:

```shosa
figure solo
dancer solo humanoid on solo

section Main {
  solo {
    steps { left.fwd right.fwd left.fwd together }
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
| `music "song.lys"` | The song this is danced to (see "Dancing to a song"). Optional. |
| `figure NAME` | An owner of choreography. Required: at least one (see "Figures and dancers"). |
| `dancer NAME humanoid on FIGURE …` | A body that dances a figure. Required: at least one. |
| `pose NAME { TRACK { target } … }` | A named pose. |
| `move NAME { TRACK { events } … }` | A named move. |
| `section NAME { FIGURE { TRACK { … } … } … }` | A stretch of time: what each figure does in it. It may begin with lines of its own — `tempo N`, `time N/N`, `upbeat N` — before its first block (see "What a section begins with"). |
| `form { A B A }` | The order the sections are played in. Required: every score plays one. The unnamed score plays this one; `form NAME { … }` is for the score that names it (a file whose scores all name their form has no unnamed one). Repeat signs: see "Doing it again". A `tempo N` or `time N/N` before a name: see "What a section begins with". |
| `score { dancer NAME … }` | One file written: who dances, and which form. Required: at least one. `score NAME { … }` is one more (see "Scores"). |

Names are letters, digits and `_`. The words in the left column are reserved, and a pose
or a move cannot take the name of a word of the language — any word of any table here,
so not `half`, `low`, `side`, `center`, `together` either (SH1005 says so) — (a mark's name, after its `!`,
can be any word at all). Definitions can be used before
or after the line that defines them. A figure and its dancer may have the same name.

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
  `loop`, `inplace`, `tempo N` (or `tempo N%`), `title "…"`, `music "…"`. A dancer the score does not
  list is not in it.
- **A score starts IN the first word of each row**: at beat 0 every part is where its
  row's first word says, and nothing is seen going there. A part no row writes is
  neutral: arms hanging, torso upright, head straight, feet side by side. Within the
  score, where a figure is, which way it faces and how it is posed carry over from one
  section of the form to the next.
- **A game character's file has one form and one score per movement** — `Idle`, `Walk`,
  `Attack` …, as in the complete file above: `form Idle { Idle }` and
  `score Idle { dancer hero  form Idle  loop }`.
- `loop` in a score — the end joins the start. The score is solved as if it followed
  itself: the way to the first word of each row is made at the END of the loop, in its
  last word, so a looping walk's first step lands on beat 0 every time round, and
  `body { guard:8 }` in a loop simply stays in guard. The feet must end as
  they start (same stance, same facing; travelling is fine) or SH2005 says so.
  A bounce in that guard is one more row, `steps { { both.low@small both.low } q q q }`:
  `both` changes the height and leaves the stance alone (see "Tracks"). (That row is
  8 beats, like the guard beside it: with bar lines, `{ both.low@small both.low } q | % |`.)
- `inplace` in a score — the sections are written the same (`left.fwd` still steps), but
  the even travel over the floor is taken out of the output and reported as a velocity,
  for an engine that moves the character itself. Usually with `loop`.
- A section lasts as long as its longest track. A part no track writes stays as it is.

## Tracks: one row per part of the body

A section holds a block for each figure that does something in it, and the block holds
tracks: `section Main { solo { steps { … }  arms { … } } }`. The examples below show
the tracks alone; in a file each of them stands inside a figure's block.

| Track | Meaning |
|---|---|
| `body` | The whole body. Takes the poses and moves you have defined, `then` and `skip`; a narrower track written beside it wins for its own part. |
| `steps` | The feet on the floor: steps, turns, jumps, changes of height. Where the weight is. |
| `arms` | Both arms at once, mirrored: `side` is away from the midline for each. |
| `leftArm` | The left arm: where the hand points from the shoulder, or a place on the body it goes to. |
| `rightArm` | The right arm. |
| `leftLeg` | The left leg when it carries no weight: where it points from the hip. |
| `rightLeg` | The right leg when it carries no weight. |
| `hips` | The hips turned over feet that stay where they stand: `left` (30 degrees), `right.20`, `center`. The chest goes round with them; the head does not. |
| `torso` | The spine: `up` is upright, `fwd.high` leans forward 45 degrees. |
| `twist` | The chest turned on the hips, about the spine: `left` (30 degrees), `right.20`, `center`. The feet and the hips stay. |
| `head` | Where the face looks: `fwd` is straight ahead. It nods with the chest's lean, but does not turn with a `twist`. |

Tracks may overlap: `body` covers everything, `arms` covers both arms. **Where two tracks
write the same part at the same time, the narrower one wins**; when the narrower one has
ended, or says `skip`, the part follows the wider one again (it is where the wider
track's word says at that moment, having gone there through the narrower track's last
word). So `body { guard:8 }` with
`head { fwd.left:2 … }` is a guard whose head looks around, and `arms { side:4 }` with
`leftArm { up:4 }` puts the right arm to the side and the left arm up.

The `steps` row holds two parts: where the feet stand, and how high the body stands on
them. `both.low` writes only the height, so beside a wider track it takes the height
and leaves the feet: `body { guard:8 }` with `steps { { both.low@small both.low } q q q }`
is a guard that bounces, `body { walk:4 }` with `steps { both.low:4 }` the same walk
with bent knees. A step, a turn or a jump writes the feet, and **a step is taken or it
is not**: a wider track's step (a pose's stance included) is taken only if that track
has the feet at the beat the step is written at. One whose beat falls where a narrower
track has the feet is not taken at all.

## Events: `target:length@annotation!mark`

A track is a row of events (words). Each is a target, then optionally `:` and a length,
`@` annotations and one `!` mark — glued together, no spaces:

```
side.high            there for one beat (the default)
side.high:2          for two beats
side.high:1/2        for half a beat      (also :1.5, :3/4)
side.high:0.3s       for 0.3 seconds
side.high:12f        for 12 frames (at the file's fps)
side.high:2@sharp    gone to at the last moment: an accent
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
dancer counts "on 3" is the word AT beat 2: `arms { side.low:2 clap … }`.

**The way to a word is made BEFORE its beat, in the time of the word before it in the row.**
How much of that time depends on the part:

| Part | When it sets out |
|---|---|
| The feet — everything in `steps`: a step, `together`, a shift, a turn, a height | In the second half of the word before, and no more than 0.5 s before its beat. A foot STANDS on its beat and is carried to the next one late: in `left.side together`, the left foot is down on beat 0, and the right closes between beat 0.5 and beat 1. |
| A jump | It LANDS on its beat. The bend and the flight are in the word before it (in up to a second of it). |
| Arms, legs, torso, head | Through ALL of the word before. `arms { side:4 up:4 }` is at the side on beat 0 and travels the whole four beats to be up on beat 4. A gesture sets out for the next word as soon as it has arrived, unless told to stay (`then`, below); it eases in and out, so it slows at each word (for one unbroken movement through a word, `@linear` on that word and on the one after it: `down side@linear up@linear`). |
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
  `then` with nothing after it simply stays. For the feet and for a `@sharp` word a
  `then` only waits: they still set out late in it (its second half; its last 0.15 s).
- **The first word of a row is where the part IS when the score starts.** Nothing is
  seen going there: `body { guard jab }` starts in guard, and the jab lands on beat 1.
  To show the body going to its first target, write first where it comes from:
  `arms { down side:3 }` raises them through beat 0 to 1; `arms { then side:3 }` the same
  from wherever they are. There is no length 0 (SH1009): every word takes time —
  so a score cannot end on the instant its last word arrives. "Back in the stance at
  2 s" in a score of 2 s is the stance arriving earlier and standing to the end, and
  a figure that comes back "on the next bar's 1" has to come back inside the last bar.
  The same for the feet: a step written on beat 0 of a score that does not loop is a
  foot already there. To SEE it land, give the score a lead-in (`steps { then:4 | left.fwd … }`).
  `shosa check` says it under the score (``solo starts with `left.fwd` already made``; a `left.place` too): count that
  step out of how far the dancer goes — "two steps forward, two back" from beat 0 ends one step behind.
  `shosa check` also says where each dancer ends (``solo ends 0.5 m back of where it began``,
  ``solo ends where it began``): read it instead of adding the steps up.
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
  first part). Its length is the sum of the parts as written, for a defined move too:
  a 4-beat `kick` that starts on the last beat of a 3/4 bar is `kick:1~ | :3`
  (`kick~ | :3` would be 4 + 3 beats). One that crosses several lines is tied over each:
  a 12-beat `drill` is `drill:4~ | :4~ | :4 |`. Staying still needs no tie: `then:4 | then:4 |`.
- `{ … } & { … }` — rows that happen together, inside one track. All start at the same
  time; the whole is as long as the longest row. The braces are required on both sides.
  Where two rows write the same part, the LATER row wins:
  `body { { walk:4 } & { wave:4 } }` walks while one arm waves.
  A word in front of a group has its length written (`guard:1 { jab } & { stepIn }`):
  a bare word followed by `{` would read as the name of a track.
- `!name` marks the beat its word is written at: the moment the hand is there, the
  foot lands, the jump comes down. (On a move it is the beat the move is written at:
  the beat of its first word.) It does nothing to the movement; it is written to the output for the engine.
  `shosa check` lists every mark with its beat and its second, and the sheet writes
  it in green over the first pose at or after it. Any event that moves something can
  carry one (a step, a turn, a hand going to a place), and a name may be used as often
  as you like: an engine gets one event for each. A bar done again by `%` gives its
  marks again, and so does a section the form plays again: each event says the
  section that wrote it and which play of it this is (`shosa check`: `!hit at beat 12 = 6 s   Attack, play 2`),
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
    steps { left.side right.side together | % | }          // begins at the section's first bar line
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
  again: `rightArm { { up:1/2@bent side.high:1/2@bent } q q }` waves three times.
  `q` is the nearest group in braces written before it in the same track (plain events
  in between do not matter: `{ a b } then:1 q`). Without a length it lasts as long as
  the group; `q:2` stretches or squeezes the group into 2 beats; `q@mirror` swaps left
  and right (`steps { { left.side together } q@mirror }` goes left, then right). A `q`
  with no group before it is an error (SH1026): one event is simply written twice.
- **A bar: `%`.** Alone between bar lines, `%` is the bar before it again; `%2` is the
  two bars before it (any number: `%4`, `%8`; one bar sixteen times is `a | % | %2 | %4 | %8 |`): `steps { left.side together right.side together | % | % | % | }`,
  `steps { vine:4 | vine@mirror:4 | %2 | }`. The bars must be whole (no event tied into
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
  of a bar, with the line after it: `steps { left.side together right.side verse 1-2 { together } verse 3 { jump.together } | }`
  changes the last beat only. A `%` in a verse is the bar written before it — in
  the same verse, or before the verses (never a bar of the verse beside it).
- A change INSIDE a longer run is written over it, in a narrower track that says `skip`
  up to that moment: `body { vine:4 | rock:4 | %2 | }` with `arms { skip:3bar | skip:2 then side | }`
  has the arms out on the last beat only, raised through the `then` (see "Tracks": the narrower wins).

## Poses and moves

```
pose guard { steps { left.fwd.low@small }  arms { fwd.25@bent } }
move kick  { rightLeg { fwd.low@bent fwd@sharp!hit fwd.low@bent down } }   // knee up, kick, knee, down
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
  `guard` again moves nothing. (A step written directly in `steps` always steps.)
- A pose takes `@mirror`, `@free`, `@smooth`, `@sharp`, `@linear`; a move takes `@mirror`
  and `@free`. Definitions take no arguments: a variation is the same word with the
  change written over it (a narrower track, or a later `&` row). A move may hold a
  `body` row of other moves, so a whole pass can be one word:
  `move pass { body { vine:4 vine@mirror:4 } }`.

## Gesture targets (arms, legs, torso, head)

(The `twist` and `hips` tracks have words of their own: see "The twist" and "The hips" below.)

A target is a direction, written as words joined by dots: any of the direction words, and
at most one level.

| Direction | Meaning |
|---|---|
| `fwd` | Forward: the way the figure faces. |
| `back` | Backward. |
| `side` | Away from the body's midline: left for a left limb, right for a right limb. |
| `across` | Toward and past the midline: the opposite of `side`. |
| `left` | To the figure's left. For the torso and the head; a limb says `side` or `across`. |
| `right` | To the figure's right. For the torso and the head. |

| Level | Meaning |
|---|---|
| `low` | 45 degrees below horizontal. For the feet: knees bent (the hips 16 cm lower; a deep crouch is `both.low@large`). |
| `mid` | Horizontal (the default). For the feet: normal standing height. |
| `high` | 45 degrees above horizontal. For the feet: on the toes. |

| Alone | Meaning |
|---|---|
| `up` | Straight up. |
| `down` | Down: an arm hanging at the side as an arm nobody wrote does (the hand by the thigh, a little forward), a free leg back on the floor. |

- Two direction words make a diagonal: `fwd.side`, `back.side.low`, `fwd.left.high`.
- **Degrees in brackets after the second direction say how far round from the first
  toward it**, 0 to 90, where the plain diagonal is 45: `head { fwd.left(30) }` looks 30°
  to the left of straight ahead, `rightArm { fwd.across(18) }` reaches a little across
  (with square shoulders the fist ends in front of the middle of the body: on the face of someone who stands
  straight ahead. A shoulder that has come round with `hips` or `twist` is already nearer the middle, so
  the number gets smaller — still about 18 with the shoulders 45° round, about 5 with them side-on, 90°;
  read the fist in `shosa clip`, and again after changing the turn), `fwd.side(70)` is
  nearly out to the side. `fwd.left(0)` is `fwd`, `fwd.left(90)` is `left`. A level goes after
  the brackets as after any direction: `fwd.left(30).10` (30° left, 10° up),
  `torso { fwd.left(20).70 }`, `fwd.across(15).high`. With `@mirror` the sides swap as
  they do without brackets. The brackets need two directions that are square to each
  other, and stand after the second: `left(30)` (from where?), `fwd.back(30)` and
  `fwd(30).left` are errors (SH1013).
- No level means `mid`. A level with no direction is not a target; use `up` or `down`.
- **A number in the place of the level is degrees above the horizontal**, from -90 to 90:
  `fwd.30`, `side.-30`, `back.70`. (`high` is 45, `low` is -45.) An arm swinging while
  walking is `fwd.-70` / `back.-70`: 20° from hanging.
- The number is always measured from the HORIZONTAL, also for the torso: `torso { fwd.60 }`
  leans 30° forward of upright (a bow), `fwd.80` is a nod of the whole body, `back.70`
  leans back 20°.
- **The torso leans with a straight back** (a bow folds at the hips). `@round` rounds it:
  `torso { fwd.80@round }` is a boxer's hunch, `up@round` a slouch. The direction still
  says where the chest is from the hips; the spine bows back from that line, so the
  chest, the shoulders and the head lean about 15° further forward than the word says
  (lift the face again with the head, by the lean and 15° more: `head { fwd.15 }` over `up@round`,
  `head { fwd.25 }` over `fwd.80@round`). The back always rounds
  forward, whichever way the torso leans; there is no word for arching it back.
- `up` and `down` stand alone: `fwd.up` is not a word — write `fwd.high`.
- **Arms and legs say `side` / `across`, never `left` / `right`** (error SH1011). That is
  what lets `arms { side }` be right for both arms at once.
- The torso and the head say `left` / `right`, never `side` / `across`.
- For an arm the direction is where the HAND is from the shoulder; for a leg, where the
  foot is from the hip; for the torso, where the spine points (`up` is upright,
  `fwd.high` leans 45° forward, `fwd` is horizontal); for the head, where the face looks
  (`fwd` straight ahead, `fwd.left` turned, `fwd.-25` a nod down).
- Arms, legs and torso are measured against the room: `down` is toward the floor even
  when the torso leans. The head's up and down is measured against the chest, so it follows a bow;
  its left and right is measured from where the feet point, so it does not follow a twist or the hips (see `twist` below).
  So in a forward fold `arms { down }` hangs toward the floor, and in a bow unwritten
  arms hang in front of the thighs.
- **An arm at `down` hangs as an arm nobody wrote does**: the elbow a little bent, the
  hand by the thigh and a little forward — not a straight line under the shoulder
  joint, which on a character is inside the hip. So `down` is how an arm is put back,
  and how a row says "hanging" in its first word (`arms { down:3 then side }`).
- **An arm follows the torso only when its event says `@body`.** Arms left at `up` stay
  vertical while the torso leans to the side. For a side bend with the arms overhead,
  write the lean and, over the same beats, the arms again with `@body`:
  `torso { left.high:2 up:2 }` with `arms { up:2@body up:2 }`.
- Bending: `@bent` brings the hand or foot closer along the same direction. A raised
  knee is `fwd@bent` (thigh about horizontal) or `fwd.low@bent` (lower); a guard with
  the fists up in front of the face is `fwd.25@bent`.
  **A hand travels from where it is to the target, so a straight punch is the guard's own
  direction without `@bent`**: from `fwd.25@bent`, `fwd.25@sharp` goes out along one line
  (to `fwd` it would come down like a chop). Both hands at a place near the middle of the
  body (`arms { chin }`, `arms { chest }`) bring the elbows across each other: for two arms
  side by side use a direction.
- **Never send a part to the exact opposite of where it is** (`down` straight to `up`,
  `fwd` straight to `back`): the way round is then unwritten (warning SH2001). Go through
  a target in between — `arms { fwd up }` raises them through the front, `arms { side up }`
  through the side.

An arm can also go to a place on the own body instead of a direction:

| Arm target | Meaning |
|---|---|
| `hip` | The hand on the hip of its own side. |
| `chest` | The hand in front of the chest. |
| `shoulder` | The hand on its own shoulder. |
| `head` | The hand beside the head. |
| `chin` | The hand in front of the chin: one hand in thought, one fist held close. (Both arms there cross at the elbows: a guard is `fwd.25@bent`.) |
| `belly` | The hand on the belly. |
| `across.shoulder` | The hand on the opposite shoulder. With `arms`: arms folded. |
| `clap` | The hand at the midline in front of the chest. With `arms`: the two hands meet. |

`arms { clap!clap }` is a clap on its beat (add `@sharp` for a crisp one); `arms { across.shoulder }` folds the arms.

An arm or the head can take a place on ANOTHER figure,
`FIGURE.PLACE`: `rightArm { b.hand:2 }` reaches for b's right hand, `head { b.head }`
looks at b.

| Place | Meaning |
|---|---|
| `hand` | The other figure's hand of the same side. If it reaches back, the two hands meet half-way, at the height of a handshake. |
| `shoulder` | The other figure's shoulder of the same side. |
| `head` | The other figure's head. |
| `chest` | The other figure's chest. |

The figure named must be danced by exactly one dancer (SH1021). The arm only reaches as
far as it is long (about 0.6 m from the shoulder): bring the two bodies within reach with
their `at` (where the dancer stands: the middle of the feet) and their steps. A hand
that does not get there is a warning that says how far short it is (SH2006).
Two facing dancers whose hands meet in front of them stand about a metre apart; for a
meeting above the heads or beside the hips (`.high`, `.low`) the arms slant, so about 0.8 m.
Hands of the same side (both left hands) are diagonal to each other between two who face,
which costs a little more: about 0.7 m for those `.low`. Two who touch low and also
bend their knees deep stand 0.8 m apart and lean in a little (`torso { fwd.70 }`):
at 0.7 m the bent knees meet.
Two bodies are in the same place (SH2007) when their trunks come within about 0.2 m of
each other: nearer than that in the picture is only one standing behind the other.
So are two whose legs go through each other, of the legs they stand on (two who face
at 0.7 m and both bend their knees deep); a leg in the air is not looked at, since a kick is meant to land.

Two hands that reach for each other can meet at another height, when BOTH say the same
level: `a { rightArm { b.hand.high } }` with `b { rightArm { a.hand.high } }` is a high
five above the heads; `.mid` meets at shoulder height, `.low` beside the hips.

### Legs

`leftLeg` / `rightLeg` are for a leg that carries NO weight. Make the gesture, then put
the leg back with `down`.

- From standing on both feet nothing else is needed: the weight goes over the other
  foot by itself as the leg lifts, and comes back as it is put down. `steps` can be
  left out, or `then`.
- After a step the weight is on the foot that stepped, so the OTHER leg is free
  (after `left.fwd`, the right leg).
- While either foot is in the middle of a step, or the other leg is also off the
  floor, a leg gesture is an error (SH2004). The leg lifts through the word BEFORE its
  gesture, so keep the steps out of that word: give the leg a `then` of the beat it
  may lift in (`rightLeg { skip:3 then fwd@bent down }` lifts in beat 3 only).
- A leg put `down` lands where it stood before the gesture.

## The twist: `twist`

The torso's word says where the spine points; **`twist` turns the chest about the spine**,
on hips that stay where the feet put them. It is a row of its own because it is another
axis and moves at other times: a guard keeps its lean through every punch.

| Twist | Meaning |
|---|---|
| `left` | The chest turns to the figure's left, the right shoulder comes forward: 30 degrees, or `left.N` for N degrees (up to 45). |
| `right` | The chest turns to the figure's right, the left shoulder comes forward: 30 degrees, or `right.N`. |
| `center` | No twist: the shoulders square over the hips. |

```shosa
twist { center:2 left:2 center:2 right.20:2 }   // square, 30° left on beat 2, square on 4, 20° right on 6
```

- `left` is the way `turn.left` goes: the chest faces left, the RIGHT shoulder comes
  forward. A right-handed punch that lands is `left`; drawing that fist back is `right`.
- The number is degrees of turn (0 to 45), not a level; `left` alone is 30, and it may
  have a decimal point (`left.7.5`). More than 45 is an error (SH1013): to face another
  way the feet turn (`steps { turn.left.quarter }`).
- What turns: the shoulders, and so the arms that hang on them. A hand comes 19 cm
  further forward between `right` and `left` (30° each way), on the same line: **an arm's direction is
  still measured against the room** (`fwd` is where the figure faces, twisted or not).
  An arm that says `@body` goes round with the chest: `arms { side@body }` over a twist
  swings the arms round like a turnstile.
- What does not turn: the hips, the legs, and **the head. A twist turns the shoulders
  under the head**, so the face keeps looking where the `head` row says (ahead, if there
  is none) — a fighter's eyes stay on the target. To look over the shoulder, turn the
  head as well: `head { fwd.left }` is 45° from where the feet point, `head { left }`
  90°, and `head { fwd.left(60) }` any number between (a little past a chest twisted 45°).
- It goes like a gesture: through all of the word before, `@sharp` for the snap of a
  punch, `then` to stay. A twist nobody wrote is `center`.
- In a pose it is the stance's own twist (`twist { right.20 }`: the left shoulder
  ahead), and a move that twists says where it comes back to with the pose's name in
  its `twist` row — it does not need to know how the torso leans (`guard@mirror` in a
  `twist` row is the mirrored stance's twist, `left.20`):

```shosa
pose guard { arms { fwd.25@bent }  torso { fwd.80@round }  twist { right.20 }  head { fwd.25 } }
move cross {
  rightArm { fwd.low@bent fwd.25@sharp!hit guard }   // elbow back, fist out, back to guard
  twist    { right.35     left@sharp          guard }   // shoulder back, round with the fist, back
}
```

- Two rows of an `&` that both twist at the same time: the later row has the chest
  (as with any part), and the earlier one is warned that its twist is not seen
  (SH2009) — two punches look apart, but they share the one chest. Give the twist to
  one of them, or write them one after the other. What counts is what is lost: an earlier
  row that only stands in a pose's twist (`guard` above) loses nothing and is not warned;
  an earlier row's whip is lost, and warned, whatever the later row has there — a pose's
  name too (a move that begins `loose:1 left@sharp …` holds the chest for that first beat).
  A `skip` there leaves the chest to the other row. The same for the hips; one warning
  names both when a move loses both.
- Under a lean the turn is about the leaning spine, so measured on the floor it is a
  little less than its number (10° of lean: 38° for 40), and the head, which sits on
  the spine, shifts a few centimetres sideways though the face keeps its direction.
- On the contact sheet a twist is read under each pose, where the figure is drawn from
  above: the shoulder line (left half blue, right half red) lies across the grey line
  of the hips when twisted, and hides it when square (15° shows). In the pose itself it
  shows only from the side (`--view side`: the two shoulders come apart); from the front
  the shoulder line only gets shorter. For the number, read the two shoulders in
  `shosa clip` (below).

## The hips: `hips`

**`hips` turns the hips over feet that stay where they stand** — the hip a punch comes
from, the wind-up of a throw, a twist dance. It is written exactly as a twist is, one
storey down:

| Hips | Meaning |
|---|---|
| `left` | The hips turn to the figure's left, the right hip comes forward: 30 degrees, or `left.N` for N degrees (up to 45). The chest and the shoulders go round with them. |
| `right` | The hips turn to the figure's right, the left hip comes forward: 30 degrees, or `right.N`. |
| `center` | The hips square over the feet. |

```shosa
hips  { center:2 left:2 center:2 right.20:2 }   // square, 30° left on beat 2, square on 4, 20° right on 6
```

- `left` is the way `turn.left` and `twist { left }` go: the hips face left, the RIGHT
  hip comes forward. The number is degrees (0 to 45; `left` alone is 30); more is an
  error (SH1013), because the feet do not move: to face another way they turn (`steps { turn.left.quarter }`).
- **The chest stands on the hips and goes round with them.** `hips { left.30 }` alone
  turns the hips, the chest and the shoulders 30°. A `twist` is on top of that:
  `hips { left.30 }` with `twist { left.30 }` puts the shoulders 60° from the feet (a
  punch from the hip; with 45 and 45 the shoulders are side-on), and with
  `twist { right.30 }` the shoulders stay square over hips that swing (a twist dance:
  `hips { left right }` over `twist { right left }`, the same beats).
- What does not turn: the feet (they stay planted; the knees give), **the head** (as
  under a twist, the face looks where the `head` row says), and what an arm or a leg
  says (`fwd` is still where the feet point). An `@body` arm goes with the chest; a
  hand on the `hip` or the `belly` goes with the hips.
- Everything else is as for a twist: it goes through all of the word before, `@sharp`
  snaps, `then` stays; nobody wrote it means `center`; in a pose it is the stance's
  own (`hips { right.10 }`), and a move comes back to it with the pose's name in its
  `hips` row (so the pose needs a `hips` row, as it needs a `twist` row for `twist`);
  two rows of an `&` that both turn the hips at the same time are warned (SH2009).

```shosa
pose guard { arms { fwd.25@bent }  torso { fwd.80@round }  hips { right.10 }  twist { right.10 }  head { fwd.25 } }
move cross {
  rightArm { fwd.low@bent fwd.25@sharp!hit guard }   // elbow back, fist out, back to guard
  hips     { right.20     left.15@sharp       guard }   // the hip back, and through
  twist    { right.20     left.20@sharp       guard }   // the shoulder as far again on top
}
```

- On the contact sheet, from above: the grey line of the hips turns away from where
  the feet point, and the shoulder line with it (crossing it only if there is a twist too).
  The hips are narrower than the shoulders: the dark dot at each end of their line is what
  shows them when the shoulders are square over them (a dot above the shoulder line on one side, below it on the other).
  `shosa check` still says which way the dancer faces by its feet: hips turned at the
  end are not "turned 30° to its left".

## The feet: `steps`

The `steps` track says where the weight is. Its events are:

**A step** — `FOOT.DIRECTION`, optionally with a level: `left.fwd`, `right.side`,
`left.back.low`. The foot LANDS on the word's beat and carries the weight from then on.

| Foot | Meaning |
|---|---|
| `left` | The left foot steps and takes the weight. |
| `right` | The right foot steps and takes the weight. |
| `both` | No foot moves: only the height changes (`both.low`, `both.high`). |

| Step direction | Meaning |
|---|---|
| `fwd` | One stride ahead of the supporting foot. |
| `back` | One stride behind the supporting foot. |
| `side` | Apart from the supporting foot, to the stepping foot's own side. |
| `across` | Crossing in front of the supporting foot. |
| `place` | Beside the supporting foot: a step on the spot. |

- **A step is placed from the OTHER foot, not from where the stepping foot was.**
  `left.fwd` puts the left foot one stride (0.5 m) ahead of the right foot. So
  `left.fwd right.fwd left.fwd` is a walk, and `left.fwd right.side` puts the right foot
  level with the left one and apart from it: "forward, side", as a dancer counts it.
- So **a foot already ahead does not advance by stepping "ahead" again**: from a stance
  with the left foot a small step in front, `left.fwd@small` puts it where it is. To
  move the stance forward, reach with the front foot and close up with the back one:
  `left.fwd right.back@small` (the right foot ends a small step behind the left).
- `side` is 0.55 m apart; standing width is 0.2 m.
  Directions combine: `left.fwd.side`. A step with `side` in it carries the body
  0.35 m that way (`@small` 0.6 of it) once the other foot has followed, and
  the next plain step lands at standing width beside it: walking with every left step
  `left.fwd.side` drifts 0.35 m to the left each time. There is no word for a
  sideways distance in metres. `back` is the same 0.5 m as `fwd`:
  `left.fwd together right.back together` goes out and comes home in one bar, and
  `left.fwd right.fwd left.back right.back together` does it in walking steps (the
  fourth step leaves the left foot a stride ahead; `together` brings it home).
- `across` puts the foot just in front of the other one (0.12 m), crossed over;
  `back.across` just behind it — the cross-behind of a grapevine. A cross is not a
  stride. Since the next step is placed from the crossed foot, a line of
  side / cross / side drifts by that 0.12 m; alternate front and behind to cancel it.
- A level on a step is the height you arrive at: `low` knees bent (the hips 16 cm lower:
  a fighter's stance, a plié — not a squat), `high` on the toes
  (only these words: degrees are not for the feet).
  **A step with no level arrives at normal height**, so after `right.side.high` a plain
  `left.fwd` comes down again; to stay up, write `.high` on every step.
  `then` and a leg gesture keep the height they find: after a `.low` step the knees
  stay bent until a step without a level, or `both.mid`.
- `@small` and `@large` scale a step (0.6 and 1.4): how far it goes, not its height.
- `left.fwd@turn(left.quarter)` turns DURING the step: the standing foot swivels, and the
  step lands where it would after the turn. `@turn(SIDE.AMOUNT)`, amounts below.
- The same foot twice in a row is allowed (it moves again from the other foot).

**Other things the feet do:**

| Event | Meaning |
|---|---|
| `together` | The free foot closes to the supporting foot; the weight is on both. |
| `turn` | A pivot on the supporting foot: `turn.left.quarter`, `turn.right.half`. |
| `jump` | Off the floor, landing on both feet: `jump`, `jump.fwd`, `jump.apart`, `jump.together`. |
| `shift` | The weight moves and neither foot leaves the floor: `shift.left`, `shift.right`, `shift.center`. |
| `lift` | Only for a free body (`@free`, or `free` on the dancer): the feet hang at a height in metres, `lift(0.4)`. `lift.fwd(0.4)` also travels. |
| `land` | Only for a free body: back down to the floor. |

- `together` takes a level like a step: `together.high` closes on the toes. It moves the
  foot that does not carry the weight, so after a step or a `shift.left`. Where the weight
  is on both feet (after `jump.apart`, after `shift.center`) neither is the free one, and
  BOTH close, each half the way, to under the weight (to STAND with the feet apart and the weight
  between them from the start, `jump.apart` is the first word of `steps`: a first word is already made,
  so no jump is seen; `left.side` instead leaves the weight over the left foot): `jump.apart:1 then:1 together:2`
  slides the feet back in (`jump.together` jumps them in).
- `both.low`, `both.mid`, `both.high` — no foot moves; the height changes (a knee bend,
  a rise onto the toes). The weight stays where it was too. Here `@small` / `@large` scale the bend:
  `both.low@small` is a slight bend (8 cm), `both.low` bent knees (16 cm), `both.low@large`
  a deep crouch (40 cm), so a light bounce in a low stance is `both.low@small both.low`,
  and a duck or a squat is `both.low@large`. (On a step `@small` / `@large` are the
  stride: to step into a crouch, the step and then `both.low@large`.)
- `shift.left`, `shift.right`, `shift.center` — the weight moves over that foot (or
  between them); no foot leaves the floor, and the height stays as it is. The basis
  of standing and waiting.
- `turn.left.quarter`, `turn.right.half` — side, then amount:

| Turn amount | Meaning |
|---|---|
| `eighth` | 45 degrees. |
| `quarter` | 90 degrees. |
| `half` | 180 degrees. |
| `full` | 360 degrees. |

  A turn pivots on the foot that carries the weight: the one that took the last step or
  that a `shift` went to (on both, if the feet are together). A free leg may gesture during a turn.
  After it "forward" is the new facing. `turn.b` turns to face
  figure b.
- `jump`, optionally with one or two of these words (`jump.fwd`, `jump.apart`):

| Jump word | Meaning |
|---|---|
| `place` | Land where you took off (the default). |
| `fwd` | Land 0.4 m ahead. |
| `back` | Land 0.3 m behind. |
| `left` | Land 0.4 m to the figure's left: a dodge is `jump.left@small`. |
| `right` | Land 0.4 m to the figure's right. |
| `apart` | Land with the feet 0.6 m apart. |
| `together` | Land with the feet at standing width. |

  A jump LANDS on its beat, on both feet; the bend and the flight are in the word before
  it, and the knees give a little after. How high it goes follows from the time that
  word gives it (the longer, up to a second, the higher; a `then` counts as that word, so
  `then:2 jump` is a high one and `then:1/2 jump` a hop); it is not written.
  A quarter of a second gives 2 cm of air — the sheet shows bent knees and no flight — and
  half a second 7 cm: a leap that is to be SEEN in the air needs half a second or more before it. It lands at the
  height it took off from (low stays low), and a plain `jump` keeps the feet as far apart as they were.
  `jump@turn(right.half)` turns in the air. Jumps in a row, each on its beat: `jump jump jump`.
  `jump.left` / `jump.right` go to the figure's own left and right (a sidestep or a
  dodge: `jump.right@small`; a dancer `facing stageLeft` has upstage on its left, so two who
  face each other and both write `jump.left` go to opposite walls); with `fwd` or `back` they go diagonally (`jump.back.left`).
- `lift(0.4)` — ONLY for a free body (`@free` on the event, or `free` on the dancer line;
  otherwise SH1022): the feet hang 0.4 m above the floor until told otherwise.
  `lift.fwd(0.4)` / `lift.back(0.4)` / `lift.left(0.4)` / `lift.right(0.4)` also travel.
  `land` comes back to the floor.
  A double jump: `steps { then:0.3s lift(0.4):0.3s@free lift(0.9):0.3s@free land:0.4s@free }`
  (0.4 m up at 0.3 s, 0.9 m at 0.6 s, down at 0.9 s).
- `then` — nothing new: the feet stay, and the next word's foot sets out in its second half.

## Annotations

| Annotation | Meaning |
|---|---|
| `@smooth` | The way TO this word eases in and out (the default). |
| `@sharp` | Go at the last moment: the part waits, then gets there in no more than 0.15 s before its beat. An accent — a punch, a clap. Fast, so it can be too fast (SH2003). |
| `@linear` | The way TO this word is at constant speed: no slowing as it arrives. |
| `@bent` | Fold the elbow or the knee: the hand or foot stays in direction but comes closer. |
| `@body` | For an arm: measure the direction against the torso instead of the room, so the arm leans when the torso leans. |
| `@round` | For the torso: the back is rounded. The direction still says where the chest is from the hips; the spine bows back from that line, and the chest, the shoulders and the head lean further forward (`torso { fwd.80@round }` is a fighter's guard, `up@round` a slouch). Without it the back is straight, as in a bow from the hips. |
| `@small` | A shorter step or jump. On `both.low` / `together.low`: a slighter knee bend. |
| `@large` | A longer step or jump. On `both.low` / `together.low`: a deeper crouch. |
| `@mirror` | Left and right swapped: `kick@mirror` kicks with the other leg. |
| `@free` | The rules of weight, gravity and speed do not apply to this event. Bones still keep their length. |
| `@turn` | For a step or a jump: turn while doing it, `left.fwd@turn(left.quarter)`, `jump@turn(right.half)`. The step lands where it would after the turn. |

`@bent` is for arm and leg directions; `@body` for arm directions; `@round` for torso
directions; `@small` / `@large`
for steps, jumps and `both` / `together`; `@turn(…)` for steps and jumps. Several can
follow one event, and the length, the annotations and the mark may come in any order:
`fwd:1@bent@sharp`, `kick@mirror:2`.

## Figures and dancers

A `figure` owns choreography; a `dancer` is a body that performs it. A file for one body
has one of each (`figure solo`, `dancer solo humanoid on solo`). Several bodies:

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

form { Verse }
score { dancer alice  dancer bob  dancer carol }
```

- EVERY track sits inside a figure's block (`lead { steps { … } }`); a track written
  directly in a section is an error (SH1003).
- `dancer NAME humanoid on FIGURE`, then any of, on the same line:
  `at X Z` (metres; x is stage left — the dancers' left, so the RIGHT of the sheet,
  which looks from the audience; z toward the audience), `facing front | back |
  stageLeft | stageRight` (or degrees; positive turns to the dancer's own left),
  `mirror` (left and right swapped), `delay N` (starts N beats late: a canon),
  `free` (weight, gravity and speed limits never apply), `in SECTION …` (dances only there;
  in the other sections the dancer is still on the floor, standing as they were left).
- Not written, a dancer stands at the origin (`at 0 0`) and faces the audience (`facing front`).
- `delay N` makes the score N beats longer: the others stand still while the late dancer
  finishes. `mirror` mirrors turns as well, so after a quarter turn a mirrored dancer faces
  the opposite way to the others.
- Several dancers can dance one figure. A dancer appears in the scores that list it
  (`score solo { dancer bea }`: bea alone).
- Two bodies face to face, 1 m apart:
  `dancer ann humanoid on a at -0.5 0 facing stageLeft` and
  `dancer ben humanoid on b at 0.5 0 facing stageRight`.
  `at` is where the feet stand together: a stance with a foot forward (`left.fwd.low@small`)
  brings each of them nearer, and at 1 m the front legs of two such fighters meet (SH2007).
  Start those 1.1 m apart or more.

## Dancing to a song

`music "song.lys"` names a song written in Lily#: a path from this file's folder (`"song.lys"` beside it, `"../song.lys"` one folder up). The dance
then takes its tempo, its time and the order of its sections from the song: **the song's
own form is the form, so this is the one kind of file that writes none.**

```
music "song.lys"                  // or: music "song.lys" score karaoke  (one of the song's scores)

figure solo
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
- A score danced to a song may `loop` (a practice cut that goes round) and be `inplace`,
  like any other.
- Do not write `music` unless you were given the song: without its file the line is an
  error (`SH1901`).

## What the language cannot say (yet)

Do not try to express these; mention them as a limitation instead.

- Hands and fingers, the face, the shoulders on their own (a shrug).
- Where the shoulders are from the feet as one number (it is `hips` plus `twist`), "as far as it goes"
  (write the limit, 45), a place on the other dancer's face (`b.chin`), the way an elbow travels in a hook.
- Holding an object. Contact other than a hand reaching a place.
- Walking along a curve. Running (both feet off the floor between steps); hopping on one leg.
- Sitting, kneeling, lying down, or anything else where a part other than the feet
  carries weight. Standing on one leg is possible (a leg gesture).
- Arm swing while walking is not added for you: unwritten arms hang still.
- A change of tempo or of time in the middle of a section (a section says them where it begins; a dance to a song follows its song).
- Where an elbow or a knee points.

## Checking your work

```
shosa check FILE            diagnostics, or "no problems"; every score with its length, and each section in it with its beats, bars, seconds, tempo and time, a step or jump the score starts with already made, and where each dancer ends from where it began; then every mark
shosa check FILE --score Rage   one score only (`--score ""` the unnamed one)
shosa sheet FILE -o X.png   the movement on one image per score (X.png for the unnamed score, X-Idle.png, X-Punch.png …)
shosa sheet FILE -o X.png --score Punch --step 0.5 --from 0 --to 4   one score, closer poses (all three in beats; --step 0.1 for a punch)
shosa sheet FILE -o X.png --view side   seen from stage left: a figure facing front is in profile (one facing stageLeft or stageRight is in profile WITHOUT it)
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
A looping score says `loops on the spot` or `travels 1 m forward each time round`.
A score danced to a song is headed `Patrol to song.lys: …`. The marks come last, each with
its score in front (`score !clap at beat 1 = 0.556 s` for the unnamed one), and with
`   Attack, play 2` behind when the score plays that section more than once, and with
the dancer behind (`score !hit at beat 1 = 0.5 s (a)`) when the score has more than one.
The line of a first step already made is longer than quoted above (``guard starts with
`left.fwd` already made: the first step is not seen (right for a stance, …)``); a foot
that a pose places says nothing. A section not written in bars is counted in whole
bars all the same (5 beats in 4/4 read `2 bars`).

When the sheet cannot settle it — which shoulder is ahead, whether the head moved, how
high a fist is — read the numbers. `shosa clip` writes `fps` (30), `joints` (their names
in order: `pelvis`, `chest`, `head`, `nose`, `leftShoulder`, `rightHand`, `leftAnkle` …)
and, for each of `figures`, `frames`: one row for every frame, x y z for every joint in
that order, in metres. The frame of a moment is its seconds × `fps` (0.75 s is frame 22.5: a `@sharp` word
is still on its way at frame 22 and there at 23, so take the later one). y is up, z is
toward the audience, x toward stage left — for a dancer facing front, z is forward and
+x its own left. (Standing upright, this figure's head has its middle at 1.72 m and its shoulders
are at 1.45 m. In the guard of "The twist" the head is at 1.67 m: a punch at `fwd.25`
arrives at 1.64 m, nose height, one at `fwd.18` at 1.57 m, the chin.)

Diagnostics are `FILE(line,column): severity CODE: message`. SH0xxx is syntax, SH1xxx is
a word or a count that does not make sense, SH2xxx is a movement the body cannot do
(SH2002 a step longer than the leg, SH2003 too fast, SH2004 both feet off the floor,
SH2005 a loop that does not join, SH2006 a hand that cannot reach the other dancer,
SH2007 two dancers whose heads, bodies or standing legs are in the same place,
SH2009 two rows of an `&` that twist the chest, or turn the hips, at the same time). The message says what to change.

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
**Under each pose the figure is drawn again from above**, audience at the bottom (so a figure facing front
has its nose down and its left, blue, on the right): the grey line is the hips, the blue and red line over it the
shoulders, the ring the head with its nose. Read there which way the figure faces, how far the chest is
twisted on the hips (the two lines cross; in a lean they lie one ahead of the other, so compare their slopes), where the face looks, and a lean (the shoulders ahead of the hips).
A dark dot ends the hips' line on each side. Which way, and which shoulder or hip is ahead, reads at any angle; the degrees read to about ±10, and a face's up and down not at all (`--view side`).
With more than one dancer each has its own place in that row, left to right as in the pose, with its name under it (and at its ring on the floor);
how far apart they stand is on the floor plan, not there.
**Left limbs are blue, right limbs are red**; the torso and shoulder line are dark. The camera stands
in front and a little to stage left, so a figure facing front shows its left on the right
of the picture; the short line on the head is the nose. From there a punch or a kick
straight ahead is foreshortened: judge those, bows and nods with `--view side`.
Under the poses is the floor from above,
audience at the bottom: the grey line is the path of the hips, the blue and red marks are
footprints, the ring is where the figure started. Each pose is drawn in the middle of its
own place in the row, so **travel is seen only on the floor**, not from pose to pose. Always look at the sheet: a file with
no diagnostics can still be the wrong movement (a mirrored arm, a step the wrong way).

## Rules and gotchas

- Never leave out `figure`, `dancer`, `form` or `score`; every track is inside a figure's block.
- A word is AT its target ON its beat, and stays its length. A step written on beat 1 lands on beat 1.
- The way there is in the word BEFORE: `arms { down:4 side }` takes four beats to get there.
  To stay and then go, a `then` before the next word: `down:3 then side`.
- A score starts IN its first words: `body { guard jab }` starts in guard, the jab lands on beat 1. There is no `:0`.
- A definition without a length takes its defined length; write the length when counting.
- Limbs: `side` / `across`. Torso and head: `left` / `right`.
- `torso` leans, `twist` turns the shoulders on the hips, `hips` turns the hips (and all above them) over the feet (`left.20`: degrees of turn). None turns the feet or the head.
- Degrees round, between two directions, are in brackets: `fwd.left(30)`. A number after a dot is the level.
- Degrees are from the horizontal, for the torso too.
- A step is measured from the other foot.
- Opposite targets need one in between.
- A leg gesture ends with `down`, and never overlaps a step.
- A step with no level comes back to normal height.
- Arms do not lean with the torso unless the event says `@body`.
- `body` takes only your poses and moves, `then` and `skip`.
- Count every bar and write the `|`. An event across a bar line is tied: `kick:2~ | :1`.
- Again: `{ … } q` for a few events, `%` for a bar, `|: A :|` in the form for sections.
