# Choreographing in Shosa — a guide for LLMs

Read this (the tool `shosa_guide`, or `shosa guide`) before you choreograph a dance — to a song
or not — longer than a few bars. The grammar (`shosa_grammar`, `shosa grammar`) says how each
thing is written; this says what makes it a dance, how to get a long one written in good time,
and ends with Shosa's vocabulary files: poses and moves of a style, ready to use by name.

**These are a choreographer's habits, not a form to fill.** Break one where the dance is better
for it, and say which in the plan. Make a dance you would enjoy watching.

## Answer each reply within five minutes, thinking included

A long think can be cut off by the connection and the whole reply is lost: the user waits and gets
nothing. Think about this step only and leave the next step to its own reply. Never write a whole
song's dance in one reply or one edit: the plan, then the head of the file, then a section (or two
short ones) a step.

## Steps

1. **Read**: the grammar, unless it is in this conversation; the song (`shosa_song`); this guide.
2. **The plan**, in your reply, short lines under the headings below. It is your working notes.
3. **The head of the file**: `music`, the `using "shosa:…"` lines of the styles you chose, the
   role and dancer lines (keep what Add a Dance… wrote), and the few moves of your own the plan
   names (the chorus's combination, the verse's groove). Check it.
4. **A section a step**: write it with `shosa_edit`, fix until it says no problems, and look at THAT
   section's sheet only (`shosa_sheet` with `from` and `to` its beats): a whole song's sheet is slow
   and too small to see. Say in a line what it does, then go on to the next.
5. At the end: the whole file checked once more, and tell the user to watch it in the preview.

## The plan

- **STYLE** — the dancer (who, what energy) and the vocabulary files: an idol or a cute pop song
  `idol` for the gestures on the words and the poses (a peace sign at the eye, a heart, a wink, a wave, a
  call) over the feet of `basics` (with a little `jazz`); "like Michael Jackson", 80s pop, funk, sharp and cool `pop` (with
  `basics`); Broadway, theatre, lyrical, sassy `jazz`. Mixing is fine: a dance is one dancer, not one file.
  **What Shosa dances well is choreography of poses hit on the beat** — an idol's dance, gestures on the
  words, a group in unison and in mirror. `ballet` and `break` are EXPERIMENTAL: a pirouette, a backspin
  are momentum, and Shosa has none. `ballet` reads as a beginner at the barre (its slow positions and
  steps are fine for something classical or elegant; keep to those, with flowing arms: `quality { through }`).
  `break` has the shapes and not the movement: only when breaking is asked for by name, and say then that
  it is rough; for hip-hop take `pop` and `basics`.
- **MAP** — every section the song plays: its bars, an energy from 1 to 5, and its ROLE: intro,
  verse, pre-chorus, chorus, bridge, instrumental, outro. **Sections with other names that are the
  same music are one role** (a transcribed song may call them A, A2 … A8 and C, C2, C3): find them
  by their bars and words, and dance each role from one move (below).
- **MOTIFS** — the chorus's combination (four or eight bars, the dance's hook, the same every
  chorus, so the audience knows it when it comes back), the verse's groove, the bridge's change.
  By name: the vocabulary's moves, and your own made of them.
- **FLOOR** — where the dancer goes: the verse near the spot, the chorus travelling (side, forward
  and back, a turn), the bridge to another place or facing (a diagonal, a profile). The levels:
  low (a plié, `height { low }`), standing, high (a rise, a jump).
- **ACCENTS** — the big moments: the held syllables (`~`), the first syllable after a rest (`/`), the
  song's hits, the last chorus as the high point, the final pose.
- **ENDING** — the last pose, on the song's last note, held.

## A long song in good time

- **A role is a move**: write the chorus's eight bars once as `move chorus { body { … } }` (rows for
  the steps, the arms, the head), and every chorus is `body { chorus }` — one line. A chorus that
  should differ the third time is the move with a row written over it: `body { chorus }` and
  `arms { … }` beside it. So a 3-minute song is four or five moves and a dozen short sections, not
  a hundred bars written out.
- Use the vocabulary's names instead of writing poses and moves: one word for a step pattern,
  another for the arms over it.
- `%` repeats a bar; `{ … } q q q` repeats a group; `verse 2 { … }` is what a section played again
  does differently (see the grammar's "Doing it again").

## Variety — what keeps a dance from being one pattern

- **The hands are a layer too** (`hands`, `leftHand`, `rightHand`): a fist on a hit, `spread` fingers
  on an open pose (`spread.fingers(up).palm(fwd)`: jazz hands), `point` with a pointing arm, `peace` at
  the face, `count(3)` on a counted lyric, `open.palm(up)` offered to the audience, `soft` for ballet.
  One row for the whole dance (`hands { fist }`) already reads better than none; change it on accents.
- **The face is a layer too** (`face`, for a dancer with a character): one row for the whole dance
  (`face { smile(50) }`) already reads better than a blank face. Then accents: a wink on a pose to the
  audience (`smile(50).winkRight:1/2`, the look again after it), `surprised` on a "eh?!", the full
  `smile` on the last pose, `sad(40)` or `relaxed.closed` in a quiet bridge. A few in a song, not every
  bar. The eyes blink by themselves.
- **Formations** (two dancers or more): where everybody stands is a layer of its own, and a change of
  it is what an audience sees first. Write the pictures once (`formation line { … }`,
  `formation vee { … }`: each dancer `at X Z`) and walk into them with one word of `steps`, the same
  for every dancer of a role: `then:4 | go.vee:4 |` walks through the `then` and stands in the vee on
  the next bar's first beat. Change the picture where a section changes, or for a chorus (the lead
  forward, the others back); `go.home` goes back to where they began. Give a change its time: four
  beats for a place up to 2 m away, eight for one across the stage (`check` says SH2026 when it is
  too far for the time). Dancers whose ways cross go round each other by themselves. A pose ON the
  arrival beat (the arms' word on the same beat as the `go`) makes the new picture land.
- **Hand in hand** (two dancers or more, side by side, 0.6 to 1 m apart): `arms { neighbor.hand:2 then:6 }`
  takes the hands of whoever stands on each side, beside the hips, and holds them while the line
  sways or walks; `neighbor.hand.high` lifts the joined hands (the end of a last chorus, a curtain
  call with a bow: `torso { up.fwd(40) }` under it). The ends of the line hold their free hand the
  same way. One arm (`leftArm { neighbor.hand }`) leaves the other for a wave or a mic. A moment
  for a bridge, a last chorus, the bow — let go again (`down`) before the next figure.
- **Layers**: a dance is feet × arms × head × height × facing. Change one layer every two bars,
  two every four, all of them where a section changes. The same steps under new arms is a new
  figure; the same arms on new steps too. Most vocabulary moves are feet only or arms only so that
  they combine: say both.
- **No feet pattern more than two bars running** but a groove under arms that change; no section
  with the same feet AND arms as the one before.
- **Repeat at the phrase, never at the beat**: a chorus that comes back the same is good (it is
  recognised); a two-beat figure for eight bars is not.
- **Contrast**: sharp against smooth (`@sharp`, `@smooth`), big against small, travelling against
  still. A held pose on a held note is one of the strongest things a dance does — stillness is a
  move.
- **Directions**: the front most of the time, but the diagonals and a profile for a phrase; a turn
  at most every few bars, not every bar.
- **Count in eights** (two bars of 4/4): change on 1 of a phrase, accent on 1 and the "and" before it.

## Dancing to the music

- **Tempo**: above about 140 a step every two beats and the arms on the beats; below about 90 big,
  slow, sustained moves (a `quality { through }` row: the words passed without stopping; the long lengths), the steps fewer.
- **Energy follows the MAP**: low in the verse (small steps, arms near the body), rising in the
  pre-chorus, the biggest in the chorus (travelling, arms overhead, turns, a jump), and the last
  chorus bigger still.
- **Words**: the mouth follows the song by itself (`sings PART`). A gesture that shows a word now and
  then reads well — a point on "you", a hand to the chest on "I" or "heart", arms up on "sky" — but
  not on every line.
- **Bars without words** (the intro, an instrumental) are for the steps and the travelling.

## What reads as good dancing

- The face to the audience; the head following the hand that leads (`head { … }` towards it).
- Clear changes of weight: a step lands, then the next one goes.
- Each phrase ending on a shape held for a moment, not drifting into the next.
- The shapes big enough to see from the back of a hall: arms away from the body, a lunge that is a
  lunge.

## The vocabulary files

`using "shosa:NAME"` at the top of a dance brings in one of these: its poses and moves are then
words of the dance, as if written in it. Several may be used together (no name is in two of them).
A move of these goes after any other: each starts and ends standing on both feet, facing where it
began, unless its comment says otherwise. Write a length to fit a move to the music (`jazzSquare:8`
is it at half speed), `@mirror` for the other side, and a narrower track to take only part of one
(`arms { … }` of a whole-body move). Their comments say each one's beats and tracks.

What trips a dance written with them (each met while they were made):
- **A move longer than the bar crosses its bar line tied**: an 8-beat move in 4/4 is
  `kickSpinPoint:4~ | :4 |`, and the tie comes before an annotation (`kickSpinPoint:4~@mirror | :4 |`).
  Or leave the bar lines out of that row.
- **Feet patterns go in `body`**: `steps { stepTouch }` keeps only its steps and drops its height;
  a leg move (`march`) is not steps at all. `body { stepTouch }` with `arms { … }` beside it.
- **`@sharp` goes in 0.15 s**, so a shape hit `@sharp` from far away is too fast (SH2003). Hit a pose
  from a shape near it, or give the way a short `then`: `skyPoint:7/2 then:1/2` arrives on the beat.
- **A shape held needs `then`**: an arm or the height sets off for the next word as soon as it
  arrives, so `skyPoint:4 down` drifts down for four beats; `skyPoint:1 then:3 down` holds it.
- A pose that writes `height`, laid over a feet move in the same `body` row, wins over the move's
  height: put the pose's arms in an `arms` row instead (`arms { skyPoint:4 }`).

### `using "shosa:ballet"`

```shosa
// shosa:ballet — classical ballet, turned out, on straight legs. A dance brings it in with: using "shosa:ballet"
// EXPERIMENTAL: it reads as a beginner at the barre — the positions and the slow steps are there, the turns and jumps have no momentum. Good for an adage; say that it is rough when it is asked for. A dancer's corrections to this file are welcome.
// The feet positions (first … fifth) set the turnout (80) and the straight legs, and every move after keeps them:
// begin a ballet dance with one of them. The arm positions and portDeBras / preparation are arms only; the
// barre moves (plie … grandBattement) are a height or a leg only; the jumps and glissade / pasDeBourree / balance
// are steps (and height) only: put any arms over any of them. arabesque, attitude, pirouette, chaine and reverence
// are whole-body (an arms row beside them wins over their arms). The working leg is the right one; @mirror for the left.
// Hands: `hands { soft }` once is a ballet hand for the whole dance. A lifted foot is pointed with `pointed` in a leg's row of
// your own (`rightLeg { back.up(10).pointed }`), and `height { pointe }` stands on the tips of the toes (relevePointe).
// Not in Shosa (yet), so not here: spotting in a turn, a cabriole and the beats that land on one foot, rond de jambe en l'air,
// the arm's curve of a port de bras (it goes point to point).

// ---- Positions of the feet: poses, turned out 80 on straight legs, the weight on both feet.

// first — heels together, the feet in a line. The start of a barre, of a plié series, of a révérence.
pose first  { turnout { out(80) }  steps { together }  height { straight } }
// second — the feet apart (0.4 m), heels in line. For grand plié, an échappé's landing, a wide accent.
pose second { turnout { out(80) }  steps { rightFoot.side(0.4m) }  height { straight } }
// third — the right heel against the middle of the left foot. Rare in the centre: a gentler fifth.
pose third  { turnout { out(80) }  steps { rightFoot.fwd(0.08m).across(0.06m) }  height { straight } }
// fourth — the right foot a step ahead and crossed, both turned out: the preparation of a pirouette (here the
// right in front; fourth@mirror has the left in front).
pose fourth { turnout { out(80) }  steps { rightFoot.fwd(0.3m).across(0.1m) }  height { straight } }
// fifth — the right heel at the left toes, crossed: ballet's home. Where jumps and small steps start and end.
// fifth@mirror has the left foot in front.
pose fifth  { turnout { out(80) }  steps { rightFoot.fwd(0.08m).across(0.13m) }  height { straight } }

// ---- Positions of the arms: poses, arms only, rounded with the elbows held out.

// brasBas — low, rounded in front of the thighs: the rest of the arms, at the start and end of everything.
pose brasBas    { arms { fwd.across(30).down(70).bent(40).elbow(side) } }
// enAvant — first position of the arms: rounded in front of the diaphragm. The arms of a turn, and the way up.
pose enAvant    { arms { fwd.across(15).down(25).bent(50).elbow(side) } }
// aLaSeconde — second position of the arms: open to the sides, a little forward and below the shoulders. Over
// tendus, jumps, travelling steps: the arms that hold still.
pose aLaSeconde { arms { side.fwd(20).down(15).bent(20) } }
// enHaut — fifth position of the arms: rounded above the head, a little forward. A pose to hit on a high note.
pose enHaut     { arms { fwd.up(70).bent(40).elbow(side) } }

// ---- Arm patterns: arms only, over any steps.

// preparation — 4 beats; arms. Bras bas, up to first, open to second: the arms before an exercise or a phrase,
// in the bar of introduction.
move preparation { arms { brasBas:1 enAvant:2 aLaSeconde:1 } }
// portDeBras — 8 beats; arms. Bras bas, first, fifth en haut, open to second, down to bras bas: the arms
// through all four, for a held note or the bars without words (also over plie / grandPlie).
move portDeBras  { arms { brasBas:1 enAvant:2@through enHaut:2 aLaSeconde:2 brasBas:1 } }

// ---- Barre and centre: the height or the right leg only.

// plie — 4 beats; height. A demi-plié: down for two, up for two. In any position (in first and fifth the knees
// go out over the toes).
move plie      { height { straight:2 low(10cm):3/2 straight:1/2 } }
// grandPlie — 8 beats; height. Down for four, up for four, deep; best in second (in first and fifth the heels
// should rise, which Shosa cannot say). Slow music, the start of a class.
move grandPlie { height { straight:4 low(40cm):7/2 straight:1/2 } }
// releve — 4 beats; height. Up onto the toes (demi-pointe) on 1, held, down on 3. Stretched to 2 for a quick one.
move releve    { height { straight:1 high:1 then:1 straight:1 } }

// tenduFront — 2 beats; right leg. The foot slides out along the floor to the front, pointed, held, and
// back in. In fours, on the counts of a quiet phrase.
move tenduFront  { rightLeg { down:1/2 fwd.floor:1/2 then:1/2 down:1/2 } }
// tenduSide — 2 beats; right leg. As tenduFront, to the side (à la seconde).
move tenduSide   { rightLeg { down:1/2 side.floor:1/2 then:1/2 down:1/2 } }
// tenduBack — 2 beats; right leg. As tenduFront, behind.
move tenduBack   { rightLeg { down:1/2 back.floor:1/2 then:1/2 down:1/2 } }
// degageFront — 2 beats; right leg. Brushed out through the tendu a little off the floor, and back in: a
// tendu with an accent, quicker music.
move degageFront { rightLeg { down:1/4 fwd.floor:1/4@through fwd.down(65):1/2 fwd.floor:1/2 down:1/2 } }
// degageSide — 2 beats; right leg. As degageFront, to the side.
move degageSide  { rightLeg { down:1/4 side.floor:1/4@through side.down(65):1/2 side.floor:1/2 down:1/2 } }
// degageBack — 2 beats; right leg. As degageFront, behind.
move degageBack  { rightLeg { down:1/4 back.floor:1/4@through back.down(65):1/2 back.floor:1/2 down:1/2 } }
// rondDeJambe — 4 beats; right leg. Rond de jambe à terre en dehors: the toe draws a half circle on the floor,
// front, side, back, and closes through first. (En dedans, back to front, is not here.)
move rondDeJambe { rightLeg { down:1/2 fwd.floor:1/2 fwd.side.floor:1/2@through side.floor:1/2@through back.side.floor:1/2@through back.floor:1/2 down:1 } }
// passe — 2 beats; right leg. Retiré: the toe up to the front of the standing knee, the knee out, and down.
// On one leg for a beat: a balance, or with releve beside it.
move passe       { rightLeg { down:1/2 otherKnee:1 down:1/2 } }
// developpe — 4 beats; right leg. Développé à la seconde: up through retiré, the leg unfolds to the side at hip
// height, and comes down. Slow music, a held note.
move developpe   { rightLeg { down:1/2 otherKnee:1 side:3/2 down:1 } }
// grandBattement — 2 beats; right leg. Thrown high to the front (40° above level, up by beat 3/4), lowered with
// control by 1 1/2: a big accent. Not shorter than 2 beats at 120 (faster, the foot is too fast: SH2003).
move grandBattement { rightLeg { down:3/4 fwd.up(40):3/4 down:1/2 } }

// ---- Big poses: whole body (leg, arms, torso, head). On the left leg; @mirror on the right.

// arabesque — 4 beats; whole body. First arabesque: the right leg rises behind through the tendu to hip height,
// the body leans a little forward, the left arm reaches forward and the right out and back; held a beat, and
// down. Stretch it (arabesque:8) to hold it longer: the peak of a phrase, a held high note.
move arabesque {
  rightLeg { down:1/2 back.floor:1/2@through back:1 then:1 back.floor:1/2 down:1/2 }
  torso    { up:1/2 then:1/2 up.fwd(15):1 then:1 up:1 }
  leftArm  { brasBas:1/2 enAvant:1/2@through fwd.up(15):1 then:1 brasBas:1 }
  rightArm { brasBas:1/2 aLaSeconde:1/2@through side.back(30).down(5):1 then:1 brasBas:1 }
  head     { fwd:1 fwd.up(10):1 then:1 fwd:1 }
}
// attitude — 4 beats; whole body. Attitude derrière: the right leg up behind, the knee bent and out above the foot,
// the right arm en haut and the left in second; held a beat, and down. Stretch it to hold it.
move attitude {
  rightLeg { down:1/2 back.floor:1/2@through back.up(10).bent.knee(side.up):1 then:1 back.floor:1/2 down:1/2 }
  torso    { up:1 up.fwd(5):1 then:1 up:1 }
  rightArm { brasBas:1/2 enAvant:1/2@through enHaut:1 then:1 brasBas:1 }
  leftArm  { brasBas:1/2 enAvant:1/2@through aLaSeconde:1 then:1 brasBas:1 }
  head     { fwd:1 fwd.left(20).up(10):1 then:1 fwd:1 }
}

// ---- Turns: whole body.

// pirouette — 4 beats; steps, height, right leg, arms. A single pirouette en dehors from fourth: the right foot
// steps back into fourth in plié (beat 1), the weight goes forward, up onto the left toes with the right in passé
// and one turn to the right (beat 2 to 3), and the right foot closes behind in fifth. Ends facing as it began, a
// little forward. No spotting (the head turns with the body).
move pirouette {
  steps    { then:1 rightFoot.back(0.3m).across(0.1m):1 shift.left:1 turn.right(360):1/2 rightFoot.back(0.08m).across(0.13m):1/2 }
  height   { straight:1 low(10cm):1/2 then:1/2 high:1 then:1/2 straight:1/2 }
  rightLeg { skip:3/2 then:1/2 otherKnee:1 skip:1 }
  leftArm  { brasBas:1 enAvant:2 then:1/2 brasBas:1/2 }
  rightArm { brasBas:1 aLaSeconde:1/2 then:1/2 enAvant:1 then:1/2 brasBas:1/2 }
}
// chaine — 4 beats; steps, height, arms. Chaînés: two turns to the right on the toes in four half-turn steps,
// travelling right, the arms in first; down and the feet together at the end. In a row across the floor.
move chaine {
  steps  { then:1/2 rightFoot.side(0.08m):3/4@turn(right(180)) leftFoot.side(0.08m):3/4@turn(right(180)) rightFoot.side(0.08m):3/4@turn(right(180)) leftFoot.side(0.08m):3/4@turn(right(180)) together:1/2 }
  height { straight:1/2 high:5/2 then:1/2 straight:1/2 }
  arms   { aLaSeconde:1/2 enAvant:5/2 then:1/2 aLaSeconde:1/2 }
}

// ---- Jumps: steps only (the jump makes its own plié). Arms over them: aLaSeconde, enAvant, brasBas.

// saute — 2 beats; steps. A jump in place, landing as it stood (in fifth, in first, in second). On a beat of
// light music, in fours.
move saute      { steps { then:1 jump.up(10cm):1 } }
// changement — 2 beats; steps. From fifth: a jump that lands in fifth with the other foot in front.
move changement { steps { then:1 jump.change.up(10cm):1 } }
// echappe — 4 beats; steps. Échappé sauté: from fifth, a jump out to second (beat 1), and a jump in again
// (beat 3), closing into fifth with the left foot in front (echappe@mirror: the right).
move echappe    { steps { then:1 jump.apart(0.5m).up(8cm):1 then:1 jump.leftFoot.fwd(0.08m).across(0.13m).up(8cm):1 } }
// entrechat — 2 beats; steps. Entrechat quatre from fifth: a jump whose legs beat in the air (they close the other
// way round and open again) and land as they stood. Small and brilliant: in a row on quick music.
move entrechat  { steps { then:1 jump.beat.up(12cm):1 } }
// royale — 2 beats; steps. From fifth: a jump that beats as it stands and lands with the other foot in front (a
// changement with a beat). After entrechat, or instead of changement on an accent.
move royale     { steps { then:1 jump.change.beat.up(12cm):1 } }
// relevePointe — 4 beats; height. Up onto the tips of the toes on 1, held, down on 3: the relevé of pointe work.
move relevePointe { height { then:1/2 pointe:1/2 then:3/2 then:1/2 straight:1 } }
// assemble — 4 beats; steps, left leg. Assemblé from fifth with the right foot in front: the back (left) leg brushes
// out to the side, the jump brings the feet together in the air and lands in fifth with the left in front (beat 2).
// assemble@mirror from fifth@mirror.
move assemble {
  steps   { then:2 jump.change.up(12cm):2 }
  leftLeg { skip:1/2 then:1/2 side.down(45):1/2 skip:5/2 }
}

// ---- Travelling steps: steps and height.

// glissade — 2 beats; steps, height. A gliding step to the right: from plié the right foot slides out, the weight
// goes over it, and the left closes in front in fifth (beat 1). Before a jump, or in pairs along a phrase.
move glissade {
  steps  { then:1/2 rightFoot.side(0.45m):1/2 leftFoot.fwd(0.08m).across(0.13m):1 }
  height { straight:1/4 low(8cm):1/4 straight:1/2 low(8cm):1/2 straight:1/2 }
}
// pasDeBourree — 2 beats; steps, height. Pas de bourrée dessous: on the toes, the right foot behind, the left to
// the side, the right in front in fifth with a plié. Moves a little left; a link between two bigger steps.
move pasDeBourree {
  steps  { then:1/2 rightFoot.back(0.08m).across(0.1m):1/2 leftFoot.side(0.3m):1/2 rightFoot.fwd(0.08m).across(0.13m):1/2 }
  height { straight:1/4 low(6cm):1/4 high:1/2 then:1/2 low(6cm):1/4 straight:1/4 }
}
// balance — 3 beats IN 3/4 (a waltz bar); steps, height. Balancé: down onto the right foot to the side, up onto
// the left behind it, down on the right. Ends with the weight on the right foot: the next is balance@mirror (to
// the left), or a shift.center before anything else.
move balance {
  steps  { rightFoot.side(0.4m) leftFoot.back(0.08m).across(0.05m) rightFoot.fwd(0.08m) }
  height { low(5cm) high straight }
}

// ---- The end.

// reverence — 8 beats; whole body. The bow: a step to the right, the left foot pointed behind, a plié with the
// head and the body bowed and the arms from second down to bras bas, and the feet together. To end on.
move reverence {
  steps   { then:1 rightFoot.side(0.4m):1 then:5 together:1 }
  leftLeg { skip:1 then:1 back.floor:3 then:1 skip:2 }
  height  { straight:2 then:2 low(10cm):1 then:2 straight:1 }
  torso   { up:2 then:2 up.fwd(20):1 then:2 up:1 }
  head    { fwd:2 then:2 fwd.down(30):1 then:2 fwd:1 }
  arms    { brasBas:1 aLaSeconde:1 then:2 brasBas:4 }
}
```

### `using "shosa:basics"`

```shosa
// shosa:basics — the everyday steps, arm patterns and poses of a dance to a pop or idol song. A dance brings it in with: using "shosa:basics"
// Feet patterns (steps, height, legs, a little torso) and arm patterns (arms, sometimes the head) are apart: put any arms over any
// feet, the feet in `body` and the arms in `arms` (narrower, so it wins): `body { stepTouch:4 }` with `arms { handClap:2 handClap:2 }`.
// (Not `steps { stepTouch }`: that keeps only the steps, and march/kick/hopKnee are legs.) Poses and combos are whole-body.
// A height row that begins with a dip sinks to it through the word before: after a long `then`, slowly. Hit a pose `@sharp` only
// from a shape near it (from hanging arms to highV is too fast, SH2003).
// Hands are a row of their own over any of these: `hands { fist }`, `rightHand { peace.fingers(up).palm(fwd) }`, `hands { spread }`. Kneeling and the floor are in shosa:break.
// Not in Shosa (yet), so not here: a shoulder shrug, a heart made of two fingers.

// ---- Feet: steps, height, legs. They leave the arms and the head to another row. ----

// stepTouch — 4 beats; steps, height. Step right and close, step left and close, a small dip on each step: the step for a verse,
// under any arm pattern. Ends where it began. @mirror starts to the left.
move stepTouch {
  steps  { rightFoot.side(0.45m) together leftFoot.side(0.45m) together }
  height { { low(6cm):1/2 mid:1/2 } q q q }
}

// sideTogether — 4 beats; steps, height. Side, together, side, together to the right: travels 0.7 m right. Follow it with
// sideTogether@mirror to come back: the travelling step for bars without words.
move sideTogether {
  steps  { rightFoot.side(0.45m) together rightFoot.side(0.45m) together }
  height { { low(6cm):1/2 mid:1/2 } q q q }
}

// stepBallChange — 2 beats; steps, height. A small step forward on the right on the beat, the weight back onto the left in
// place on the "and" of the next beat, and the right closes: a quick link before a turn or a new direction. Ends where it began.
// @mirror for the left.
move stepBallChange {
  steps  { rightFoot.fwd(0.2m):1 leftFoot.back(0.2m):1/2 stand:1/2 }
  height { low(6cm):1 mid:1/2 low(6cm):1/2 }
}

// march — 4 beats; legs. Marching on the spot, the knees up: right, left, right, each foot down on the beat (1, 2, 3), still
// on 4. A beat to keep while the arms do the work (a chant, a call-and-response chorus). @mirror starts on the left.
move march {
  rightLeg { skip:1/4 then:1/4 fwd.down(55).bent(70):1/2 down:1 then:1/2 fwd.down(55).bent(70):1/2 down:1 }
  leftLeg  { skip:1 then:1/2 fwd.down(55).bent(70):1/2 down:2 }
}

// bounce — 4 beats; height only. Down on every beat, up on every "and": the groove under a held pose, a clap or a sway.
// Stretch it to 8 for a long intro; it works under any `steps` row.
move bounce {
  height { { low(7cm):1/2 mid:1/2 } q q q }
}

// sway — 4 beats; steps, torso. The weight over the right, the left, the right, and back to the middle, the chest leaning a
// little against the hips: for a slow song, a ballad's verse, a held note. @mirror starts to the left.
move sway {
  steps { shift.right shift.left shift.right shift.center }
  torso { up.left(4) up.right(4) up.left(4) up }
}

// hopKnee — 2 beats; steps, a leg. Onto the right foot, hop on it on beat 1 with the left knee up, the left foot down beside
// it on the "and": a bright accent at the end of a line. Ends on the right foot (the left free): hopKnee@mirror next shifts back.
move hopKnee {
  steps   { then:1/2 shift.right:1/2 hop:1 }
  leftLeg { skip:1/2 then:1/2 fwd.down(50).bent(90):1/2 down:1/2 }
}

// kick — 2 beats; a leg. The right knee up on the "and", a low kick forward on beat 1, the foot back down on 2: an accent on
// a hit in the music. @mirror kicks the left.
move kick {
  rightLeg { down:1/2 fwd.down(40).bent(70):1/2 fwd.down(20):1/2@sharp down:1/2 }
}

// vine — 4 beats; steps, height. Grapevine to the right: side, cross behind, side, close. Travels about 0.8 m right;
// vine@mirror comes back. For the bars without words, or the whole group crossing the stage.
move vine {
  steps  { rightFoot.side(0.45m) leftFoot.back(0.03m).across rightFoot.side(0.45m) together }
  height { { low(6cm):1/2 mid:1/2 } q q q }
}

// quarterTurn — 2 beats; steps. A quarter turn to the right on both feet, facing the new way on beat 1 and standing there:
// to face a partner or the side of the stage. Stretch it (:4) for a slow turn. @mirror turns left. Ends turned 90° right.
move quarterTurn {
  steps { together:1 turn.right(90):1 }
}

// halfTurn — 4 beats; steps. Step forward on the right, pivot half round to the left on beat 2, close: a pivot turn to face
// the back, or (twice) round to the front again. Ends turned 180° left. @mirror steps on the left and turns right.
move halfTurn {
  steps { rightFoot.fwd(0.3m):1 turn.left(180):1 stand:2 }
}

// ---- Arms: arms, leftArm, rightArm, sometimes the head. They leave the feet to another row. ----

// handClap — 2 beats; arms. Hands open low on the beat, clap on the next: two of them in a bar clap on 2 and 4 (the
// backbeat). Over stepTouch or march, the chorus's easiest arms.
move handClap {
  arms { side.down(40):1 clap:1@sharp!clap }
}

// pointOut — 4 beats; arms, head. The right hand drawn to the chest, then pointing out to the audience on beat 1 and held,
// the left hand on the hip, the eyes after the point: on "you" in a lyric, the first word after a rest. @mirror points with the left.
move pointOut {
  rightArm { chest:1 fwd.side(25).up(15):2@sharp then:1 }
  leftArm  { hip:4 }
  head     { fwd:1 fwd.right(15):3@sharp }
}

// waveOverhead — 4 beats; arms. Both arms up and swaying over the head, to the left and the right, one beat each, as a
// concert crowd does: for a chorus, a held note, the last chorus. @mirror sways right first.
move waveOverhead {
  leftArm  { up.side(25) up.across(25) up.side(25) up.across(25) }
  rightArm { up.across(25) up.side(25) up.across(25) up.side(25) }
}

// helloWave — 2 beats; arms. The right hand up by the head, waving side to side once a beat, the left hand on the hip: hello,
// goodbye, the intro and the outro. @mirror waves the left.
move helloWave {
  rightArm { { up.bent(30):1/2 side.up(60).bent(30):1/2 } q }
  leftArm  { hip:2 }
}

// armRoll — 2 beats; arms. The forearms rolling round each other in front of the chest, one roll a beat: for a rising
// build before the chorus, or as "and round" in a lyric.
move armRoll {
  rightArm { fwd.across(25).up(10).bent(110).elbow(side):1/2@through fwd.across(25).down(20).bent(110).elbow(side):1/2@through fwd.across(25).up(10).bent(110).elbow(side):1/2@through fwd.across(25).down(20).bent(110).elbow(side):1/2 }
  leftArm  { fwd.across(25).down(20).bent(110).elbow(side):1/2@through fwd.across(25).up(10).bent(110).elbow(side):1/2@through fwd.across(25).down(20).bent(110).elbow(side):1/2@through fwd.across(25).up(10).bent(110).elbow(side):1/2 }
}

// reachPull — 2 beats; the right arm. The right hand reaches out in front on the beat, and pulls back sharp to the ribs on the
// next, the elbow behind: "come here", a grab on an accent. @mirror reaches with the left.
move reachPull {
  rightArm { fwd.side(15).up(10):1 fwd.down(60).bent(120):1@sharp }
}

// ---- Poses: a shape to hit (on its beat; `@sharp` for a snap) and hold, at the end of a phrase. Whole-body unless said. ----

// highV — pose; arms, head. Both arms up and out in a V, the chin up: the top of a build, the first beat of a chorus, a jump.
pose highV {
  arms { side.up(50) }
  head { fwd.up(10) }
}

// handsOnHips — pose; arms. Both hands on the hips: a sassy rest between moves, the last word of a cheeky line.
pose handsOnHips {
  arms { hip }
}

// pointUp — pose; arms, head. The right hand up and out to the sky, the left on the hip, the eyes after it: a held high
// note. @mirror with the left hand up.
pose pointUp {
  rightArm { side.up(60) }
  leftArm  { hip }
  head     { fwd.right(15).up(15) }
}

// bigHeart — pose; arms, head. The arms curved over the head, the hands meeting above it: a big heart, the idol's "I love you",
// a held last syllable. The head tilts a little.
pose bigHeart {
  arms { up.across(20).bent(80).elbow(side) }
  head { fwd.tilt(left(10)) }
}

// bigFinish — pose; steps, arms, torso, head. Feet wide, arms open high, the chest lifted, the chin up: the final pose of the
// song or of a big section. Stands in a stance 0.5 m wide.
pose bigFinish {
  steps { rightFoot.side(0.5m) }
  arms  { side.up(30) }
  torso { up.back(5) }
  head  { fwd.up(15) }
}

// ---- Combos: feet and arms together. ----

// touchClap — 4 beats; steps, height, arms. stepTouch with a clap on each close: the chorus step every idol group knows.
// @mirror starts to the left.
move touchClap {
  steps  { rightFoot.side(0.45m) together leftFoot.side(0.45m) together }
  height { { low(6cm):1/2 mid:1/2 } q q q }
  arms   { side.down(40) clap:1@sharp!clap side.down(40) clap:1@sharp!clap }
}

// kickBallChange — 2 beats; a leg, steps. A low kick of the right foot on the "and", the foot down on its ball on beat 1, the
// left closing to it on the next "and": a punchy end to a phrase, often before a turn. @mirror kicks with the left.
move kickBallChange {
  rightLeg { down:1/2 fwd.down(25):1/4@sharp skip:5/4 }
  steps    { then:1 rightFoot.place:1/2 stand:1/2 }
}

// stepPointV — 8 beats; steps, arms, head. Two steps out to the right with the right hand pointing out, the same to the left,
// then both arms into a V over a stepTouch: a whole 8-count for a chorus. Ends where it began, the arms in the V.
move stepPointV {
  body { pointOut:4 pointOut:4@mirror }
  steps { stepTouch:4 stepTouch:4 }
  arms  { skip:6 then:1 highV:1 }
}
```

### `using "shosa:break"`

```shosa
// shosa:break — breaking and floor work: toprock on the feet, getting down, footwork on the hands, freezes, spins on the head and the back, and getting up. A dance brings it in with: using "shosa:break"
// EXPERIMENTAL: the shapes are there, but danced it does not yet look like breaking (no momentum: a spin is turned, not thrown). Do not choose it for a dance unless breaking is asked for by name; a breaker's corrections to this file are welcome.
// Every move is the whole body (steps, torso, arms, legs, height): write it in `body`. Each starts standing on both feet and ends standing where it says,
// so any follows any; a set is toprock, a way down, footwork or a spin, a freeze on an accent, a way up. They take room: 1.5 m around the dancer.
// Hands for them: `hands { fist }` in toprock, `hands { spread }` on the floor. Slower is safer: stretch a move (`headspin:12`) before it warns of speed (SH2003).
// Not in Shosa (yet), so not here: weight on an elbow or a forearm (a baby freeze's elbow in the hip, an elbow freeze), a windmill's and a flare's
// travelling roll (windmillRoll turns on the spot), walking on the hands, threading a leg through an arm.

// ---- toprock: on the feet, before going down ----

// toprock — 4 beats; steps, arms, torso. The basic toprock: step forward across on 1, back on 2, the other side on 3 and 4, the arms opening as
// the foot comes back. The groove of a whole verse; @mirror starts with the left foot.
move toprock {
  steps { then:1/2 rightFoot.fwd(0.3m).across(0.1m):1/2 rightFoot.place:1 leftFoot.fwd(0.3m).across(0.1m):1 leftFoot.place:1/2 stand:1/2 }
  arms  { fwd.across(20).down(20).bent(80):1 side.down(30).bent(40):1 fwd.across(20).down(20).bent(80):1 side.down(30).bent(40):1/2 down:1/2 }
  torso { up.fwd(8):1 up.back(5):1 up.fwd(8):1 up:1 }
}

// sideRock — 4 beats; steps, arms, height. The side-to-side toprock: a wide step right with a bounce, together, left, together, the arms swinging
// across the body. Under a chorus, facing the crowd.
move sideRock {
  steps  { then:1/2 rightFoot.side(0.5m):1/2 leftFoot.place:1 leftFoot.side(0.5m):1 rightFoot.place:1/2 stand:1/2 }
  height { mid:1/2 low(8cm):1/2 mid:1/2 low(8cm):1/2 mid:1/2 low(8cm):1/2 mid:1/2 mid:1/2 }
  arms   { down:1/2 fwd.across(30).bent(90):1 side.bent(70):1 fwd.across(30).bent(90):1 down:1/2 }
}

// ---- down, on the floor, and up ----

// kneeDrop — 4 beats; whole body. Down onto the right knee on 1 (the left foot in front), a beat held, up on 3. An accent that goes down and comes
// straight back; @mirror the left knee.
move kneeDrop {
  steps { then:1 rightKnee:1 then:1 feet:1 }
  torso { up:1 up.fwd(10):1 then:1 up:1 }
  arms  { down:1 side.down(30):1 then:1 down:1 }
}

// floorSit — 8 beats; whole body. Sits down on the floor behind the feet, the knees up and the hands behind (beat 2), leans back with one leg
// kicked up (beat 4), and stands up again where it stood. A cool break in the middle of a set.
move floorSit {
  steps    { then:1 seat.back(0.3m):1 then:4 feet.fwd(0.3m):2 }
  torso    { up:2 back.up(35):2 then:1 up:1 up:2 }
  arms     { down:1 back.floor:1 then:3 down:1 down:2 }
  leftLeg  { down:1 fwd.floor.bent(90):1 then:3 down:1 down:2 }
  rightLeg { down:1 fwd.floor.bent(90):1 fwd.up(40):2 fwd.floor.bent(90):1 down:1 down:2 }
}

// floorSteps — 8 beats; whole body. Footwork: down onto the hands in a crouch, the legs stepping round in turn under the body (left out to the
// front and round, then the right), and up. The busy middle of a set; best at 100 to 120.
move floorSteps {
  steps    { then:1 hands.fwd(0.45m):1 then:4 feet.back(0.45m):2 }
  torso    { up.fwd(40):1 fwd.up(25):1 then:4 fwd.up(50):1 up:1 }
  height   { low(40cm):1 then:5 low(40cm):1 mid:1 }
  leftLeg  { down:1 back.side(40).floor.bent(70):1 fwd.side(60).floor.bent(40):1 side.floor.bent(80):1 back.side(40).floor.bent(70):1 then:1 down:1 down:1 }
  rightLeg { down:1 back.side(40).floor.bent(70):1 then:2 fwd.side(60).floor.bent(40):1 back.side(40).floor.bent(70):1 down:1 down:1 }
}

// pushUps — 8 beats; whole body. Down into a plank on the hands, two push-ups (down on 3 and 5), and back up to standing. Strength as a joke, or
// the way into footwork.
move pushUps {
  steps { then:1 hands.fwd(0.7m):1 then:4 feet.back(0.7m):2 }
  torso { up.fwd(60):1 fwd.up(15):1 then:4 fwd.up(60):1 up:1 }
  legs  { down:1 back.floor:1 then:3 down:1 down:2 }
  arms  { down:2 down.bent(90):1 down:1 down.bent(90):1 down:1 down:2 }
}

// ---- freezes: a shape held on an accent ----

// handstandFreeze — 8 beats; whole body. Kicks up to a handstand 0.6 m ahead (there on beat 3), the legs bent apart in the air, holds two beats and
// comes down where it stood. The end of a phrase; give the hold more with a longer word (handstandFreeze:12).
move handstandFreeze {
  steps { then:1 hands.fwd(0.6m):2 then:3 feet.back(0.6m):2 }
  torso { up.fwd(50):1 fwd.down(30):1 down:1 then:3 fwd.down(30):1 up:1 }
  legs  { down:2 side.up(55).bent(70):1 then:2 down:1 down:2 }
}

// oneHandFreeze — 8 beats; whole body. Up onto the hands, then onto the right hand alone, the body tipped over it and the left arm out (beats 4 to 5),
// back onto both and down. The harder freeze, for the biggest accent; @mirror the left hand.
move oneHandFreeze {
  steps   { then:1 hands.fwd(0.6m):2 rightHand:1 then:1 hands:1 feet.back(0.6m):2 }
  torso   { up.fwd(50):1 fwd.down(30):1 down:1 down.left(25):1 then:1 down:1 fwd.down(30):1 up:1 }
  legs    { down:2 side.up(50).bent(60):1 then:3 down:1 down:1 }
  leftArm { down:3 side.up(30):1 then:1 down:1 down:2 }
}

// headstandFreeze — 8 beats; whole body. Down onto the head and the hands (beat 3), the legs in a V, held, and down again. Steadier than the
// handstand and a way into headspin.
move headstandFreeze {
  steps { then:1 head.fwd(0.4m):2 then:3 feet.back(0.4m):2 }
  torso { up.fwd(60):1 fwd.down(40):1 down:1 then:3 fwd.down(40):1 up:1 }
  arms  { down:2 side.floor:1 then:3 down:1 down:1 }
  legs  { down:2 side.up(40):1 then:2 down:1 down:2 }
}

// ---- power: turning on what carries the weight ----

// headspin — 8 beats; whole body. Onto the head, two turns to the left with the legs in a V and the hands off the floor (beats 3 to 5), the hands
// down again and out. The peak of a set: once.
move headspin {
  steps { then:1 head.fwd(0.4m):2 turn.left(720):2 then:1 feet.back(0.4m):2 }
  torso { up.fwd(60):1 fwd.down(40):1 down:1 then:3 fwd.down(40):1 up:1 }
  arms  { down:2 side.floor:1 side.down(20):1 then:1 side.floor:1 down:1 down:1 }
  legs  { down:2 side.up(40):1 then:2 down:1 down:2 }
}

// backspin — 8 beats; whole body. Sits, rolls onto the upper back with the knees tucked (beat 3), spins twice (beats 3 to 5), sits up and stands.
// The classic spin, easier on the eye than the headspin: after footwork.
move backspin {
  steps { then:1 seat.back(0.3m):1 back.back(0.4m):1 turn.left(720):2 seat.fwd(0.4m):1 feet.fwd(0.3m):2 }
  torso { up:2 back.down(25):1 then:2 up:1 up:2 }
  legs  { down:1 fwd.floor.bent(90):1 fwd.up(20).bent(120):1 then:2 fwd.floor.bent(90):1 down:1 down:1 }
  arms  { down:1 back.floor:1 chest:1 then:2 back.floor:1 down:1 down:1 }
}

// windmillRoll — 8 beats; whole body. On the back, the legs wide: the body rolls over the shoulders onto its front and round onto its back again
// while it turns once (beats 3 to 6): a windmill on the spot. Then up. For the loudest bars; it needs the tempo under 120.
move windmillRoll {
  steps { then:1 seat.back(0.3m):1 back.back(0.4m):1 turn.left(360):3 seat.fwd(0.4m):1 feet.fwd(0.3m):1 }
  torso { up:2 back.down(15).facing(up):1 back.down(10).facing(left):1@through back.down(10).facing(down):1@through back.down(15).facing(up):1 up:1 up:1 }
  legs  { down:1 fwd.floor.bent(90):1 side.up(45):1 then:3 fwd.floor.bent(90):1 down:1 }
  arms  { down:1 back.floor:1 side.up(20):1 then:3 back.floor:1 down:1 }
}

// cartwheel — 8 beats; whole body. A cartwheel to the left: the hands down 0.6 m to the left, the body over them sideways with the legs apart
// (on the hands on beat 2, upside down on 3), and onto the feet 0.6 m further on (beat 5). Travels 1.2 m; @mirror goes right.
move cartwheel {
  steps { then:2 hands.left(0.6m):1 then:2 feet.left(0.6m):3 }
  torso { up:2 left:1@through down.facing(fwd):1@through right:1@through up:3 }
  legs  { down:2 side.up(30):1 then:1 then:1 down:3 }
}
```

### `using "shosa:idol"`

```shosa
// shosa:idol — the staple gestures of an idol song: a peace sign at the eye, a heart, a wink, cat paws, a blown kiss, waves and points to the audience, fist pumps and jumps. A dance brings it in with: using "shosa:idol"
// These are the words an audience reads at once: put one on a word of the lyric ("love", "you", "listen", a call), on the last beat
// of a phrase, on a held note. Between them dance the feet of shosa:basics (stepTouch, bounce, vine …): most of these are arms,
// hands, head and face only, and lie over any feet (`body { stepTouch:4 }` with `arms { armSwing:4 }`, or both in one `body` row with `&`).
// They write the hands and the face too, and those are kept until a word changes them: a `hands { relaxed }` or a `face { smile(50) }`
// after one lets it go. A pose that winks holds the wink as long as it is the face's word: over a long hold write the face beside it
// (`face { smile(60).winkLeft:1/2 smile(60) }`). The figure of sticks has no fingers and no face: look at these on a character.
// Not in Shosa (yet), so not here: a heart of two fingers, a cheek puffed out, the eyes looking another way than the head.

// ---- Poses: a shape to hit on its beat and hold. Arms, hands, head and face; only idolFinish places the feet. ----
// Hit one from a shape near it, or give the way a short `then` (`then:1/2 peaceEye:3/2`): `@sharp` from hanging arms is too fast (SH2003).

// peaceEye — pose; arms, hands, head, face. A peace sign beside the right eye, the palm to the front, the left hand on the hip, the head
// tilted to the hand, the other eye winking: THE idol pose, the last beat of a chorus line, a photo. @mirror for the left hand.
pose peaceEye {
  rightArm  { side.fwd(35).up(40).bent(125) }
  leftArm   { hip }
  rightHand { peace.fingers(up.across(25)).palm(fwd) }
  leftHand  { fist }
  head      { fwd.tilt(right(12)) }
  face      { smile(60).winkLeft }
}

// heartHands — pose; arms, hands, head, face. The two hands make a heart in front of the chest, the head tilted: on "love", "heart",
// "suki", the last word of a sweet line. heartBeat is the same heart, beating.
pose heartHands {
  arms  { chest }
  hands { heart }
  head  { fwd.tilt(left(10)) }
  face  { smile(60) }
}

// cheekPoint — pose; arms, hands, head, face. The right first finger at the cheek, the head tilted onto it, the left hand on the hip:
// "hmm?", "me?", a cute thinking face between two lines. @mirror for the left hand.
pose cheekPoint {
  rightArm  { side.fwd(30).up(15).bent(130) }
  leftArm   { hip }
  rightHand { point.fingers(up.across(30)) }
  leftHand  { fist }
  head      { fwd.tilt(right(15)) }
  face      { smile(50) }
}

// catPaws — pose; arms, hands, head, face. Both hands up beside the cheeks as a cat's paws, the head tilted: "nyan", a playful
// accent, a cute song's hook. cuteSway rocks from side to side in a shape near it.
pose catPaws {
  arms  { side.fwd(30).up(30).bent(125) }
  hands { claw.fingers(up).palm(fwd) }
  head  { fwd.tilt(left(12)) }
  face  { smile(60) }
}

// saluteWink — pose; arms, hands, head, face. A salute with the right hand at the forehead, the left on the hip, a wink:
// "roger!", "leave it to me", the first beat of a bright chorus. @mirror for the left hand.
pose saluteWink {
  rightArm  { forehead }
  leftArm   { hip }
  rightHand { open }
  leftHand  { fist }
  head      { fwd.tilt(right(8)) }
  face      { smile(50).winkLeft }
}

// wish — pose; arms, hands, head, face. The palms together in front of the chest, the head bowed and the eyes shut: a prayer,
// a wish, "please", the quiet bar of a ballad or a bridge.
pose wish {
  arms  { clap }
  hands { open.fingers(up).palm(across) }
  head  { fwd.down(20) }
  face  { relaxed.closed }
}

// earListen — pose; arms, hands, torso, head, face.The right hand cupped behind the ear, the body leaning that way, the face to the
// audience: "let me hear you", before a call the audience answers. @mirror for the left ear.
pose earListen {
  rightArm  { side.fwd(10).up(35).bent(140) }
  leftArm   { hip }
  rightHand { open.fingers(up).palm(fwd) }
  leftHand  { fist }
  torso     { up.right(10) }
  head      { fwd.left(15).tilt(right(10)) }
  face      { smile(50) }
}

// handOnHeart — pose; arms, hands, head, face. The right hand flat on the chest, the left arm hanging, the head a little down:
// "I", "my heart", a promise; the still moment of a verse. @mirror for the left hand.
pose handOnHeart {
  rightArm  { chest }
  leftArm   { down }
  rightHand { open.fingers(across.up(20)).palm(back) }
  leftHand  { relaxed }
  head      { fwd.down(10) }
  face      { smile(30) }
}

// callOut — pose; arms, hands, torso, head, face. Both hands round the mouth as a megaphone, the body leaning out to the audience:
// a shout ("se-no!", "everybody!"), the bar before the audience's call.
pose callOut {
  arms  { fwd.across(10).up(30).bent(128) }
  hands { open.fingers(up).palm(across) }
  torso { up.fwd(10) }
  head  { fwd.up(8) }
  face  { surprised(40) }
}

// fistUp — pose; arms, hands, head, face. The right fist straight up over the shoulder, the left on the hip, the chin up, a big smile:
// "yes!", "let's go", a win; the top of a build. @mirror for the left fist. fistPump punches it on the beats.
pose fistUp {
  rightArm  { side.fwd(20).up(70).bent(20) }
  leftArm   { hip }
  hands     { fist }
  head      { fwd.up(10) }
  face      { smile }
}

// idolFinish — pose; feet (a stance 0.35 m wide), arms, hands, hips, head, face. A peace sign thrown up and out to the right, the left
// hand on the hip, the hips turned, the chin up, the whole smile: the last pose of the song. @mirror for the left hand.
pose idolFinish {
  steps     { leftFoot.side(0.35m) }
  rightArm  { side.up(55) }
  leftArm   { hip }
  rightHand { peace.fingers(up).palm(fwd) }
  leftHand  { fist }
  hips      { left(12) }
  head      { fwd.right(10).up(10).tilt(right(8)) }
  face      { smile }
}

// ---- Feet: steps, height, turnout. They leave the arms to another row. ----

// bunnyHop — 4 beats; steps, arms, hands, head. The hands on the head as a rabbit's ears, two little jumps on beats 1 and 3, the head
// tilting each way: a cute hook, "pyon pyon". Ends with the feet together. Take the feet alone with `steps { bunnyHop }`.
move bunnyHop {
  steps { together:1 jump:1 then:1 jump:1 }
  arms  { side.fwd(15).up(58).bent(125):4 }
  hands { open.fingers(up).palm(fwd):4 }
  head  { fwd.tilt(left(10)):2 fwd.tilt(right(10)):2 }
}

// cuteBounce — 4 beats; height, turnout. A bounce on every beat with the knees turned in: the groove under a cute pose or cuteSway.
// The knees are parallel again at the end.
move cuteBounce {
  height  { { low(6cm):1/2 mid:1/2 } q q q }
  turnout { in(15):3 then:1/2 parallel:1/2 }
}

// starJump — 4 beats; steps, arms, hands, face. The fists drawn in, a jump that lands on beat 1 with the feet apart and the arms thrown
// up in a V, the fingers spread, held, and a jump back to standing on beat 3: the first beat of the last chorus, a "yay!".
move starJump {
  steps { stand:1 jump.apart:1 then:1 jump.stand:1 }
  arms  { fwd.down(40).bent(100):1/2 then:1/2 side.up(50):1 then:1 down:1 }
  hands { fist:1 spread:2 relaxed:1 }
  face  { smile(50):1 smile:2 smile(50):1 }
}

// ---- Arms, hands, head, face. They leave the feet to another row. ----

// blowKiss — 2 beats; rightArm, rightHand, head, face. The right hand at the lips with the eyes shut, then thrown out to the audience on
// beat 1, the palm up: "chu", the end of a sweet line, a goodbye. @mirror for the left hand.
move blowKiss {
  rightArm  { chin:1 fwd.side(15).up(10).bent(20):1 }
  rightHand { open.palm(back):1 open.palm(up):1 }
  head      { fwd.down(5):1 fwd.up(8):1 }
  face      { smile(40).closed:1 smile(60):1 }
}

// beckon — 2 beats; arms, hands. Both hands held out palms up and drawn in to the chest, once a beat: "come on", "one more time",
// calling the audience in before a chorus.
move beckon {
  arms  { { fwd.down(10).bent(30):1/2 fwd.down(15).bent(120):1/2 } q }
  hands { open.palm(up):2 }
}

// twoHandWave — 4 beats; arms, hands, head. Both open hands up at the shoulders, waving to the right and the left together once a beat,
// the head tilting with them: hello and goodbye to the whole hall, the intro, the outro. @mirror waves left first.
move twoHandWave {
  rightArm { { side.fwd(10).up(40).bent(90):1/2 side.fwd(35).up(35).bent(110):1/2 } q q q }
  leftArm  { { side.fwd(35).up(35).bent(110):1/2 side.fwd(10).up(40).bent(90):1/2 } q q q }
  hands    { spread.fingers(up).palm(fwd):4 }
  head     { fwd.tilt(right(8)) fwd.tilt(left(8)) fwd.tilt(right(8)) fwd.tilt(left(8)) }
}

// twinkle — 4 beats; arms, hands. The arms come down from a high V through the sides while the fingers open and close fast:
// sparkles falling, "kira kira", stars, a held note that fades. Ends with the arms low at the sides.
move twinkle {
  arms  { side.up(60):1 side.up(20):1@through side.down(30):1@through side.down(60):1 }
  hands { { spread:1/4 claw:1/4 } q q q q q q q }
}

// pointAround — 4 beats; rightArm, leftArm, hands, head. The right hand at the chest, then pointing to the audience's one side on
// beat 1, the middle on 2, the other side on 3, the eyes after the finger, the left hand on the hip: "you, and you, and you",
// eye contact with the whole hall. @mirror points with the left.
move pointAround {
  rightArm  { chest:1 fwd.across(30).up(10):1@sharp fwd.up(10):1@sharp fwd.side(40).up(10):1@sharp }
  leftArm   { hip:4 }
  rightHand { point:4 }
  leftHand  { fist:4 }
  head      { fwd:1 fwd.left(25):1@sharp fwd:1@sharp fwd.right(30):1@sharp }
}

// rainbowArc — 4 beats; rightArm, leftArm, rightHand, head. The right hand draws a big arc over the head from the left to the right,
// the eyes after it, the left hand on the hip: "rainbow", "sky", "the whole world", a long word. @mirror draws it the other way.
move rainbowArc {
  rightArm  { fwd.across(40).down(15):1 up.across(25):1@through up.side(25):1@through side.up(10):1 }
  leftArm   { hip:4 }
  rightHand { open:4 }
  head      { fwd.left(25).down(10):1 fwd.left(10).up(25):1@through fwd.right(15).up(25):1@through fwd.right(30):1 }
}

// clapUp — 2 beats; arms. The arms up and open on the beat, a clap over the head on the next: two in a bar clap on 2 and 4 with the
// audience, the last chorus, an instrumental the hall claps along to.
move clapUp {
  arms  { up.side(25).bent(30):1 up.across(13).bent(30):1@sharp!clap }
  hands { open:2 }
}

// cuteSway — 4 beats; arms, hands, shoulders, head, face. Both fists under the chin, the shoulders turning right and left and the head
// tilting against them, one beat each: a coy, sweet bar, the verse of a cute song. @mirror turns left first.
move cuteSway {
  arms      { fwd.across(12).up(20).bent(140):4 }
  hands     { fist:4 }
  shoulders { right(15) left(15) right(15) center }
  head      { fwd.tilt(left(12)) fwd.tilt(right(12)) fwd.tilt(left(12)) fwd }
  face      { smile(60):4 }
}

// armSwing — 4 beats; rightArm, leftArm, hands. Both bent arms swing together to the right, the left, the right, the left, fists
// closed, one beat each: the arms of the chorus's side step (over stepTouch they go the way the step goes). @mirror swings left first.
move armSwing {
  rightArm { fwd.side(50).down(10).bent(90) fwd.across(30).down(10).bent(90) fwd.side(50).down(10).bent(90) fwd.across(30).down(10).bent(90) }
  leftArm  { fwd.across(30).down(10).bent(90) fwd.side(50).down(10).bent(90) fwd.across(30).down(10).bent(90) fwd.side(50).down(10).bent(90) }
  hands    { fist:4 }
}

// fistPump — 2 beats; rightArm, leftArm, hands. The right fist punches up on each beat and comes back to the shoulder between, the
// left hand on the hip: "oi! oi!", "hey! hey!", a call with the audience. @mirror for the left fist.
move fistPump {
  rightArm { { side.fwd(30).up(70).bent(10):1/2@sharp side.fwd(30).up(40).bent(120):1/2 } q }
  leftArm  { hip:2 }
  hands    { fist:2 }
}

// heartBeat — 2 beats; arms, hands. The heart of two hands at the chest pushed out to the audience on each beat and drawn back:
// "doki doki", a heart that beats, "I love you" given away.
move heartBeat {
  arms  { { fwd.across(22).down(5).bent(60):1/2@sharp chest:1/2 } q }
  hands { heart:2 }
}

// idolBow — 4 beats; torso, arms, face. The hands together in front, a deep bow on beat 1 with the eyes shut, held, and up again on
// beat 3: "thank you", after the last pose, the end of an MC.
move idolBow {
  torso { up:1 up.fwd(40):1 then:1 up:1 }
  arms  { belly:4 }
  face  { smile(50):1 smile(40).closed:2 smile(50):1 }
}

// curtainCall — 8 beats; arms, torso, face. Hand in hand with whoever stands on each side (`neighbor.hand`): the hands taken beside
// the hips on beat 1, lifted above the heads on beat 3, a deep bow under them on beat 4, up again on beat 6 and the hands let go:
// the whole line's thank-you after the last song. For dancers side by side, 0.6 to 1 m apart; an end of the line (or a dancer
// alone) holds its free hand the same way.
move curtainCall {
  arms  { down:1 neighbor.hand:1 then:1 neighbor.hand.high:3 then:1 down:1 }
  torso { up:3 then:1 up.fwd(40):1 then:1 up:2 }
  face  { smile(60):4 smile(40).closed:2 smile(60):2 }
}

// ---- Combos: whole body. ----

// idolTurn — 4 beats; steps, arms, hands, head, face. The arms drawn in, a whole turn to the right on beat 1 to 2, and out of it
// into peaceEye, held: the end of a chorus. @mirror turns left, with the left hand's peace sign.
move idolTurn {
  steps { together:1 then:1 turn.right(360):2 }
  arms  { fwd.down(30).bent(100):2 skip:2 }
  body  { skip:2 peaceEye:2 }
}

// loveCombo — 8 beats; whole body. A side step right and left with armSwing, then standing: the heart beats twice and is held:
// a chorus line that ends on "love". @mirror starts to the left.
move loveCombo {
  steps  { rightFoot.side(0.4m) together leftFoot.side(0.4m) together then:4 }
  height { { low(6cm):1/2 mid:1/2 } q q q then:4 }
  body   { armSwing:4 heartBeat:2 heartHands:2 }
}

// callCombo — 8 beats; whole body. Two bars of a call: fistPump twice (four punches), then the starJump: the bars where the
// audience shouts along, the way into the last chorus. @mirror for the left fist.
move callCombo {
  body { fistPump:2 fistPump:2 starJump:4 }
}
```

### `using "shosa:jazz"`

```shosa
// shosa:jazz — theatre (Broadway) and lyrical jazz dance. A dance brings it in with: using "shosa:jazz"
// The steps (jazzSquare … hitchKick) are feet, height and legs only: put any of the arm shapes and
// arm moves (jazzSecond … punchUp) over them in an `arms` row. Isolations are one row each; the
// finishing poses and the combinations are whole-body. `ready` takes any pose back to neutral.
// A move's first step or kick lands ON the beat it is written at (it is made in the word before).
// Jazz hands are `hands { spread.fingers(up).palm(fwd) }` over any arms here; a kicking foot is pointed or flexed in a
// leg's row of your own (`rightLeg { fwd.up(20).pointed }`).
// Not in Shosa (yet), so not here: a hip pushed out to the side or a head slide (the hips and the head turn, they do
// not move sideways), a back arched back (it only leans), knee slides (floor work is in shosa:break).

// ---- Steps: feet, height and legs ----

// jazzSquare — 4 beats; steps. Right crosses over the left (1), left back (2), right side (3), left
// closes (4): a box on the floor, ends where it began. Verses and bars without words. @mirror.
move jazzSquare {
  steps { rightFoot.fwd(0.1m).across(0.1m) leftFoot.back(0.1m) rightFoot.side(0.5m) stand }
}

// jazzKickBallChange — 2 beats; rightLeg, steps. "1 & 2": a low kick to the front on the move's beat, the
// right foot down under the body on "&", the weight onto the left on 2 (and back between the feet).
// A pickup into a phrase, a fill. @mirror.
move jazzKickBallChange {
  rightLeg { fwd.down(30):1/4 skip:7/4 }
  steps    { skip:1/2 rightFoot.place:1/2 leftFoot.place:1/2 shift.center:1/2 }
}

// jazzPasDeBourree — 2 beats; steps. Jazz pas de bourrée, "1 & 2": the right crosses behind the left, the
// left steps to the side, the right steps beside it. Travels about 0.4 m left; the weight ends on the right. @mirror.
move jazzPasDeBourree {
  steps { rightFoot.back(0.05m).across(0.1m):1/2 leftFoot.side(0.3m):1/2 rightFoot.place:1 }
}

// chasse — 2 beats; steps. "1 & 2": right to the side, the left chases it closed, right to the side
// again: travels about 0.35 m to the right, the weight on the right. Follow it with chasse@mirror to come back.
move chasse {
  steps { rightFoot.side(0.35m):1/2 together:1/2 rightFoot.side(0.35m):1 }
}

// jazzWalk — 4 beats; steps, height, hips. Three low, deliberate walking steps forward, left first,
// each hip leading its leg, and the feet closed on 4: travels about 1 m forward. A strut on a groove.
move jazzWalk {
  steps  { leftFoot.fwd(0.45m) rightFoot.fwd(0.45m) leftFoot.fwd(0.45m) stand }
  height { low(10cm):3 mid }
  hips   { right(15) left(15) right(15) center }
}

// pivotTurn — 2 beats; steps. Step forward on the right (1), pivot half round to the left on both
// feet, up on the toes (2): ends facing BACK, about 0.1 m on. Twice to face front again. @mirror turns right.
move pivotTurn {
  steps  { rightFoot.fwd(0.4m):1 turn.left(180):1 }
  height { high:1 mid:1 }
}

// chaineTurns — 2 beats; steps, height. One chaîné turn to the right on the toes, in two half turns,
// travelling about 0.5 m right; ends facing front. Usually twice or more ({ chaineTurns } q q). @mirror.
move chaineTurns {
  steps  { leftFoot.side(0.25m)@turn(right(180)) rightFoot.side(0.25m)@turn(right(180)) }
  height { high:2 }
}

// piqueTurn — 2 beats; steps, height, leftLeg. Step out onto the right on the toes and turn once to the
// right on it, the left foot at the right knee (parallel passé); down on both on 2. A turn on a big accent. @mirror.
move piqueTurn {
  steps   { rightFoot.side(0.3m):1 turn.right(360):1 }
  height  { high:3/2 mid:1/2 }
  leftLeg { down:1/4 otherKnee.knee(fwd):3/4 then:1/2 down:1/2 }
}

// parallelPasse — 2 beats; rightLeg, height. The right foot drawn up to the left knee, legs parallel,
// rising onto the toe of the standing foot, and down. A held note, a balance. @mirror.
move parallelPasse {
  rightLeg { down:1/2 otherKnee.knee(fwd):1 down:1/2 }
  height   { mid:1/2 high:1 mid:1/2 }
}

// fanKick — 4 beats; rightLeg. The straight right leg sweeps an arc: crossed in front, up over the
// front, out to the side, and down. A showy accent on a fill. @mirror.
move fanKick {
  rightLeg { down:1 fwd.across(40).up(20):1/2 fwd.up(60):1/2@through side.up(20):1 down:1 }
}

// hitchKick — 4 beats; steps, legs. A scissor leap forward: the weight onto the left, the right knee
// comes up (2), the left kicks out level past it in the air (2.5), the right lands (3), the left closes
// beside it (3.5). Travels 0.6 m forward, the weight ends on the right. A climax, the end of a run. @mirror.
move hitchKick {
  steps    { then:1/2 shift.left:1/2 then:2 leap.fwd(0.6m):1 }
  rightLeg { skip:3/2 then:1/2 fwd.bent:1/2 then:1/2 down:1 }
  leftLeg  { skip:2 then:1/2 fwd:1/2 then:1/2 down:1/2 }
}

// ---- The torso and isolations: one row (or a few) each ----

// contraction — pose; torso, head, height. The torso rounded forward from the middle, the head down,
// the knees soft: hit it on a hard accent. Then release or ready.
pose contraction {
  torso  { up.fwd(15).round }
  head   { fwd.down(25) }
  height { low(8cm) }
}

// release — pose; torso, head, height. Out of the contraction: the chest lifted and leaning back,
// the face up. On the next accent or a long note. Then ready.
pose release {
  torso  { up.back(15) }
  head   { fwd.up(30) }
  height { mid }
}

// hipIsolation — 2 beats; hips. The hips snapped round right, then left, over still feet and square
// shoulders, and back. Over any steps or a still stance; @mirror starts left.
move hipIsolation {
  hips { center:1/2 right(25):1/2@sharp left(25):1/2@sharp center:1/2 }
}

// headIsolation — 4 beats; head. The face snapped to the right, front, left, front, one beat each:
// a dry accent on each beat, often over a still body. @mirror starts left.
move headIsolation {
  head { fwd.right(60):1@sharp fwd:1@sharp fwd.left(60):1@sharp fwd:1@sharp }
}

// ---- Arms: shapes to hit, and arm moves (an arms or leftArm/rightArm row) ----

// jazzSecond — pose; arms. Both arms straight out to the sides, a little forward, strong: the basic
// jazz arm, over a square, a walk, chassés.
pose jazzSecond { arms { side.fwd(10).up(5).bent(0) } }

// jazzV — pose; arms. Both arms up in a wide V: the end of a phrase, a jump, "ta-da".
pose jazzV { arms { side.up(55).bent(0) } }

// jazzDiagonal — pose; leftArm, rightArm. One long diagonal line, the right arm high, the left arm
// low: with a lunge or a hip-out pose. @mirror for the left arm high.
pose jazzDiagonal {
  rightArm { side.up(45).bent(0) }
  leftArm  { side.down(45).bent(0) }
}

// punchUp — 2 beats; rightArm. The right hand drawn in to the shoulder, punched straight up on beat 1
// (!hit), and down again. A hit in the music. @mirror for the left arm.
move punchUp {
  rightArm { down:1/2 shoulder:1/2 up:1/2@sharp!hit side.down(60):1/2 }
}

// ---- Finishing poses and whole-body shapes ----

// ready — pose; steps, height, torso, head, hips, shoulders, arms. Standing neutral: feet at standing
// width, upright, arms down. Takes any pose or isolation back to the start.
pose ready {
  steps     { stand }
  height    { mid }
  torso     { up }
  head      { fwd }
  hips      { center }
  shoulders { center }
  arms      { down }
  leftLeg   { down }
  rightLeg  { down }
}

// jazzLunge — 2 beats; steps, height, torso, head, arms. Step out to the right into a lunge, the
// right knee bent, the left leg long, arms in the diagonal, the face to the high hand: ENDS in the
// lunge, to hold to the end of the phrase; ready to come out. @mirror.
move jazzLunge {
  steps    { then:1/2 rightFoot.side(0.8m):1/2 shift.right:1 }
  height   { mid:1/2 then:1/2 low(15cm):1 }
  torso    { up:1 up:1 }
  rightArm { down:1/2 then:1/2 side.up(45).bent(0):1 }
  leftArm  { down:1/2 then:1/2 side.down(45).bent(0):1 }
  head     { fwd:1 fwd.right(30).up(20):1 }
}

// hipOut — 2 beats; steps, rightLeg, hips, arms, head. The weight onto the left, the right toe out in
// front, the hips turned, the left hand on the hip, the right arm up, the head tilted: a sassy
// finishing pose; ENDS in it (ready to come out). @mirror.
move hipOut {
  steps    { then:1 shift.left:1 }
  rightLeg { skip:1 fwd.side(30).floor:1 }
  hips     { center:1 left(20):1 }
  leftArm  { down:1 hip:1 }
  rightArm { down:1 side.up(60).bent(0):1 }
  head     { fwd:1 fwd.right(20).tilt(right(15)):1 }
}

// layout — 4 beats; steps, rightLeg, torso, arms, head. A back bend as far as Shosa allows (a straight lean back, no arch):
// the right leg kicks up to the front as the chest goes back, the arms up and the face to the ceiling,
// held a beat, and back to standing. A climax, the top of a phrase. @mirror.
move layout {
  steps    { shift.left:1 then:2 shift.center:1 }
  rightLeg { down:1 fwd.up(30):2 down:1 }
  torso    { up:1 up.back(25):2 up:1 }
  arms     { down:1/2 fwd:1/2 up.body:2 side.down(30):1/2 down:1/2 }
  head     { fwd:1 fwd.up(50):2 fwd:1 }
}

// ---- Combinations: whole bars ----

// jazzCombo — 8 beats; steps, legs, arms. Jazz square, kick-ball-change, pas de bourrée (left), arms
// in second through the square and up in a V on the pas de bourrée (they stay up: the next arms row takes them). Two bars of a verse, about 0.4 m to the right; @mirror.
move jazzCombo {
  body { jazzSquare:4 jazzKickBallChange:2 jazzPasDeBourree:2@mirror }
  arms { jazzSecond:4 then:2 jazzV:2 }
}

// turnCombo — 8 beats; whole body. Two chaîné turns right, a piqué turn, and the lunge to finish:
// a chorus's last two bars. Ends in the lunge, about 1.25 m to the right. @mirror.
move turnCombo {
  body { chaineTurns:2 chaineTurns:2 piqueTurn:2 jazzLunge:2 }
}
```

### `using "shosa:pop"`

```shosa
// shosa:pop — 80s pop dance: sharp hits that freeze, snapped kicks, fast spins into a freeze, toe rises, robot moves. A dance brings it in with: using "shosa:pop"
// Poses are upper-body shapes to hit on an accent and hold (arms, torso, head, hips, shoulders, height; only wideHit and toeStand place the feet):
// put them over any steps. Feet moves (steps, height, a leg) and upper-body moves (arms, head, shoulders, hips) are apart: lay any of one over any of the other.
// Not in Shosa (yet), so not here: the backward glide (a flat foot sliding back under the weight, the other heel up), a hip thrust to the side, a shrug, a hat in the hand, a lean past the toes, sliding feet. The pointing finger and the fist are a hand's row over these: `rightHand { point }`, `hands { fist }`.

// ---- poses: a shape to hit on an accent and hold ----
// To hit one from a shape far from it, give the way a short `then`: `skyPoint:7/2 then:1/2 | robotHit:4 |` arrives on the bar line.
// `@sharp` (`robotHit@sharp`) goes in 0.15 s: only from a near shape, or SH2003 says it is too fast.

// skyPoint — pose; arms, torso, head, hips, shoulders, height. The right arm straight up and out, pointing at the sky, the face after it,
// the left arm low: the end of a phrase, a held last note. @mirror for the left arm.
pose skyPoint {
  rightArm  { side.up(60).bent(0) }
  leftArm   { side.down(50).bent(0) }
  torso     { up.left(5) }
  head      { fwd.right(25).up(35) }
  hips      { left(10) }
  shoulders { right(10) }
  height    { mid }
}

// toeStand — pose; feet (together), height, turnout, arms, torso, head. Up on the toes, the feet together and the knees turned in,
// the arms straight down by the sides, the face level: the freeze after a spin or on a high held note.
pose toeStand {
  steps     { together }
  height    { high }
  turnout   { in(15) }
  arms      { down.bent(0) }
  torso     { up }
  head      { fwd }
  hips      { center }
  shoulders { center }
}

// leanBack — pose; arms, torso, head, height. The body leans back from the feet, the arms hanging straight, the face held level:
// a cool hold over a bar without words. Go into it slowly (`leanBack:2`) and come out with a sharp hit.
pose leanBack {
  arms      { down.bent(0) }
  torso     { up.back(20) }
  head      { fwd }
  hips      { center }
  shoulders { center }
  height    { straight }
}

// hatBrim — pose; arms, torso, head, height. The right hand at the brim of an (unseen) hat, the head down and a little to the right,
// the left arm low: the opening freeze before the music starts, or the last one. @mirror for the left hand.
pose hatBrim {
  rightArm  { forehead }
  leftArm   { side.down(20).bent(0) }
  torso     { up.fwd(5) }
  head      { fwd.right(15).down(30) }
  hips      { right(10) }
  shoulders { left(10) }
  height    { low(5cm) }
}

// robotHit — pose; arms, torso, head, height. Square robot arms: the right forearm up from an elbow out to the side, the left forearm
// across in front, the head turned sharp to the right: a robotic accent. @mirror for the other side.
pose robotHit {
  rightArm  { side.up(45).bent.elbow(side) }
  leftArm   { fwd.across(20).bent.elbow(side) }
  torso     { up }
  head      { fwd.right(60) }
  hips      { center }
  shoulders { center }
  height    { mid }
}

// wideHit — pose; feet (a wide stance), height, arms, torso, head. Feet wide, knees bent, both arms snapped down and out, the face down:
// the big accent at the end of a chorus line. It stands wide: a feet move after it (or `steps { stand }`) brings the feet in.
pose wideHit {
  steps     { leftFoot.side(0.6m) }
  height    { low(10cm) }
  arms      { side.down(35).bent(0) }
  torso     { up.fwd(10) }
  head      { fwd.down(30) }
  hips      { center }
  shoulders { center }
}

// ---- feet: steps, height and the legs (lay any arms over them) ----

// kickSnap — 2 beats; rightLeg. The knee comes up, the foot snaps out forward on beat 1 and straight back to the knee, then down:
// on a snare accent. @mirror for the left leg.
move kickSnap {
  rightLeg { down:1/2 fwd.down.bent(110):1/2 fwd.down(10):1/4@sharp!kick fwd.down.bent(110):1/4 down:1/2 }
}

// sideKick — 2 beats; rightLeg, torso. The knee up, the foot snaps out to the side on beat 1, the body leaning away, and back:
// an accent, often after kickSnap. @mirror for the left leg.
move sideKick {
  rightLeg { down:1/2 side.down(30).bent(110):1/2 side.down(5):1/4@sharp!kick side.down(30).bent(110):1/4 down:1/2 }
  torso    { up:1/2 up:1/2 up.left(15):1/4@sharp up:3/4 }
}

// toeRise — 4 beats; height, turnout. Snaps up onto the toes on beat 1 with the knees turned in, holds through beat 3, comes down:
// a held note. Put a pose's arms over it: `arms { skyPoint:4 }` beside `body { toeRise:4 }` (in the body row the pose's own height would win).
move toeRise {
  height  { mid:1 high:2@sharp then:1/2 mid:1/2 }
  turnout { parallel:1 in(15):2@sharp then:1/2 parallel:1/2 }
}

// spinFreeze — 4 beats; steps, height, arms. On beat 1 the arms pull in, a fast whole turn to the right on the toes ending on beat 2,
// the arms snapped out, frozen there on the toes, down by beat 4: the end of a phrase. @mirror turns left. `spinFreeze:2` is a quicker one.
move spinFreeze {
  steps  { together:1 then:1 turn.right(360):2 }
  height { mid:1 high:1 then:1 mid:1 }
  arms   { down:1 fwd.down(40).bent(80):1 side.down(30).bent(0):1@sharp then:1/2 down:1/2 }
}

// attitudeWalk — 4 beats; steps, shoulders, head. Three short strutting steps forward (left, right, left), the shoulders swung against them,
// the face front, and the feet closed on beat 3: the walk-on of an intro, a bar between hits. @mirror starts on the right. Travels 1.2 m forward.
move attitudeWalk {
  steps     { leftFoot.fwd(0.4m) rightFoot.fwd(0.4m) leftFoot.fwd(0.4m) stand }
  shoulders { right(15) left(15) right(15) center }
  head      { fwd:4 }
}

// crossStep — 4 beats; steps, height. The right foot crosses in front of the left, the left steps out, the right crosses behind, the feet close:
// travels to the left (0.5 m), a bouncy bar between hits. @mirror travels right.
move crossStep {
  steps  { rightFoot.across(0.13m) leftFoot.side(0.35m) rightFoot.back.across(0.13m) stand }
  height { { low(6cm):1/2 mid:1/2 } q q q }
}

// ---- upper body: arms, head, shoulders, hips (lay them over any feet) ----

// robotArms — 4 beats; rightArm, leftArm, head. Robot moves: each beat the arms go to a new square shape at one even speed and stop dead,
// the head clicking the other way: a bar of synth or drum machine. @mirror starts with the left.
move robotArms {
  rightArm { down:1 fwd.bent:1@linear side.up(45).bent.elbow(side):1@linear down:1@linear }
  leftArm  { down:1 then:1 fwd.bent:1@linear down:1@linear }
  head     { fwd:1 fwd.left(45):1@sharp fwd.right(45):1@sharp fwd:1@sharp }
}

// pointHits — 4 beats; rightArm, leftArm, head. The right arm draws in and snaps out straight to point: up and out on beat 1, down and
// across on beat 2, straight ahead on beat 3 (held), the eyes after the hand, the left hand on the hip: the accents of a chorus's words.
// @mirror for the left arm.
move pointHits {
  rightArm { down:1/2 side.up(40).bent(100):1/2 side.up(40).bent(0):1/2@sharp fwd.across(30).down(40).bent(100):1/2
             fwd.across(30).down(40).bent(0):1/2@sharp fwd.side(20).bent(100):1/2 fwd.side(20).bent(0):1@sharp }
  leftArm  { down:1/2 hip:7/2 }
  head     { fwd:1 fwd.right(30).up(30):1@sharp fwd.left(15).down(25):1@sharp fwd:1@sharp }
}

// shoulderPops — 4 beats; shoulders. The shoulders snap round, right, middle, left, middle, on every half beat: a bar of the hi-hat.
move shoulderPops {
  shoulders { center:1/2 right(25):1/2@sharp center:1/2@sharp left(25):1/2@sharp center:1/2@sharp right(25):1/2@sharp left(25):1/2@sharp center:1/2@sharp }
}

// hipPops — 4 beats; hips. The hips snap round to the right and to the left on each beat (Shosa turns the hips; it does not push them sideways):
// a groove in a bar without words.
move hipPops {
  hips { center:1/2 right(30):1/2@sharp center:1/2 left(30):1/2@sharp center:1/2 right(30):1/2@sharp left(30):1/2@sharp center:1/2 }
}

// headSnaps — 4 beats; head. The head snaps to look right, left, and front, each on a beat: a sharp look over a held pose. @mirror looks left first.
move headSnaps {
  head { fwd:1 fwd.right(80):1@sharp fwd.left(80):1@sharp fwd:1@sharp }
}

// ---- combinations: whole body ----

// kickSpinPoint — 8 beats; whole body. kickSnap, a spinFreeze to the right, then the skyPoint hit on beat 6 held to the end:
// a chorus's last line. @mirror for the other side.
move kickSpinPoint {
  body { kickSnap:2 spinFreeze:4 skyPoint:2 }
}

// strutPoint — 8 beats; whole body. attitudeWalk forward, then up on the toes (toeRise) with the pointHits over it: an intro, or the bars
// before a chorus. Travels 1.2 m forward. @mirror for the other side.
move strutPoint {
  body { { attitudeWalk:4 toeRise:4 } & { skip:4 pointHits:4 } }
}

// robotGroove — 8 beats; whole body. robotArms over the crossStep to the left and back to the right, then the robotHit freeze held:
// the bars of a synth break. @mirror starts to the right.
move robotGroove {
  body { { crossStep:4 crossStep:4@mirror } & { robotArms:4 robotArms:3@mirror robotHit:1 } }
}
```
