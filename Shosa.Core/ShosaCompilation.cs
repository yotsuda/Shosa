using Shosa.Core.Motion;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

namespace Shosa.Core;

/// <summary>
/// The pipeline in one place, each stage an immutable value made from the one before:
/// text -> SyntaxTree -> ScoreIr (a MotionIr per clip) -> a MotionClip per clip. The CLI runs it
/// once per file; a language server keeps the last one and feeds
/// <see cref="SyntaxTree.WithChange"/> into the next.
/// </summary>
public sealed class ShosaCompilation
{
    private ShosaCompilation(SyntaxTree tree, ScoreIr score, List<MotionClip> clips, List<Diagnostic> diagnostics)
    {
        Tree = tree;
        Score = score;
        Clips = clips;
        Diagnostics = diagnostics;
    }

    public SyntaxTree Tree { get; }

    /// <summary>The score that was asked for: one <see cref="MotionIr"/> per clip.</summary>
    public ScoreIr Score { get; }

    /// <summary>The solved clips, in the order of <see cref="ScoreIr.Clips"/>.</summary>
    public IReadOnlyList<MotionClip> Clips { get; }

    /// <summary>The first clip; an empty one for a file that has none yet.</summary>
    public MotionIr Ir => Score.Clips.Count > 0 ? Score.Clips[0] : Empty;
    public MotionClip Clip => Clips.Count > 0 ? Clips[0] : EmptyClip;

    private static readonly MotionIr Empty = new();
    private static readonly MotionClip EmptyClip = Solver.Solve(Empty, []);

    /// <summary>Syntax, meaning and movement diagnostics together, in source order.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    /// <param name="songs">
    /// Reads the song a `music` line names (<see cref="Music.SongReader.For"/> for a file on
    /// disk). Without one the file is compiled by what it says itself.
    /// </param>
    public static ShosaCompilation Compile(string text, string? score = null, SongSource? songs = null) =>
        Compile(SyntaxTree.Parse(text), score, songs);

    public static ShosaCompilation Compile(SyntaxTree tree, string? score = null, SongSource? songs = null)
    {
        var (bound, found) = Binder.Bind(tree, score, songs);
        var diagnostics = tree.Diagnostics.Concat(found).ToList();
        var seen = diagnostics.Select(d => (d.Code, d.Span)).ToHashSet();
        var clips = new List<MotionClip>();
        // A section danced in two clips is solved twice; say each thing once, and which scores it
        // is in when it is not in all of them (the same bar is too fast only in the score that
        // plays it faster).
        var movement = new List<(Diagnostic First, List<string> Scores)>();
        foreach (var ir in bound.Clips)
        {
            var solved = new List<Diagnostic>();
            var clip = Solver.Solve(ir, solved);
            clips.Add(clip);
            foreach (var d in solved.DistinctBy(d => (d.Code, d.Span)))
            {
                if (seen.Add((d.Code, d.Span)))
                    movement.Add((d, [clip.Name]));
                else if (movement.Find(m => m.First.Code == d.Code && m.First.Span == d.Span) is { Scores: { } scores })
                    scores.Add(clip.Name);
            }
        }
        foreach (var (first, scores) in movement)
        {
            string Named(string name) => name == "" ? "the unnamed score" : $"score {name}";
            diagnostics.Add(scores.Count == clips.Count ? first
                : first with { Message = $"{first.Message} (In {string.Join(", ", scores.Select(Named))}.)" });
        }
        return new ShosaCompilation(tree, bound, clips, diagnostics.OrderBy(d => d.Span.Start).ToList());
    }

    /// <summary>1-based line and column of a position in the text.</summary>
    public static (int Line, int Column) LineColumn(string text, int position)
    {
        int line = 1, column = 1;
        for (int i = 0; i < position && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }
        return (line, column);
    }
}
