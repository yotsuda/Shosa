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
    private ShosaCompilation(SyntaxTree tree, SyntaxTree bound, ScoreIr score, List<MotionClip> clips, List<Diagnostic> diagnostics, IReadOnlyList<IncludedFile> included, List<SolvedClip> solved)
    {
        Tree = tree;
        Bound = bound;
        Included = included;
        Score = score;
        Clips = clips;
        Diagnostics = diagnostics;
        _solved = solved;
    }

    /// <summary>Each clip as the solver gave it, for the compilation made after this one (<see cref="Compile(SyntaxTree, string?, SongSource?, UsingSource?, ShosaCompilation?)"/>'s `previous`).</summary>
    private readonly List<SolvedClip> _solved;

    /// <summary>The file as written. With `using` lines, what is bound is this and the files they bring in, after it.</summary>
    public SyntaxTree Tree { get; }

    /// <summary>
    /// What was bound: <see cref="Tree"/>, and after its text the text of every file the `using`
    /// lines brought in (<see cref="Included"/> says where each is). The same tree when there are none.
    /// </summary>
    public SyntaxTree Bound { get; }

    /// <summary>The files the `using` lines brought in, every one (theirs too): an editor compiles this again when one of them changes.</summary>
    public IReadOnlyList<IncludedFile> Included { get; }

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
    /// <param name="usings">
    /// Reads the files the `using` lines name (<see cref="UsingSource.For"/> for a file on disk).
    /// Without one a `using` line brings in nothing.
    /// </param>
    /// <param name="previous">
    /// The compilation before an edit (an editor's): a clip whose IR the edit left as it was, but for where
    /// its words are in the text, is not solved again — its figures are the same objects as in that one
    /// (DESIGN.md 120.8). What comes out is what it would be without.
    /// </param>
    public static ShosaCompilation Compile(string text, string? score = null, SongSource? songs = null, UsingSource? usings = null, ShosaCompilation? previous = null) =>
        Compile(SyntaxTree.Parse(text), score, songs, usings, previous);

    public static ShosaCompilation Compile(SyntaxTree tree, string? score = null, SongSource? songs = null, UsingSource? usings = null, ShosaCompilation? previous = null)
    {
        var main = tree;
        IReadOnlyList<IncludedFile> included = [];
        var diagnostics = new List<Diagnostic>();
        if (usings is not null && Usings.Any(tree))
        {
            var combined = Usings.Expand(tree, usings);
            (tree, included) = (combined.Tree, combined.Files);
            diagnostics.AddRange(combined.Diagnostics);
        }
        var (bound, found) = Binder.Bind(tree, score, songs, [.. included.Where(f => Library.IsLibrary(f.Path)).Select(f => (f.Start, f.End))]);
        diagnostics.AddRange(tree.Diagnostics.Concat(found));
        var seen = diagnostics.Select(d => (d.Code, d.Span)).ToHashSet();
        var clips = new List<MotionClip>();
        // A section danced in two clips is solved twice; say each thing once, and which scores it
        // is in when it is not in all of them (the same bar is too fast only in the score that
        // plays it faster).
        var movement = new List<(Diagnostic First, List<string> Scores)>();
        var kept = new List<SolvedClip>();
        foreach (var ir in bound.Clips)
        {
            var one = (previous is null ? null : SolvedClip.Moved(previous._solved, ir)) ?? SolvedClip.Solve(ir);
            kept.Add(one);
            var (clip, solved) = (one.Clip, one.Said);
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
        if (included.Count > 0)
        {
            // A word of a used file is not in the text being edited: nothing there to light up.
            int mainLength = main.Text.Length;
            foreach (var clip in clips)
                clip.Spans.RemoveAll(s => s.Start >= mainLength);
            // What is said of a used file is said on the `using` line it came by; what is said of the end, at the end of this one.
            string? folder = usings!.MainPath is { } path ? Path.GetDirectoryName(path) : null;
            diagnostics = diagnostics
                .Select(d => d.Span.Start >= tree.Text.Length ? d with { Span = new TextSpan(mainLength, 0) } : Usings.Attribute(d, tree.Text, mainLength, included, folder))
                .DistinctBy(d => (d.Code, d.Span, d.Message)).ToList();
        }
        return new ShosaCompilation(main, tree, bound, clips, diagnostics.OrderBy(d => d.Span.Start).ToList(), included, kept);
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
