using Shosa.Core.Motion;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

namespace Shosa.Core;

/// <summary>
/// The pipeline in one place, each stage an immutable value made from the one before:
/// text -> SyntaxTree -> MotionIr -> MotionClip. The CLI runs it once per file; a language
/// server keeps the last one and feeds <see cref="SyntaxTree.WithChange"/> into the next.
/// </summary>
public sealed class ShosaCompilation
{
    private ShosaCompilation(SyntaxTree tree, MotionIr ir, MotionClip clip, List<Diagnostic> diagnostics)
    {
        Tree = tree;
        Ir = ir;
        Clip = clip;
        Diagnostics = diagnostics;
    }

    public SyntaxTree Tree { get; }
    public MotionIr Ir { get; }
    public MotionClip Clip { get; }

    /// <summary>Syntax, meaning and movement diagnostics together, in source order.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public static ShosaCompilation Compile(string text) => Compile(SyntaxTree.Parse(text));

    public static ShosaCompilation Compile(SyntaxTree tree)
    {
        var (ir, bound) = Binder.Bind(tree);
        var diagnostics = tree.Diagnostics.Concat(bound).ToList();
        var clip = Solver.Solve(ir, diagnostics);
        return new ShosaCompilation(tree, ir, clip, diagnostics.OrderBy(d => d.Span.Start).ToList());
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
