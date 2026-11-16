# Writing trial 28 — subject A

Files: patrol.shosa, door.shosa, rope.shosa, patrol-edit.shosa. Notes (BEFORE/AFTER for each file): scratch\notes.txt. Measurements: out\*.json. Sheets: out\*.png.

## 1. patrol.shosa (tempo 110, loop inPlace)
`steps { { leftFoot.fwd rightFoot.fwd leftFoot.fwd rightFoot.fwd together:2 turn.right(180):1 } | q | }` in `time 7/4`.
- Check runs: 1. Diagnostics: none ("guard loops on the spot").

| Q | Prediction | Measured | |
|---|---|---|---|
| (a) pelvis stops in the steps? | never stops; slowest ~0.6 m/s against a mean of 0.92; from ~0 at beat 0 | never stops: 0.88–1.06 m/s over beats 0–2.7 (mean 0.92). Drops to 0.47 at beat 3 and 0 at beat 4 (the close). Already 1.0 m/s at beat 0: the start from standing falls in beats 13.5–14 (a burst to 1.59 m/s) | HIT that it never stops / MISS on the numbers |
| (b) double support | ~0.11 s | 0.09–0.13 s | HIT |
| (c) turns | 4→6, 11→13 | feet −0.1° at b4.03, −180° at b6.05; the second turn ends at b13.0 | HIT |
| (d) diagnostics | none | none | HIT |

Also seen: a one-frame velocity hitch at the loop seam (1.23, then 0.10, then 1.02 m/s). `inPlace` reports velocity [0,0], and all the out-and-back travel stays in the clip.

## 2. door.shosa (tempo 90)
`steps { then:1 leftFoot.fwd rightFoot.fwd leftFoot.fwd stand:5 }`, `rightArm { down:5 then:1 fwd.down(50):1 fwd.down(65).bent:1 down:1 }`
- Check runs: 1. Diagnostics: none.

| Q | Prediction | Measured | |
|---|---|---|---|
| (a) pace per step | b1 0.6, b2 0.75, b3 0.6, b4 0; slows only in the last beat | b1 0.84 (after a 1.30 m/s burst at b0.8), b2 0.77, b3 0.39, b4 0.00. Slows from b2.5, during the third step. Clean stop, no overshoot | b2 and b4 HIT; b1, b3 and "only the last beat" MISS |
| (b) which foot, when | right, b3.2→4.0 | right, lifts b3.25, lands b4.0 | HIT |
| (c) hand at reach | 39 cm in front, 99 cm up | 38.5 cm in front of the pelvis, 98.6 cm up | HIT |
| (d) diagnostics | none | none | HIT |

Also seen: the pull ends 18 cm in front but 7 cm higher (106 cm). The hand rises as it pulls.

## 3. rope.shosa (tempo 120)
`steps { together:1 { jump.up(5cm) } q q q q q q q }`. The arms make 4-point circles with @through, one per beat, then hold.
- Check runs: 1. Diagnostics: none. I also ran 2 scratch copies with up(10cm) and up(20cm); the second gave 8× SH2016 "it rises 12.2 cm".

| Q | Prediction | Measured | |
|---|---|---|---|
| (a) dips between landings | 2 | 1 (lowest at b+0.3, then up into the rise) | MISS |
| (b) depth | ~6.5 cm below straight | 6.0 below mid ≈ 6.5 below straight | HIT |
| (c) feet at top | level | pointed: the toe is 11.1 cm below the ankle (6.0 standing). The toe stays ON the floor: check --at says "on its toes" | MISS |
| (d) diagnostics | none | none | HIT |

The feet never leave the floor. up(5cm) raises the body 5.6 cm by pointing the feet. With up(10cm) the toe is still on the floor. The most the timing gives is 12.2 cm, and then the toe clears the floor by about 0.5 cm for one frame. "Feet 5 cm off the floor, once a beat at 120" cannot be done, so I kept up(5cm). The hands draw a smooth forward circle about 21 × 14 cm at hip height, but it is not timed to the flight.

## 4. patrol-edit.shosa
Each step became `STEP:1/4 then:3/4`. That is 8 tokens added (4 lengths and 4 thens); the turns are untouched.
- Check runs: 2, no diagnostics either time. Run 1 used `:1/2 then:1/2`: the stop lasted 0.72 beat and the step took 0.27 beat, with the pelvis at 5.35 m/s. Run 2 changed the four values.
- Measured: YES, the pelvis stops at each step. Its speed is exactly 0 from each landing to b+0.60 (0.33 s). Then it covers 0.5 m in 0.4 beat, peaking at 3.6 m/s: a dash, not a march. Prediction "stops about half a beat (b0.25–0.75)": HIT that it stops. It stops dead at the landing itself, though, with no slowing down.

## Grammar issues
1. A stop and a step of set lengths can't be written separately. The grammar says "For a step and a `@sharp` word a `then` only waits: they still set out late in it (its second half …)". So the stop is Lstep + Lthen/2 and the swing is Lthen/2; half a beat standing plus half a beat stepping would need Lstep = 0.
2. There is no check on the pelvis speed. A step out of a then moved the pelvis at 5.35 m/s with no warning; SH2003 checks only hands and feet. Every first step out of a standstill is a burst (1.3–1.6 m/s).
3. `jump.up(N)` gives no flight below about 12 cm. "half a second 10 cm (how far the body rises; a foot swinging through clears the floor by more)" and "A quarter of a second gives 3 cm of air" read as the feet leaving the floor. In fact the toe stays on the floor (up(10cm): "on its toes").
4. The feet point in a jump, but the grammar says they can't: it lists "A foot that points or flexes when you choose (it points only with its toe on the other leg or on the floor …)" under what cannot be said.
5. One knee bend per jump, not two. "the knees give a little after", "waits on the floor through the rest of the word before" and "The jump bends its own knees before it takes off" read as two dips between landings. There is only one.
6. `inPlace` ("the even travel over the floor is taken out … reported as a velocity") does nothing for an out-and-back patrol: velocity [0,0], and the whole walk stays in the clip. The grammar doesn't warn about this case.
7. The "When it sets out" table, "After anything else (a `then`, a shift, a jump, the start)", does not name a turn. A step after a turn starts from standing in the second half of the turn word.
8. "A step straight after a step (or a `stand`, a `together`) WALKS … without stopping" led me to expect full pace until the close. The body slows from half a beat before the last forward step lands (0.39 m/s at the third landing in door).
9. `bent` "brings the hand … closer along the same direction", so a pull straight back at the same height means working out direction and bend by hand: my pull still rose 7 cm. There is no target for a hand's place in the room except stay; "Holding an object" is a listed limit.
10. The pelvis speed hitches for one frame at the loop seam (1.23, then 0.10, then 1.02 m/s) where the same step mid-loop is smooth. The grammar promises "the end joins the start".
