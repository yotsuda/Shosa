using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Shosa.Core.Motion;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

namespace Shosa.Core;

/// <summary>
/// One clip as the solver gave it, kept by a compilation for the next one (DESIGN.md 120.8): an edit that
/// leaves a clip's IR as it was but for where its words are in the text — a comment, a blank line, a
/// section this score does not dance — does not solve it again. The figures go on as the same objects,
/// and what the solver said of it is said again at the words' new places.
/// </summary>
internal sealed class SolvedClip(MotionIr ir, MotionClip clip, List<Diagnostic> said, string? key = null)
{
    public MotionIr Ir => ir;
    public MotionClip Clip => clip;
    /// <summary>What the solver said of it, every one, in its order.</summary>
    public List<Diagnostic> Said => said;

    private string? _key = key;

    /// <summary>
    /// Everything the solver reads of the IR, and nothing of where the words are: two IRs of one key are
    /// solved to the same figures. Made when it is first asked for (a compilation nobody edits never is).
    /// </summary>
    public string Key => _key ??= KeyOf(ir);

    public static SolvedClip Solve(MotionIr ir)
    {
        var said = new List<Diagnostic>();
        return new SolvedClip(ir, Solver.Solve(ir, said), said);
    }

    // The IR as JSON without the places in the text: whatever is added to the IR later is in the key by itself.
    private static readonly JsonSerializerOptions Keys = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { LeavePlacesOut } },
    };

    private static void LeavePlacesOut(JsonTypeInfo info)
    {
        if (info.Type != typeof(Seg) && info.Type != typeof(SectionIr) && info.Type != typeof(MarkIr))
            return;
        for (int i = info.Properties.Count - 1; i >= 0; i--)
        {
            if (info.Properties[i].Name is nameof(Seg.Src) or nameof(Seg.Len))
                info.Properties.RemoveAt(i);
        }
    }

    private static string KeyOf(MotionIr ir) => JsonSerializer.Serialize(ir, Keys);

    /// <summary>
    /// One of <paramref name="solved"/> as the clip of <paramref name="ir"/>, when the IR is one of theirs with
    /// its words elsewhere in the text; null when it has to be solved.
    /// </summary>
    public static SolvedClip? Moved(IReadOnlyList<SolvedClip> solved, MotionIr ir)
    {
        if (solved.Count == 0)
            return null;
        string key = KeyOf(ir);
        foreach (var was in solved)
        {
            if (was.Key == key && was.SaidAt(ir) is { } said)
                return new SolvedClip(ir, Solver.Moved(ir, was.Clip), said, key);
        }
        return null;
    }

    /// <summary>
    /// What the solver said of this clip, at the places the same words have in <paramref name="next"/> (an IR of
    /// the same key). Null when that cannot be told for certain: then the clip is solved, which is always right.
    /// </summary>
    private List<Diagnostic>? SaidAt(MotionIr next)
    {
        // Where each word went. The solver tells words apart by where they begin (to say a thing once), so
        // words that began apart must still, and words that began together must still: one to one.
        var starts = new Dictionary<int, int>();
        var back = new Dictionary<int, int>();
        var lengths = new Dictionary<(int Start, int Length), int>();
        bool Went(int src, int len, int nextSrc, int nextLen)
        {
            if (starts.TryGetValue(src, out int there) ? there != nextSrc : back.ContainsKey(nextSrc))
                return false;
            starts[src] = nextSrc;
            back[nextSrc] = src;
            if (lengths.TryGetValue((src, len), out int known) && known != nextLen)
                return false;
            lengths[(src, len)] = nextLen;
            return true;
        }

        if (ir.Sections.Count != next.Sections.Count || ir.Marks.Count != next.Marks.Count || ir.Dancers.Count != next.Dancers.Count)
            return null;
        for (int i = 0; i < ir.Sections.Count; i++)
        {
            if (!Went(ir.Sections[i].Src, ir.Sections[i].Len, next.Sections[i].Src, next.Sections[i].Len))
                return null;
        }
        for (int i = 0; i < ir.Marks.Count; i++)
        {
            if (!Went(ir.Marks[i].Src, ir.Marks[i].Len, next.Marks[i].Src, next.Marks[i].Len))
                return null;
        }
        for (int d = 0; d < ir.Dancers.Count; d++)
        {
            foreach (var (channel, segs) in ir.Dancers[d].Tracks)
            {
                if (!next.Dancers[d].Tracks.TryGetValue(channel, out var nextSegs) || nextSegs.Count != segs.Count)
                    return null;
                for (int i = 0; i < segs.Count; i++)
                {
                    if (!Went(segs[i].Src, segs[i].Len, nextSegs[i].Src, nextSegs[i].Len))
                        return null;
                }
            }
        }

        var moved = new List<Diagnostic>(said.Count);
        foreach (var d in said)
        {
            if (lengths.TryGetValue((d.Span.Start, d.Span.Length), out int length))
                moved.Add(d with { Span = new TextSpan(starts[d.Span.Start], length) });
            else if (starts.TryGetValue(d.Span.Start, out int start))
                moved.Add(d with { Span = new TextSpan(start, d.Span.Length) });   // the word `section`, of a section's span
            else if (d.Span == default)
                moved.Add(d);   // said of no word (two dancers in one place, in a clip without sections)
            else
                return null;
        }
        return moved;
    }
}
