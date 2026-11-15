# Trial 26 / A — report

(Saved by the trial runner from the subject's returned text: the subject was not allowed to write this file. Notes are in `scratch\notes.txt`.)

Two task requirements were not met: the quarter-beat salute drop always got SH2003 (the final file drops in half a beat), and the left leg is never straight in the stance lunge.

## 1. soldier.shosa (Salute, LookFar)
Check runs: **5**. (1) `then:1/4 down:3/4`: SH2003, about 13 m/s. (2) with `@linear`: SH2003, 11 m/s. (3) `then:1/2 down:1/2`: none. (4) `then:1/3 down:2/3`: SH2003, 11 m/s. (5) back to `then:1/2`: none.

- Salute: `rightArm { down:1/2 then:1/2 forehead:2 then:1/2 down:1/2 }`
- LookFar: `leftArm { down:1 forehead:8 then:1 down:1 }`, `head { fwd:1 then:2 fwd.left(40):4 fwd.right(40):2 fwd:2 }`

| Q | Prediction | Measured | |
|---|---|---|---|
| (a) elbow while held | beside the body | 14 cm outside the shoulder, 9 cm forward, 24 cm up | HIT |
| (a) hand to nose | ~10 cm | 8.1 cm | HIT |
| (b) hand at forehead while the head turns | follows, 1-2 cm | hand–nose 7.8–8.4 cm; at most 2.7 cm drift in the head's frame | HIT |
| (c) diagnostic | maybe SH2003 | SH2003 on the quarter-beat drop; none in the final file | HIT |

Unwanted: on the way up to `forehead` the hand swings across the front of the body (beat 0.75: 5 cm past the midline, 41 cm in front, elbow forward); the elbow goes out only on arrival; the drop takes the same path back.

## 2. greeter.shosa
Check runs: **1**. `rightArm { down:1 side:1@through up:1/2 { up.across(25):1/2 up.side(25):1/2 } up.across(25):1/2 up.side(25):1 side:1@through down:1 }`

| Q | Prediction | Measured | |
|---|---|---|---|
| (a) hand speed at the horizontal | ~1.8 m/s | 2.26 m/s | MISS (value) |
| (a) fastest point? | yes | peak 2.34 m/s one frame later; no dip | HIT |
| (b) stops at the swing ends | yes | 0.14 / 0.00 / 0.00 / 0.32 m/s at the ends, 2.53 mid-swing | HIT |
| (c) diagnostic | none | none | HIT |

The way down does not pause either (1.94 m/s). The first swing (from `up`) is half as wide as the others.

## 3. stance.shosa
Check runs: **2**, no diagnostics (v1 `low(15cm)`, final `low(8cm)`).
`steps { leftFoot.side(0.6m):1 shift.right:1 shift.left:1 shift.center:1 shift.right:2 then:1 shift.center:1 }`, `height { mid:3 then:1 low(…):2 then:1 mid:1 }`

| Q | Prediction | Measured (v1) | |
|---|---|---|---|
| (a) pelvis drop in the shifts | ~2.5 cm | 2.9–3.0 cm | HIT |
| (a) sideways in the shifts | ~11 cm | 10.3 cm | HIT |
| (b) lunge, sideways | ~20 cm | 25.5 cm (the foot is 30 cm out) | MISS |
| (b) lunge, depth | ~15 cm | 24.7 cm | MISS |
| (c) left leg straight | yes | left knee 61°, right 91° | MISS |

Probes: `low(5cm)` 17 cm over, 10 cm down (left knee 37°); `low(8cm)` 23 cm over, 16 cm down (45°). The pelvis drops half a beat before it moves sideways.

## 4. greeter-edit.shosa
Check runs: **1**. Tokens: **5** (3 changed, 2 added: `then:1/2`, `q`). Speed at the horizontal 4.48 m/s (peak 4.58); ends 0.00–0.32 m/s; still overhead 1.06–1.94; six swings. All HIT.

## Grammar issues
1. The speed limit (SH2003) has no number.
2. "Drops sharply in a quarter beat" cannot be written (`@sharp` is "no more than 0.15 s"; `then:1/4` gets SH2003).
3. The path to `forehead` is neither described nor controllable (hand crosses the front, elbow out only on arrival).
4. Whether `forehead` follows a head turn (left/right) is not documented (it does).
5. `low(N)` in a lunge is misleading: the recipe `height { mid low(15cm):1 }` gave 24.7 cm lower at 0.6 m.
6. No straight-legged lunge; both knees bend (45–61°).
7. Steps and height are timed differently: the lunge goes down first, then across; a shift cannot take its whole beat.
8. `q` cannot make its last repeat longer.
9. No idiom for a wave around a direction; the first swing from `up` is half as wide.
10. `together` as the first word of `steps` is not covered (only a first step "already made"); it worked.
11. The front-view sheet hid the bent trailing leg (61°).
