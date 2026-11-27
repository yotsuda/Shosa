using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Shosa.Core.Motion;

namespace Shosa.Lsp;

/// <summary>
/// What an edit changed in a document's clips, for an editor that keeps the clips it was sent
/// (DESIGN.md 120). A dance of two minutes is megabytes of frames, and an edit moves a few bars
/// of them: the editor is sent the rows that came out otherwise, and puts them into what it has.
///
/// A patch says, for each clip of the file now, in order, where it comes from:
///   { whole: CLIP }            a clip the editor has not (a new name, another count of figures)
///   { from: N }                the editor's clip N as it is
///   { from: N, head?, figures? }   the editor's clip N, with
///     head     the clip without its figures (name, tempos, spans, sections, marks …), whole, when any of it changed
///     figures  one entry per figure, null for a figure as it is, else
///       head     the figure without its rows (id, name, velocity, model …), whole, when any of it changed
///       rows     { frames?, turns?, mouth? }: runs of rows that changed, each [first frame, row, row, …]
///       counts   { frames?, turns?, mouth? }: how many rows a list has now, when it is not as many as it had (a bar
///                added at the end, or taken away: the rows before it are mostly as they were). The rows past the
///                old end are a run in `rows`.
///       mic, juggle   whole when anything of it changed; null when the figure has it no more
/// The editor's half is editors\vscode\src\preview\patch.js; the tests have one of their own.
/// </summary>
internal static class ClipPatch
{
    /// <summary>A clip as the editor has it, with what is compared whole: its head and its figures' heads, as JSON.</summary>
    public sealed record Sent(MotionClip Clip, JsonElement Head, string HeadText, JsonElement[] Figures, string[] FigureTexts);

    // A clip without its figures, and a figure without its rows.
    private static readonly JsonSerializerOptions Heads = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { LeaveOut } },
    };

    private static readonly string[] OfTheClip = ["figures"];
    private static readonly string[] OfTheFigure = ["frames", "turns", "mouth", "mic", "touch", "juggle"];

    private static void LeaveOut(JsonTypeInfo info)
    {
        string[] names = info.Type == typeof(MotionClip) ? OfTheClip : info.Type == typeof(ClipFigure) ? OfTheFigure : [];
        for (int i = info.Properties.Count - 1; i >= 0 && names.Length > 0; i--)
        {
            if (names.Contains(info.Properties[i].Name))
                info.Properties.RemoveAt(i);
        }
    }

    public static List<Sent> Of(IReadOnlyList<MotionClip> clips, IReadOnlyList<Sent>? was = null) =>
        [.. clips.Select(clip => was?.FirstOrDefault(s => ReferenceEquals(s.Clip, clip)) ?? Of(clip))];

    private static Sent Of(MotionClip clip)
    {
        var head = JsonSerializer.SerializeToElement(clip, Heads);
        var figures = clip.Figures.Select(figure => JsonSerializer.SerializeToElement(figure, Heads)).ToArray();
        return new Sent(clip, head, head.GetRawText(), figures, [.. figures.Select(f => f.GetRawText())]);
    }

    /// <summary>The patch that makes <paramref name="now"/> of <paramref name="was"/> (see the class).</summary>
    public static object Between(IReadOnlyList<Sent> was, IReadOnlyList<Sent> now)
    {
        var taken = new bool[was.Count];
        var clips = new List<object>();
        foreach (var next in now)
        {
            // The clip of the same name: a score keeps its name through an edit of its dance.
            int from = -1;
            for (int i = 0; i < was.Count && from < 0; i++)
            {
                if (!taken[i] && was[i].Clip.Name == next.Clip.Name && SameShape(was[i].Clip, next.Clip))
                    from = i;
            }
            if (from < 0)
            {
                clips.Add(new { whole = next.Clip });
                continue;
            }
            taken[from] = true;
            var before = was[from];
            var entry = new Dictionary<string, object?> { ["from"] = from };
            if (!ReferenceEquals(before.Clip, next.Clip))
            {
                if (before.HeadText != next.HeadText)
                    entry["head"] = next.Head;
                var figures = new object?[next.Clip.Figures.Count];
                for (int f = 0; f < figures.Length; f++)
                    figures[f] = Figure(before, next, f);
                if (figures.Any(f => f is not null))
                    entry["figures"] = figures;
            }
            clips.Add(entry);
        }
        return new { clips };
    }

    /// <summary>The same count of figures, each with the same lists of rows (of whatever length): what a patch of rows needs.</summary>
    private static bool SameShape(MotionClip a, MotionClip b)
    {
        if (a.Figures.Count != b.Figures.Count)
            return false;
        for (int f = 0; f < a.Figures.Count; f++)
        {
            ClipFigure x = a.Figures[f], y = b.Figures[f];
            if ((x.Turns is null) != (y.Turns is null) || (x.Mouth is null) != (y.Mouth is null) || (x.Hands is null) != (y.Hands is null))
                return false;
        }
        return true;
    }

    private static object? Figure(Sent before, Sent next, int f)
    {
        ClipFigure x = before.Clip.Figures[f], y = next.Clip.Figures[f];
        var entry = new Dictionary<string, object?>();
        if (before.FigureTexts[f] != next.FigureTexts[f])
            entry["head"] = next.Figures[f];
        var rows = new Dictionary<string, object>();
        var counts = new Dictionary<string, int>();
        void Rows(string key, List<float[]>? was, List<float[]>? now)
        {
            if (was is null || now is null)
                return;
            if (Runs(was, now) is { } runs)
                rows[key] = runs;
            if (was.Count != now.Count)
                counts[key] = now.Count;
        }
        Rows("frames", x.Frames, y.Frames);
        Rows("turns", x.Turns, y.Turns);
        Rows("mouth", x.Mouth, y.Mouth);
        Rows("hands", x.Hands, y.Hands);
        if (rows.Count > 0)
            entry["rows"] = rows;
        if (counts.Count > 0)
            entry["counts"] = counts;
        if (!Same(x.Mic, y.Mic))
            entry["mic"] = y.Mic;
        if (!Same(x.Touch, y.Touch))
            entry["touch"] = y.Touch;
        if (!Same(x.Juggle, y.Juggle))
            entry["juggle"] = y.Juggle;
        return entry.Count > 0 ? entry : null;
    }

    /// <summary>
    /// The runs of rows that are not what they were (or that there were not), each [first frame, row, row, …]; null when
    /// every row is.
    /// </summary>
    private static List<object[]>? Runs(List<float[]> was, List<float[]> now)
    {
        List<object[]>? runs = null;
        for (int f = 0; f < now.Count; f++)
        {
            if (f < was.Count && Same(was[f], now[f]))
                continue;
            int end = f + 1;
            while (end < now.Count && !(end < was.Count && Same(was[end], now[end])))
                end++;
            var run = new object[1 + end - f];
            run[0] = f;
            for (int i = f; i < end; i++)
                run[1 + i - f] = now[i];
            (runs ??= []).Add(run);
            f = end;
        }
        return runs;
    }

    private static bool Same(float[] a, float[] b) => ReferenceEquals(a, b) || a.AsSpan().SequenceEqual(b);

    private static bool Same(List<float[]>? a, List<float[]>? b)
    {
        if (a is null || b is null || a.Count != b.Count)
            return a is null && b is null;
        for (int i = 0; i < a.Count; i++)
        {
            if (!Same(a[i], b[i]))
                return false;
        }
        return true;
    }

    private static bool Same(List<int[]> a, List<int[]> b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!a[i].AsSpan().SequenceEqual(b[i]))
                return false;
        }
        return true;
    }

    private static bool Same(List<float>? a, List<float>? b) =>
        a is null || b is null ? a is null && b is null : a.SequenceEqual(b);

    private static bool Same(ClipMic? a, ClipMic? b) =>
        a is null || b is null ? a is null && b is null : a.Hand == b.Hand && Same(a.Up, b.Up);

    private static bool Same(List<ClipTouch>? a, List<ClipTouch>? b) =>
        a is null || b is null ? a is null && b is null
        : a.Count == b.Count && a.Zip(b).All(p => p.First.Hand == p.Second.Hand && p.First.Place == p.Second.Place && Same(p.First.Weight, p.Second.Weight));

    private static bool Same(ClipJuggle? a, ClipJuggle? b) =>
        a is null || b is null ? a is null && b is null
        : a.Prop == b.Prop && a.Count == b.Count && Same(a.Frames, b.Frames) && Same(a.Weight, b.Weight) && Same(a.Flights, b.Flights)
            && Same(a.Styles, b.Styles) && Same(a.Mills, b.Mills) && Same(a.Pull, b.Pull) && Same(a.Wheel, b.Wheel) && Same(a.Follow, b.Follow) && Same(a.Plane, b.Plane);
}
