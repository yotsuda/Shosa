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

    // The children that are nodes, and those that are tokens, each in source order: found once. The binder asks
    // for them at every word of every play of a section, and each asking made four objects to filter the children
    // (DESIGN.md 120.13).
    private SyntaxNode[]? _nodes;
    private SyntaxToken[]? _tokens;

    public IReadOnlyList<SyntaxNode> ChildNodes()
    {
        if (_nodes is { } existing)
            return existing;
        var children = Children();
        int count = 0;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is SyntaxNode)
                count++;
        }
        var nodes = count == 0 ? [] : new SyntaxNode[count];
        for (int i = 0, n = 0; i < children.Count; i++)
        {
            if (children[i] is SyntaxNode node)
                nodes[n++] = node;
        }
        return Interlocked.CompareExchange(ref _nodes, nodes, null) ?? nodes;
    }

    public IEnumerable<SyntaxNode> ChildNodes(SyntaxKind kind)
    {
        var nodes = ChildNodes();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Kind == kind)
                yield return nodes[i];
        }
    }

    public IReadOnlyList<SyntaxToken> ChildTokens()
    {
        if (_tokens is { } existing)
            return existing;
        var children = Children();
        int count = 0;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is SyntaxToken)
                count++;
        }
        var tokens = count == 0 ? [] : new SyntaxToken[count];
        for (int i = 0, n = 0; i < children.Count; i++)
        {
            if (children[i] is SyntaxToken token)
                tokens[n++] = token;
        }
        return Interlocked.CompareExchange(ref _tokens, tokens, null) ?? tokens;
    }

    /// <summary>The n-th child token of a kind that is really in the source, or null.</summary>
    public SyntaxToken? Token(SyntaxKind kind, int nth = 0)
    {
        if (nth < 0)
            return null;
        var tokens = ChildTokens();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == kind && !tokens[i].IsMissing && nth-- == 0)
                return tokens[i];
        }
        return null;
    }

    public SyntaxNode? Child(SyntaxKind kind)
    {
        var nodes = ChildNodes();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Kind == kind)
                return nodes[i];
        }
        return null;
    }

    public IEnumerable<SyntaxNode> DescendantNodes()
    {
        // Each node before its own children, the children in order (one walk, not an iterator inside an iterator for every level).
        var waiting = new Stack<(IReadOnlyList<SyntaxNode> Nodes, int Next)>();
        waiting.Push((ChildNodes(), 0));
        while (waiting.Count > 0)
        {
            var (nodes, next) = waiting.Pop();
            if (next >= nodes.Count)
                continue;
            waiting.Push((nodes, next + 1));
            yield return nodes[next];
            waiting.Push((nodes[next].ChildNodes(), 0));
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
