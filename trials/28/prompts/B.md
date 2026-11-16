You are a test subject in a writing trial for Shosa, a text language for character motion. You learn the language ONLY from `C:\MyProj\Shosa\trials\28\GRAMMAR_FOR_LLM.md` (read the whole thing first). Do NOT read any other file in C:\MyProj\Shosa (no docs, samples, trials of other numbers or folders, source code, CLAUDE.md). Work only in `C:\MyProj\Shosa\trials\28\B\` (put scratch files in `trials\28\B\scratch\`, images and json in `trials\28\B\out\`).

Tools: the compiler is `C:\MyProj\Shosa\Shosa.Cli\bin\Debug\net10.0\shosa.exe` (run it with no arguments to see its commands: `check` (also `--at`), `sheet` (contact-sheet PNG; look at the PNGs with your Read tool; options like `--step`, `--view side`, `--from`, `--to`), `clip` (per-frame joint positions as JSON), etc.). Use the PowerShell or Bash tool. Do not modify or rebuild the compiler.

Procedure for EACH file (in this order):
1. Before running anything on it, write in `trials\28\B\scratch\notes.txt` a BEFORE section: which words you chose, places where the grammar was unclear to you, the diagnostics you expect, and your answers to the prediction questions.
2. Write the file and run `check`. Fix until there are no diagnostics; count the runs.
3. Answer the prediction questions first from the contact sheet alone (write that down), then measure with `clip` and say HIT or MISS for each.
4. Write an AFTER section: check runs and what each diagnostic was, what you saw that you did not want, surprises, anything you could not express.

Tasks (dance; each says how the motion should look; choose the words yourself):

1. `trials\28\B\vine.shosa` — tempo 120, 4/4, two bars. Bar 1: a grapevine travelling to the dancer's right — right foot to the side, left foot crossing behind it, right foot to the side, left foot closes beside the right; one count each. Bar 2: the same to the left, mirrored. The arms open to the sides on the first count and stay. The travel should flow across the floor; the dancer does not stop on each count.
   Predictions: (a) Over bar 1, does the pelvis ever stop moving to the right (slowest sideways speed vs. average)? (b) How far does the dancer travel in bar 1 (cm), and where does it end after both bars? (c) Any diagnostic?

2. `trials\28\B\echappe.shosa` — tempo 96, 2/4, ballet. Start standing in a closed position with the feet together and turned out, arms low. Count 1: spring out to land with the feet apart in a wide second position. Count 2: stay. Then, WITHOUT jumping, bring the feet back together over count 3 and stay on count 4. Then do the same again, but this time close the feet with the weight going onto the LEFT foot (the right foot comes in to it). Finally one échappé that closes by jumping back in.
   Predictions: (a) In the first close without a jump, which foot moves and where does the pelvis go first (cm, which side)? (b) In the second, where do the feet end, compared with where the first close ended? (c) In the jump back in, how many times does the pelvis go down between the landing in second and the landing closed? (d) At the top of the jumps, are the feet pointed (toe below the ankle, cm, against standing)? (e) Any diagnostic?

3. `trials\28\B\turns.shosa` — tempo 80, 4/4. Standing on both feet, a slow quarter turn to the right that takes two whole beats, starting right away on beat 0. Then stand two beats. Then on one foot (the left; the right foot lifted to the left knee) one full turn to the right, landing on the next beat, quick: it should take only half a beat. Close and stand.
   Predictions: (a) At beat 1, how far has the slow quarter turn gone (degrees)? (b) When does the full turn start and end (beats)? (c) Any diagnostic?

4. `trials\28\B\vine-edit.shosa` — copy vine.shosa and change it as little as possible so that every count is a sharp hit: on each count the body arrives and freezes for a moment (about half a count) before the next step sets out, as dancers hit a beat. Count how many tokens you changed or added. Then measure: does the pelvis now stop on each count?

At the end, write your report to `trials\28\B\report.md` and also return it: for each file, check runs, the diagnostics, the HIT/MISS table, and then a numbered "grammar issues" list (things the grammar did not say, said misleadingly, or could not express). Be concrete and quote the grammar lines that misled you.