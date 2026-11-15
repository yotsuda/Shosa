# Trial 27 — subject B — report

(Saved by the runner from the subject's returned text: subagents cannot write report files.)

I learned the language only from `trials/27/GRAMMAR_FOR_LLM.md`. Files: `plie.shosa`, `developpe.shosa`, `balance.shosa`, `developpe-edit.shosa`. Working notes are in `scratch/notes.txt`; images and JSON are in `out/`.

## 1. plie.shosa

```
turnout { out(80):4 | then:4 | then:4 | }
steps   { leftFoot.side(0.6m):4 | shift.right:2 then:2~ | :2 shift.center:2 | }
height  { straight:4 | low(25cm):2 then:2~ | :2 straight:2 | }
arms    { side.fwd(20).down(15).bent(20):4 | then:4 | then:4 | }
```

- check runs: **1**, no diagnostics. The only output besides that was the info line "starts with `leftFoot.side(0.6m):4` already made".
- Before the first run I caught a mistake of my own: `then:2 | then:2 shift.center` would have made the rise last 2 beats, because only the last of several `then`s is the way. I changed it to a tie, `then:2~ | :2`.

| Q | Sheet only | Prediction | Measured (clip / check --at) | |
|---|---|---|---|---|
| (a) bottom: sideways / down | 20 / 22 cm | 27 / 25 cm | 25.5 cm toward the right foot (the foot is 30 cm out) / 25.0 cm below the wide stance standing straight | HIT |
| (b) knees at bottom | R 90, L 30 | L 60, R 95 | L 61, R 91 | HIT |
| (c) halfway (beat 2) | 10 / 11 cm | 14 / 12 cm | 12.7 / 12.5 cm; the sideways:down ratio is the same at every beat, so the path is one straight diagonal | HIT |
| (d) diagnostic | none | none | none | HIT |

Things I did not want:
- At the bottom the right knee is 35 cm further out than the right ankle. The shin slants about 50° outward, so the knee is far past the toes instead of over them.
- The left knee is 4 cm inside its ankle.
- From the front camera the left leg looks almost straight although it is bent 61°. That threw off my sheet-only estimate for (b).

## 2. developpe.shosa

```
pose brasBas { arms { fwd.across(30).down(70).bent(40).elbow(side) } }
pose first   { arms { fwd.across(15).down(25).bent(50).elbow(side) } }
pose second  { arms { side.fwd(20).down(15).bent(20) } }
steps    { rightFoot.fwd(0.08m).across(0.13m):4 | then:4 | then:2 rightFoot.back(0.08m).across(0.13m):2 | }
rightLeg { down:2 otherKnee:2 | side:4 | skip:4 | }
arms     { brasBas:2 first:2~ | :1 second:3 | then:2 brasBas:2 | }
```

- check runs: **1**, no diagnostics.

| Q | Sheet only | Prediction | Measured | |
|---|---|---|---|---|
| (a) foot height during the hold | ~90 cm, no drift | 90 cm | 89 cm at beats 4, 6 and 8 (ankle y 0.973, the same as the pelvis) | HIT |
| (b) arms when the leg arrives (beat 4) | between first and second | between, about 3/4 of the way | between: hands 81% of the way out sideways, elbows bent 31° (first is 50°, second 20°); in second at beat 5 | HIT |
| (c) diagnostic | none | none | none | HIT |

Things I did not want:
- The unfolding does not keep the knee up. At beat 3 the foot is lower than at the retiré (ankle y 0.595 → 0.523 → 0.973). The leg swings down, out and up about the hip instead of extending the lower leg from a high knee.

Good surprises:
- `side:4 skip:4` held perfectly still through beats 4–8.
- The close lowers to the floor out to the side (around beat 9.3) and slides in to fifth behind by beat 10. That is a proper ballet close.

## 3. balance.shosa

```
pose openRight { rightArm { side.fwd(20).down(15).bent(20) }  leftArm { fwd.across(15).down(25).bent(50).elbow(side) } }
pose lowFront  { arms { fwd.across(30).down(70).bent(40).elbow(side) } }
steps  { stand:3 | { rightFoot.side(0.4m) leftFoot.back(0.08m).across(0.05m) rightFoot.fwd(0.08m) } | q@mirror | q | q@mirror | then:3 | }
height { mid:3 | { low(5cm) high mid } | q | q | q | mid:3 | }
arms   { openRight:3@mirror | then:3/2 lowFront:3/2@through | openRight:3/2 lowFront:3/2@through | openRight:3/2@mirror lowFront:3/2@through | openRight:3/2 lowFront:3/2@through | openRight:3@mirror | }
```

The file has a lead-in bar so that the first step is seen, and a bar at the end to stand.

- check runs: **3**.
  1. Three SH1040 errors (word order): I wrote `openRight@mirror:3` where it must be `openRight:3@mirror`. This was my slip; the grammar states the order clearly. The same run also said "solo ends 0.32 m back of where it began", with no diagnostic.
  2. No problems, but still 0.32 m back. `rightFoot.place` after the cross-behind is placed beside the crossed-back foot, so each balancé drifted 8 cm back. I changed it to `rightFoot.fwd(0.08m)`.
  3. No problems; the dancer ends 0.04 m back (the last stance has the right foot 8 cm behind).

| Q | Sheet only | Prediction | Measured | |
|---|---|---|---|---|
| (a) do the arms stop mid-swing? | no | no, only at the open line | Relative to the shoulder, hand speed reaches about 0 only at beats 3, 6 and 9. At the low point it is 0.84 m/s for one frame against 1.2–1.3 m/s around it: a small kink, not a stop. Speed in the room has extra dips (e.g. 0.26 m/s at beat 8.53) from the body travelling under the arm. | HIT |
| (b) sideways travel per balancé | ~35 cm | 45 cm | 45 cm (middle of the feet 0 → −0.45 m and back) | HIT |
| (c) diagnostic | none | none | none in the final file (SH1040 ×3 on the way) | HIT |

Things I did not want:
- "On the ball of the foot" could only be written as `height { high }`, which puts both feet on the toes on beat 2 and rises through beat 1.

## 4. developpe-edit.shosa

- check runs: **1**, no diagnostics.
- Result: the foot is 89 cm up from beat 4 to 12, the arms are exactly in second at beat 4 (the same hand positions as at beats 5 and 12), and the leg and arms are down by beat 14.
- Tokens: **10 touched**.
  - Changed (3): `side:4`→`side:4~`, `first:2~`→`first:2`, `second:3`→`second:4`.
  - Added (6): `then:4 |` in steps; `| :4` in rightLeg (the tie); `then:4 |` in arms.
  - Removed (1): `:1`.
- It costs this much because the hold is a length repeated in three rows (steps, leg, arms). Making it a pose ("so that its length is in one place") would fight against the arms deliberately having timing of their own.

## Grammar issues

1. **`low(N)` on wide feet is measured from the wide stance, not from standing.** "`low` bends the knees, the hips 16 cm lower than standing straight on the same feet (on wide feet, lower than the wide stance straight)". A task that says "25 cm lower than standing" is ambiguous, and the grammar gives no number for how much lower a 0.6 m stance already is than feet together.
2. **How far a deep shift travels cannot be written.** "The hips stay at that depth and go on toward the foot as far as the other leg reaches". The pelvis went 25.5 of 30 cm, and there is no way to say "80% of the way" or "fully over". The only example is at 0.7 m ("with the weight over the right foot").
3. **Turned-out plié knee placement.** "turned out, a plié (`height { low }`) takes the knees out over the toes instead of forward". At `out(80)`, `low(25cm)` with a shift, the right knee ends 35 cm outside its ankle (far past the toes), and the left knee is 4 cm inside its ankle. Either the description is misleading or this is a solver issue; nothing in the grammar lets me fix it.
4. **`then` before a `shift`: wait or way?** The timing table says "For the feet and for a `@sharp` word a `then` only waits: they still set out late in it". The shift row's example (`then:1 shift.right:2` "moves it from beat 1 to beat 2") shows that for a shift the `then` IS the way. The "for the feet" sentence should except shifts explicitly.
5. **Several `then`s across a bar line.** "Of several `then` in a row only the LAST is the way to the next word". This makes the natural `shift.right:2 then:2 | then:2 shift.center:2 |` rise in 2 beats, not 4; you need a tie (`then:2~ | :2`). "Staying still needs no tie: `then:4 | then:4 |`" is true but invites the mistake. A line saying "a slow way that crosses a bar line is a tied `then`" would help.
6. **Développé path.** "`rightLeg { otherKnee fwd.up(15):2 down }` a développé that goes up through it and unfolds to the front". From `otherKnee` to `side` the foot first drops (y 0.595 → 0.523) and then swings up about the hip. "A leg swings about the hip" explains why, but then the example is misleading. There is no word for unfolding from a raised knee (keeping the thigh up while the shin extends); maybe `@through` on a `side.bent` in between would do it, but the grammar does not say.
7. **`place` is not "in place".** "`place` | Beside the supporting foot: a step on the spot." After a cross-behind, "step on the right in place" written as `rightFoot.place` moves the whole stance back 8 cm per balancé (0.32 m after four). The cross/side drift is mentioned ("a line of side / cross / side drifts by that 0.12 m") but not for `place`. There is no step that puts a foot down where it already was.
8. **Demi-pointe on one foot.** `height { high }` is "On the toes" for both feet. A balancé's second step "on the ball of the foot" cannot be said. "What the language cannot say" lists "standing on pointe" and "a foot that points or flexes when you choose", but not half-toe on one foot.
9. **A leg word followed by `skip`.**
   - The grammar says a gesture "sets out for the next word as soon as it has arrived" and "To keep a leg as it is, write `then`", but not whether `side:4 skip:4` holds `side` through its 4 beats. It did, but I had to test it.
   - It also does not say how the `skip`'s lowering (over the skip's time) combines with the step's late set-out (≤0.5 s). The leg actually lowered over about 1.5 beats and slid in.
10. **Contact sheet and turnout.** From the front camera a turned-out bent knee looks straight (61° looked like about 30°), and a symmetric arm pose looks lopsided (the camera stands to stage left). The grammar warns about foreshortening for kicks ("judge those … with `--view side`") but not for turned-out knees or second-position arms.
11. **Minor:** the `@through` point leaves a one-frame dip in hand speed (0.84 against 1.2–1.3 m/s). It is not a stop, but "arrives at speed and … leaves at that speed" is not exactly true.
