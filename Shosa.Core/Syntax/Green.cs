using System.Text;

namespace Shosa.Core.Syntax;

/// <summary>
/// Green nodes: immutable, position-free, and therefore shareable between trees. An edit
/// builds a new tree that reuses every green node it did not touch (Roslyn's design).
/// </summary>
internal abstract class GreenNode
{
    protected GreenNode(SyntaxKind kind, int fullWidth)
    {
        Kind = kind;
        FullWidth = fullWidth;
    }

    public SyntaxKind Kind { get; }

    /// <summary>Width including trivia. The tree is lossless: the widths add up to the text.</summary>
    public int FullWidth { get; }

    public abstract int SlotCount { get; }
    public abstract GreenNode GetSlot(int index);
    public abstract void WriteTo(StringBuilder sb);

    public override string ToString()
    {
        var sb = new StringBuilder();
        WriteTo(sb);
        return sb.ToString();
    }
}

internal sealed class GreenToken : GreenNode
{
    public GreenToken(SyntaxKind kind, string leading, string text, string trailing, bool isMissing = false)
        : base(kind, leading.Length + text.Length + trailing.Length)
    {
        Leading = leading;
        Text = text;
        Trailing = trailing;
        IsMissing = isMissing;
    }

    /// <summary>Whitespace, line breaks and comments before the token.</summary>
    public string Leading { get; }
    public string Text { get; }
    /// <summary>Whitespace and a comment after the token, up to and including the line break.</summary>
    public string Trailing { get; }
    /// <summary>A token the parser needed and the source did not have. Zero width.</summary>
    public bool IsMissing { get; }

    public static GreenToken Missing(SyntaxKind kind) => new(kind, "", "", "", isMissing: true);

    public override int SlotCount => 0;
    public override GreenNode GetSlot(int index) => throw new ArgumentOutOfRangeException(nameof(index));
    public override void WriteTo(StringBuilder sb) => sb.Append(Leading).Append(Text).Append(Trailing);
}

internal sealed class GreenInner : GreenNode
{
    private readonly GreenNode[] _children;

    public GreenInner(SyntaxKind kind, IReadOnlyList<GreenNode> children)
        : base(kind, children.Sum(c => c.FullWidth))
    {
        _children = children.ToArray();
    }

    public override int SlotCount => _children.Length;
    public override GreenNode GetSlot(int index) => _children[index];

    public override void WriteTo(StringBuilder sb)
    {
        foreach (var child in _children)
            child.WriteTo(sb);
    }
}
