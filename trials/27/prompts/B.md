You are a test subject in a writing trial for Shosa, a text language for character motion. You learn the language ONLY from `C:\MyProj\Shosa\trials\27\GRAMMAR_FOR_LLM.md` (read the whole thing first). Do NOT read any other file in C:\MyProj\Shosa (no docs, samples, trials of other numbers or folders, source code, CLAUDE.md). Work only in `C:\MyProj\Shosa\trials\27\B\` (put scratch files in `trials\27\B\scratch\`, images and json in `trials\27\B\out\`).

Tools: the compiler is `C:\MyProj\Shosa\Shosa.Cli\bin\Debug\net10.0\shosa.exe` (run it with no arguments to see its commands: `check` (also `--at`), `sheet` (contact-sheet PNG; look at the PNGs with your Read tool; options like `--step`, `--view side`, `--from`, `--to`), `clip` (per-frame joint positions as JSON), etc.). Use the PowerShell or Bash tool. Do not modify or rebuild the compiler.

Procedure for EACH file (in this order):
1. Before running anything on it, write in `trials\27\B\scratch\notes.txt` a BEFORE section: which words you chose, places where the grammar was unclear to you, the diagnostics you expect, and your answers to the prediction questions.
2. Write the file and run `check`. Fix until there are no diagnostics; count the runs.
3. Answer the prediction questions first from the contact sheet alone (write that down), then measure with `clip` and say HIT or MISS for each.
4. Write an AFTER section: check runs and what each diagnostic was, what you saw that you did not want, surprises, anything you could not express.

Tasks (ballet class; each says how the motion should look; choose the words yourself):

1. `trials\27\B\plie.shosa` — tempo 60, 4/4, turned out, wide second position (feet about 0.6 m apart), arms in second. Over 4 beats the pelvis travels toward the right foot while sinking into a deep plié, both knees bending, so that the weight ends mostly over the right foot with the pelvis about 25 cm lower than standing; the path should be one smooth diagonal, not down-then-across. Hold 2 beats. Over 4 beats rise back to the middle, standing straight. Stand 2 beats.
   Predictions: (a) At the bottom, how far is the pelvis from the middle of the feet (cm) and how low (cm)? (b) The angle of each knee at the bottom. (c) Halfway through the 4 beats down, how far has the pelvis gone sideways and how far down (cm)? (d) Any diagnostic?

2. `trials\27\B\developpe.shosa` — tempo 66, 4/4, turned out, fifth position right foot front, arms bras bas. Développé à la seconde with the right leg: over 2 beats the right foot draws up the standing leg to the knee (retiré), over 2 beats the leg unfolds to the side at about 90 degrees (straight); hold 4 beats; over 2 beats it lowers and closes to fifth behind. The arms: they rise to first while the foot draws up, and open to second while the leg unfolds, but arrive in second ONE beat after the leg has arrived; they lower to bras bas with the leg.
   Predictions: (a) height of the right foot above the floor during the hold (cm). (b) At the moment the leg arrives at the side, where are the arms (between first and second, or already in second)? (c) Any diagnostic?

3. `trials\27\B\balance.shosa` — tempo 120, 3/4, a waltz balancé travelling side to side: step to the right side on beat 1, step the left foot behind the right on beat 2 (on the ball of the foot), step on the right in place on beat 3; then the same to the left; 4 balancés in all (right, left, right, left). The arms sweep with it: on each balancé both arms swing over to the side the body travels to, passing low in front without stopping, ending with the arms in an open line to that side (one arm out to the side, the other rounded in front).
   Predictions: (a) Do the arms stop anywhere during a swing, other than at the end? (b) How far does the body travel sideways on one balancé (cm)? (c) Any diagnostic?

4. `trials\27\B\developpe-edit.shosa` — copy developpe.shosa and change it as little as possible: hold the leg at the side for 8 beats instead of 4, and the arms should arrive in second at the same moment as the leg instead of a beat later. Count how many tokens you changed or added.

At the end, write your report to `trials\27\B\report.md` and also return it: for each file, check runs, the diagnostics, the HIT/MISS table, and then a numbered "grammar issues" list (things the grammar did not say, said misleadingly, or could not express). Be concrete and quote the grammar lines that misled you.
