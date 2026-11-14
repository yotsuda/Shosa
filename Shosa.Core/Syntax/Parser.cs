namespace Shosa.Core.Syntax;

/// <summary>
/// Recursive descent over the token list. It never throws and never drops a token: what it
/// needs and does not find becomes a zero-width missing token, what it cannot place goes
/// into an Error node. A half-typed file therefore still has a tree the editor can use.
///
/// The parser knows the SHAPE of the language and none of its vocabulary: `left.fwd`,
/// `side.high` and `turn.left.quarter` are all "words joined by dots" here. Which words
/// exist is the binder's business, so a new movement word never changes the grammar.
///
/// A top-level item never depends on what follows it: the optional tail of a one-line item
/// (a figure's label, a dancer's clauses, `score NAME` after `music`) is taken only from the
/// same line. That is what lets <see cref="SyntaxTree.WithChange"/> reuse the items around an edit.
/// </summary>
internal sealed class Parser
{
    private static readonly HashSet<string> TopKeywords =
        ["title", "tempo", "time", "fps", "music", "figure", "dancer", "pose", "move", "section", "form", "score"];
    private static readonly HashSet<string> ClauseKeywords = ["on", "at", "facing", "mirror", "delay", "free", "in"];
    private static readonly HashSet<string> ScoreItemKeywords = ["form", "tempo", "title", "music", "dancer"];

    private readonly List<GreenToken> _tokens;
    private int _index;
    private int _offset;        // where the current token's leading trivia starts
    private int _prevEnd;       // where the previous token's text ended
    private bool _lineEnded;    // the previous token was the last one on its line
    private bool _prevTight;    // nothing, not even a space, followed the previous token

    public Parser(List<GreenToken> tokens) => _tokens = tokens;

    public List<Diagnostic> Diagnostics { get; } = [];

    private GreenToken Current => _tokens[_index];
    private SyntaxKind Kind => Current.Kind;
    private GreenToken Peek(int n) => _tokens[Math.Min(_index + n, _tokens.Count - 1)];
    private bool AtTopKeyword => Kind == SyntaxKind.Identifier && TopKeywords.Contains(Current.Text);
    private bool AtBlockEnd => Kind is SyntaxKind.CloseBrace or SyntaxKind.EndOfFile || AtTopKeyword;
    private bool AtName => Kind == SyntaxKind.Identifier && !AtTopKeyword;
    private bool SameLine => !_lineEnded && Kind != SyntaxKind.EndOfFile;
    private bool At(string word) => Kind == SyntaxKind.Identifier && Current.Text == word;

    private GreenToken Next()
    {
        var token = Current;
        _prevEnd = _offset + token.Leading.Length + token.Text.Length;
        _offset += token.FullWidth;
        _lineEnded = token.Trailing.Contains('\n');
        _prevTight = token.Trailing.Length == 0;
        if (_index < _tokens.Count - 1)
            _index++;
        return token;
    }

    private GreenToken Missing(SyntaxKind kind, string what)
    {
        Diagnostics.Add(new Diagnostic("SH0010", DiagnosticSeverity.Error, new TextSpan(_prevEnd, 0), $"{what} expected here."));
        return GreenToken.Missing(kind);
    }

    private GreenToken Expect(SyntaxKind kind, string what) =>
        Kind == kind && !(kind == SyntaxKind.Identifier && AtTopKeyword) ? Next() : Missing(kind, what);

    /// <summary>Like <see cref="Expect"/>, but does not reach into the next line for it.</summary>
    private GreenToken ExpectOnLine(SyntaxKind kind, string what) => SameLine ? Expect(kind, what) : Missing(kind, what);

    private GreenNode SkipUnexpected()
    {
        var token = Current;
        Diagnostics.Add(new Diagnostic("SH0011", DiagnosticSeverity.Error,
            new TextSpan(_offset + token.Leading.Length, token.Text.Length), $"'{token.Text}' does not belong here."));
        return new GreenInner(SyntaxKind.Error, [Next()]);
    }

    public (List<GreenNode> Items, GreenToken EndOfFile) ParseItems()
    {
        var items = new List<GreenNode>();
        while (Kind != SyntaxKind.EndOfFile)
            items.Add(ParseItem());
        return (items, Current);
    }

    private GreenNode ParseItem()
    {
        if (Kind == SyntaxKind.Identifier)
        {
            switch (Current.Text)
            {
                case "title": return new GreenInner(SyntaxKind.Header, [Next(), ExpectOnLine(SyntaxKind.String, "A title in quotes")]);
                case "tempo": return new GreenInner(SyntaxKind.Header, [Next(), ExpectOnLine(SyntaxKind.Number, "Beats per minute")]);
                case "fps": return new GreenInner(SyntaxKind.Header, [Next(), ExpectOnLine(SyntaxKind.Number, "Frames per second")]);
                case "time":
                    return new GreenInner(SyntaxKind.Header,
                    [
                        Next(),
                        ExpectOnLine(SyntaxKind.Number, "Beats per bar"),
                        ExpectOnLine(SyntaxKind.Slash, "'/'"),
                        ExpectOnLine(SyntaxKind.Number, "The beat unit"),
                    ]);
                case "music": return new GreenInner(SyntaxKind.Header, ParseMusic());
                case "figure": return ParseFigure();
                case "dancer": return ParseDancer();
                case "pose": return ParseDefinition(SyntaxKind.PoseDecl, "A pose name");
                case "move": return ParseDefinition(SyntaxKind.MoveDecl, "A move name");
                case "section": return ParseSection();
                case "form": return ParseForm();
                case "score": return ParseScore();
            }
        }
        return SkipUnexpected();
    }

    /// <summary>music "song.lys" (score NAME)?</summary>
    private List<GreenNode> ParseMusic()
    {
        var children = new List<GreenNode> { Next(), ExpectOnLine(SyntaxKind.String, "A file name in quotes") };
        if (SameLine && At("score"))
        {
            children.Add(Next());
            children.Add(ExpectOnLine(SyntaxKind.Identifier, "A score name"));
        }
        return children;
    }

    private GreenNode ParseFigure()
    {
        var children = new List<GreenNode> { Next(), ExpectOnLine(SyntaxKind.Identifier, "A figure name") };
        if (SameLine && Kind == SyntaxKind.String)
            children.Add(Next());
        return new GreenInner(SyntaxKind.FigureDecl, children);
    }

    /// <summary>dancer NAME RIG clause*, all on one line.</summary>
    private GreenNode ParseDancer()
    {
        var children = new List<GreenNode> { Next(), ExpectOnLine(SyntaxKind.Identifier, "A dancer name") };
        if (SameLine && AtName && !ClauseKeywords.Contains(Current.Text))
            children.Add(Next());
        while (SameLine && Kind != SyntaxKind.CloseBrace)
        {
            if (!AtName)
            {
                children.Add(SkipUnexpected());
                continue;
            }
            var clause = new List<GreenNode> { Next() };
            while (SameLine && (Kind is SyntaxKind.Number or SyntaxKind.String || (AtName && !ClauseKeywords.Contains(Current.Text))))
                clause.Add(Next());
            children.Add(new GreenInner(SyntaxKind.Clause, clause));
        }
        return new GreenInner(SyntaxKind.DancerDecl, children);
    }

    private GreenNode ParseDefinition(SyntaxKind kind, string what)
    {
        var children = new List<GreenNode> { Next(), Expect(SyntaxKind.Identifier, what), Expect(SyntaxKind.OpenBrace, "'{'") };
        while (!AtBlockEnd)
            children.Add(Kind == SyntaxKind.Identifier ? ParseTrack() : SkipUnexpected());
        children.Add(Expect(SyntaxKind.CloseBrace, "'}'"));
        return new GreenInner(kind, children);
    }

    private GreenNode ParseSection()
    {
        var children = new List<GreenNode> { Next(), Expect(SyntaxKind.Identifier, "A section name") };
        while (AtName)   // loop, inplace
            children.Add(Next());
        children.Add(Expect(SyntaxKind.OpenBrace, "'{'"));
        while (!AtBlockEnd)
        {
            if (Kind != SyntaxKind.Identifier)
                children.Add(SkipUnexpected());
            // `a { steps {` is a figure's block; `steps { left.fwd` is a track of the one unnamed figure.
            else if (Peek(1).Kind == SyntaxKind.OpenBrace && Peek(2).Kind == SyntaxKind.Identifier && Peek(3).Kind == SyntaxKind.OpenBrace)
                children.Add(ParseFigureBlock());
            else
                children.Add(ParseTrack());
        }
        children.Add(Expect(SyntaxKind.CloseBrace, "'}'"));
        return new GreenInner(SyntaxKind.SectionDecl, children);
    }

    private GreenNode ParseFigureBlock()
    {
        var children = new List<GreenNode> { Next(), Expect(SyntaxKind.OpenBrace, "'{'") };
        while (!AtBlockEnd)
            children.Add(Kind == SyntaxKind.Identifier ? ParseTrack() : SkipUnexpected());
        children.Add(Expect(SyntaxKind.CloseBrace, "'}'"));
        return new GreenInner(SyntaxKind.FigureBlock, children);
    }

    private GreenNode ParseTrack()
    {
        var children = new List<GreenNode> { Next(), Expect(SyntaxKind.OpenBrace, "'{'") };
        ParseTrackItems(children);
        children.Add(Expect(SyntaxKind.CloseBrace, "'}'"));
        return new GreenInner(SyntaxKind.Track, children);
    }

    private void ParseTrackItems(List<GreenNode> children)
    {
        while (!AtBlockEnd)
        {
            if (Kind == SyntaxKind.Bar)
            {
                children.Add(new GreenInner(SyntaxKind.BarLine, [Next()]));
            }
            else if (Kind == SyntaxKind.OpenBrace)
            {
                children.Add(ParseOverlay());
            }
            else if (Kind == SyntaxKind.Identifier)
            {
                if (Current.Text == "repeat" && Peek(1).Kind == SyntaxKind.Number)
                {
                    children.Add(ParseRepeat());
                }
                else if (Peek(1).Kind == SyntaxKind.OpenBrace)
                {
                    // `name {` can only open the next track: this one was left unclosed.
                    // Stop here so the missing '}' is reported and the next track survives.
                    return;
                }
                else
                {
                    children.Add(ParseEvent());
                }
            }
            else
            {
                children.Add(SkipUnexpected());
            }
        }
    }

    private GreenNode ParseRepeat()
    {
        var children = new List<GreenNode> { Next(), Next(), Expect(SyntaxKind.OpenBrace, "'{'") };
        ParseTrackItems(children);
        children.Add(Expect(SyntaxKind.CloseBrace, "'}'"));
        return new GreenInner(SyntaxKind.Repeat, children);
    }

    /// <summary>{ ... } & { ... }: rows that start together.</summary>
    private GreenNode ParseOverlay()
    {
        var children = new List<GreenNode> { ParseSequence() };
        while (Kind == SyntaxKind.Amp)
        {
            children.Add(Next());
            if (Kind == SyntaxKind.OpenBrace)
            {
                children.Add(ParseSequence());
            }
            else
            {
                children.Add(Missing(SyntaxKind.OpenBrace, "'{' (the rows on both sides of '&' are in braces)"));
                break;
            }
        }
        return new GreenInner(SyntaxKind.Overlay, children);
    }

    private GreenNode ParseSequence()
    {
        var children = new List<GreenNode> { Next() };
        ParseTrackItems(children);
        children.Add(Expect(SyntaxKind.CloseBrace, "'}'"));
        return new GreenInner(SyntaxKind.Sequence, children);
    }

    private GreenNode ParseEvent()
    {
        var children = new List<GreenNode> { ParseTarget(arguments: true) };
        bool hasDuration = false;

        while (true)
        {
            if (Kind == SyntaxKind.Colon && !hasDuration)
            {
                hasDuration = true;
                var duration = new List<GreenNode> { Next(), Expect(SyntaxKind.Number, "A length") };
                if (Kind == SyntaxKind.Slash)
                {
                    duration.Add(Next());
                    duration.Add(Expect(SyntaxKind.Number, "A number"));
                }
                // A unit is written against the number: 0.3s, 12f.
                if (_prevTight && Current.Leading.Length == 0 && (At("s") || At("f")))
                    duration.Add(Next());
                children.Add(new GreenInner(SyntaxKind.Duration, duration));
            }
            else if (Kind == SyntaxKind.At)
            {
                var annotation = new List<GreenNode> { Next(), Expect(SyntaxKind.Identifier, "A name after '@'") };
                if (Kind == SyntaxKind.OpenParen)
                    annotation.Add(ParseArguments());
                children.Add(new GreenInner(SyntaxKind.Annotation, annotation));
            }
            else if (Kind == SyntaxKind.Bang)
            {
                children.Add(new GreenInner(SyntaxKind.Mark, [Next(), Expect(SyntaxKind.Identifier, "A name after '!'")]));
            }
            else
            {
                break;
            }
        }

        return new GreenInner(SyntaxKind.Event, children);
    }

    private GreenNode ParseTarget(bool arguments)
    {
        var children = new List<GreenNode> { Next() };
        while (Kind == SyntaxKind.Dot)
        {
            children.Add(Next());
            // A dot at the end of a line is a word still being typed: do not reach into
            // the next line for it (this is the place completion offers the next word).
            if (SameLine && (AtName || Kind == SyntaxKind.Number))
                children.Add(Next());
            else
                children.Add(Missing(SyntaxKind.Identifier, "A word after '.'"));
        }
        if (arguments && Kind == SyntaxKind.OpenParen)
            children.Add(ParseArguments());
        return new GreenInner(SyntaxKind.Target, children);
    }

    /// <summary>( 0.4 ) or ( left.quarter ).</summary>
    private GreenNode ParseArguments()
    {
        var children = new List<GreenNode> { Next() };
        while (SameLine)
        {
            if (Kind == SyntaxKind.Number)
                children.Add(Next());
            else if (AtName)
                children.Add(ParseTarget(arguments: false));
            else
                break;
        }
        children.Add(ExpectOnLine(SyntaxKind.CloseParen, "')'"));
        return new GreenInner(SyntaxKind.Arguments, children);
    }

    private GreenNode ParseForm()
    {
        var children = new List<GreenNode> { Next() };
        if (AtName)
            children.Add(Next());
        children.Add(Expect(SyntaxKind.OpenBrace, "'{'"));
        while (!AtBlockEnd)
        {
            if (Kind == SyntaxKind.Identifier)
            {
                var item = new List<GreenNode> { Next() };
                if (Kind == SyntaxKind.Star)
                {
                    item.Add(Next());
                    item.Add(Expect(SyntaxKind.Number, "A repeat count"));
                }
                children.Add(new GreenInner(SyntaxKind.FormItem, item));
            }
            else
            {
                children.Add(SkipUnexpected());
            }
        }
        children.Add(Expect(SyntaxKind.CloseBrace, "'}'"));
        return new GreenInner(SyntaxKind.FormDecl, children);
    }

    /// <summary>score NAME? { form F  tempo 80  dancer alice  music "x.lys" score k  title "..." }</summary>
    private GreenNode ParseScore()
    {
        var children = new List<GreenNode> { Next() };
        if (AtName)
            children.Add(Next());
        children.Add(Expect(SyntaxKind.OpenBrace, "'{'"));
        while (Kind is not (SyntaxKind.CloseBrace or SyntaxKind.EndOfFile))
        {
            if (Kind == SyntaxKind.Identifier && ScoreItemKeywords.Contains(Current.Text))
            {
                var item = new List<GreenNode> { Next() };
                while (Kind is SyntaxKind.Number or SyntaxKind.String || AtName || (At("score") && SameLine))
                    item.Add(Next());
                children.Add(new GreenInner(SyntaxKind.ScoreItem, item));
            }
            else if (AtTopKeyword)
            {
                break;   // the block was left unclosed: the next item starts here
            }
            else
            {
                children.Add(SkipUnexpected());
            }
        }
        children.Add(Expect(SyntaxKind.CloseBrace, "'}'"));
        return new GreenInner(SyntaxKind.ScoreDecl, children);
    }
}
