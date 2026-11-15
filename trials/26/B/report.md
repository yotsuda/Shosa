# Trial 26 / B — report

(Saved by the trial runner from the subject's returned text: the subject was not allowed to write this file. Notes are in `scratch\notes.txt`.)

## 1. port.shosa (port de bras, 72, 3/4)

- **Check runs:** 1. No diagnostics.
- **How I wrote it:** four poses holding only `arms`: bras bas, first, fifth and second. The row is `bbas:1 armsFirst:1@through armsFifth:1 | armsSecond:3 |`, written twice, then `bbas:3 |`.

| Prediction | Before | From the sheet | Measured (clip, right hand) | |
|---|---|---|---|---|
| (a) Hand speed passing first | not zero, ~0.6 m/s | not zero, ~0.8–1 m/s | 0.78 m/s arriving, 1.21 at beat 1, 1.63 leaving | HIT (not zero) / MISS (number) |
| (b) Pause in fifth? | no, only an instant's stop | no | 0.07 m/s at beat 2, 0.51 at 2.1 | HIT |
| (c) Diagnostic | none | – | none | HIT |

Unwanted: the hand's speed jumps at first position, from 0.78 m/s to 1.63 m/s within two frames.

## 2. attitude.shosa (96, 4/4)

- **Check runs:** 2, neither with a diagnostic. Run 1 `back.up(60).bent.knee(side.up)` put the knee at 1.32 m and the foot at 1.44 m, no warning (misread "thigh 30° up" for `back.bent`). Run 2 `back.down(35).bent.knee(side.up)`: thigh level (−2°), 33° out to the side from straight back, knee 90°.

| Prediction | Before | From the sheet | Measured | |
|---|---|---|---|---|
| (a) Left knee height in the hold | ~90 cm | ~95 cm | 95.6 cm | HIT |
| (b) Standing leg straight | yes, ~4° | yes | right knee 4° | HIT |
| (c) Diagnostic | none | – | none | HIT |

## 3. transfer.shosa (80, 4/4, second at 0.7 m)

- **Check runs:** 1. No diagnostics.

| Prediction | Before | From the sheet | Measured (pelvis) | |
|---|---|---|---|---|
| (a) Transfers: sideways | ~9 cm | ~10 cm | 8.7 cm each way | HIT |
| (a) Transfers: drop | 2–3 cm | a few cm | 3.0 cm | HIT |
| (b) Deep plié: sideways | ~20 cm | ~25 cm | 29 cm (6 cm short of over the foot) | MISS |
| (b) Deep plié: lower | 15 cm | 20+ cm | 29.4 cm | MISS |
| (c) Diagnostic | none | – | none | HIT |

Unwanted: each transfer takes 0.56 s, not 2 beats. `low(15cm)` with `shift.right` sank 29 cm (without the shift 15.0 cm; `low(8cm)` with the shift 15.4 cm but only 18 cm toward the foot). The pelvis moves in an L (down first, sideways in the last 0.5 s; reverse on the way up). The left leg is straight: a lunge, not a plié in second.

## 4. attitude-edit.shosa

- **Check runs:** 1. No diagnostics. **Tokens:** 6 added, 0 changed (`then:4 |` in the `body` row; a new row `arms { closed:3/2 }`). Leg arrives at 1, hands at 1.5, hold ends at 13, back in fifth at 15. Caveat: the arms travel from 0 to 1.5, not from 0.5 to 1.5.

## Grammar issues

1. `@through` does not do what the grammar says ("the way to it arrives at speed and the way on to the next word leaves at that speed"): arrives at 0.78 m/s, leaves at 1.63 m/s.
2. The thigh angle of `back.bent` is hard to read from the example comment ("an attitude: the thigh 30° up"), can only be found by trial with `clip`; a foot 1.44 m up behind gave no warning. A small table from foot direction to thigh angle would help.
3. No way to make a slow weight transfer (a shift sets out "no more than 0.5 s before its beat"); not listed under "What the language cannot say".
4. In a lunge, `low(N)` does not give the depth: `low(15cm)` + `shift.right` at 0.7 m is 29.4 cm lower.
5. A shift and a height over the same beats do not move together (L-shaped path).
6. No plié in second with the weight over one foot (both knees bent, pelvis off-centre).
7. "0.5 m apart the hips go half way, 0.7 m apart a quarter" — of what?
8. The named ballet arm examples do not include second (only in the assemblé paragraph).
9. How to make the arms start late (not only arrive late) beside a pose is not said.
10. A pose cannot hold its parts at different times; "arms half a beat after the leg" needs an extra row.
11. Turnout on a leg in the air pushes an attitude 33° to the side; no word for "thigh straight behind" in a turned-out attitude.
