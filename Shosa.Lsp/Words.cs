using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

namespace Shosa.Lsp;

/// <summary>
/// Which words can stand at a place in the source. Completion offers them and hover looks
/// one of them up, so the two cannot disagree; both read <see cref="Vocabulary"/>, the table
/// the binder checks against.
/// </summary>
internal static class Words
{
    private static readonly Vocabulary.Word Hold = new("hold", "Stay as you are.");
    private static readonly Vocabulary.Word Repeat = new("repeat", "repeat N { … } performs the events inside N times.");

    private static readonly Vocabulary.Word[] TurnSides =
    [
        new("left", "Turn to the figure's left (counter-clockwise seen from above)."),
        new("right", "Turn to the figure's right."),
    ];

    private static readonly Vocabulary.Word[] FigureProperties =
    [
        new("rig", "The body: rig humanoid"),
        new("at", "Start position in metres: at X Z (x is stage left, z is toward the audience)."),
        new("facing", "facing front | back | stageLeft | stageRight, or degrees (positive turns left)."),
    ];

    private static readonly Vocabulary.Word[] Facings =
    [
        new("front", "Toward the audience."),
        new("back", "Away from the audience."),
        new("stageLeft", "Toward stage left (+x)."),
        new("stageRight", "Toward stage right (-x)."),
    ];

    public static readonly Vocabulary.Word[] TopLevel =
    [
        new("title", "title \"…\""),
        new("tempo", "tempo N — beats per minute."),
        new("time", "time N/N — beats per bar / beat unit."),
        new("figure", "figure NAME \"label\" { rig humanoid }"),
        new("phrase", "phrase NAME { FIGURE { TRACK { … } } }"),
        new("form", "form { A B*2 } — the order the phrases are performed in."),
    ];

    private static bool IsLimb(string track) => track is "arms" or "leftArm" or "rightArm" or "leftLeg" or "rightLeg";
    private static bool IsArm(string track) => track is "arms" or "leftArm" or "rightArm";

    /// <summary>
    /// The words that can come next in a target of this track, given the words already
    /// written before the cursor in the same target (empty at the start of an event).
    /// </summary>
    public static IEnumerable<Vocabulary.Word> ForTarget(string track, IReadOnlyList<string> before)
    {
        if (track == "steps")
        {
            if (before.Count == 0)
                return [.. Vocabulary.Feet, .. Vocabulary.SupportActions, Hold, Repeat];
            return before[0] switch
            {
                "left" or "right" => Except([.. Vocabulary.StepDirections, .. Vocabulary.Levels], before.Skip(1)),
                "both" or "together" => Vocabulary.Levels,
                "turn" => before.Count == 1 ? TurnSides : before.Count == 2 ? Vocabulary.TurnAmounts : [],
                "jump" => Except(Vocabulary.JumpWords, before.Skip(1)),
                _ => [],
            };
        }

        // The torso and the head say left / right; a limb says side / across.
        var directions = Vocabulary.Directions.Where(w => IsLimb(track) ? w.Name is not ("left" or "right") : w.Name is not ("side" or "across"));
        if (before.Count == 0)
        {
            IEnumerable<Vocabulary.Word> first = [.. directions, .. Vocabulary.Poles];
            if (IsArm(track))
                first = first.Concat(Vocabulary.Anchors);
            return [.. first, Hold, Repeat];
        }
        if (before[0] is "up" or "down" or "hold" || Vocabulary.Has(Vocabulary.Anchors, before[0]) && IsArm(track))
            return [];
        bool hasLevel = before.Any(w => Vocabulary.Has(Vocabulary.Levels, w));
        return Except(hasLevel ? directions : [.. directions, .. Vocabulary.Levels], before);
    }

    public static IEnumerable<Vocabulary.Word> ForAnnotation(string track) => Vocabulary.Annotations.Where(w => w.Name switch
    {
        "bent" => IsLimb(track),
        "body" => IsArm(track),
        "small" or "large" => track == "steps",
        _ => true,
    });

    private static IEnumerable<Vocabulary.Word> Except(IEnumerable<Vocabulary.Word> words, IEnumerable<string> used)
    {
        var taken = used.ToHashSet();
        // A direction and its opposite cannot both be in one target.
        foreach (var (a, b) in new[] { ("fwd", "back"), ("side", "across"), ("left", "right"), ("apart", "together") })
        {
            if (taken.Contains(a))
                taken.Add(b);
            else if (taken.Contains(b))
                taken.Add(a);
        }
        return words.Where(w => !taken.Contains(w.Name));
    }

    /// <summary>What can be typed at a position, and what kind of thing it is.</summary>
    public static (IEnumerable<Vocabulary.Word> Words, string Kind) At(SyntaxTree tree, int offset)
    {
        string text = tree.Text;
        int wordStart = offset;
        while (wordStart > 0 && (char.IsLetterOrDigit(text[wordStart - 1]) || text[wordStart - 1] == '_'))
            wordStart--;
        char trigger = wordStart > 0 ? text[wordStart - 1] : '\0';

        var track = Enclosing(tree, SyntaxKind.Track, offset);
        if (track is not null)
        {
            string name = track.Token(SyntaxKind.Identifier)?.Text ?? "";
            if (trigger == '@')
                return (ForAnnotation(name), "annotation");
            if (trigger == ':' || (trigger != '.' && wordStart > 0 && char.IsAsciiDigit(text[wordStart - 1])))
                return ([], "none");
            return (ForTarget(name, trigger == '.' ? ChainBefore(text, wordStart - 1) : []), "word");
        }

        if (Enclosing(tree, SyntaxKind.FigureBlock, offset) is not null)
            return (Vocabulary.Tracks, "track");

        if (Enclosing(tree, SyntaxKind.PhraseDecl, offset) is not null)
            return (FigureNames(tree).Select(n => new Vocabulary.Word(n.Id, $"Figure {n.Label}")), "figure");

        if (Enclosing(tree, SyntaxKind.FormDecl, offset) is not null)
        {
            var phrases = tree.Root.ChildNodes(SyntaxKind.PhraseDecl).Select(p => p.Token(SyntaxKind.Identifier, 1)?.Text).OfType<string>();
            return (phrases.Select(p => new Vocabulary.Word(p, "Phrase")), "phrase");
        }

        if (Enclosing(tree, SyntaxKind.FigureDecl, offset) is not null)
        {
            string before = text[..wordStart].TrimEnd();
            if (before.EndsWith("facing", StringComparison.Ordinal))
                return (Facings, "value");
            if (before.EndsWith("rig", StringComparison.Ordinal))
                return ([new Vocabulary.Word("humanoid", "A human figure, 1.7 m tall.")], "value");
            return (FigureProperties, "property");
        }

        return (TopLevel, "keyword");
    }

    private static IEnumerable<(string Id, string Label)> FigureNames(SyntaxTree tree) =>
        from figure in tree.Root.ChildNodes(SyntaxKind.FigureDecl)
        let id = figure.Token(SyntaxKind.Identifier, 1)
        where id is not null
        select (id.Text, figure.Token(SyntaxKind.String)?.Text ?? id.Text);

    /// <summary>The words of a dotted target that stand before the dot at <paramref name="dot"/>.</summary>
    private static List<string> ChainBefore(string text, int dot)
    {
        var words = new List<string>();
        int end = dot;
        while (end > 0 && text[end] == '.')
        {
            int start = end;
            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
                start--;
            if (start == end)
                break;
            words.Insert(0, text[start..end]);
            end = start - 1;
            if (end < 0)
                break;
        }
        return words;
    }

    /// <summary>The innermost node of a kind whose braces the position is between.</summary>
    public static SyntaxNode? Enclosing(SyntaxTree tree, SyntaxKind kind, int offset)
    {
        SyntaxNode? found = null;
        foreach (var node in tree.Root.DescendantNodes())
        {
            if (node.Kind != kind || node.Position > offset)
                continue;
            var open = node.Token(SyntaxKind.OpenBrace);
            if (open is null || offset <= open.Span.Start)
                continue;
            var close = node.Token(SyntaxKind.CloseBrace);
            if (close is not null && offset > close.Span.Start)
                continue;
            found = node;   // later in document order is deeper or further on; keep the last
        }
        return found;
    }

    /// <summary>The documentation of the word at a position, or null.</summary>
    public static (Vocabulary.Word Word, TextSpan Span)? Describe(SyntaxTree tree, int offset)
    {
        var token = tree.Root.DescendantTokens()
            .FirstOrDefault(t => t.Kind == SyntaxKind.Identifier && !t.IsMissing && offset >= t.Span.Start && offset <= t.Span.End);
        if (token is null)
            return null;

        Vocabulary.Word? word = null;
        var parent = token.Parent;
        string track = Enclosing(tree, SyntaxKind.Track, token.Span.Start)?.Token(SyntaxKind.Identifier)?.Text ?? "";

        if (parent.Kind == SyntaxKind.Target)
        {
            var before = parent.ChildTokens().Where(t => t.Kind == SyntaxKind.Identifier && !t.IsMissing && t.Span.Start < token.Span.Start)
                .Select(t => t.Text).ToList();
            word = ForTarget(track, before).FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.Annotation)
        {
            word = Vocabulary.Annotations.FirstOrDefault(w => w.Name == token.Text) is { } a ? new Vocabulary.Word("@" + a.Name, a.Doc) : null;
        }
        else if (parent.Kind == SyntaxKind.Track && token == parent.ChildTokens().First())
        {
            word = Vocabulary.Tracks.FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.Repeat)
        {
            word = Repeat;
        }
        else if (parent.Kind is SyntaxKind.Header or SyntaxKind.FigureDecl or SyntaxKind.PhraseDecl or SyntaxKind.FormDecl
                 && token == parent.ChildTokens().First())
        {
            word = TopLevel.FirstOrDefault(w => w.Name == token.Text);
        }

        return word is null ? null : (word, token.Span);
    }
}
