You are a test subject in a writing trial for Shosa, a text language for character motion. You learn the language ONLY from `C:\MyProj\Shosa\trials\26\GRAMMAR_FOR_LLM.md` (read the whole thing first). Do NOT read any other file in C:\MyProj\Shosa (no docs, samples, trials of other numbers or folders, source code, CLAUDE.md). Work only in `C:\MyProj\Shosa\trials\26\A\` (put scratch files in `trials\26\A\scratch\`, images and json in `trials\26\A\out\`).

Tools: the compiler is `C:\MyProj\Shosa\Shosa.Cli\bin\Debug\net10.0\shosa.exe` (run it with no arguments to see its commands: `check` (also `--at`), `sheet` (contact-sheet PNG; look at the PNGs with your Read tool; options like `--step`, `--view side`, `--from`, `--to`), `clip` (per-frame joint positions as JSON), etc.). Use the PowerShell or Bash tool. Do not modify or rebuild the compiler.

Procedure for EACH file (in this order):
1. Before running anything on it, write in `trials\26\A\scratch\notes.txt` a BEFORE section: which words you chose, places where the grammar was unclear to you, the diagnostics you expect, and your answers to the prediction questions.
2. Write the file and run `check`. Fix until there are no diagnostics; count the runs.
3. Answer the prediction questions first from the contact sheet alone (write that down), then measure with `clip` and say HIT or MISS for each.
4. Write an AFTER section: check runs and what each diagnostic was, what you saw that you did not want, surprises, anything you could not express.

Tasks (a game; each says how the motion should look; choose the words yourself):

1. `trials\26\A\soldier.shosa` — tempo 120, two scores (one export each):
   - `Salute`: stands at attention (feet together, arms down by the sides). The right hand comes up to the forehead in half a beat, the elbow out to the side; it stays there 2 beats, then drops sharply back to the side in a quarter beat. The head stays level the whole time.
   - `LookFar`: the left hand shades the eyes at the forehead (over 1 beat); keeping it there, the head turns slowly to the left about 40 degrees (2 beats), then to the right about 40 degrees (4 beats), then back to the front (2 beats), and the hand comes down (1 beat).
   Predictions: (a) In `Salute`, while held, is the right elbow beside the body or in front of it, and how far is the right hand from the nose (cm)? (b) In `LookFar`, does the hand stay at the forehead while the head turns (how far does it move relative to the head, cm)? (c) Any diagnostic?

2. `trials\26\A\greeter.shosa` — tempo 100, a shopkeeper NPC greeting. The right arm starts hanging down at the side, and over 2 beats sweeps out to the side and on up to straight overhead in ONE continuous arc: it must not slow down or pause when it passes the horizontal. Then, overhead, it waves: the arm swings left and right 4 times (one swing per half beat), and finally comes back down the same arc (out to the side, down) over 2 beats, again without pausing at the horizontal.
   Predictions: (a) the speed of the right hand at the moment it passes the horizontal on the way up (m/s), and is it the fastest point of the sweep? (b) In the wave, does the hand stop at each end of a swing? (c) Any diagnostic?

3. `trials\26\A\stance.shosa` — tempo 90, a martial artist in a wide stance, feet about 0.6 m apart, standing with soft knees. Shift the weight onto the right foot (1 beat), onto the left foot (1 beat), back to the middle (1 beat); during these shifts the pelvis should not drop noticeably. Then a deep side lunge to the right: the pelvis about 15 cm lower than standing and over the right foot (1 beat), hold 2 beats, come back up to the middle (1 beat). The feet never move.
   Predictions: (a) during the weight shifts, how far does the pelvis drop (cm) and how far does it move sideways from the middle (cm)? (b) in the lunge, how far from the middle and how low (cm)? (c) is the left leg straight in the lunge?

4. `trials\26\A\greeter-edit.shosa` — copy greeter.shosa and change it as little as possible: the sweep up becomes 1 beat, the arm stays still overhead for 1 beat before waving, and it waves 6 times instead of 4. Count how many tokens you changed or added.

At the end, write your report to `trials\26\A\report.md` and also return it: for each file, check runs, the diagnostics, the HIT/MISS table, and then a numbered "grammar issues" list (things the grammar did not say, said misleadingly, or could not express). Be concrete and quote the grammar lines that misled you.
