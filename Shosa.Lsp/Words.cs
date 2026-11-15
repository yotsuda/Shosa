using System.Globalization;
using System.Text.RegularExpressions;
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
    private static readonly Vocabulary.Word Hold = new("then", "Nothing new on this beat: the part stays through the word before, and goes to the next word through the `then`. For the feet and a @sharp word it only waits.");
    private static readonly Vocabulary.Word Skip = new("skip", "This track says nothing for this long: a wider track (body, arms) has the part meanwhile.");
    private static readonly Vocabulary.Word Again = new("q", "The group in braces before it, again: { a b } q q. With a length the group is stretched to it; q@mirror swaps left and right.");

    private static readonly Vocabulary.Word Verse = new("verse", "verse N { … } — done only the Nth time the score plays this section: verse 2, verse 1,3, verse 1-2. Verses side by side share one place in the row.");
    private static readonly Vocabulary.Word Bar = new("bar", "Whole bars: skip:3bar says nothing for three bars, then:2bar stays for two. At the start of a bar; the next | closes it.");

    private static readonly Vocabulary.Word[] TurnSides =
    [
        new("left", "Turn to the figure's left (counter-clockwise seen from above)."),
        new("right", "Turn to the figure's right."),
    ];

    private static readonly Vocabulary.Word[] DancerClauses =
    [
        new("on", "on FIGURE — the figure whose choreography this dancer dances."),
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

    private static readonly Vocabulary.Word[] ScoreItems =
    [
        new("dancer", "dancer NAME — a dancer who appears in this score. Every score lists at least one."),
        new("form", "form NAME — the form this score plays, instead of the unnamed one."),
        new("loop", "The end joins the start: the score is solved as if it followed itself."),
        new("inplace", "The travel over the floor is taken out of the output and given to the engine as a velocity."),
        new("tempo", "tempo N — the tempo this score begins at; the tempos after it keep their proportion. tempo 50% is half of what it would begin at; tempo 4. = 60 says it by a note."),
        new("title", "title \"…\""),
        new("music", "music \"song.lys\" score NAME — this score is danced to another song, or to another of the song's scores."),
    ];

    private static readonly Vocabulary.Word[] SectionItems =
    [
        new("upbeat", "upbeat N — the section's rows may begin N beats before its first bar line, over the end of what is played before. A row uses it by beginning with a bar of N beats."),
        new("tempo", "tempo N — beats per minute from this section on, until a section that writes another is played. tempo 80% is of the tempo before it; tempo 4. = 60 counts a note (a dotted crotchet) instead of the beat."),
        new("time", "time N/N — the time of this section, wherever it is played. A section that writes none is in the time at the top of the file."),
    ];

    private static readonly Vocabulary.Word[] FormChanges =
    [
        new("tempo", "tempo N — the section named next is played at this tempo this time, whatever it writes itself, and that time only: the name after it is played as if this were not written. tempo 110% is of the tempo that play would go at; tempo 4. = 60 counts a note instead of the beat."),
        new("time", "time N/N — the section named next is played in this time this time, whatever it writes itself, and that time only: the name after it is played as if this were not written."),
    ];

    public static readonly Vocabulary.Word[] TopLevel =
    [
        new("title", "title \"…\""),
        new("tempo", "tempo N — beats per minute. tempo 4. = 60 counts a note (a dotted crotchet) instead of the beat; in a file danced to a song, tempo 80% is of the song's."),
        new("time", "time N/N — beats per bar / beat unit."),
        new("fps", "fps N — frames per second of the output, and what `:12f` counts (30 if not written)."),
        new("music", "music \"song.lys\" score NAME — the Lily# song this is danced to: tempo, time and the order of the sections come from it."),
        new("figure", "figure NAME — an owner of choreography: its tracks are written in its block in each section."),
        new("dancer", "dancer NAME humanoid on FIGURE at X Z facing … — a body that dances a figure."),
        new("pose", "pose NAME { TRACK { target } } — a named way of standing or holding the body."),
        new("move", "move NAME { TRACK { events } } — a named row of events; written with a length it is stretched to it."),
        new("section", "section NAME { FIGURE { TRACK { … } } } — a stretch of time: what each figure does in it."),
        new("form", "form { A |: B :| } — the order the sections are played in. A named one (form short { B }) is played by the score that names it."),
        new("score", "score NAME { dancer D  form F  loop } — one file written: who dances, and which form. The unnamed score plays the unnamed form."),
    ];

    /// <summary>
    /// What a top-level word is completed to: the least the item needs, with the names the
    /// file already has filled in. Nothing of a file is left out, so the editor writes it.
    /// </summary>
    public static string? Snippet(SyntaxTree tree, string word)
    {
        string figure = FigureNames(tree).Select(f => f.Id).FirstOrDefault() ?? "solo";
        string dancer = DancerNames(tree).FirstOrDefault() ?? figure;
        string sections = string.Join(' ', tree.Root.ChildNodes(SyntaxKind.SectionDecl).Select(s => s.Token(SyntaxKind.Identifier, 1)?.Text).OfType<string>());
        return word switch
        {
            "title" => "title \"${1:Untitled}\"",
            "tempo" => "tempo ${1:100}",
            "time" => "time ${1:4}/${2:4}",
            "fps" => "fps ${1:30}",
            "music" => "music \"$1\"",
            "figure" => "figure ${1:" + figure + "}",
            "dancer" => "dancer ${1:" + dancer + "} humanoid on ${2:" + figure + "}",
            "pose" => "pose ${1:name} {\n  ${2:arms} { $0 }\n}",
            "move" => "move ${1:name} {\n  ${2:steps} { $0 }\n}",
            "section" => "section ${1:Main} {\n  ${2:" + figure + "} {\n    ${3:steps} { $0 }\n  }\n}",
            "form" => "form { ${1:" + (sections == "" ? "Main" : sections) + "} }",
            "score" => "score { dancer ${1:" + dancer + "} }",
            _ => null,
        };
    }

    /// <summary>What `verse` is completed to in a row: the play it is for, and its braces.</summary>
    public const string VerseSnippet = "verse ${1:2} { $0 }";

    /// <summary>
    /// Whether the cursor stands in the length of a `skip` or a `then`, after a whole number
    /// (`skip:3`, `then:2b`): the one place the unit `bar` can be written.
    /// </summary>
    private static bool AtBars(string text, int wordStart, int offset)
    {
        string typed = text[wordStart..offset];
        int digits = typed.TakeWhile(char.IsAsciiDigit).Count();
        if (digits == 0 || !"bar".StartsWith(typed[digits..], StringComparison.Ordinal))
            return false;
        int end = wordStart - 1, start = end;   // the colon
        while (start > 0 && char.IsLetter(text[start - 1]))
            start--;
        return text[start..end] is "skip" or "then";
    }

    private static IEnumerable<string> DancerNames(SyntaxTree tree) =>
        tree.Root.ChildNodes(SyntaxKind.DancerDecl).Select(d => d.Token(SyntaxKind.Identifier, 1)?.Text).OfType<string>();

    private static bool IsLimb(string track) => track is "arms" or "leftArm" or "rightArm" or "leftLeg" or "rightLeg";
    private static bool IsArm(string track) => track is "arms" or "leftArm" or "rightArm";

    /// <summary>The poses and moves a file defines, as words: they can be written in any track that shares a part with them.</summary>
    /// <param name="lengths">The beats each move takes when written without a length (<see cref="ScoreIr.MoveLengths"/>), to say them.</param>
    public static List<Vocabulary.Word> Definitions(SyntaxTree tree, IReadOnlyDictionary<string, double>? lengths = null) =>
        tree.Root.ChildNodes().Where(n => n.Kind is SyntaxKind.PoseDecl or SyntaxKind.MoveDecl)
            .Select(n => (Node: n, Name: n.Token(SyntaxKind.Identifier, 1)?.Text)).Where(d => d.Name is not null)
            .Select(d => new Vocabulary.Word(d.Name!, (d.Node.Kind == SyntaxKind.PoseDecl ? "Pose" : "Move") + " of this file: "
                + string.Join(", ", d.Node.ChildNodes(SyntaxKind.Track).Select(t => t.Token(SyntaxKind.Identifier)?.Text)) + "."
                + (d.Node.Kind == SyntaxKind.MoveDecl && lengths is not null && lengths.TryGetValue(d.Name!, out double beats)
                    ? $" Written without a length it takes {Beats(beats)}; with one ({d.Name}:{Math.Max(1, Math.Round(beats * 2)).ToString(CultureInfo.InvariantCulture)}) it is stretched to it."
                    : d.Node.Kind == SyntaxKind.PoseDecl ? " Written without a length it is reached in 1 beat." : "")))
            .ToList();

    private static string Beats(double beats) => beats.ToString("0.###", CultureInfo.InvariantCulture) + (beats == 1 ? " beat" : " beats");

    /// <summary>What a `music` line can be completed from: the song files beside the dance, and the scores a song has.</summary>
    public sealed record Songs(Func<IReadOnlyList<string>> Files, Func<string, IReadOnlyList<string>> ScoresOf);

    private static readonly Regex MusicFile = new("""\bmusic\s+"([^"]*)$""", RegexOptions.Compiled);
    private static readonly Regex MusicScore = new("""\bmusic\s+"([^"]*)"\s+score\s+\w*$""", RegexOptions.Compiled);
    private static readonly Regex MusicTail = new("""\bmusic\s+"[^"]*"\s+\w*$""", RegexOptions.Compiled);

    /// <summary>
    /// What can be typed on a `music` line (at the top of the file or in a score): the file's
    /// name inside the quotes, `score` after it, a score of that song after `score`. Null
    /// anywhere else. <c>From</c> is where the text that a file name replaces begins: a name has
    /// dots and dashes in it, which an editor would not take for one word.
    /// </summary>
    public static (IEnumerable<Vocabulary.Word> Words, string Kind, int? From)? AtMusic(SyntaxTree tree, int offset, Songs? songs)
    {
        string text = tree.Text;
        int lineStart = text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1;
        if (lineStart > offset)
            return null;
        string line = text[lineStart..offset];
        if (line.Contains("//", StringComparison.Ordinal))
            return null;   // a comment

        if (MusicFile.Match(line) is { Success: true } file)
        {
            var files = songs?.Files() ?? [];
            return (files.Select(f => new Vocabulary.Word(f, "A Lily# song beside this file.")), "file", offset - file.Groups[1].Length);
        }
        if (MusicScore.Match(line) is { Success: true } score)
        {
            var scores = songs?.ScoresOf(score.Groups[1].Value) ?? [];
            return (scores.Select(s => new Vocabulary.Word(s, $"A score of {score.Groups[1].Value}.")), "value", null);
        }
        if (MusicTail.IsMatch(line))
        {
            var of = new Vocabulary.Word("score", "score NAME — one of the song's scores, instead of its default.");
            return (Enclosing(tree, SyntaxKind.ScoreDecl, offset) is null ? [of] : [of, .. ScoreItems], "property", null);
        }
        return null;
    }

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
            return before.Count == 0 ? [.. definitions, Hold, Skip, Again] : [];

        if (track == "steps")
        {
            if (before.Count == 0)
                return [.. Vocabulary.Feet, .. Vocabulary.SupportActions, .. definitions, Hold, Skip, Again];
            return before[0] switch
            {
                "left" or "right" => Except([.. Vocabulary.StepDirections, .. Vocabulary.Levels], before.Skip(1)),
                "both" or "together" => Vocabulary.Levels,
                "turn" => before.Count == 1 ? [.. TurnSides, .. figures] : before.Count == 2 ? Vocabulary.TurnAmounts : [],
                "jump" => Except(Vocabulary.JumpWords, before.Skip(1)),
                "shift" => before.Count == 1 ? Vocabulary.ShiftWords : [],
                "lift" or "land" => Except(Vocabulary.JumpWords.Where(w => w.Name is "fwd" or "back" or "left" or "right"), before.Skip(1)),
                _ => [],
            };
        }

        if (track is "twist" or "hips")
        {
            if (before.Count == 0)
                return [.. track == "hips" ? Vocabulary.HipsWords : Vocabulary.TwistWords, .. definitions, Hold, Skip, Again];
            return [];   // after left / right comes a number of degrees
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
            return [.. first, .. definitions, Hold, Skip, Again];
        }
        if (IsArm(track) && before is ["across"])
            return [.. Except([.. directions, .. Vocabulary.Levels], before), new("shoulder", "across.shoulder: the hand on the opposite shoulder.")];
        if (before[0] is "up" or "down" or "then" or "skip" || Vocabulary.Has(Vocabulary.Anchors, before[0]) && IsArm(track))
            return [];
        bool hasLevel = before.Any(w => Vocabulary.Has(Vocabulary.Levels, w) || double.TryParse(w, out _));
        return Except(hasLevel ? directions : [.. directions, .. Vocabulary.Levels], before);
    }

    public static IEnumerable<Vocabulary.Word> ForAnnotation(string track) => Vocabulary.Annotations.Where(w => w.Name switch
    {
        "bent" => IsLimb(track),
        "body" => IsArm(track),
        "round" => track is "torso",
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
            if (trigger == ':' && AtBars(text, wordStart, offset))
                return ([Bar], "value");
            if (trigger is ':' or '!' or '(' || (trigger != '.' && wordStart > 0 && char.IsAsciiDigit(text[wordStart - 1])))
                return ([], "none");
            var targets = ForTarget(name, trigger == '.' ? ChainBefore(text, wordStart - 1) : [], Definitions(tree), figures);
            // A verse is written where an event is, in a section's row: a move is not played a first and a second time.
            bool inSection = Enclosing(tree, SyntaxKind.PoseDecl, offset) is null && Enclosing(tree, SyntaxKind.MoveDecl, offset) is null;
            return (trigger != '.' && inSection ? targets.Append(Verse) : targets, "word");
        }

        if (Enclosing(tree, SyntaxKind.FigureBlock, offset) is not null
            || Enclosing(tree, SyntaxKind.PoseDecl, offset) is not null || Enclosing(tree, SyntaxKind.MoveDecl, offset) is not null)
            return (Vocabulary.Tracks, "track");

        if (Enclosing(tree, SyntaxKind.SectionDecl, offset) is { } section)
        {
            // The lines a section begins with come before its first block, each on its own line.
            int lineBegins = text.LastIndexOf('\n', Math.Max(0, wordStart - 1)) + 1;
            if (text[lineBegins..wordStart].Trim() is "tempo" or "time" or "upbeat")
                return ([], "none");
            bool atHead = !section.ChildNodes().Any(n => n.Kind is SyntaxKind.FigureBlock or SyntaxKind.Track && n.Span.Start < wordStart);
            return (atHead ? [.. figures, .. SectionItems] : figures, "figure");
        }

        if (Enclosing(tree, SyntaxKind.FormDecl, offset) is { } form)
        {
            // After `tempo` or `time` comes a number.
            string? before = text[form.Token(SyntaxKind.OpenBrace)!.Span.End..Math.Max(wordStart, form.Token(SyntaxKind.OpenBrace)!.Span.End)]
                .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            return before is "tempo" or "time" or "=" ? ([], "none") : ([.. sections, .. FormChanges], "section");
        }

        // The one-line items: what has been written on this line so far decides.
        int lineStart = text.LastIndexOf('\n', Math.Max(0, wordStart - 1)) + 1;

        if (Enclosing(tree, SyntaxKind.ScoreDecl, offset) is { } score)
        {
            // After `dancer` a dancer of the file, after `form` one of its named forms.
            int from = Math.Max(lineStart, Math.Min(wordStart, score.Token(SyntaxKind.OpenBrace)!.Span.End));
            string? before = text[from..wordStart].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (before == "dancer")
                return (DancerNames(tree).Select(d => new Vocabulary.Word(d, "Dancer")), "figure");
            if (before == "form")
                return (tree.Root.ChildNodes(SyntaxKind.FormDecl).Select(f => f.Token(SyntaxKind.Identifier, 1)?.Text).OfType<string>().Select(f => new Vocabulary.Word(f, "Form")), "section");
            return (ScoreItems, "property");
        }

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
        if (line.Length > 0)
            return ([], "none");

        return (TopLevel, "keyword");
    }

    private static IEnumerable<(string Id, string Label)> FigureNames(SyntaxTree tree) =>
        from figure in tree.Root.ChildNodes(SyntaxKind.FigureDecl)
        let id = figure.Token(SyntaxKind.Identifier, 1)
        where id is not null
        select (id.Text, id.Text);

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
    public static (Vocabulary.Word Word, TextSpan Span)? Describe(SyntaxTree tree, int offset, IReadOnlyDictionary<string, double>? lengths = null)
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
            word = ForTarget(track, before, Definitions(tree, lengths), figures).FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind is SyntaxKind.PoseDecl or SyntaxKind.MoveDecl && token == parent.Token(SyntaxKind.Identifier, 1))
        {
            // The name where it is defined says the same as where it is used.
            word = Definitions(tree, lengths).FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.Annotation)
        {
            word = Vocabulary.Annotations.FirstOrDefault(w => w.Name == token.Text) is { } a ? new Vocabulary.Word("@" + a.Name, a.Doc) : null;
        }
        else if (parent.Kind == SyntaxKind.Mark)
        {
            word = new Vocabulary.Word("!" + token.Text, "A moment named for the engine: the beat this word is written at, when the part is there. Written to the events of the output.");
        }
        else if (parent.Kind == SyntaxKind.Track && first)
        {
            word = Vocabulary.Tracks.FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.Clause && first)
        {
            word = DancerClauses.FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.SectionItem && first)
        {
            word = SectionItems.FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.FormChange && first)
        {
            word = FormChanges.FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.ScoreItem && first)
        {
            word = ScoreItems.FirstOrDefault(w => w.Name == token.Text);
        }
        else if (first && parent.Kind is SyntaxKind.Header or SyntaxKind.FigureDecl or SyntaxKind.DancerDecl or SyntaxKind.PoseDecl
                     or SyntaxKind.MoveDecl or SyntaxKind.SectionDecl or SyntaxKind.FormDecl or SyntaxKind.ScoreDecl)
        {
            word = TopLevel.FirstOrDefault(w => w.Name == token.Text);
        }

        return word is null ? null : (word, token.Span);
    }
}
