# Trial 27 — subject A — report

(Saved by the runner from the subject's returned text: subagents cannot write report files.)

Files: `trials\27\A\idle.shosa`, `salute.shosa`, `cover.shosa`, `idle-edit.shosa`. I read only `trials\27\GRAMMAR_FOR_LLM.md` and `prompts\A.md`.

Every problem I hit was silent: no file ever got a diagnostic. In idle.shosa the first version, which `check` called fine, lifted the left foot 6 cm at the loop point.

## 1. idle.shosa (tempo 60, looping)

**Check runs: 4. Diagnostics: none in any run.** I found each problem only by measuring with `clip` and `check --at`.

- **Run 1:** `steps { leftFoot.side(0.4m):2 shift.right:4 shift.left:2 }`.
  - `check` reported no problems.
  - At the loop the left foot lifted 6 cm (beats 7.5–8). The loop's "way back to the first word" took the stance step again.
  - The pelvis also stood still from beat 6 to 7.5.
- **Run 2:** the stance went into a pose: `pose wide { steps { leftFoot.side(0.4m) } }` with `body { wide:8 }`, and steps became `skip:2 shift.right:4 shift.left:2`.
  - The feet stayed down.
  - The weight was still for beats 6–8.
- **Run 3:** `body { wide:8 }` with `steps { shift.left:4 shift.right:4 }`.
  - The motion was a clean sine.
  - But the stance quietly became 0.2 m: the steps row "had the feet" at beat 0, so the pose's step was not taken.
- **Run 4 (final):** `body { wide:8 }`, `steps { skip:4 shift.right:15/4 shift.left:1/4 }`, `head { fwd:4 fwd.right(30):4 }`.
  - The feet are 0.4 m apart and never move.
  - The weight is on the left at 0, on the right at 4, and back on the left at 7.75.
  - It then holds for 0.25 beat before going round again.

| Prediction | From sheet (v1) | Measured (v1) | HIT/MISS | Final version |
|---|---|---|---|---|
| (a) pelvis never still; travel ~28 cm | never still, 25–30 cm | still for beats 6–7.5; travel 33 cm (−22.5…+10.5) | MISS (still) / ~HIT (33 vs 28) | always moving except an 8-frame hold at the left end; travel 33.1 cm |
| (b) no jump at the loop | none visible | left foot lifts 6 cm; largest move 3.29 cm/frame at the loop vs ~0.5 elsewhere | MISS | last frame = first frame; largest move anywhere 0.53 cm/frame |
| (c) no diagnostic | none | none | HIT | none |

The head turns 0° → 30° right → 0°, in step with the weight. The pelvis also bobs 3 cm, twice per cycle (the sink that comes with a shift).

## 2. salute.shosa (tempo 100)

**Check runs: 1. Diagnostics: none.**

What I wrote:
- `height { straight }`
- `steps { together:2/3 turn.right(30):2/3 turn.right(30):2/3 turn.right(30):13/3 }`: three pivots, because the feet set out late and one `turn.right(90)` would turn only in the last 0.5 s.
- `rightArm { down.bent(0):2 then:1/2 side.down(30).bent(60):1/2@through forehead:2 then:1 down.bent(0):1 }`

| Prediction | From sheet | Measured | HIT/MISS |
|---|---|---|---|
| (a) hand past the chest midline on the way up: about −3 cm (stays right) | about −5 cm, never crosses | −4.0 cm, nearest at the forehead itself; 58 cm out to the right at beat 2.44 | HIT |
| (b) facing at the end | 90° to its right | 90° right, in three 30° steps; "ends where it began, turned 90° to its right" | HIT |
| (c) no diagnostic | none | none | HIT |

What I saw and did not want:
- **On the way down** (`forehead` → `down.bent(0)`, no point in between) the hand crosses 17 cm to the left of the chest midline at beat 5.5. The task only limited the way up, so I left it.
- The turn is a staircase: three quick 30° pivots with stops in between.

## 3. cover.shosa (tempo 90)

**Check runs: 1. Diagnostics: none.** The only extra line was the info line "starts with `leftFoot.side(0.4m):1` already made".

What I wrote:
- `steps { leftFoot.side(0.4m):1 then:2 shift.left:1 then:1 shift.center:2 }`
- `height { mid low(40cm):4 then:1 mid:1 }`
- `torso { up:1 then:2 up.right(30):1 then:1 up:2 }`
- The head is not written.

| Prediction | From sheet | Measured | HIT/MISS |
|---|---|---|---|
| (a) crouch: pelvis 40 cm low, knees ~110° | ~40 cm, ~100° | 39.6 cm; knees 112° both (113/102 while peeking) | HIT |
| (b) peek: pelvis ~15 cm left of the feet's middle; head ~20 cm right of its standing place | 15–20 cm left; 20–25 cm right | pelvis 17.2 cm left (left ankle 20 cm from the middle); head 19.8 cm right and 49 cm lower | HIT |
| (c) no diagnostic | none | none | HIT |

The unwritten head tilts 30° with the lean. That matches the grammar and reads as a peek.

## 4. idle-edit.shosa

**Check runs: 1. Diagnostics: none. Tokens changed or added: 9** (5 changed, 4 added).

- `body { wide:12 }`
- `steps { skip:2 then:4 shift.right:2 then:15/4 shift.left:1/4 }`
- `head { fwd:2 then:4 fwd.right(30):2 then:4 }`

Measured:
- The pelvis rests for beats 0–2 (2.25 beats counting the quarter beat before 0), moves over 2–6, rests over 6–8, and moves back over 8–11.75.
- The face holds 0° over 0–2, reaches 30° right at 6, holds it to 8, and is back to 0° by 12.
- The feet never lift, and the loop joins with no jump (largest move 0.53 cm/frame).

## Grammar issues

1. **A loop whose steps row begins with a stance step lifts that foot at the loop point, and nothing warns.**
   - The grammar says "`loop` … the way to the first word of each row is made at the END of the loop, in its last word … The feet must end as they start (same stance…) or SH2005 says so", and "A step already made is a way of standing".
   - So I read `leftFoot.side(0.4m)` as a first word that is just kept.
   - Instead the step was taken again at the end: the left foot rose 6 cm, with no SH2005 and no other diagnostic.
2. **There is no way to begin with a wide stance and the weight on one foot at beat 0.**
   - The grammar says "A step already made is a way of standing, so the weight is on both feet … To begin on the foot it stepped onto, shift to it next". In a loop, "next" costs time.
   - My workaround (a pose in `body` plus a steps row that starts with `skip`) relies on something never stated: that a loop's weight at beat 0 is wherever the end of the loop leaves it.
3. **A pose's stance in `body` is silently dropped if the steps row starts with a shift.**
   - "a wider track's step (a pose's stance included) is taken only if that track has the feet at the beat the step is written at" is accurate.
   - But it is not obvious that a `shift` counts as having the feet, and the feet ending at 0.2 m instead of 0.4 m gets no warning.
4. **In a skip-first row no word can sit on the loop's last beat, so a return to the start costs a hold.** I needed `shift.left:1/4` at 7.75, which holds for a quarter beat. The grammar does not cover the loop rule for a row whose first word is `skip`.
5. **A shift has no `@through`.**
   - The grammar says "`@through` … Not for steps", yet a shift moves "as a gesture", and a gesture "slows at each word".
   - So a weight path through a passing point (centre → right → left → centre) cannot avoid slowing at the centre.
6. **A turn of the feet cannot take a written duration.** "The feet — a step, `stand`, a turn — In the second half of the word before, and no more than 0.5 s before its beat." "Turn 90° over 2 beats" can only be a staircase of small pivots, and "What the language cannot say" does not list this.
7. **How a hand travels to or from a place on the body is not said.**
   - `forehead` says "the elbow out to the side: a salute", but not by which way the hand gets there.
   - Going down to `down` it crossed 17 cm past the midline, in front of the chest. Going up it stayed right only because I added `side.down(30).bent(60)@through`.
   - The only hint, "Both hands at a place near the middle … bring the elbows across each other", is about two hands.
8. **A foot lifting at the loop point is invisible on a half-beat sheet.** "Anything shorter than that gap is invisible" is stated, but the 6 cm lift showed only in `check --at 7.75` ("left 6 cm up"). `check` could flag "a foot leaves the floor at beat …" in a loop.
9. **Minor: two meanings of "standing".** `low(40cm)` is "lower than standing straight in the same place", while an unwritten row stands at `mid`, 0.5 cm lower. Harmless, but it invites doubt.
10. **Editing rests is spread across rows (task 4).**
    - Every rest means adding a `then` and recomputing lengths in every row that moves together (steps and head).
    - A pose holds "ONE target and no length", and a move cannot carry the loop's way back across its own end.
    - So "the weight and the head move together" cannot be written in one place.
