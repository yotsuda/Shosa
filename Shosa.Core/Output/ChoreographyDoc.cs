using System.Text;
using Shosa.Core.Semantics;

namespace Shosa.Core.Output;

/// <summary>
/// How to choreograph a whole dance, for an AI that is about to: docs/CHOREOGRAPHY_FOR_LLM.md, after
/// Lily#'s docs/COMPOSE_FOR_LLM.md (its composing guide). The grammar (<see cref="GrammarDoc"/>) says how
/// each thing is written; this says what makes a dance of it, and ends with Shosa's own files of parts
/// (`using "shosa:jazz"`, <see cref="Library"/>), printed from the files themselves, as the catalogue
/// the dance picks its poses and moves from. Kept apart from the grammar so that a small change to a
/// dance does not send it. A test keeps the file in docs/ identical to what this prints.
/// (The owner's report 2026-11-27: Copilot choreographed a 3-minute song with three moves of its own,
/// repeated for three minutes, and took a long time over it.)
/// </summary>
public static class ChoreographyDoc
{
    public static string Render()
    {
        var sb = new StringBuilder();
        sb.Append(Guide);
        foreach (string name in Library.Names)
        {
            sb.Append("\n### `using \"shosa:").Append(name).Append("\"`\n\n```shosa\n");
            string text = Library.Read(name)!.ReplaceLineEndings("\n").TrimEnd('\n');
            sb.Append(text).Append("\n```\n");
        }
        return sb.ToString();
    }

    private const string Guide = """
        # Choreographing in Shosa — a guide for LLMs

        Read this (the tool `shosa_guide`, or `shosa guide`) before you choreograph a dance — to a song
        or not — longer than a few bars. The grammar (`shosa_grammar`, `shosa grammar`) says how each
        thing is written; this says what makes it a dance, how to get a long one written in good time,
        and ends with Shosa's vocabulary files: poses and moves of a style, ready to use by name.

        **These are a choreographer's habits, not a form to fill.** Break one where the dance is better
        for it, and say which in the plan. Make a dance you would enjoy watching.

        ## Answer each reply within five minutes, thinking included

        A long think can be cut off by the connection and the whole reply is lost: the user waits and gets
        nothing. Think about this step only and leave the next step to its own reply. Never write a whole
        song's dance in one reply or one edit: the plan, then the head of the file, then a section (or two
        short ones) a step.

        ## Steps

        1. **Read**: the grammar, unless it is in this conversation; the song (`shosa_song`); this guide.
        2. **The plan**, in your reply, short lines under the headings below. It is your working notes.
        3. **The head of the file**: `music`, the `using "shosa:…"` lines of the styles you chose, the
           role and dancer lines (keep what Add a Dance… wrote), and the few moves of your own the plan
           names (the chorus's combination, the verse's groove). Check it.
        4. **A section a step**: write it with `shosa_edit`, fix until it says no problems, and look at THAT
           section's sheet only (`shosa_sheet` with `from` and `to` its beats): a whole song's sheet is slow
           and too small to see. Say in a line what it does, then go on to the next.
        5. At the end: the whole file checked once more, and tell the user to watch it in the preview.

        ## The plan

        - **STYLE** — the dancer (who, what energy) and the vocabulary files: an idol or a cute pop song
          `basics` (with a little `jazz`); "like Michael Jackson", 80s pop, funk, sharp and cool `pop` (with
          `basics`); Broadway, theatre, lyrical, sassy `jazz`; classical, elegant, a waltz `ballet`. Mixing
          is fine: a dance is one dancer, not one file.
        - **MAP** — every section the song plays: its bars, an energy from 1 to 5, and its ROLE: intro,
          verse, pre-chorus, chorus, bridge, instrumental, outro. **Sections with other names that are the
          same music are one role** (a transcribed song may call them A, A2 … A8 and C, C2, C3): find them
          by their bars and words, and dance each role from one move (below).
        - **MOTIFS** — the chorus's combination (four or eight bars, the dance's hook, the same every
          chorus, so the audience knows it when it comes back), the verse's groove, the bridge's change.
          By name: the vocabulary's moves, and your own made of them.
        - **FLOOR** — where the dancer goes: the verse near the spot, the chorus travelling (side, forward
          and back, a turn), the bridge to another place or facing (a diagonal, a profile). The levels:
          low (a plié, `height { low }`), standing, high (a rise, a jump).
        - **ACCENTS** — the big moments: the held syllables (`~`), the first syllable after a rest (`/`), the
          song's hits, the last chorus as the high point, the final pose.
        - **ENDING** — the last pose, on the song's last note, held.

        ## A long song in good time

        - **A role is a move**: write the chorus's eight bars once as `move chorus { body { … } }` (rows for
          the steps, the arms, the head), and every chorus is `body { chorus }` — one line. A chorus that
          should differ the third time is the move with a row written over it: `body { chorus }` and
          `arms { … }` beside it. So a 3-minute song is four or five moves and a dozen short sections, not
          a hundred bars written out.
        - Use the vocabulary's names instead of writing poses and moves: one word for a step pattern,
          another for the arms over it.
        - `%` repeats a bar; `{ … } q q q` repeats a group; `verse 2 { … }` is what a section played again
          does differently (see the grammar's "Doing it again").

        ## Variety — what keeps a dance from being one pattern

        - **Layers**: a dance is feet × arms × head × height × facing. Change one layer every two bars,
          two every four, all of them where a section changes. The same steps under new arms is a new
          figure; the same arms on new steps too. Most vocabulary moves are feet only or arms only so that
          they combine: say both.
        - **No feet pattern more than two bars running** but a groove under arms that change; no section
          with the same feet AND arms as the one before.
        - **Repeat at the phrase, never at the beat**: a chorus that comes back the same is good (it is
          recognised); a two-beat figure for eight bars is not.
        - **Contrast**: sharp against smooth (`@sharp`, `@smooth`), big against small, travelling against
          still. A held pose on a held note is one of the strongest things a dance does — stillness is a
          move.
        - **Directions**: the front most of the time, but the diagonals and a profile for a phrase; a turn
          at most every few bars, not every bar.
        - **Count in eights** (two bars of 4/4): change on 1 of a phrase, accent on 1 and the "and" before it.

        ## Dancing to the music

        - **Tempo**: above about 140 a step every two beats and the arms on the beats; below about 90 big,
          slow, sustained moves (`@smooth`, the long lengths), the steps fewer.
        - **Energy follows the MAP**: low in the verse (small steps, arms near the body), rising in the
          pre-chorus, the biggest in the chorus (travelling, arms overhead, turns, a jump), and the last
          chorus bigger still.
        - **Words**: the mouth follows the song by itself (`sings PART`). A gesture that shows a word now and
          then reads well — a point on "you", a hand to the chest on "I" or "heart", arms up on "sky" — but
          not on every line.
        - **Bars without words** (the intro, an instrumental) are for the steps and the travelling.

        ## What reads as good dancing

        - The face to the audience; the head following the hand that leads (`head { … }` towards it).
        - Clear changes of weight: a step lands, then the next one goes.
        - Each phrase ending on a shape held for a moment, not drifting into the next.
        - The shapes big enough to see from the back of a hall: arms away from the body, a lunge that is a
          lunge.

        ## The vocabulary files

        `using "shosa:NAME"` at the top of a dance brings in one of these: its poses and moves are then
        words of the dance, as if written in it. Several may be used together (no name is in two of them).
        A move of these goes after any other: each starts and ends standing on both feet, facing where it
        began, unless its comment says otherwise. Write a length to fit a move to the music (`jazzSquare:8`
        is it at half speed), `@mirror` for the other side, and a narrower track to take only part of one
        (`arms { … }` of a whole-body move). Their comments say each one's beats and tracks.

        What trips a dance written with them (each met while they were made):
        - **A move longer than the bar crosses its bar line tied**: an 8-beat move in 4/4 is
          `kickSpinPoint:4~ | :4 |`, and the tie comes before an annotation (`kickSpinPoint:4~@mirror | :4 |`).
          Or leave the bar lines out of that row.
        - **Feet patterns go in `body`**: `steps { stepTouch }` keeps only its steps and drops its height;
          a leg move (`march`) is not steps at all. `body { stepTouch }` with `arms { … }` beside it.
        - **`@sharp` goes in 0.15 s**, so a shape hit `@sharp` from far away is too fast (SH2003). Hit a pose
          from a shape near it, or give the way a short `then`: `skyPoint:7/2 then:1/2` arrives on the beat.
        - **A shape held needs `then`**: an arm or the height sets off for the next word as soon as it
          arrives, so `skyPoint:4 down` drifts down for four beats; `skyPoint:1 then:3 down` holds it.
        - A pose that writes `height`, laid over a feet move in the same `body` row, wins over the move's
          height: put the pose's arms in an `arms` row instead (`arms { skyPoint:4 }`).

        """;
}
