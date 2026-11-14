namespace Shosa.Core.Syntax;

/// <summary>A parsed source file: the text, its lossless tree and the syntax diagnostics.</summary>
public sealed class SyntaxTree
{
    private readonly GreenInner _green;
    private SyntaxNode? _root;

    private SyntaxTree(string text, GreenInner green, List<Diagnostic> diagnostics, int reusedItems)
    {
        Text = text;
        _green = green;
        Diagnostics = diagnostics;
        ReusedItems = reusedItems;
    }

    public string Text { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>How many top-level items this tree took unchanged from the tree before it.</summary>
    public int ReusedItems { get; }

    public SyntaxNode Root => _root ??= new SyntaxNode(_green, null, 0);

    public string ToFullString() => _green.ToString();

    public static SyntaxTree Parse(string text)
    {
        var (items, eof, diagnostics) = ParseText(text);
        items.Add(eof);
        return new SyntaxTree(text, new GreenInner(SyntaxKind.CompilationUnit, items), diagnostics, 0);
    }

    private static (List<GreenNode> Items, GreenToken EndOfFile, List<Diagnostic> Diagnostics) ParseText(string text)
    {
        var lexer = new Lexer(text);
        var parser = new Parser(lexer.Lex());
        var (items, eof) = parser.ParseItems();
        var diagnostics = lexer.Diagnostics.Concat(parser.Diagnostics).OrderBy(d => d.Span.Start).ToList();
        return (items, eof, diagnostics);
    }

    /// <summary>
    /// Incremental reparse. Top-level items (a header line, a figure, a phrase, the form) are
    /// self-contained and their green nodes carry no position, so every item the edit did not
    /// touch is taken over as it is and only the text between them is parsed again.
    /// An item is reusable in front of the edit only if it parsed without a diagnostic: an
    /// unfinished item might have gone on to swallow the new text.
    /// </summary>
    public SyntaxTree WithChange(TextChange change)
    {
        int changeStart = change.Span.Start, changeEnd = change.Span.End;
        string newText = string.Concat(Text.AsSpan(0, changeStart), change.NewText, Text.AsSpan(changeEnd));
        int delta = newText.Length - Text.Length;

        int itemCount = _green.SlotCount - 1;   // the last slot is the end-of-file token
        var starts = new int[itemCount + 1];
        for (int i = 0; i < itemCount; i++)
            starts[i + 1] = starts[i] + _green.GetSlot(i).FullWidth;

        bool IsClean(int i) => !Diagnostics.Any(d => d.Span.Start >= starts[i] && d.Span.Start <= starts[i + 1]);

        // The lexer keeps no state across a line break, so an item whose text ends with one
        // reads the same whatever comes after it. One that ends in mid-line does not: `1`
        // followed by `.5`, or a `//` typed after it, would reach back into it.
        bool EndsLine(int i) => starts[i + 1] > 0 && Text[starts[i + 1] - 1] == '\n';

        int prefix = 0;
        while (prefix < itemCount && starts[prefix + 1] <= changeStart && IsClean(prefix) && EndsLine(prefix))
            prefix++;

        int suffix = prefix;
        while (suffix < itemCount && starts[suffix] < changeEnd)
            suffix++;

        int middleStart = starts[prefix];
        var items = new List<GreenNode>();
        for (int i = 0; i < prefix; i++)
            items.Add(_green.GetSlot(i));

        if (suffix == itemCount)
        {
            // Nothing reusable after the edit: parse from the first damaged item to the end.
            var (tail, tailEof, tailDiagnostics) = ParseText(newText[middleStart..]);
            items.AddRange(tail);
            items.Add(tailEof);
            return new SyntaxTree(newText, new GreenInner(SyntaxKind.CompilationUnit, items),
                tailDiagnostics.Select(d => d.Shift(middleStart)).ToList(), prefix);
        }

        int middleEnd = starts[suffix] + delta;
        string middleText = newText[middleStart..middleEnd];
        var (middle, middleEof, middleDiagnostics) = ParseText(middleText);
        if (middleDiagnostics.Count > 0 || middleEof.FullWidth > 0 || (middleText.Length > 0 && middleText[^1] != '\n'))
        {
            // The damaged part did not close cleanly, or does not end its line, so where the
            // next item begins is no longer certain. Parse everything.
            return Parse(newText);
        }

        items.AddRange(middle);
        for (int i = suffix; i < _green.SlotCount; i++)
            items.Add(_green.GetSlot(i));

        var diagnostics = Diagnostics.Where(d => d.Span.Start >= starts[suffix]).Select(d => d.Shift(delta)).ToList();
        return new SyntaxTree(newText, new GreenInner(SyntaxKind.CompilationUnit, items), diagnostics,
            prefix + (itemCount - suffix));
    }
}
