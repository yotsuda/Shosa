namespace Shosa.Core.Syntax;

/// <summary>A token as seen from the red tree: its text and where that text is.</summary>
public sealed record SyntaxToken(SyntaxKind Kind, string Text, TextSpan Span, bool IsMissing, SyntaxNode Parent)
{
    /// <summary>True when a line break follows the token before anything else does.</summary>
    public bool EndsLine { get; init; }
}

/// <summary>
/// Red node: a position-aware view over a green node, made on demand and cached.
/// One generic class for now; typed nodes (PhraseDeclSyntax, EventSyntax ...) are the kind
/// of boilerplate a source generator writes once the grammar has settled.
/// </summary>
public sealed class SyntaxNode
{
    internal readonly GreenNode Green;
    private object[]? _children;   // SyntaxNode | SyntaxToken, in source order
    private TextSpan? _span;

    internal SyntaxNode(GreenNode green, SyntaxNode? parent, int position)
    {
        Green = green;
        Parent = parent;
        Position = position;
    }

    public SyntaxKind Kind => Green.Kind;
    public SyntaxNode? Parent { get; }

    /// <summary>Start of the node including its leading trivia.</summary>
    public int Position { get; }

    public TextSpan FullSpan => new(Position, Green.FullWidth);

    /// <summary>The node's own text: first token's first character to last token's last.</summary>
    public TextSpan Span
    {
        get
        {
            if (_span is { } cached)
                return cached;
            SyntaxToken? first = null, last = null;
            foreach (var token in DescendantTokens())
            {
                first ??= token;
                last = token;
            }
            var span = first is null ? new TextSpan(Position, 0) : new TextSpan(first.Span.Start, last!.Span.End - first.Span.Start);
            _span = span;
            return span;
        }
    }

    public IReadOnlyList<object> Children()
    {
        if (_children is { } existing)
            return existing;

        var list = new object[Green.SlotCount];
        int position = Position;
        for (int i = 0; i < list.Length; i++)
        {
            var green = Green.GetSlot(i);
            if (green is GreenToken token)
            {
                list[i] = new SyntaxToken(token.Kind, token.Text,
                    new TextSpan(position + token.Leading.Length, token.Text.Length), token.IsMissing, this)
                {
                    EndsLine = token.Trailing.Contains('\n'),
                };
            }
            else
            {
                list[i] = new SyntaxNode(green, this, position);
            }
            position += green.FullWidth;
        }
        return Interlocked.CompareExchange(ref _children, list, null) ?? list;
    }

    public IEnumerable<SyntaxNode> ChildNodes() => Children().OfType<SyntaxNode>();
    public IEnumerable<SyntaxNode> ChildNodes(SyntaxKind kind) => ChildNodes().Where(n => n.Kind == kind);
    public IEnumerable<SyntaxToken> ChildTokens() => Children().OfType<SyntaxToken>();

    /// <summary>The n-th child token of a kind that is really in the source, or null.</summary>
    public SyntaxToken? Token(SyntaxKind kind, int nth = 0) =>
        ChildTokens().Where(t => t.Kind == kind && !t.IsMissing).ElementAtOrDefault(nth);

    public SyntaxNode? Child(SyntaxKind kind) => ChildNodes().FirstOrDefault(n => n.Kind == kind);

    public IEnumerable<SyntaxNode> DescendantNodes()
    {
        foreach (var child in ChildNodes())
        {
            yield return child;
            foreach (var nested in child.DescendantNodes())
                yield return nested;
        }
    }

    public IEnumerable<SyntaxToken> DescendantTokens()
    {
        foreach (var child in Children())
        {
            if (child is SyntaxToken token)
            {
                yield return token;
            }
            else
            {
                foreach (var nested in ((SyntaxNode)child).DescendantTokens())
                    yield return nested;
            }
        }
    }

    /// <summary>The innermost node whose own text contains the position.</summary>
    public SyntaxNode? FindNode(int position)
    {
        if (!FullSpan.Contains(position))
            return null;
        foreach (var child in ChildNodes())
        {
            if (child.FindNode(position) is { } found)
                return found;
        }
        return this;
    }

    public override string ToString() => Green.ToString();
}
