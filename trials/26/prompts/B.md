You are a test subject in a writing trial for Shosa, a text language for character motion. You learn the language ONLY from `C:\MyProj\Shosa\trials\26\GRAMMAR_FOR_LLM.md` (read the whole thing first). Do NOT read any other file in C:\MyProj\Shosa (no docs, samples, trials of other numbers or folders, source code, CLAUDE.md). Work only in `C:\MyProj\Shosa\trials\26\B\` (put scratch files in `trials\26\B\scratch\`, images and json in `trials\26\B\out\`).

Tools: the compiler is `C:\MyProj\Shosa\Shosa.Cli\bin\Debug\net10.0\shosa.exe` (run it with no arguments to see its commands: `check` (also `--at`), `sheet` (contact-sheet PNG; look at the PNGs with your Read tool; options like `--step`, `--view side`, `--from`, `--to`), `clip` (per-frame joint positions as JSON), etc.). Use the PowerShell or Bash tool. Do not modify or rebuild the compiler.

Procedure for EACH file (in this order):
1. Before running anything on it, write in `trials\26\B\scratch\notes.txt` a BEFORE section: which words you chose, places where the grammar was unclear to you, the diagnostics you expect, and your answers to the prediction questions.
2. Write the file and run `check`. Fix until there are no diagnostics; count the runs.
3. Answer the prediction questions first from the contact sheet alone (write that down), then measure with `clip` and say HIT or MISS for each.
4. Write an AFTER section: check runs and what each diagnostic was, what you saw that you did not want, surprises, anything you could not express.

Tasks (ballet class; each says how the motion should look; choose the words yourself):

1. `trials\26\B\port.shosa` — a port de bras, tempo 72, 3/4, legs turned out, standing in first position. Arms start low and rounded in front (bras bas). Over 2 beats they rise through first position (rounded in front of the body at stomach height) and continue to fifth (rounded overhead) in one flowing movement, without stopping in first. Over 1 beat they open out to second (to the sides, slightly rounded); over 3 beats they lower to bras bas. Repeat the whole phrase once.
   Predictions: (a) the speed of a hand when the arms pass first position on the way up (m/s; is it zero?). (b) Do the arms pause in fifth before opening? (c) Any diagnostic?

2. `trials\26\B\attitude.shosa` — tempo 96, 4/4, turned out, start in fifth position right foot front, arms bras bas. Over 1 beat: the left leg lifts behind into attitude (thigh about 90 degrees behind, knee bent about 90 degrees), the left arm rises to fifth (overhead) and the right arm opens to second. Hold this still for 2 bars (8 beats). Over 2 beats, close back to fifth with arms to bras bas. Stand 1 beat.
   Predictions: (a) height of the left knee above the floor during the hold (cm). (b) Does the standing leg stay straight? (c) Any diagnostic?

3. `trials\26\B\transfer.shosa` — tempo 80, 4/4, turned out, in a wide second position (feet about 0.7 m apart), arms in second. Transfer the weight onto the right foot over 2 beats, then onto the left over 2 beats, then back to the middle over 2 beats, keeping the pelvis level and not visibly lower. Then a deep plié in second that travels over the right foot (pelvis about 15 cm lower than standing straight, over the right foot) over 2 beats, hold 2 beats, rise back to the middle standing over 2 beats. The feet stay in place.
   Predictions: (a) during the transfers, how far the pelvis moves sideways from the middle (cm) and how far it drops (cm). (b) in the deep plié over the right foot, how far from the middle and how low (cm). (c) Any diagnostic?

4. `trials\26\B\attitude-edit.shosa` — copy attitude.shosa and change it as little as possible: hold the attitude for 3 bars instead of 2, and the arms should arrive in their places half a beat after the leg (the leg still takes 1 beat). Count how many tokens you changed or added.

At the end, write your report to `trials\26\B\report.md` and also return it: for each file, check runs, the diagnostics, the HIT/MISS table, and then a numbered "grammar issues" list (things the grammar did not say, said misleadingly, or could not express). Be concrete and quote the grammar lines that misled you.
