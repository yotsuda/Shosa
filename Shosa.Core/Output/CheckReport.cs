using System.Globalization;
using Shosa.Core.Motion;
using Shosa.Core.Semantics;

namespace Shosa.Core.Output;

/// <summary>
/// What `shosa check` says, as lines of text: the one report the command line prints and the
/// language server hands to an AI assistant in the editor (`shosa/check`), so the two read the same.
/// </summary>
public static class CheckReport
{
    /// <summary>A line for each diagnostic: <c>FILE(line,column): severity CODE: message</c>.</summary>
    public static IEnumerable<string> Problems(ShosaCompilation compilation, string text, string path)
    {
        foreach (var diagnostic in compilation.Diagnostics)
        {
            var (line, column) = ShosaCompilation.LineColumn(text, diagnostic.Span.Start);
            yield return $"{path}({line},{column}): {diagnostic.Severity.ToString().ToLowerInvariant()} {diagnostic.Code}: {diagnostic.Message}";
        }
    }

    /// <summary>
    /// How far a dancer is at the end from where it began, said in its own first directions:
    /// "ends 1 m forward and 0.35 m to its left of where it began, turned 90° to its left". The
    /// place is the middle between the two ankles (the hips sway with the weight). A loop travels
    /// that far every time round; `inplace` takes the travel out, and it is said all the same.
    /// </summary>
    private static string? Travel(MotionClip clip, ClipFigure figure)
    {
        if (figure.Frames.Count < 2)
            return null;
        int last = figure.Frames.Count - 1;
        Vec3 Feet(int frame) => (figure.Joint(frame, Rig.LAnkle) + figure.Joint(frame, Rig.RAnkle)) * 0.5;
        // Where the feet and the turns put it, not the line of the hips: the hips can be turned over the feet.
        Vec3 Facing(int frame) => Vec3.Forward(figure.Facing[frame == 0 ? 0 : 1]);

        var moved = Feet(last) - Feet(0);
        if (clip.Inplace && figure.Velocity is [var vx, var vz])
            moved += new Vec3(vx, 0, vz) * clip.SecondsAt(clip.TotalBeats);
        Vec3 fwd = Facing(0), left = Vec3.Up.Cross(fwd), now = Facing(last);
        double ahead = Math.Round(moved.Dot(fwd), 2), aside = Math.Round(moved.Dot(left), 2);
        double turned = Math.Round(Math.Atan2(now.Dot(left), now.Dot(fwd)) * 180 / Math.PI);

        static string M(double metres) => Math.Abs(metres).ToString("0.##", CultureInfo.InvariantCulture) + " m";
        var parts = new List<string>();
        if (Math.Abs(ahead) >= 0.02)
            parts.Add($"{M(ahead)} {(ahead > 0 ? "forward" : "back")}");
        if (Math.Abs(aside) >= 0.02)
            parts.Add($"{M(aside)} to its {(aside > 0 ? "left" : "right")}");
        string turn = Math.Abs(turned) < 1 ? ""
            : Math.Abs(turned) >= 179.5 ? ", turned half round"
            : $", turned {Math.Abs(turned).ToString(CultureInfo.InvariantCulture)}° to its {(turned > 0 ? "left" : "right")}";

        if (clip.Loop)
        {
            string taken = clip.Inplace ? " (taken out of the movement: inplace)" : "";
            return parts.Count == 0 ? $"loops on the spot{turn}" : $"travels {string.Join(" and ", parts)} each time round{turn}{taken}";
        }
        return parts.Count == 0 ? $"ends where it began{turn}" : $"ends {string.Join(" and ", parts)} of where it began{turn}";
    }

    /// <summary>
    /// The moments of `--at 3,4.5,0.75s` (the language server's `at` is the same text): beats, or
    /// seconds with an s, with commas between. Null, with what was not a number, if one is not.
    /// </summary>
    public static IReadOnlyList<(double Value, bool Seconds)>? Moments(string said, out string? wrong)
    {
        var moments = new List<(double, bool)>();
        wrong = null;
        foreach (string moment in said.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            bool seconds = moment.EndsWith('s');
            if (!double.TryParse(seconds ? moment[..^1] : moment, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                wrong = moment;
                return null;
            }
            moments.Add((value, seconds));
        }
        return moments;
    }

    /// <summary>
    /// `shosa check --at`: how each dancer's body is turned at the moments asked for (a beat, or
    /// seconds), a line for each score and dancer. The hips, the shoulders and the face are counted
    /// as the language counts them — from the way the feet face, on the floor — so what was written
    /// (`hips { left.20 }`, a `twist` on top, `head { fwd.right(30) }`) is read back as numbers.
    /// </summary>
    public static IEnumerable<string> At(ShosaCompilation compilation, IReadOnlyList<(double Value, bool Seconds)> moments)
    {
        static string N(double value) => Math.Round(value, 3).ToString(CultureInfo.InvariantCulture);
        static string Turned(double degrees)
        {
            double size = Math.Round(Math.Abs(degrees), 1);
            return size == 0 ? "0°" : $"{size.ToString(CultureInfo.InvariantCulture)}° {(degrees > 0 ? "left" : "right")}";
        }

        foreach (var clip in compilation.Clips)
        {
            string label = clip.Name == "" ? "score" : clip.Name;
            foreach (var (value, seconds) in moments)
            {
                double beat = seconds ? TempoMap.Beat(TempoMap.Of(clip.Tempos, clip.Tempo), value) : value;
                if (beat < -1e-9 || beat > clip.TotalBeats + 1e-9)
                {
                    yield return $"  {label} at {(seconds ? N(value) + " s" : "beat " + N(value))}: not in the score ({N(clip.TotalBeats)} beats, {N(clip.SecondsAt(clip.TotalBeats))} s)";
                    continue;
                }
                beat = Math.Clamp(beat, 0, clip.TotalBeats);
                foreach (var figure in clip.Figures)
                {
                    var angles = figure.Angles(clip.FrameAt(beat));
                    string who = clip.Figures.Count > 1 ? figure.Name + ": " : "";
                    string up = Math.Round(Math.Abs(angles.FaceUp), 1) == 0 ? ""
                        : $" and {Math.Round(Math.Abs(angles.FaceUp), 1).ToString(CultureInfo.InvariantCulture)}° {(angles.FaceUp > 0 ? "up" : "down")}";
                    string feet = Math.Round(Math.Abs(angles.Feet), 1) == 0 ? "" : $" (the feet face {Turned(angles.Feet)} of the front)";
                    yield return $"  {label} at beat {N(beat)} = {N(clip.SecondsAt(beat))} s: {who}hips {Turned(angles.Hips)}, shoulders {Turned(angles.Shoulders)}, face {Turned(angles.Face)}{up}{feet}";
                }
            }
        }
    }

    /// <summary>
    /// <c>FILE: no problems</c> when there is no diagnostic; then every score: how long it is
    /// and how many dance in it, and a line for each section it plays — where it falls, how long
    /// it takes, the tempo and the time it is played in. A cut of the piece may begin in another
    /// tempo and time than the file, and a writer counts in bars and checks a speed in seconds:
    /// all of it is said, for every score. Then every mark with its beat and second.
    /// </summary>
    public static IEnumerable<string> Scores(ShosaCompilation compilation, string path)
    {
        static string Label(string name) => name == "" ? "score" : name;
        static string N(double value) => Math.Round(value, 3).ToString(CultureInfo.InvariantCulture);

        if (compilation.Diagnostics.Count == 0)
            yield return $"{path}: no problems";

        foreach (var clip in compilation.Clips)
        {
            string Seconds(double from, double to) => (clip.SecondsAt(to) - clip.SecondsAt(from)).ToString("0.##", CultureInfo.InvariantCulture) + " s";
            string head = clip.Music is { } song ? Label(clip.Name) + " to " + song.File : Label(clip.Name);
            yield return $"  {head}: {N(clip.TotalBeats)} beats, {Seconds(0, clip.TotalBeats)}, {clip.Figures.Count} dancer{(clip.Figures.Count == 1 ? "" : "s")}";

            var tempos = TempoMap.Of(clip.Tempos, clip.Tempo);
            var meters = Bars.Of(clip.Meters, clip.BeatsPerBar);
            var rows = new List<string[]>();
            foreach (var s in clip.Sections)
            {
                // With the bars, since a writer counts in them: "upbeat of 1, then 8 bars from beat 1".
                int bars = Bars.Count(meters, s.T0 + s.Pickup, s.T1);
                string count = $"{bars} bar{(bars == 1 ? "" : "s")}";
                if (s.Pickup > 0)
                    count = $"upbeat of {N(s.Pickup)}, then {count} from beat {N(s.T0 + s.Pickup)}";
                // A song may change tempo or time inside a section: each change with its beat, since the bars are counted by them.
                bool Inside(double beat) => beat > s.T0 + 1e-9 && beat < s.T1 - 1e-9;
                string tempo = string.Concat(tempos.Where(t => Inside(t.Beat)).Select(t => $", {N(t.Bpm)} from beat {N(t.Beat)}"));
                var first = Bars.MeterAt(meters, s.T0);
                string time = string.Concat(meters.Where(m => Inside(m.Beat)).Select(m => $", {m.BeatsPerBar}/{m.BeatUnit} from beat {N(m.Beat)}"));
                // In a compound time (6/8, 9/8, 12/8) the beat is the quaver and the pulse a dancer
                // feels is the dotted crotchet: the tempo is said by both, "at 288 (4. = 96)".
                double bpm = TempoMap.BpmAt(tempos, s.T0);
                string felt = first is { BeatUnit: 8, BeatsPerBar: 6 or 9 or 12 } ? $" (4. = {N(bpm / 3)})" : "";
                rows.Add([s.Name, $"{N(s.T0)}-{N(s.T1)}", count, Seconds(s.T0, s.T1),
                    $"at {N(bpm)}{felt}{tempo}", $"{first.BeatsPerBar}/{first.BeatUnit}{time}"]);
            }
            foreach (var row in rows)
                yield return ("    " + string.Join("   ", row.Select((cell, i) => cell.PadRight(rows.Max(r => r[i].Length))))).TrimEnd();

            // The first word of a row is where the body is when the score starts, so a step, a jump, a
            // shift or a turn written there is not seen. That is right for a stance and a surprise for a
            // walk ("two steps forward, two back" ends a step away from where it began). Beginning with a
            // step is how most scores begin, so it is not a diagnostic; it is said here, where the
            // writer reads what the score came to. A loop makes the way to its first word at its end.
            if (compilation.Score.Clips.FirstOrDefault(ir => ir.Name == clip.Name) is { Loop: false } written)
            {
                foreach (var dancer in written.Dancers)
                {
                    if (!dancer.Tracks.TryGetValue(Channels.Support, out var segs))
                        continue;
                    foreach (var seg in segs.Where(s => s.T0 == 0 && s.T1 == 0))
                    {
                        string? what = seg.Kind switch
                        {
                            SegKinds.Step when !seg.Stance => "step",
                            SegKinds.Jump => "jump",
                            SegKinds.Shift when seg.Foot != "center" => "shift",
                            SegKinds.Turn => "turn",
                            _ => null,
                        };
                        string text = compilation.Tree.Text;
                        if (what is not null && seg.Src >= 0 && seg.Src + seg.Len <= text.Length)
                            yield return $"    {dancer.Name} starts with `{text.Substring(seg.Src, seg.Len)}` already made: the first {what} is not seen (right for a stance, or a count the foot only stands on; to see it, a `then` before it)";
                    }
                }
            }

            // Where each dancer is at the end, from where it began: "back where it started" is a thing a
            // writer wants to be sure of, and the words do not show it (a step is placed from the other
            // foot, a stance leaves a foot a stride ahead).
            foreach (var figure in clip.Figures)
            {
                if (Travel(clip, figure) is { } travel)
                    yield return $"    {figure.Name} {travel}";
            }
        }

        // Marks do nothing to the movement, so this is the only place a writer sees where they fell.
        foreach (var clip in compilation.Clips)
        {
            foreach (var mark in clip.Marks)
            {
                // A section the score plays more than once gives its marks each time, under the same name: say which time this is.
                string play = clip.Marks.Any(m => m.Section == mark.Section && m.Play != mark.Play) ? $"   {mark.Section}, play {mark.Play}" : "";
                string who = (clip.Figures.Count > 1 ? $" ({mark.Figure})" : "") + play;
                yield return FormattableString.Invariant(
                    $"  {Label(clip.Name)} !{mark.Name} at beat {Math.Round(mark.Beat, 3)} = {Math.Round(mark.Seconds, 3)} s{who}");
            }
        }
    }
}
