namespace Shosa.Core.Syntax;

/// <summary>
/// Recursive descent over the token list. It never throws and never drops a token: what it
/// needs and does not find becomes a zero-width missing token, what it cannot place goes
/// into an Error node. A half-typed file therefore still has a tree the editor can use.
///
/// The parser knows the SHAPE of the language and none of its vocabulary: `left.fwd`,
/// `side.high` and `turn.left.quarter` are all "words joined by dots" here. Which words
/// exist is the binder's business, so a new movement word never changes the grammar.
/// </summary>
internal sealed class Parser
{
    private static readonly HashSet<string> TopKeywords = ["title", "tempo", "time", "figure", "phrase", "form"];
    private static readonly HashSet<string> PropertyKeywords = ["rig", "at", "facing"];

    private readonly List<GreenToken> _tokens;
    private int _index;
    private int _offset;    // where the current token's leading trivia starts
    private int _prevEnd;   // where the previous token's text ended

    public Parser(List<GreenToken> tokens) => _tokens = tokens;

    public List<Diagnostic> Diagnostics { get; } = [];

    private GreenToken Current => _tokens[_index];
    private SyntaxKind Kind => Current.Kind;
    private GreenToken Peek(int n) => _tokens[Math.Min(_index + n, _tokens.Count - 1)];
    private bool AtTopKeyword => Kind == SyntaxKind.Identifier && TopKeywords.Contains(Current.Text);
    private bool AtBlockEnd => Kind is SyntaxKind.CloseBrace or SyntaxKind.EndOfFile || AtTopKeyword;
    private bool AtName => Kind == SyntaxKind.Identifier && !AtTopKeyword;

    private GreenToken Next()
    {
        var token = Current;
        _prevEnd = _offset + token.Leading.Length + token.Text.Length;
        _offset += token.FullWidth;
        if (_index < _tokens.Count - 1)
            _index++;
        return token;
    }

    private GreenToken Expect(SyntaxKind kind, string what)
    {
        if (Kind == kind && !(kind == SyntaxKind.Identifier && AtTopKeyword))
            return Next();
        Diagnostics.Add(new Diagnostic("SH0010", DiagnosticSeverity.Error, new TextSpan(_prevEnd, 0), $"{what} expected here."));
        return GreenToken.Missing(kind);
    }

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
                case "title": return new GreenInner(SyntaxKind.Header, [Next(), Expect(SyntaxKind.String, "A title in quotes")]);
                case "tempo": return new GreenInner(SyntaxKind.Header, [Next(), Expect(SyntaxKind.Number, "Beats per minute")]);
                case "time":
                    return new GreenInner(SyntaxKind.Header,
                    [
                        Next(),
                        Expect(SyntaxKind.Number, "Beats per bar"),
                        Expect(SyntaxKind.Slash, "'/'"),
                        Expect(SyntaxKind.Number, "The beat unit"),
                    ]);
                case "figure": return ParseFigure();
                case "phrase": return ParsePhrase();
                case "form": return ParseForm();
            }
        }
        return SkipUnexpected();
    }

    private GreenNode ParseFigure()
    {
        var children = new List<GreenNode> { Next(), Expect(SyntaxKind.Identifier, "A figure name") };
        if (Kind == SyntaxKind.String)
            children.Add(Next());
        children.Add(Expect(SyntaxKind.OpenBrace, "'{'"));
        while (!AtBlockEnd)
            children.Add(Kind == SyntaxKind.Identifier ? ParseProperty() : SkipUnexpected());
        children.Add(Expect(SyntaxKind.CloseBrace, "'}'"));
        return new GreenInner(SyntaxKind.FigureDecl, children);
    }

    private GreenNode ParseProperty()
    {
        var children = new List<GreenNode> { Next() };
        while (Kind is SyntaxKind.Number or SyntaxKind.String || (AtName && !PropertyKeywords.Contains(Current.Text)))
            children.Add(Next());
        return new GreenInner(SyntaxKind.Property, children);
    }

    private GreenNode ParsePhrase()
    {
        var children = new List<GreenNode>
        {
            Next(),
            Expect(SyntaxKind.Identifier, "A phrase name"),
            Expect(SyntaxKind.OpenBrace, "'{'"),
        };
        while (!AtBlockEnd)
            children.Add(Kind == SyntaxKind.Identifier ? ParseFigureBlock() : SkipUnexpected());
        children.Add(Expect(SyntaxKind.CloseBrace, "'}'"));
        return new GreenInner(SyntaxKind.PhraseDecl, children);
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

    private GreenNode ParseEvent()
    {
        var children = new List<GreenNode> { ParseTarget() };

        if (Kind == SyntaxKind.Colon)
        {
            var duration = new List<GreenNode> { Next(), Expect(SyntaxKind.Number, "A number of beats") };
            if (Kind == SyntaxKind.Slash)
            {
                duration.Add(Next());
                duration.Add(Expect(SyntaxKind.Number, "A number"));
            }
            children.Add(new GreenInner(SyntaxKind.Duration, duration));
        }

        while (Kind == SyntaxKind.At)
        {
            var annotation = new List<GreenNode> { Next(), Expect(SyntaxKind.Identifier, "A name after '@'") };
            if (Kind == SyntaxKind.OpenParen)
            {
                annotation.Add(Next());
                annotation.Add(Expect(SyntaxKind.Number, "A number"));
                annotation.Add(Expect(SyntaxKind.CloseParen, "')'"));
            }
            children.Add(new GreenInner(SyntaxKind.Annotation, annotation));
        }

        return new GreenInner(SyntaxKind.Event, children);
    }

    private GreenNode ParseTarget()
    {
        var children = new List<GreenNode> { Next() };
        while (Kind == SyntaxKind.Dot)
        {
            var dot = Next();
            children.Add(dot);
            // A dot at the end of a line is a word still being typed: do not reach into
            // the next line for it (this is the place completion offers the next word).
            bool lineEnded = dot.Trailing.Contains('\n');
            if (!lineEnded && AtName)
            {
                children.Add(Next());
            }
            else
            {
                Diagnostics.Add(new Diagnostic("SH0010", DiagnosticSeverity.Error, new TextSpan(_prevEnd, 0), "A word expected after '.'."));
                children.Add(GreenToken.Missing(SyntaxKind.Identifier));
            }
        }
        return new GreenInner(SyntaxKind.Target, children);
    }

    private GreenNode ParseForm()
    {
        var children = new List<GreenNode> { Next(), Expect(SyntaxKind.OpenBrace, "'{'") };
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
}
