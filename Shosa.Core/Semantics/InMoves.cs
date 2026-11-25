using System.Globalization;
using Shosa.Core.Syntax;

namespace Shosa.Core.Semantics;

/// <summary>
/// What is wrong with a word of a pose or a move, said at that word, once, with the beats it comes at
/// (DESIGN.md 123.5). Said where the move is used, it was said at every use, as often as it was used, and
/// never which of its words it was (the writing trial of 123.4).
/// </summary>
internal sealed class InMoves(string? dancer = null)
{
    private readonly Dictionary<(string Code, TextSpan Span), (int Index, string Within, SortedSet<double> Beats)> _said = [];

    /// <summary>Where a thing said of this word goes: the word in its pose or move, else the word itself.</summary>
    public static TextSpan Place(Seg seg) =>
        seg.Word is { } word ? new TextSpan(word, seg.WordLen ?? 0) : new TextSpan(seg.Src, seg.Len);

    /// <summary>Adds what is said of <paramref name="seg"/> to <paramref name="list"/>, or, said already of the same word in its move, only its beat.</summary>
    public void Say(List<Diagnostic> list, string code, DiagnosticSeverity severity, Seg seg, string message)
    {
        var span = Place(seg);
        if (seg.Word is null || seg.Within is null)
        {
            list.Add(new Diagnostic(code, severity, span, message));
            return;
        }
        if (_said.TryGetValue((code, span), out var said))
        {
            said.Beats.Add(Math.Round(seg.At, 2));
            return;
        }
        _said[(code, span)] = (list.Count, seg.Within, [Math.Round(seg.At, 2)]);
        list.Add(new Diagnostic(code, severity, span, message));
    }

    /// <summary>Puts the beats on what was said, once all of it is.</summary>
    public void Finish(List<Diagnostic> list)
    {
        foreach (var (index, within, beats) in _said.Values)
        {
            var all = beats.Select(b => b.ToString("0.##", CultureInfo.InvariantCulture)).ToList();
            string at = all.Count == 1 ? $"beat {all[0]}"
                : $"beats {string.Join(", ", all.Take(all.Count - 1))} and {all[^1]}";
            string where = dancer is null ? "where it comes" : $"where {dancer} does it";
            list[index] = list[index] with { Message = $"{list[index].Message} This word is in `{within}`; it is so {where}, at {at}." };
        }
        _said.Clear();
    }
}
