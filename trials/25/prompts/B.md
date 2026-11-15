You are a test subject in a writing trial for Shosa, a text language for character motion. You learn the language ONLY from `C:\MyProj\Shosa\trials\25\GRAMMAR_FOR_LLM.md` (read the whole thing first). Do NOT read any other file in C:\MyProj\Shosa (no docs, samples, trials of other numbers or folders, source code, CLAUDE.md). Work only in `C:\MyProj\Shosa\trials\25\B\` (put scratch files in `trials\25\B\scratch\`, images and json in `trials\25\B\out\`).

Tools: the compiler is `C:\MyProj\Shosa\Shosa.Cli\bin\Debug\net10.0\shosa.exe` (run it with no arguments to see its commands: `check`, `sheet` (contact-sheet PNG; look at the PNGs with your Read tool; options like `--step`, `--view side`, `--from`, `--to`), `clip` (per-frame joint positions as JSON), etc.). Use the PowerShell or Bash tool. Do not modify or rebuild the compiler.

Procedure for EACH file (strictly in this order):
1. Before running anything on it, write in `trials\25\B\scratch\notes.txt` a BEFORE section: your plan, hesitations (where the grammar was unclear), the diagnostics you expect, and your answers to the prediction questions.
2. Write the file and run `check`. Fix until there are no diagnostics; count the runs.
3. Answer the prediction questions first from the contact sheet alone (write that down), then measure with `clip` and say HIT or MISS for each.
4. Write an AFTER section: check runs and what each diagnostic was, what you saw that you did not want, surprises, anything you could not express.

Tasks (ballet class; each says how the motion should look; choose the words yourself):

1. `trials\25\B\saute.shosa` — tempo 96, 2/4, legs turned out, arms low and rounded in front (bras bas). Start in first position. Prepare: bend the knees over 1 beat into a demi-plié (pelvis about 8 cm lower than standing straight) and hold it one more beat. Then 8 small jumps in first, one per beat, each about 8 cm, all the same; in the air the legs are fully stretched; each lands in the demi-plié. Then stretch the legs and stand 2 beats.
   Predictions: (a) during the preparation and before the first take-off, how many separate sinkings of the pelvis, and its lowest point relative to standing straight (cm)? (b) knee bend (degrees) at the top of a jump. (c) the pelvis between two jumps: lowest point relative to standing straight (cm).

2. `trials\25\B\echappe.shosa` — tempo 88, 2/4, turned out, arms bras bas, start in fifth position with the right foot in front. From a demi-plié, jump and land with the feet apart to the sides in second position (about shoulder width plus a little, both feet on the floor, in demi-plié), then jump again and close back into fifth, this time with the LEFT foot in front. Do it 4 times (fronts alternate). One jump per beat.
   Predictions: (a) in second position, the distance between the ankles (cm). (b) after each closing, which foot is in front. (c) does the pelvis travel sideways during the jump into second?

3. `trials\25\B\assemble.shosa` — tempo 96, 2/4, turned out, start in fifth right foot front. Preparation: demi-plié on the last beat before the first jump. Then 4 assemblés: the back foot is thrown out to the side about 45 degrees, the dancer jumps off the other foot, both legs meet stretched in the air, and they land together in fifth in demi-plié with the thrown foot now in FRONT. One every 2 beats. Arms open to the side on the take-off and return low on landing. End standing straight.
   Predictions: (a) front foot after each landing. (b) the highest the thrown ankle gets above the floor (cm). (c) are both feet in the air at the same time in each jump (yes/no, and for how long, s)?

4. `trials\25\B\saute-edit.shosa` — copy saute.shosa and change as little as possible: make the preparation plié deeper (12 cm) and hold it 2 beats instead of 1; make jumps 7 and 8 twice as high as the others; end with a rise onto the toes for 1 beat before standing. Count how many tokens you changed or added.

At the end, write your report to `trials\25\B\report.md` and also return it: for each file, check runs, the diagnostics, the HIT/MISS table, and then a numbered "grammar issues" list (things the grammar did not say, said misleadingly, or could not express). Be concrete and quote the grammar lines that misled you.
