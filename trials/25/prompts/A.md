You are a test subject in a writing trial for Shosa, a text language for character motion. You learn the language ONLY from `C:\MyProj\Shosa\trials\25\GRAMMAR_FOR_LLM.md` (read the whole thing first). Do NOT read any other file in C:\MyProj\Shosa (no docs, samples, trials of other numbers or folders, source code, CLAUDE.md). Work only in `C:\MyProj\Shosa\trials\25\A\` (put scratch files in `trials\25\A\scratch\`, images and json in `trials\25\A\out\`).

Tools: the compiler is `C:\MyProj\Shosa\Shosa.Cli\bin\Debug\net10.0\shosa.exe` (run it with no arguments to see its commands: `check`, `sheet` (contact-sheet PNG; look at the PNGs with your Read tool; options like `--step`, `--view side`, `--from`, `--to`), `clip` (per-frame joint positions as JSON), etc.). Use the PowerShell or Bash tool. Do not modify or rebuild the compiler.

Procedure for EACH file (strictly in this order):
1. Before running anything on it, write in `trials\25\A\scratch\notes.txt` a BEFORE section: your plan, hesitations (where the grammar was unclear), the diagnostics you expect, and your answers to the prediction questions.
2. Write the file and run `check`. Fix until there are no diagnostics; count the runs.
3. Answer the prediction questions first from the contact sheet alone (write that down), then measure with `clip` and say HIT or MISS for each.
4. Write an AFTER section: check runs and what each diagnostic was, what you saw that you did not want, surprises, anything you could not express.

Tasks (each says how the motion should look; choose the words yourself):

1. `trials\25\A\hero.shosa` — a game character, tempo 120, two scores (one export each):
   - `Idle` (loops): stands with soft knees, and every beat it does a tiny bounce in place where both feet just leave the floor by about 3 cm. 4 beats.
   - `Charge`: from standing, it sinks into a deep crouch (pelvis about 15 cm lower than standing straight) over 1 beat and stays down 2 beats gathering power, then leaps straight up as high as it can given the time, about 30 cm, and lands back in place with soft knees and stands 1 beat. The crouch it is holding should be the take-off; it must not sink a second time before leaving the floor.
   Predictions: (a) In `Charge`, how many times does the pelvis go down before the take-off, and what is its lowest point relative to standing straight (cm)? (b) At the top of the leap, are the knees straight or bent? (c) In `Idle`, the ankle's highest point above its standing height in each bounce (cm).

2. `trials\25\A\rope.shosa` — a villager skipping rope, tempo 120, 4/4. Bar 1: both feet together, jumps every half beat, each about 4 cm high, 8 times, all the same height. Bar 2: the same rhythm but alternating feet (hop on the right, hop on the left, ...), each about 3 cm. Bar 3: two bigger jumps on both feet, one per two beats, about 15 cm, then stands. Arms: hands low beside the hips, forearms out a little, turning the rope (you may keep them still).
   Predictions: (a) bar 1, each jump's height of the pelvis above standing (cm), and how long the feet are on the floor between two jumps (s). (b) Does any diagnostic appear, and which? (c) bar 3, the lowest pelvis before each big jump relative to standing (cm).

3. `trials\25\A\frog.shosa` — a children's game "frog jumps", tempo 100. The child squats very low (pelvis about 30 cm below standing, hands touching near the floor in front of the feet) over 2 beats, then three frog jumps forward, 0.5 m each, every 2 beats, landing back in the low squat each time; after the third, it stands up over 2 beats. Predictions: (a) between landing and the next take-off, does the pelvis rise or sink, and by how much? (b) where does it end relative to where it began?

4. `trials\25\A\rope-edit.shosa` — copy rope.shosa and change it as little as possible: bar 1 jumps become 5 cm (if the compiler says it cannot, write down what it said and what you did), bar 3 becomes three big jumps evenly spaced in the bar, and it ends on one foot with the other foot raised at the front of the standing ankle. Count how many tokens you changed or added.

At the end, write your report to `trials\25\A\report.md` and also return it: for each file, check runs, the diagnostics, the HIT/MISS table, and then a numbered "grammar issues" list (things the grammar did not say, said misleadingly, or could not express). Be concrete and quote the grammar lines that misled you.
