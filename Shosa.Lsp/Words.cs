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
    private static readonly Vocabulary.Word Skip = new("skip", "This track says nothing for this long: a wider track (body, arms) has the part meanwhile.");
    private static readonly Vocabulary.Word Repeat = new("repeat", "repeat N { … } performs the events inside N times.");

    private static readonly Vocabulary.Word[] TurnSides =
    [
        new("left", "Turn to the figure's left (counter-clockwise seen from above)."),
        new("right", "Turn to the figure's right."),
    ];

    private static readonly Vocabulary.Word[] DancerClauses =
    [
        new("on", "on FIGURE — the figure whose choreography this dancer dances. Not written in a file without figures."),
        new("at", "Start position in metres: at X Z (x is stage left, z is toward the audience)."),
        new("facing", "facing front | back | stageLeft | stageRight, or degrees (positive turns left)."),
        new("mirror", "Dances the figure with left and right swapped."),
        new("delay", "delay N — starts N beats late (a canon)."),
        new("free", "The rules of weight and gravity never apply to this body: it can hang in the air (lift)."),
        new("in", "in SECTION … — dances only in these sections."),
    ];

    private static readonly Vocabulary.Word[] Facings =
    [
        new("front", "Toward the audience."),
        new("back", "Away from the audience."),
        new("stageLeft", "Toward stage left (+x)."),
        new("stageRight", "Toward stage right (-x)."),
    ];

    private static readonly Vocabulary.Word[] SectionAttributes =
    [
        new("loop", "The end joins the start: the section is solved as if it followed itself."),
        new("inplace", "The travel over the floor is taken out of the output and given to the engine as a velocity."),
    ];

    private static readonly Vocabulary.Word[] ScoreItems =
    [
        new("form", "form NAME — the order this score plays."),
        new("tempo", "tempo N — this score's own tempo."),
        new("title", "title \"…\""),
        new("dancer", "dancer NAME — only these dancers appear."),
        new("music", "music \"song.lys\" score NAME"),
    ];

    public static readonly Vocabulary.Word[] TopLevel =
    [
        new("title", "title \"…\""),
        new("tempo", "tempo N — beats per minute."),
        new("time", "time N/N — beats per bar / beat unit."),
        new("fps", "fps N — frames per second of the output, and what `:12f` counts (30 if not written)."),
        new("music", "music \"song.lys\" — the song this is danced to."),
        new("figure", "figure NAME \"label\" — an owner of choreography. A file for one body needs none."),
        new("dancer", "dancer NAME humanoid on FIGURE at X Z facing … — a body that dances a figure."),
        new("pose", "pose NAME { TRACK { target } } — a named way of standing or holding the body."),
        new("move", "move NAME { TRACK { events } } — a named row of events; written with a length it is stretched to it."),
        new("section", "section NAME [loop] [inplace] { TRACK { … } } — a stretch of time. Without a form, each section is a clip."),
        new("form", "form { A B*2 } — the order the sections are performed in, as one clip."),
        new("score", "score NAME { form F  tempo N  dancer D } — a unit of output."),
    ];

    private static bool IsLimb(string track) => track is "arms" or "leftArm" or "rightArm" or "leftLeg" or "rightLeg";
    private static bool IsArm(string track) => track is "arms" or "leftArm" or "rightArm";

    /// <summary>The poses and moves a file defines, as words: they can be written in any track that shares a part with them.</summary>
    public static List<Vocabulary.Word> Definitions(SyntaxTree tree) =>
        tree.Root.ChildNodes().Where(n => n.Kind is SyntaxKind.PoseDecl or SyntaxKind.MoveDecl)
            .Select(n => (Node: n, Name: n.Token(SyntaxKind.Identifier, 1)?.Text)).Where(d => d.Name is not null)
            .Select(d => new Vocabulary.Word(d.Name!, (d.Node.Kind == SyntaxKind.PoseDecl ? "Pose" : "Move") + " of this file: "
                + string.Join(", ", d.Node.ChildNodes(SyntaxKind.Track).Select(t => t.Token(SyntaxKind.Identifier)?.Text)) + "."))
            .ToList();

    /// <summary>
    /// The words that can come next in a target of this track, given the words already
    /// written before the cursor in the same target (empty at the start of an event).
    /// </summary>
    public static IEnumerable<Vocabulary.Word> ForTarget(string track, IReadOnlyList<string> before,
        IReadOnlyList<Vocabulary.Word>? definitions = null, IReadOnlyList<Vocabulary.Word>? figures = null)
    {
        definitions ??= [];
        figures ??= [];
        if (before.Count > 0 && figures.Any(f => f.Name == before[0]))
            return before.Count == 1 ? Vocabulary.Parts : [];

        if (track == "body")
            return before.Count == 0 ? [.. definitions, Hold, Skip, Repeat] : [];

        if (track == "steps")
        {
            if (before.Count == 0)
                return [.. Vocabulary.Feet, .. Vocabulary.SupportActions, .. definitions, Hold, Skip, Repeat];
            return before[0] switch
            {
                "left" or "right" => Except([.. Vocabulary.StepDirections, .. Vocabulary.Levels], before.Skip(1)),
                "both" or "together" => Vocabulary.Levels,
                "turn" => before.Count == 1 ? [.. TurnSides, .. figures] : before.Count == 2 ? Vocabulary.TurnAmounts : [],
                "jump" => Except(Vocabulary.JumpWords, before.Skip(1)),
                "shift" => before.Count == 1 ? Vocabulary.ShiftWords : [],
                "lift" or "land" => before.Count == 1 ? Vocabulary.JumpWords.Where(w => w.Name is "fwd" or "back") : [],
                _ => [],
            };
        }

        // The torso and the head say left / right; a limb says side / across.
        var directions = Vocabulary.Directions.Where(w => IsLimb(track) ? w.Name is not ("left" or "right") : w.Name is not ("side" or "across"));
        if (before.Count == 0)
        {
            IEnumerable<Vocabulary.Word> first = [.. directions, .. Vocabulary.Poles];
            if (IsArm(track))
                first = first.Concat(Vocabulary.Anchors.Where(a => !a.Name.Contains('.')));
            if (IsArm(track) || track == "head")
                first = first.Concat(figures);
            return [.. first, .. definitions, Hold, Skip, Repeat];
        }
        if (IsArm(track) && before is ["across"])
            return [.. Except([.. directions, .. Vocabulary.Levels], before), new("shoulder", "across.shoulder: the hand on the opposite shoulder.")];
        if (before[0] is "up" or "down" or "hold" or "skip" || Vocabulary.Has(Vocabulary.Anchors, before[0]) && IsArm(track))
            return [];
        bool hasLevel = before.Any(w => Vocabulary.Has(Vocabulary.Levels, w) || double.TryParse(w, out _));
        return Except(hasLevel ? directions : [.. directions, .. Vocabulary.Levels], before);
    }

    public static IEnumerable<Vocabulary.Word> ForAnnotation(string track) => Vocabulary.Annotations.Where(w => w.Name switch
    {
        "bent" => IsLimb(track),
        "body" => IsArm(track),
        "small" or "large" or "turn" => track is "steps",
        "smooth" or "sharp" or "linear" or "mirror" or "free" => true,
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
        var figures = FigureNames(tree).Select(n => new Vocabulary.Word(n.Id, $"Figure {n.Label}")).ToList();
        var sections = tree.Root.ChildNodes(SyntaxKind.SectionDecl).Select(p => p.Token(SyntaxKind.Identifier, 1)?.Text).OfType<string>()
            .Select(p => new Vocabulary.Word(p, "Section")).ToList();

        var track = Enclosing(tree, SyntaxKind.Track, offset);
        if (track is not null)
        {
            string name = track.Token(SyntaxKind.Identifier)?.Text ?? "";
            if (trigger == '@')
                return (ForAnnotation(name), "annotation");
            if (trigger is ':' or '!' or '(' || (trigger != '.' && wordStart > 0 && char.IsAsciiDigit(text[wordStart - 1])))
                return ([], "none");
            return (ForTarget(name, trigger == '.' ? ChainBefore(text, wordStart - 1) : [], Definitions(tree), figures), "word");
        }

        if (Enclosing(tree, SyntaxKind.FigureBlock, offset) is not null
            || Enclosing(tree, SyntaxKind.PoseDecl, offset) is not null || Enclosing(tree, SyntaxKind.MoveDecl, offset) is not null)
            return (Vocabulary.Tracks, "track");

        if (Enclosing(tree, SyntaxKind.SectionDecl, offset) is not null)
            return figures.Count > 0 ? (figures, "figure") : (Vocabulary.Tracks, "track");

        if (Enclosing(tree, SyntaxKind.FormDecl, offset) is not null)
            return (sections, "section");

        if (Enclosing(tree, SyntaxKind.ScoreDecl, offset) is not null)
            return (ScoreItems, "property");

        // The one-line items: what has been written on this line so far decides.
        int lineStart = text.LastIndexOf('\n', Math.Max(0, wordStart - 1)) + 1;
        var line = text[lineStart..wordStart].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (line is ["dancer", ..])
        {
            return line switch
            {
                ["dancer"] => ([], "none"),
                ["dancer", _] => ([new Vocabulary.Word("humanoid", "A human figure, 1.7 m tall.")], "value"),
                [.., "on"] => (figures, "figure"),
                [.., "facing"] => (Facings, "value"),
                [.., "at"] or [.., "at", _] or [.., "delay"] => ([], "none"),
                _ when line.Contains("in") && !DancerClauses.Any(c => c.Name == line[^1] && c.Name != "in") => ([.. sections, .. DancerClauses], "section"),
                _ => (DancerClauses, "property"),
            };
        }
        if (line is ["section", _, ..])
            return (SectionAttributes, "property");
        if (line.Length > 0)
            return ([], "none");

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
            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_' || text[start - 1] == '-'))
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
        bool first = token == parent.ChildTokens().First();

        if (parent.Kind == SyntaxKind.Target && parent.Parent?.Kind == SyntaxKind.Arguments)
        {
            word = TurnSides.Concat(Vocabulary.TurnAmounts).FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.Target)
        {
            var before = parent.ChildTokens().Where(t => t.Kind is SyntaxKind.Identifier or SyntaxKind.Number && !t.IsMissing && t.Span.Start < token.Span.Start)
                .Select(t => t.Text).ToList();
            var figures = FigureNames(tree).Select(n => new Vocabulary.Word(n.Id, $"Figure {n.Label}")).ToList();
            word = ForTarget(track, before, Definitions(tree), figures).FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.Annotation)
        {
            word = Vocabulary.Annotations.FirstOrDefault(w => w.Name == token.Text) is { } a ? new Vocabulary.Word("@" + a.Name, a.Doc) : null;
        }
        else if (parent.Kind == SyntaxKind.Mark)
        {
            word = new Vocabulary.Word("!" + token.Text, "A moment named for the engine: the instant the part arrives. Written to the events of the output.");
        }
        else if (parent.Kind == SyntaxKind.Track && first)
        {
            word = Vocabulary.Tracks.FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.Repeat)
        {
            word = Repeat;
        }
        else if (parent.Kind == SyntaxKind.Clause && first)
        {
            word = DancerClauses.FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.ScoreItem && first)
        {
            word = ScoreItems.FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.SectionDecl && !first && token != parent.Token(SyntaxKind.Identifier, 1))
        {
            word = SectionAttributes.FirstOrDefault(w => w.Name == token.Text);
        }
        else if (first && parent.Kind is SyntaxKind.Header or SyntaxKind.FigureDecl or SyntaxKind.DancerDecl or SyntaxKind.PoseDecl
                     or SyntaxKind.MoveDecl or SyntaxKind.SectionDecl or SyntaxKind.FormDecl or SyntaxKind.ScoreDecl)
        {
            word = TopLevel.FirstOrDefault(w => w.Name == token.Text);
        }

        return word is null ? null : (word, token.Span);
    }
}
