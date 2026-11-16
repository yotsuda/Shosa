You are a test subject in a writing trial for Shosa, a text language for character motion. You learn the language ONLY from `C:\MyProj\Shosa\trials\28\GRAMMAR_FOR_LLM.md` (read the whole thing first). Do NOT read any other file in C:\MyProj\Shosa (no docs, samples, trials of other numbers or folders, source code, CLAUDE.md). Work only in `C:\MyProj\Shosa\trials\28\A\` (put scratch files in `trials\28\A\scratch\`, images and json in `trials\28\A\out\`).

Tools: the compiler is `C:\MyProj\Shosa\Shosa.Cli\bin\Debug\net10.0\shosa.exe` (run it with no arguments to see its commands: `check` (also `--at`), `sheet` (contact-sheet PNG; look at the PNGs with your Read tool; options like `--step`, `--view side`, `--from`, `--to`), `clip` (per-frame joint positions as JSON), etc.). Use the PowerShell or Bash tool. Do not modify or rebuild the compiler.

Procedure for EACH file (in this order):
1. Before running anything on it, write in `trials\28\A\scratch\notes.txt` a BEFORE section: which words you chose, places where the grammar was unclear to you, the diagnostics you expect, and your answers to the prediction questions.
2. Write the file and run `check`. Fix until there are no diagnostics; count the runs.
3. Answer the prediction questions first from the contact sheet alone (write that down), then measure with `clip` and say HIT or MISS for each.
4. Write an AFTER section: check runs and what each diagnostic was, what you saw that you did not want, surprises, anything you could not express.

Tasks (a game; each says how the motion should look; choose the words yourself):

1. `trials\28\A\patrol.shosa` — tempo 110, a looping patrol for a guard NPC, written in place-independent form so the game moves the character (the loop must join). Four ordinary walking steps forward, one per beat; then, standing on both feet, turn round to face back over 2 beats; four steps forward again (now going back the way it came), one per beat; turn round again over 2 beats. The walk should look like ordinary walking: the body keeps going over the feet, it does not stop at every step.
   Predictions: (a) During the four steps, does the pelvis ever stop moving forward (slowest forward speed vs. average, m/s)? (b) For how long per step are both feet on the floor at once (s)? (c) When does each turn start and end (beats)? (d) Any diagnostic?

2. `trials\28\A\door.shosa` — tempo 90. From standing, walk three steps forward (one per beat) and stop at a door with the feet side by side; the stop must be a real stop: the body comes to rest. Wait one beat. Then the right hand reaches forward to a handle at about hip height over 1 beat, pulls it back toward the body over 1 beat, and drops to the side.
   Predictions: (a) Does the body keep its walking pace until the last step and then stop, or does it slow during the walk (pelvis forward speed at each step)? (b) When the feet come side by side, which foot moves, and when (beats)? (c) Where is the right hand at the reach (cm in front of the pelvis, cm above the floor)? (d) Any diagnostic?

3. `trials\28\A\rope.shosa` — tempo 120, skipping a rope: eight small jumps on both feet, one landing per beat, each about 5 cm off the floor, feet together; the hands make small circles at the hips (one per jump). Then stand still for one beat.
   Predictions: (a) Between two landings, how many times does the pelvis go down and come up again? (b) How deep does it go (cm below standing straight)? (c) At the top of a jump, are the feet level or pointed (how far is the toe below the ankle, cm, against standing)? (d) Any diagnostic?

4. `trials\28\A\patrol-edit.shosa` — copy patrol.shosa and change it as little as possible so that the guard now marches stiffly: on each step it comes to a full stop for a moment (about half a beat) before the next step sets out. Keep the turns as they are. Count how many tokens you changed or added. Then measure: does the pelvis now stop at each step?

At the end, write your report to `trials\28\A\report.md` and also return it: for each file, check runs, the diagnostics, the HIT/MISS table, and then a numbered "grammar issues" list (things the grammar did not say, said misleadingly, or could not express). Be concrete and quote the grammar lines that misled you.