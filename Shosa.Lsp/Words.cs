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
        new("left", "Turn to the dancer's left (counter-clockwise seen from above): left(90)."),
        new("right", "Turn to the dancer's right: right(90)."),
    ];

    private static readonly Vocabulary.Word[] DancerClauses =
    [
        new("on", "on ROLE — the role whose choreography this dancer dances."),
        new("at", "Start position in metres: at X Z (x is stage left, z is toward the audience)."),
        new("facing", "facing front | back | stageLeft | stageRight, or left(N) / right(N): N degrees turned from the front."),
        new("mirror", "Dances the role with left and right swapped."),
        new("delay", "delay N — starts N beats late (a canon)."),
        new("free", "The rules of weight and gravity never apply to this body: it can hang in the air (lift)."),
        new("in", "in SECTION … — dances only in these sections."),
        new("sings", "sings PART — the mouth sings the words of this part of the song (its singer line): sings melody."),
        new("mic", "mic right | left | both — the dancer holds a mic in that hand (both: two hands while singing, the right one between). While it sings (sings PART) the mic is at the mouth, and that arm's words wait; between phrases the arm dances its words, the mic in hand."),
        new("juggles", "juggles balls | clubs | rings — what the dancer's hands throw in its juggle row. How many is the pattern's: 3 is three, (4,4) four."),
        new("height", "height N m — how tall the preview draws the character (the VRM file), to the top of its hair: height 1.5m (or 150cm). Write the same height to match dancers; the movement does not change."),
    ];

    private static readonly Vocabulary.Word[] Props =
    [
        new("balls", "Balls (beanbags, 68 mm)."),
        new("clubs", "Clubs, turning end over end: once for a 3, twice for a 5."),
        new("rings", "Rings, thrown along the forearms: the face to the side, the edge to the front."),
    ];

    /// <summary>What a juggle row's words start from: its own words, and patterns jugglers know.</summary>
    private static readonly Vocabulary.Word[] JuggleWords =
    [
        new("every", "every N — beats from one throw to the next, from here on: every 1/2 throws on every half beat. Written before the first pattern of the row."),
        new(Vocabulary.Skip, "No throwing for its length: the hands keep the balls and the arms dance their rows. The next pattern starts again from the hands."),
        new(Vocabulary.Again, "The group in braces before it again: { 531:3 3:1 } q q"),
        new("3", "The cascade: three balls, each thrown across to the other hand."),
        new("531", "Three balls: a high throw across, a middle one, and one handed straight across."),
        new("441", "Three balls: two throws to the same hand, then one handed across."),
        new("51", "Three balls: a high throw across, then one handed across (the shower)."),
        new("4", "The fountain: four balls, each hand on its own side."),
        new("5", "The five-ball cascade."),
        new("(4,4)", "Four balls, both hands at once, each on its own side."),
        new("(4,2x)*", "Three balls, both hands at once: a throw on one side while the other hand passes one across."),
    ];

    private static readonly Vocabulary.Word[] MicHands =
    [
        new("right", "The mic in the right hand."),
        new("left", "The mic in the left hand."),
        new("both", "Both hands on the mic while singing; between phrases the right hand holds it and the left is free."),
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
        new("inPlace", "The travel over the floor is taken out of the output and given to the engine as a velocity."),
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
        new("using", "using \"parts.shosa\" — another .shosa file joins this one, as in Lily#: its poses and moves (and anything else it writes) can be used here. A file of poses and moves alone needs no score."),
        new("title", "title \"…\""),
        new("tempo", "tempo N — beats per minute. tempo 4. = 60 counts a note (a dotted crotchet) instead of the beat; in a file danced to a song, tempo 80% is of the song's."),
        new("time", "time N/N — beats per bar / beat unit."),
        new("fps", "fps N — frames per second of the output, and what `:12f` counts (30 if not written)."),
        new("music", "music \"song.lys\" score NAME — the Lily# song this is danced to: tempo, time and the order of the sections come from it."),
        new("role", "role NAME — a part in the dance, an owner of choreography: its tracks are written in its block in each section."),
        new("dancer", "dancer NAME humanoid on ROLE at X Z facing … — a body that dances a role."),
        new("pose", "pose NAME { TRACK { target } } — a named way of standing or holding the body."),
        new("move", "move NAME { TRACK { events } } — a named row of events; written with a length it is stretched to it."),
        new("section", "section NAME { ROLE { TRACK { … } } } — a stretch of time: what each role does in it."),
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
            "using" => "using \"$1\"",
            "role" => "role ${1:" + figure + "}",
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

    private static bool IsLimb(string track) => track is "arms" or "leftArm" or "rightArm" or "leftLeg" or "rightLeg" or "legs";
    private static bool IsArm(string track) => track is "arms" or "leftArm" or "rightArm";

    /// <summary>The poses and moves a file defines, as words: they can be written in any track that shares a part with them.</summary>
    /// <param name="lengths">The beats each move takes when written without a length (<see cref="ScoreIr.MoveLengths"/>), to say them.</param>
    /// <param name="brought">The poses and moves the file's `using` lines bring in (<see cref="Brought"/>).</param>
    public static List<Vocabulary.Word> Definitions(SyntaxTree tree, IReadOnlyDictionary<string, double>? lengths = null, IReadOnlyList<Vocabulary.Word>? brought = null) =>
        Described(tree.Root.ChildNodes(), "this file", lengths)
            .Concat((brought ?? []).Where(b => !tree.Root.ChildNodes().Any(n => n.Kind is SyntaxKind.PoseDecl or SyntaxKind.MoveDecl && n.Token(SyntaxKind.Identifier, 1)?.Text == b.Name)))
            .ToList();

    /// <summary>
    /// The poses and moves the `using` lines of a compiled file bring in, as words, each saying
    /// which file it is from.
    /// </summary>
    public static List<Vocabulary.Word> Brought(Shosa.Core.ShosaCompilation compilation, string? folder)
    {
        if (compilation.Included.Count == 0)
            return [];
        var words = new List<Vocabulary.Word>();
        foreach (var file in compilation.Included)
        {
            string name = file.Name(folder);
            var nodes = compilation.Bound.Root.ChildNodes().Where(n => n.Span.Start >= file.Start && n.Span.Start < file.End);
            words.AddRange(Described(nodes, name, compilation.Score.MoveLengths));
        }
        return words;
    }

    private static IEnumerable<Vocabulary.Word> Described(IEnumerable<SyntaxNode> items, string from, IReadOnlyDictionary<string, double>? lengths) =>
        items.Where(n => n.Kind is SyntaxKind.PoseDecl or SyntaxKind.MoveDecl)
            .Select(n => (Node: n, Name: n.Token(SyntaxKind.Identifier, 1)?.Text)).Where(d => d.Name is not null)
            .Select(d => new Vocabulary.Word(d.Name!, (d.Node.Kind == SyntaxKind.PoseDecl ? "Pose" : "Move") + $" of {from}: "
                + string.Join(", ", d.Node.ChildNodes(SyntaxKind.Track).Select(t => t.Token(SyntaxKind.Identifier)?.Text)) + "."
                + (d.Node.Kind == SyntaxKind.MoveDecl && lengths is not null && lengths.TryGetValue(d.Name!, out double beats)
                    ? $" Written without a length it takes {Beats(beats)}; with one ({d.Name}:{Math.Max(1, Math.Round(beats * 2)).ToString(CultureInfo.InvariantCulture)}) it is stretched to it."
                    : d.Node.Kind == SyntaxKind.PoseDecl ? " Written without a length it is reached in 1 beat." : "")));

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
            return before.Count == 1 ? Vocabulary.Parts : before.Count == 2 && before[1] == "hand" ? [.. Vocabulary.Levels.Where(w => w.Name != "sit")] : [];

        if (track == "body")
            return before.Count == 0 ? [.. definitions, Hold, Skip, Again] : [];

        if (track == "steps")
        {
            if (before.Count == 0)
                return [.. Vocabulary.Feet, .. Vocabulary.SupportActions, .. Vocabulary.Bases, .. definitions, Hold, Skip, Again];
            var jumpWays = Vocabulary.JumpWords.Where(w => w.Name is "place" or "fwd" or "back" or "left" or "right");
            return before[0] switch
            {
                "leftFoot" or "rightFoot" => Except(Vocabulary.StepDirections, before.Skip(1)),
                "turn" => before.Count == 1 ? [.. TurnSides, .. figures] : [],   // after the side, its degrees in brackets
                // (After a foot, the stance it lands in: that step's own words; how high and the beats may still follow.)
                "jump" => before.Skip(1).Any(w => w is "leftFoot" or "rightFoot")
                    ? [.. Except(Vocabulary.StepDirections, before.SkipWhile(w => w is not ("leftFoot" or "rightFoot")).Skip(1)), .. Except(Vocabulary.JumpWords.Where(w => w.Name is "up" or "beat"), before.Skip(1))]
                    : Except(Vocabulary.JumpWords, before.Skip(1)),
                "hop" or "leap" => Except(jumpWays, before.Skip(1)),
                "shift" => before.Count == 1 ? Vocabulary.ShiftWords : [],
                "lift" or "land" => Except(jumpWays.Where(w => w.Name != "place"), before.Skip(1)),
                // What carries the weight off the feet (DESIGN.md 128): where it is put from where the weight was.
                var onto when Vocabulary.Has(Vocabulary.Bases, onto) => Except(jumpWays, before.Skip(1)),
                _ => [],
            };
        }

        if (track == "height")
            return before.Count == 0 ? [.. Vocabulary.Levels, .. definitions, Hold, Skip, Again] : [];   // after low, how far in brackets

        if (track is "shoulders" or "hips")
            return before.Count == 0 ? [.. track == "hips" ? Vocabulary.HipsWords : Vocabulary.ShouldersWords, .. definitions, Hold, Skip, Again] : [];

        if (track == "turnout")
            return before.Count == 0 ? [.. Vocabulary.TurnoutWords, .. definitions, Hold, Skip, Again] : [];   // after out / in, degrees

        // A hand: its shape, then fingers of their own, then which way the fingers point and the palm faces (DESIGN.md 126).
        if (track is "hands" or "leftHand" or "rightHand")
        {
            if (before.Count == 0)
                return [.. Vocabulary.HandShapes, .. Vocabulary.Fingers, .. Vocabulary.HandTurns, .. definitions, Hold, Skip, Again];
            if (before[0] is "then" or "skip")
                return [];
            if (before.Contains("palm"))
                return [];
            if (before.Contains("fingers"))
                return Vocabulary.HandTurns.Where(w => w.Name == "palm");
            return [.. Except(Vocabulary.Fingers, before), .. Vocabulary.HandTurns];
        }

        // The torso and the head say left / right; a limb says side / across.
        var directions = Vocabulary.Directions.Where(w => IsLimb(track) ? w.Name is not ("left" or "right") : w.Name is not ("side" or "across")).ToList();
        // The shape words this track takes, in their order: after the direction, and each after the one before.
        var shapes = Vocabulary.Shapes.Where(w => w.Name switch
        {
            "bent" => IsLimb(track),
            "elbow" => IsArm(track),
            "knee" or "pointed" or "flexed" => IsLimb(track) && !IsArm(track),
            "round" or "facing" => track == "torso",
            "tilt" => track == "head",
            _ => IsLimb(track) || track == "head",
        }).ToList();
        if (before.Count == 0)
        {
            IEnumerable<Vocabulary.Word> first = [.. directions, .. Vocabulary.Poles.Where(w => w.Name != Vocabulary.Floor)];
            if (IsArm(track))
                first = first.Concat(Vocabulary.Anchors.Where(a => !a.Name.Contains('.')));
            else if (IsLimb(track))
                first = first.Concat(Vocabulary.LegAnchors.Where(a => !a.Name.Contains('.')));
            if (IsArm(track) || track == "head")
                first = first.Concat(figures);
            if (IsArm(track))
                first = first.Append(Vocabulary.StayWord);
            return [.. first, .. definitions, Hold, Skip, Again];
        }
        if (before[0] is "then" or "skip" or Vocabulary.Stay)
            return [];
        // A place: its `.across` or `.back`, then only which way the joint points.
        if (IsArm(track) && before is ["shoulder"])
            return [new("across", "shoulder.across: the hand on the opposite shoulder."), .. shapes.Where(s => s.Name == "elbow")];
        if (IsLimb(track) && !IsArm(track) && before is [var place] && Vocabulary.Has(Vocabulary.LegAnchors, place))
            return [new("back", $"{place}.back: the toe at the back of the other leg's {place[5..].ToLowerInvariant()}."), .. shapes.Where(s => s.Name == "knee")];
        if (IsArm(track) && Vocabulary.Has(Vocabulary.Anchors, string.Join('.', before)) || !IsArm(track) && Vocabulary.Has(Vocabulary.LegAnchors, string.Join('.', before)))
            return shapes.Where(s => s.Name is "elbow" or "knee");
        // After a shape word, the ones that come after it.
        int lastShape = before.Select(Vocabulary.ShapeOrder).DefaultIfEmpty(-1).Max();
        // (A toe on the floor is pointed there: after `floor` only how the knee bends and points.)
        if (lastShape >= 0)
            return shapes.Where(s => Vocabulary.ShapeOrder(s.Name) > lastShape && !(s.Name == "knee" && before.Contains("elbow"))
                && !(s.Name == "flexed" && before.Contains("pointed")) && !(before.Contains(Vocabulary.Floor) && s.Name is not ("bent" or "knee" or "elbow")));
        int horizontal = before.Count(w => directions.Any(d => d.Name == w));
        bool vertical = before.Any(w => w is "up" or "down");
        // A leg to the floor is pointed, its toe there: only a bent knee follows (`back.floor.bent(60)`).
        if (before.Contains(Vocabulary.Floor))
            return shapes.Where(s => s.Name is "bent" or "knee" or "elbow");
        // up.fwd(10): from straight up toward one way.
        if (before is ["up" or "down"])
            return [.. directions, .. shapes];
        if (vertical)
            return shapes;
        IEnumerable<Vocabulary.Word> next = horizontal == 1 ? Except(directions, before) : [];
        // (A leg and an arm go to the floor: a toe, a hand.)
        return [.. next, .. Vocabulary.Poles.Where(w => IsLimb(track) || w.Name != Vocabulary.Floor), .. shapes];
    }

    public static IEnumerable<Vocabulary.Word> ForAnnotation(string track) => Vocabulary.Annotations.Where(w => w.Name switch
    {
        "turn" => track is "steps",
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
        // A jump's landing: mirrored as they stood (`change`), or side by side (`apart`, `stand`, `together`).
        string[] widths = ["apart", "stand", "together"];
        if (taken.Contains("change"))
            taken.UnionWith(widths);
        else if (widths.Any(taken.Contains))
            taken.Add("change");
        return words.Where(w => !taken.Contains(w.Name));
    }

    /// <summary>What can be typed at a position, and what kind of thing it is.</summary>
    public static (IEnumerable<Vocabulary.Word> Words, string Kind) At(SyntaxTree tree, int offset, IReadOnlyList<Vocabulary.Word>? brought = null)
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
            // A juggle row's words are siteswap (DESIGN.md 118): offer `every`, `skip` and a few patterns to start from.
            if (name == "juggle")
            {
                if (trigger == '@')
                    return ([new Vocabulary.Word("mirror", "The left hand throws first (a pair's first number is the left hand's)."), .. Vocabulary.Tricks], "annotation");
                return trigger is ':' or '!' or '(' or '.' || (wordStart > 0 && char.IsAsciiDigit(text[wordStart - 1])) ? ([], "none") : (JuggleWords, "word");
            }
            if (trigger == '@')
                return (ForAnnotation(name), "annotation");
            if (trigger == ':' && AtBars(text, wordStart, offset))
                return ([Bar], "value");
            if (trigger is ':' or '!' or '(' || (trigger != '.' && wordStart > 0 && char.IsAsciiDigit(text[wordStart - 1])))
                return ([], "none");
            var targets = ForTarget(name, trigger == '.' ? ChainBefore(text, wordStart - 1) : [], Definitions(tree, null, brought), figures);
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
                [.., "mic"] => (MicHands, "value"),
                [.., "juggles"] => (Props, "value"),
                [.., "at"] or [.., "at", _] or [.., "delay"] or [.., "sings"] or [.., "height"] => ([], "none"),
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
            // A word's bracket belongs to it: fwd.left(30).up — skip back over it to the word.
            if (start > 0 && text[start - 1] == ')')
            {
                int depth = 0;
                while (start > 0)
                {
                    char c = text[--start];
                    if (c == ')')
                        depth++;
                    else if (c == '(' && --depth == 0)
                        break;
                }
                end = start;
            }
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
    public static (Vocabulary.Word Word, TextSpan Span)? Describe(SyntaxTree tree, int offset, IReadOnlyDictionary<string, double>? lengths = null, IReadOnlyList<Vocabulary.Word>? brought = null)
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
            // @turn(left.quarter), or where a joint points: @elbow(side.high)
            bool joint = parent.Parent.Parent?.Token(SyntaxKind.Identifier)?.Text is "elbow" or "knee";
            word = (joint ? Vocabulary.Directions.Concat(Vocabulary.Poles) : TurnSides)
                .FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind == SyntaxKind.Target)
        {
            var before = parent.ChildTokens().Where(t => t.Kind is SyntaxKind.Identifier or SyntaxKind.Number && !t.IsMissing && t.Span.Start < token.Span.Start)
                .Select(t => t.Text).ToList();
            var figures = FigureNames(tree).Select(n => new Vocabulary.Word(n.Id, $"Figure {n.Label}")).ToList();
            word = ForTarget(track, before, Definitions(tree, lengths, brought), figures).FirstOrDefault(w => w.Name == token.Text);
        }
        else if (parent.Kind is SyntaxKind.PoseDecl or SyntaxKind.MoveDecl && token == parent.Token(SyntaxKind.Identifier, 1))
        {
            // The name where it is defined says the same as where it is used.
            word = Definitions(tree, lengths, brought).FirstOrDefault(w => w.Name == token.Text);
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
