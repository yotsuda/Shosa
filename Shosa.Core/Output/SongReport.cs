using System.Globalization;
using System.Text;
using Shosa.Core.Music;
using Shosa.Core.Semantics;

namespace Shosa.Core.Output;

/// <summary>
/// What `shosa song FILE` prints: a song as a dance is written to it — its time and tempo, its
/// sections as played with their bars and seconds, who sings, and the words bar by bar with the
/// beat each syllable falls on. For an AI (and a person) about to choreograph a song: a move is
/// placed on a word by its bar and beat, which the song's own text does not show without reading
/// Lily#. The language server gives the same report to an AI assistant (`shosa/songReport`).
/// DESIGN.md 123.
/// </summary>
public static class SongReport
{
    /// <summary>A syllable held this long or longer is said with its length.</summary>
    private const double Held = 1;
    /// <summary>A silence this long or longer between two syllables is a breath, said as " /".</summary>
    private const double Breath = 1;

    /// <summary>
    /// The song a file names: a .lys itself, or the song a .shosa is danced to (its first score's `music`,
    /// in the dance's own order when it has one). The label is the song's file as written.
    /// Null with what went wrong when there is no song to report.
    /// </summary>
    public static (SongInfo Song, string Label)? Read(string path, string text, string? score, out string? problem)
    {
        problem = null;
        string folder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        var reader = new SongReader(folder);
        string file;
        IReadOnlyList<string>? order = null;
        if (path.EndsWith(".lys", StringComparison.OrdinalIgnoreCase))
            file = Path.GetFileName(path);
        else
        {
            var compilation = ShosaCompilation.Compile(text, score, reader.Read, UsingSource.For(path));
            if (compilation.Clips.FirstOrDefault(c => c.Music is not null)?.Music is not { } music)
            {
                problem = $"{Path.GetFileName(path)} is danced to no song: it has no `music \"SONG.lys\"` line (or the score asked for has none). Give the .lys itself.";
                return null;
            }
            file = music.File;
            score = music.Score;
            order = music.Order;
        }
        var song = reader.Read(file, score, order);
        if (song is null || song.Error is { } error)
        {
            problem = song?.Error ?? "Nothing here reads songs.";
            return null;
        }
        return (song, file);
    }

    public static IEnumerable<string> Lines(SongInfo song, string label)
    {
        static string N(double value) => Math.Round(value, 3).ToString(CultureInfo.InvariantCulture);
        var tempos = TempoMap.Of(song.Tempos, song.Tempo);
        var meters = Bars.Of(song.Meters, song.BeatsPerBar, song.BeatUnit);
        string Seconds(double beat) => TempoMap.Seconds(tempos, beat).ToString("0.#", CultureInfo.InvariantCulture);

        // The plays as sections of a clip: where each begins, its upbeat, and the number of its first bar.
        var sections = new List<SectionIr>();
        double at = 0;
        int bar = 1;
        foreach (var play in song.Plays)
        {
            sections.Add(new SectionIr(play.Name, at, at + play.Beats, 0, 0, play.Pickup, bar));
            bar += Bars.Count(meters, at + play.Pickup, at + play.Beats);
            at += play.Beats;
        }
        double total = at;
        int bars = bar - 1;
        // A section the song plays more than once is told apart as the dance tells it: `verse 2 { … }` in its row.
        var labels = new List<string>();
        foreach (var play in song.Plays)
        {
            int time = labels.Count(l => l == play.Name || l.StartsWith(play.Name + ", verse ", StringComparison.Ordinal)) + 1;
            labels.Add(song.Plays.Count(p => p.Name == play.Name) > 1 ? $"{play.Name}, verse {time}" : play.Name);
        }

        var first = meters[0];
        string unit = first.BeatUnit switch { 2 => "a minim", 4 => "a crotchet", 8 => "a quaver", 16 => "a semiquaver", _ => $"a 1/{first.BeatUnit} note" };
        string felt = first is { BeatUnit: 8, BeatsPerBar: 6 or 9 or 12 } ? $" (4. = {N(tempos[0].Bpm / 3)})" : "";
        // A recording has a tempo for every beat: its first says little, the mean says how fast it goes.
        string pace = song.Recording is not null && total > 0
            ? $"at about {Math.Round(total * 60 / TempoMap.Seconds(tempos, total)).ToString(CultureInfo.InvariantCulture)}"
            : $"at {N(tempos[0].Bpm)}{felt}";
        yield return $"song {Path.GetFileName(label)}: {first.BeatsPerBar}/{first.BeatUnit} {pace} (a beat is {unit}), {bars} bar{(bars == 1 ? "" : "s")}, {N(total)} beats, {Seconds(total)} s";
        if (song.Recording is { } recording)
            yield return $"  made from the recording {Path.GetFileName(recording.Audio)}: a dance keeps its beats (a tempo for every beat), and the preview plays it";
        else if (song.RecordingError is { } why)
            yield return $"  the recording beside it is not followed: {why}";
        var changes = song.Recording is null ? tempos.Skip(1).Select(t => $"{N(t.Bpm)} from beat {N(t.Beat)}").ToList() : [];
        if (changes.Count > 0)
            yield return $"  the tempo changes: {string.Join(", ", changes)}";
        if (meters.Count > 1)
            yield return $"  the time changes: {string.Join(", ", meters.Skip(1).Select(m => $"{m.BeatsPerBar}/{m.BeatUnit} from beat {N(m.Beat)}"))}";
        if (song.Scores.Count > 1)
            yield return $"  its scores: {string.Join(", ", song.Scores.Select(s => s == "" ? "(the unnamed one)" : s))}; a dance names one with `music \"{label}\" score NAME`";

        // The sections, as a dance writes them: `section NAME { … }`, bar by bar from its first bar line.
        yield return "  sections as played (a dance's `section NAME` is written from its first bar line; an upbeat comes before it):";
        var rows = sections.Select((s, i) =>
        {
            int count = Bars.Count(meters, s.T0 + s.Pickup, s.T1);
            string length = $"{count} bar{(count == 1 ? "" : "s")}" + (s.Pickup > 0 ? $" after an upbeat of {N(s.Pickup)}" : "");
            return new[] { labels[i], $"bars {s.Bar}-{s.Bar + count - 1}", length, $"beats {N(s.T0)}-{N(s.T1)}", $"{Seconds(s.T0)}-{Seconds(s.T1)} s" };
        }).ToList();
        foreach (var row in rows)
            yield return ("    " + string.Join("   ", row.Select((cell, i) => cell.PadRight(rows.Max(r => r[i].Length))))).TrimEnd();
        var unplayed = song.Sections.Where(name => song.Plays.All(p => p.Name != name)).ToList();
        if (unplayed.Count > 0)
            yield return $"  sections the song has but does not play: {string.Join(", ", unplayed)}";

        // Who sings, and the words where they fall.
        if (song.Sung is null)
        {
            yield return "  what is sung: not said by this Lily# (update the Lily# extension to see the words here)";
            yield break;
        }
        if (song.Sung.Count == 0 || song.Sung.All(s => !s.Read().Any()))
        {
            yield return "  nothing is sung: the song has no `singer` line";
            yield break;
        }
        yield return $"  sung by: {string.Join(", ", song.Sung.Select(s => s.Singer is { } name ? $"{s.Part} ({name})" : s.Part))}. In a dance, `dancer NAME humanoid \"MODEL.vrm\" sings PART` moves that dancer's mouth to the part's words.";
        yield return "  the words, bar by bar: the bar of its section, with the song's beat its bar line is on (as check, the sheet and --from count);";
        yield return "  each syllable after the beat of the bar it is sung on, counted from 1; ~N: held N beats;";
        yield return "  / before a syllable: a rest of a beat or more before it, since the last syllable ended; parts that sing the same are joined with +:";

        // Each part's syllables as tokens, bar by bar: (section index, bar in section) → text.
        var byBar = new SortedDictionary<(int Section, int Bar), List<(string Part, string Tokens, string Words)>>();
        // Where each bar line is in the song's beats, as `check`, the sheet and --from/--to count them.
        var barLines = new Dictionary<(int, int), double>();
        foreach (var part in song.Sung)
        {
            var syllables = part.Read().OrderBy(s => s.Beat).ToList();
            var tokens = new Dictionary<(int, int), (StringBuilder Tokens, StringBuilder Words)>();
            double lastEnd = double.NegativeInfinity;
            foreach (var (beat, beats, word) in syllables)
            {
                int section = sections.FindLastIndex(s => s.T0 <= beat + 1e-9);
                if (section < 0)
                    continue;
                var (global, into) = Bars.At(sections, meters, beat);
                var key = (section, global - sections[section].Bar + 1);
                // (An upbeat's bar line is where its whole bar would begin, so the beat of a syllable is still that + its beat − 1.)
                barLines.TryAdd(key, beat - into);
                if (!tokens.TryGetValue(key, out var built))
                    tokens[key] = built = (new StringBuilder(), new StringBuilder());
                if (beat - lastEnd >= Breath - 1e-9 && lastEnd > double.NegativeInfinity)
                    built.Tokens.Append(built.Tokens.Length == 0 ? "/ " : " /");
                if (built.Tokens.Length > 0 && built.Tokens[^1] != ' ')
                    built.Tokens.Append(' ');
                built.Tokens.Append(N(into + 1)).Append(word);
                if (beats >= Held - 1e-9)
                    built.Tokens.Append('~').Append(N(beats));
                built.Words.Append(word);
                lastEnd = beat + beats;
            }
            foreach (var (key, built) in tokens)
            {
                if (!byBar.TryGetValue(key, out var parts))
                    byBar[key] = parts = [];
                parts.Add((part.Part, built.Tokens.ToString(), built.Words.ToString()));
            }
        }

        for (int s = 0; s < sections.Count; s++)
        {
            var lines = byBar.Where(e => e.Key.Section == s).ToList();
            if (lines.Count == 0)
            {
                yield return $"    {labels[s]}: no words";
                continue;
            }
            yield return $"    {labels[s]}";
            foreach (var (key, parts) in lines)
            {
                string where = (key.Bar == 0 ? "upbeat" : $"bar {key.Bar}") + $" (beat {N(barLines[key])})";
                foreach (var group in parts.GroupBy(p => p.Tokens))
                    yield return $"      {where,-16} {string.Join("+", group.Select(p => p.Part))}: {group.Key}   \"{group.First().Words}\"";
            }
        }
    }
}
