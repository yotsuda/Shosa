You are a test subject in a writing trial for Shosa, a text language for character motion. You learn the language ONLY from `C:\MyProj\Shosa\trials\27\GRAMMAR_FOR_LLM.md` (read the whole thing first). Do NOT read any other file in C:\MyProj\Shosa (no docs, samples, trials of other numbers or folders, source code, CLAUDE.md). Work only in `C:\MyProj\Shosa\trials\27\A\` (put scratch files in `trials\27\A\scratch\`, images and json in `trials\27\A\out\`).

Tools: the compiler is `C:\MyProj\Shosa\Shosa.Cli\bin\Debug\net10.0\shosa.exe` (run it with no arguments to see its commands: `check` (also `--at`), `sheet` (contact-sheet PNG; look at the PNGs with your Read tool; options like `--step`, `--view side`, `--from`, `--to`), `clip` (per-frame joint positions as JSON), etc.). Use the PowerShell or Bash tool. Do not modify or rebuild the compiler.

Procedure for EACH file (in this order):
1. Before running anything on it, write in `trials\27\A\scratch\notes.txt` a BEFORE section: which words you chose, places where the grammar was unclear to you, the diagnostics you expect, and your answers to the prediction questions.
2. Write the file and run `check`. Fix until there are no diagnostics; count the runs.
3. Answer the prediction questions first from the contact sheet alone (write that down), then measure with `clip` and say HIT or MISS for each.
4. Write an AFTER section: check runs and what each diagnostic was, what you saw that you did not want, surprises, anything you could not express.

Tasks (a game; each says how the motion should look; choose the words yourself):

1. `trials\27\A\idle.shosa` — tempo 60, a looping idle for a guard NPC. Feet about 0.4 m apart, never moving. The weight drifts slowly onto the right foot over 4 beats and slowly back onto the left foot over 4 beats, continuously (no waiting, no sudden moves); the arms hang relaxed; the head glances to the right (about 30 degrees) while the weight goes right and back to the front while it goes left. It must loop without a jump.
   Predictions: (a) Does the pelvis ever stand still during the 8 beats, and how far does it travel from side to side (cm)? (b) Is there a jump in any joint at the loop point (largest frame-to-frame move there vs. elsewhere)? (c) Any diagnostic?

2. `trials\27\A\salute.shosa` — tempo 100. Standing at attention (feet together, arms down). Over 2 beats the guard turns 90 degrees to the right on the spot. Then the right hand comes up to the forehead over 1 beat, the elbow out to the side; on the way up the hand must stay on its own (right) side and not cross in front of the chest. Hold 2 beats, then the hand comes down over 1 beat.
   Predictions: (a) On the way up, how far does the right hand get past the middle line of the chest toward the left (cm; negative if it stays on the right)? (b) Which way does the body face at the end, relative to where it started? (c) Any diagnostic?

3. `trials\27\A\cover.shosa` — tempo 90, taking cover behind a low box. From standing with feet about 0.4 m apart, crouch down over 1 beat so the pelvis is about 40 cm lower than standing. Over 2 beats, while staying crouched, put the weight onto the left foot and lean the upper body to the right to peek out past the box. Hold 1 beat. Over 1 beat come back to the centred crouch, then stand up over 1 beat. The feet never move.
   Predictions: (a) In the crouch, how low is the pelvis (cm below standing), and how bent are the knees (degrees)? (b) While peeking, where is the pelvis (cm from the middle of the feet, which side) and where is the head (cm right of its standing place)? (c) Any diagnostic?

4. `trials\27\A\idle-edit.shosa` — copy idle.shosa and change it as little as possible: each drift takes 6 beats instead of 4, and at each side the weight first rests for 2 of those beats before it starts to move back (so it moves during the other 4). The head should still turn with the weight. Count how many tokens you changed or added.

At the end, write your report to `trials\27\A\report.md` and also return it: for each file, check runs, the diagnostics, the HIT/MISS table, and then a numbered "grammar issues" list (things the grammar did not say, said misleadingly, or could not express). Be concrete and quote the grammar lines that misled you.
