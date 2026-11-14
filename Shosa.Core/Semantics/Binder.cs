using System.Globalization;
using Shosa.Core.Motion;
using Shosa.Core.Syntax;

namespace Shosa.Core.Semantics;

/// <summary>
/// Syntax tree to <see cref="ScoreIr"/>: resolves names, gives every word its meaning, expands
/// the poses and moves the file defines, settles which track has each part of the body at each
/// moment, lays the clips out in time, and reports what does not make sense (SH1xxx).
/// It reads the red tree and never throws on a broken one: an event it cannot understand
/// still takes its time, so one bad word does not shift everything after it.
/// </summary>
public static class Binder
{
    /// <param name="score">The score to bind; null or "" is the file's default one.</param>
    /// <param name="songs">Reads the song a `music` line names; null where there is nothing to read one with.</param>
    public static (ScoreIr Score, List<Diagnostic> Diagnostics) Bind(SyntaxTree tree, string? score = null, SongSource? songs = null) =>
        new BinderRun(tree, score ?? "", songs).Run();
}

internal sealed class BinderRun(SyntaxTree tree, string scoreName, SongSource? songs)
{
    private const double Epsilon = 1e-6;

    private sealed class DancerSpec
    {
        public string Id = "", Name = "", Figure = "";
        public double X, Z, FacingDeg, Delay;
        public bool Mirror, Free;
        public HashSet<string>? In;
    }

    /// <summary>One event on one channel before the overlaps are settled.</summary>
    private sealed record Raw(Seg Seg, int[] Priority, int Order);

    /// <summary>
    /// Where the walk is. Channels are the parts of the body the enclosing track may write (a
    /// definition used in a track does only that track's share); Priority says who wins where
    /// two events want the same part: the narrower track, then the later row of an `&`.
    /// </summary>
    private sealed record Ctx(
        DancerSpec Dancer,
        MotionIr? Clip,
        Dictionary<string, List<Raw>> Sink,
        string[] Channels,
        string TrackName,
        int[] Priority,
        double Origin,
        bool Mirror = false,
        bool Free = false,
        double Scale = 1,
        int Depth = 0,
        bool CheckBars = false,
        bool Stance = false,
        double? ForcedLength = null,
        string? EaseOverride = null,
        TextSpan? Src = null);

    private readonly List<Diagnostic> _diagnostics = [];
    private readonly HashSet<(string, int, int)> _reported = [];
    private int _mute;
    private int _order;
    private readonly List<(MarkIr Mark, List<(string Channel, Raw Raw)> Raws)> _marks = [];   // of the dancer being bound

    private string _title = "";
    private double _tempo = 90;
    private int _beatsPerBar = 4, _beatUnit = 4, _fps = 30;
    private string? _music, _musicScore;
    private SyntaxNode? _musicNode, _timeNode;
    private bool _tempoWritten;
    private SongInfo? _song;                    // read, and without error
    private IReadOnlyList<string>? _songOrder;  // what the file's own form plays, when it is danced to a song
    private ICollection<string>? _formNames;    // the names a form may use; null for this file's sections
    private bool _anyFormName;

    /// <summary>
    /// The time is the song's: when the song is named and could not be read, how long a bar is
    /// is not known, and the bar lines are not held against a guess.
    /// </summary>
    private bool BarsKnown => _music is null || _song is not null;

    private List<TempoIr> _tempos = [];   // of the clip: more than one only from a song
    private List<MeterIr> _meters = [];
    private double _songShift = double.NaN;   // a walk's time plus this is the beat of the song; NaN when it is nowhere in it
    private double _unitTempo = 90;           // what seconds and frames are counted at

    /// <summary>
    /// Says where in the song the rows walked next are: <paramref name="songBeat"/> is where
    /// their section's dance begins, <paramref name="walkStart"/> the walk's own time there.
    /// Their bars are then the song's bars at that place, and their seconds and frames are
    /// counted at the tempo the section begins in (a change of tempo inside the section does
    /// not recount them). Null: nowhere — a file with one tempo and one time, or a definition.
    /// </summary>
    private void Place(double? songBeat, double walkStart = 0)
    {
        _songShift = songBeat is { } beat ? beat - walkStart : double.NaN;
        double tempo = songBeat is { } at ? TempoMap.BpmAt(_tempos, at) : _tempo;
        if (tempo != _unitTempo)
            _natural.Clear();   // the length of a move that counts in seconds is the tempo's
        _unitTempo = tempo;
    }

    /// <summary>The time a bar is in. A definition is written without a place: its bars are the first time's.</summary>
    private MeterIr MeterAt(Ctx ctx, double time) =>
        ctx.Depth > 0 || double.IsNaN(_songShift) ? _meters[0] : Bars.MeterAt(_meters, time + _songShift);
    private readonly Dictionary<string, string> _figures = [];           // id -> label
    private readonly List<SyntaxNode> _dancerNodes = [];
    private readonly Dictionary<string, SyntaxNode> _defs = [];
    private readonly Dictionary<string, SyntaxNode> _sections = [];
    private readonly List<string> _sectionOrder = [];
    private readonly Dictionary<string, SyntaxNode> _forms = [];
    private readonly Dictionary<string, SyntaxNode> _scores = [];
    private readonly Dictionary<string, double> _natural = [];
    private readonly HashSet<string> _expanding = [];
    private readonly Dictionary<string, double> _sectionLength = [];
    private List<DancerSpec> _active = [];

    private static readonly DancerSpec Nobody = new();

    private void Report(string code, DiagnosticSeverity severity, TextSpan span, string message)
    {
        // A section played twice, or danced by two dancers, is bound twice; say each thing once.
        if (_mute == 0 && _reported.Add((code, span.Start, span.Length)))
            _diagnostics.Add(new Diagnostic(code, severity, span, message));
    }

    private void Error(string code, TextSpan span, string message) => Report(code, DiagnosticSeverity.Error, span, message);

    private static double Number(SyntaxToken token) => double.Parse(token.Text, CultureInfo.InvariantCulture);

    private static List<SyntaxToken> Tokens(SyntaxNode node, SyntaxKind kind) =>
        node.ChildTokens().Where(t => t.Kind == kind && !t.IsMissing).ToList();

    private static string Hint(string name, IEnumerable<string> candidates) =>
        Vocabulary.Nearest(name, candidates) is { } nearest ? $" Did you mean '{nearest}'?" : "";

    private static string Format(double value) => Math.Round(value, 3).ToString(CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- the file as a whole

    /// <summary>
    /// The sections a form plays, in order, its repeats written out: `|: A :|` twice, `|: A :|*N`
    /// N times, `|: A [1. B] :| [2. C]` as A B A C (the same signs as a Lily# form).
    /// </summary>
    private List<string> Plays(SyntaxNode form)
    {
        var plays = new List<string>();
        List<string>? body = null;      // of the repeat that is open
        SyntaxNode? open = null;
        bool closed = false;            // its ':|' has been read: only further endings still belong to it
        int? times = null;
        var endings = new List<(int From, int To, List<string> Names)>();

        List<string> Names(IEnumerable<SyntaxNode> items)
        {
            var names = new List<string>();
            foreach (var item in items)
            {
                if (item.Token(SyntaxKind.Identifier) is not { } name)
                    continue;
                if (item.Token(SyntaxKind.Star) is { } star)
                    Error("SH1025", star.Span, $"A section is repeated with repeat signs: |: {name.Text} :|*{item.Token(SyntaxKind.Number)?.Text ?? "N"}");
                var known = _formNames ?? _sections.Keys;
                if (_anyFormName || known.Contains(name.Text))
                    names.Add(name.Text);
                else
                    Error("SH1005", name.Span, $"{(_formNames is null ? "No section is" : "The song has no section")} named '{name.Text}'.{Hint(name.Text, known)}");
            }
            return names;
        }

        void Finish()
        {
            if (body is null)
                return;
            if (endings.Count == 0)
            {
                for (int i = 0; i < (times ?? 2); i++)
                    plays.AddRange(body);
            }
            else
            {
                if (times is not null)
                    Error("SH1025", open!.Span, "With endings the passes are numbered ([1. A] :| [2. B]); ':|*N' cannot stand beside them.");
                for (int pass = 1; pass <= Math.Min(endings.Max(e => e.To), 999); pass++)
                {
                    plays.AddRange(body);
                    var mine = endings.Where(e => e.From <= pass && pass <= e.To).ToList();
                    if (mine.Count != 1)
                        Error("SH1025", open!.Span, $"Pass {pass} of this repeat needs exactly one ending: |: A [1. B] :| [2. C]");
                    if (mine.Count > 0)
                        plays.AddRange(mine[0].Names);
                }
            }
            (body, open, closed, times) = (null, null, false, null);
            endings.Clear();
        }

        foreach (var node in form.ChildNodes())
        {
            switch (node.Kind)
            {
                case SyntaxKind.FormItem:
                    if (closed)
                        Finish();
                    (body ?? plays).AddRange(Names([node]));
                    break;

                case SyntaxKind.FormEnding:
                    var numbers = Tokens(node, SyntaxKind.Number);
                    var names = Names(node.ChildNodes(SyntaxKind.FormItem));
                    if (body is null)
                    {
                        Error("SH1025", node.Span, "An ending belongs to a repeat: |: A [1. B] :| [2. C]");
                        plays.AddRange(names);
                    }
                    else if (numbers.Count > 0)
                    {
                        int from = (int)Math.Abs(Number(numbers[0])), to = numbers.Count > 1 ? (int)Math.Abs(Number(numbers[1])) : from;
                        endings.Add((from, Math.Max(from, to), names));
                    }
                    break;

                case SyntaxKind.FormSign:
                    bool opens = node.ChildTokens().First().Kind == SyntaxKind.Bar;
                    if (opens)
                    {
                        if (closed)
                            Finish();
                        if (body is not null)
                            Error("SH1025", node.Span, "A repeat cannot be opened inside another: close the first with ':|'.");
                        else
                            (body, open) = ([], node);
                        break;
                    }
                    if (body is null)
                    {
                        Error("SH1025", node.Span, "':|' closes a repeat that '|:' opened: |: A :|");
                        break;
                    }
                    closed = true;
                    if (node.Token(SyntaxKind.Number) is { } count)
                        times = (int)Math.Clamp(Number(count), 1, 999);
                    if (Tokens(node, SyntaxKind.Colon).Count == 2)   // :|: closes one and opens the next
                    {
                        Finish();
                        (body, open) = ([], node);
                    }
                    break;
            }
        }
        if (body is not null && !closed)
        {
            Error("SH1025", open!.Span, "This '|:' is never closed with ':|'.");
            plays.AddRange(body);
        }
        else
        {
            Finish();
        }
        return plays;
    }

    public (ScoreIr, List<Diagnostic>) Run()
    {
        Collect();

        var result = new ScoreIr { Name = scoreName };
        result.Scores.Add("");
        result.Scores.AddRange(_scores.Keys.Where(name => name != ""));

        string? formName = null;
        HashSet<string>? cast = null;
        if (_scores.TryGetValue(scoreName, out var scoreNode))
            BindScore(scoreNode, ref formName, ref cast);
        else if (scoreName != "")
            Error("SH1005", new TextSpan(0, 0), $"No score is named '{scoreName}'.{Hint(scoreName, _scores.Keys)}");

        // The song first: its tempo is what seconds and frames are counted by.
        var form = formName is not null ? _forms.GetValueOrDefault(formName) : _forms.GetValueOrDefault("");
        ReadSong(form);
        if (_tempos.Count == 0 || _tempos[0].Beat > 0)
            _tempos.Insert(0, new TempoIr(0, _tempo));
        if (_meters.Count == 0 || _meters[0].Beat > 0)
            _meters.Insert(0, new MeterIr(0, _beatsPerBar, _beatUnit));
        Place(null);

        _active = BuildDancers().Where(d => cast is null || cast.Contains(d.Id)).ToList();
        foreach (var definition in _defs)
            CheckDefinition(definition.Key, definition.Value);
        foreach (var section in _sections.Values)
            CheckSection(section);

        if (_song is not null)
        {
            // Danced to a song, the clip is the song: every section it plays, for as long as it plays it.
            var clip = NewClip(scoreName != "" ? scoreName : formName ?? "");
            BindClip(clip, _song.Plays.Select(p => (p.Name, (double?)p.Beats, p.Pickup)).ToList());
            result.Clips.Add(clip);
        }
        else if (form is not null)
        {
            var clip = NewClip(scoreName != "" ? scoreName : formName ?? "");
            BindClip(clip, Plays(form).Select(Own).ToList());
            result.Clips.Add(clip);
        }
        else
        {
            // No form: every section is a clip of its own, starting from neutral.
            foreach (string name in _sectionOrder)
            {
                var clip = NewClip(name);
                var attributes = SectionAttributes(_sections[name]);
                clip.Loop = attributes.Contains("loop");
                clip.Inplace = attributes.Contains("inplace");
                BindClip(clip, [Own(name)]);
                result.Clips.Add(clip);
            }
            if (_forms.Count == 0 && _music is null && _sectionOrder.Count > 1 && !_sections.Values.Any(s => SectionAttributes(s).Contains("loop")))
            {
                var second = _sections[_sectionOrder[1]].Token(SyntaxKind.Identifier, 1)!;
                Report("SH1018", DiagnosticSeverity.Info, second.Span,
                    $"Without a form each section is a clip of its own, starting from neutral. To play them one after the other, add: form {{ {string.Join(' ', _sectionOrder)} }}");
            }
        }

        foreach (var (name, definition) in _defs)
        {
            if (definition.Kind == SyntaxKind.MoveDecl)
                result.MoveLengths[name] = Natural(name);
        }

        return (result, _diagnostics);
    }

    private MotionIr NewClip(string name) => new()
    {
        Name = name,
        Title = _title,
        Tempo = _tempo,
        BeatsPerBar = _beatsPerBar,
        BeatUnit = _beatUnit,
        Tempos = [.. _tempos],
        Meters = [.. _meters],
        Fps = _fps,
        Music = _music is null || _song is null ? null : new MusicIr(_music, _musicScore, _songOrder, _tempoWritten ? _tempo : null),
    };

    private static List<string> SectionAttributes(SyntaxNode section) =>
        Tokens(section, SyntaxKind.Identifier).Skip(2).Select(t => t.Text).ToList();

    private void Collect()
    {
        foreach (var item in tree.Root.ChildNodes())
        {
            var name = item.Token(SyntaxKind.Identifier, 1);
            switch (item.Kind)
            {
                case SyntaxKind.Header:
                    BindHeader(item);
                    break;

                case SyntaxKind.FigureDecl:
                    if (name is not null && !_figures.TryAdd(name.Text, item.Token(SyntaxKind.String)?.Text.Trim('"') ?? name.Text))
                        Error("SH1003", name.Span, $"A figure named '{name.Text}' is already declared above.");
                    break;

                case SyntaxKind.DancerDecl:
                    _dancerNodes.Add(item);
                    break;

                case SyntaxKind.PoseDecl or SyntaxKind.MoveDecl:
                    if (name is null)
                        break;
                    if (Vocabulary.Reserved.Contains(name.Text))
                        Error("SH1005", name.Span, $"'{name.Text}' is a word of the language; a definition needs a name of its own.");
                    else if (!_defs.TryAdd(name.Text, item))
                        Error("SH1005", name.Span, $"'{name.Text}' is already defined above.");
                    break;

                case SyntaxKind.SectionDecl:
                    if (name is null)
                        break;
                    if (_sections.TryAdd(name.Text, item))
                        _sectionOrder.Add(name.Text);
                    else
                        Error("SH1005", name.Span, $"A section named '{name.Text}' is already written above.");
                    foreach (var attribute in Tokens(item, SyntaxKind.Identifier).Skip(2))
                    {
                        if (attribute.Text is not ("loop" or "inplace"))
                            Error("SH1024", attribute.Span, $"A section can be marked loop or inplace.{Hint(attribute.Text, ["loop", "inplace"])}");
                    }
                    break;

                case SyntaxKind.FormDecl:
                    if (!_forms.TryAdd(name?.Text ?? "", item))
                        Error("SH1005", (name ?? item.ChildTokens().First()).Span,
                            name is null ? "There is already a form above; give this one a name: form short { ... }" : $"A form named '{name.Text}' is already written above.");
                    break;

                case SyntaxKind.ScoreDecl:
                    if (!_scores.TryAdd(name?.Text ?? "", item))
                        Error("SH1005", (name ?? item.ChildTokens().First()).Span,
                            name is null ? "There is already a score above; give this one a name: score practice { ... }" : $"A score named '{name.Text}' is already written above.");
                    break;
            }
        }
    }

    private void BindHeader(SyntaxNode header)
    {
        var keyword = header.ChildTokens().First();
        var numbers = Tokens(header, SyntaxKind.Number);
        switch (keyword.Text)
        {
            case "title":
                if (header.Token(SyntaxKind.String) is { } title)
                    _title = title.Text.Trim('"');
                break;
            case "music":
                SetMusic(header, header.ChildTokens().Where(t => !t.IsMissing).Skip(1).ToList());
                break;
            case "tempo":
                if (numbers.Count == 1)
                    SetTempo(numbers[0]);
                break;
            case "fps":
                if (numbers.Count == 1)
                {
                    double fps = Number(numbers[0]);
                    if (fps is < 1 or > 240 || fps != Math.Floor(fps))
                        Error("SH1001", numbers[0].Span, "The frame rate is a whole number of frames per second, from 1 to 240.");
                    else
                        _fps = (int)fps;
                }
                break;
            case "time":
                if (numbers.Count == 2)
                {
                    double beats = Number(numbers[0]), unit = Number(numbers[1]);
                    if (beats < 1 || beats != Math.Floor(beats) || unit < 1 || unit != Math.Floor(unit))
                    {
                        Error("SH1001", header.Span, "The time is written as two whole numbers: time 4/4, time 3/4.");
                    }
                    else
                    {
                        _beatsPerBar = (int)beats;
                        _beatUnit = (int)unit;
                    }
                    _timeNode = header;
                }
                break;
        }
    }

    private void SetTempo(SyntaxToken number)
    {
        double tempo = Number(number);
        if (tempo is < 10 or > 400)
            Error("SH1001", number.Span, "The tempo is in beats per minute, from 10 to 400.");
        else
            (_tempo, _tempoWritten) = (tempo, true);
    }

    /// <summary>music "song.lys" (score NAME)? — at the top of the file, or in a score in its place.</summary>
    private void SetMusic(SyntaxNode node, List<SyntaxToken> values)
    {
        if (values.Count == 0 || values[0].Kind != SyntaxKind.String)
            return;
        _music = values[0].Text.Trim('"');
        _musicScore = values.Count >= 3 && values[1].Text == "score" ? values[2].Text : null;
        _musicNode = node;
    }

    /// <summary>
    /// Reads the song the file is danced to and takes from it what the file does not say itself:
    /// the tempo, the time (always the song's) and, without a `form` here, the order.
    /// </summary>
    private void ReadSong(SyntaxNode? form)
    {
        if (_music is null || _musicNode is null)
            return;
        var span = _musicNode.Span;

        // A form here is an order for the song to be played in: its names are the song's.
        if (form is not null)
        {
            (_anyFormName, _mute) = (true, _mute + 1);
            _songOrder = Plays(form);
            (_anyFormName, _mute) = (false, _mute - 1);
        }

        var song = songs?.Invoke(_music, _musicScore, _songOrder);
        if (song is null)
        {
            Report("SH1900", DiagnosticSeverity.Info, span,
                "The song is not read here: tempo, time and form are the ones written in this file.");
            _songOrder = null;
            return;
        }
        if (song.Error is null && song.Plays.Count == 0)
            song.Error = $"'{_music}' plays no section.";
        if (song.Error is not null)
        {
            Error("SH1901", span, song.Error);
            _songOrder = null;
            return;
        }

        _song = song;
        if (_timeNode is not null)
            Error("SH1902", _timeNode.Span, $"The time is the song's ({song.BeatsPerBar}/{song.BeatUnit}): its bars are where they are. Take this line out.");
        (_beatsPerBar, _beatUnit) = (song.BeatsPerBar, song.BeatUnit);
        if (!_tempoWritten)
            _tempo = Math.Clamp(song.Tempo, 10, 400);
        // A tempo written here is the one the song begins at; where the song changes tempo, it
        // changes by the same proportion.
        double faster = song.Tempo > 0 ? _tempo / song.Tempo : 1;
        _tempos = song.Tempos.Select(t => new TempoIr(t.Beat, Math.Round(t.Bpm * faster, 3))).ToList();
        _meters = [.. song.Meters];

        if (form is not null)
        {
            _formNames = song.Sections;
            Plays(form);
        }
        foreach (string name in _sectionOrder.Where(n => !song.Sections.Contains(n)))
        {
            Report("SH1905", DiagnosticSeverity.Warning, _sections[name].Token(SyntaxKind.Identifier, 1)!.Span,
                $"The song has no section named '{name}', so this one is never danced. It has: {string.Join(", ", song.Sections)}.");
        }
    }

    private void BindScore(SyntaxNode score, ref string? formName, ref HashSet<string>? cast)
    {
        foreach (var item in score.ChildNodes(SyntaxKind.ScoreItem))
        {
            var tokens = item.ChildTokens().Where(t => !t.IsMissing).ToList();
            var values = tokens.Skip(1).ToList();
            switch (tokens[0].Text)
            {
                case "form":
                    if (values.Count == 1 && _forms.ContainsKey(values[0].Text))
                        formName = values[0].Text;
                    else
                        Error("SH1005", item.Span, $"A score names one of the file's forms: form NAME.{(values.Count == 1 ? Hint(values[0].Text, _forms.Keys) : "")}");
                    break;
                case "tempo":
                    if (values.Count == 1 && values[0].Kind == SyntaxKind.Number)
                        SetTempo(values[0]);
                    break;
                case "title":
                    if (values.Count == 1 && values[0].Kind == SyntaxKind.String)
                        _title = values[0].Text.Trim('"');
                    break;
                case "music":
                    SetMusic(item, values);
                    break;
                case "dancer":
                    foreach (var value in values)
                        (cast ??= []).Add(value.Text);
                    break;
            }
        }
    }

    private List<DancerSpec> BuildDancers()
    {
        var dancers = new List<DancerSpec>();
        var ids = new HashSet<string>();
        bool hasFigures = _figures.Count > 0;

        foreach (var node in _dancerNodes)
        {
            var words = Tokens(node, SyntaxKind.Identifier);
            if (words.Count < 2)
                continue;
            var dancer = new DancerSpec { Id = words[1].Text, Name = words[1].Text };
            if (!ids.Add(dancer.Id))
            {
                Error("SH1003", words[1].Span, $"A dancer named '{dancer.Id}' is already declared above.");
                continue;
            }
            if (words.Count < 3 || words[2].Text != "humanoid")
                Error("SH1006", words.Count < 3 ? words[1].Span : words[2].Span, "A dancer names its body after its own name. The only body so far is humanoid: dancer alice humanoid");

            bool on = false;
            foreach (var clause in node.ChildNodes(SyntaxKind.Clause))
            {
                var tokens = clause.ChildTokens().Where(t => !t.IsMissing).ToList();
                var values = tokens.Skip(1).ToList();
                switch (tokens[0].Text)
                {
                    case "on":
                        on = true;
                        if (!hasFigures)
                            Error("SH1003", clause.Span, "This file has no figure, so its one dancer is written without 'on'.");
                        else if (values.Count == 1 && _figures.ContainsKey(values[0].Text))
                            dancer.Figure = values[0].Text;
                        else
                            Error("SH1003", clause.Span, $"'on' names the figure this dancer dances: on {_figures.Keys.First()}.{(values.Count == 1 ? Hint(values[0].Text, _figures.Keys) : "")}");
                        break;

                    case "at":
                        if (values.Count == 2 && values.All(v => v.Kind == SyntaxKind.Number))
                        {
                            dancer.X = Number(values[0]);
                            dancer.Z = Number(values[1]);
                        }
                        else
                        {
                            Error("SH1006", clause.Span, "A position is two numbers in metres: at 1.5 0 (x to stage left, z toward the audience).");
                        }
                        break;

                    case "facing":
                        double? degrees = values.Count == 1
                            ? values[0].Kind == SyntaxKind.Number
                                ? Number(values[0])
                                : values[0].Text switch
                                {
                                    "front" => 0,
                                    "back" => 180,
                                    "stageLeft" => 90,
                                    "stageRight" => -90,
                                    _ => null,
                                }
                            : null;
                        if (degrees is { } facing)
                            dancer.FacingDeg = facing;
                        else
                            Error("SH1006", clause.Span, "facing takes front, back, stageLeft, stageRight, or degrees (positive turns to the dancer's left).");
                        break;

                    case "mirror" when values.Count == 0:
                        dancer.Mirror = true;
                        break;

                    case "free" when values.Count == 0:
                        dancer.Free = true;
                        break;

                    case "delay":
                        if (values.Count == 1 && values[0].Kind == SyntaxKind.Number && Number(values[0]) >= 0)
                            dancer.Delay = Number(values[0]);
                        else
                            Error("SH1006", clause.Span, "delay takes a number of beats: delay 2");
                        break;

                    case "in":
                        dancer.In = [];
                        foreach (var value in values)
                        {
                            if (_sections.ContainsKey(value.Text))
                                dancer.In.Add(value.Text);
                            else
                                Error("SH1005", value.Span, $"No section is named '{value.Text}'.{Hint(value.Text, _sections.Keys)}");
                        }
                        break;

                    default:
                        Error("SH1006", tokens[0].Span,
                            $"A dancer line has no '{tokens[0].Text}'.{Hint(tokens[0].Text, ["on", "at", "facing", "mirror", "delay", "free", "in"])}");
                        break;
                }
            }

            if (hasFigures && !on)
                Error("SH1003", words[1].Span, $"Say which figure this dancer dances: dancer {dancer.Id} humanoid on {_figures.Keys.First()}");
            if (hasFigures && dancer.Figure == "")
                continue;
            if (!hasFigures && dancers.Count > 0)
            {
                Error("SH1003", words[1].Span, "A file without figures has one dancer. To have more, declare figures: figure lead");
                continue;
            }
            dancers.Add(dancer);
        }

        // A figure nobody was cast for is danced by a default body, at the origin, facing front.
        if (!hasFigures && dancers.Count == 0)
            dancers.Add(new DancerSpec { Id = "dancer", Name = _title });
        foreach (var (figure, label) in _figures)
        {
            if (!dancers.Any(d => d.Figure == figure))
                dancers.Add(new DancerSpec { Id = figure, Name = label, Figure = figure });
        }
        return dancers;
    }

    // ---------------------------------------------------------------- checks made once

    private static string[]? ChannelsOf(string trackName) => trackName switch
    {
        "body" => Channels.All,
        "steps" => [Channels.Support, Channels.Height],
        "arms" => [Channels.LeftArm, Channels.RightArm],
        Channels.LeftArm or Channels.RightArm or Channels.LeftLeg or Channels.RightLeg or Channels.Torso or Channels.Head => [trackName],
        _ => null,
    };

    private string[]? TrackChannels(SyntaxNode track, bool mirror)
    {
        if (track.Token(SyntaxKind.Identifier) is not { } name)
            return null;
        var channels = ChannelsOf(name.Text);
        if (channels is null)
        {
            Error("SH1004", name.Span, $"There is no track '{name.Text}'.{Hint(name.Text, Vocabulary.Tracks.Select(w => w.Name))}");
            return null;
        }
        return mirror ? channels.Select(Channels.Mirror).ToArray() : channels;
    }

    /// <summary>A definition is checked where it is written, used or not, with a body that may do anything.</summary>
    private void CheckDefinition(string name, SyntaxNode definition)
    {
        bool isPose = definition.Kind == SyntaxKind.PoseDecl;
        _expanding.Add(name);
        foreach (var track in definition.ChildNodes(SyntaxKind.Track))
        {
            if (TrackChannels(track, false) is not { } channels)
                continue;
            var ctx = new Ctx(Nobody, null, [], channels, track.Token(SyntaxKind.Identifier)!.Text, [0], double.NaN, Free: true, Depth: 1);
            double time = 0, bar = 0;
            if (isPose)
            {
                var items = track.ChildNodes().ToList();
                if (items.Count != 1 || items[0].Kind != SyntaxKind.Event || items[0].Child(SyntaxKind.Duration) is not null)
                    Error("SH1023", track.Token(SyntaxKind.Identifier)!.Span, "In a pose a track holds one target and no length: arms { fwd.high@bent }. A row of events is a move.");
                if (items.FirstOrDefault(i => i.Kind == SyntaxKind.Event) is { } only)
                    BindEvent(only, ctx with { ForcedLength = 1, Stance = true }, ref time, ref bar);
            }
            else
            {
                Walk(track, ctx, ref time, ref bar);
            }
        }
        _expanding.Remove(name);
    }

    private void CheckSection(SyntaxNode section)
    {
        bool hasFigures = _figures.Count > 0;
        foreach (var child in section.ChildNodes())
        {
            var name = child.Token(SyntaxKind.Identifier);
            if (name is null)
                continue;
            if (child.Kind == SyntaxKind.FigureBlock && !_figures.ContainsKey(name.Text))
            {
                Error("SH1003", name.Span, hasFigures
                    ? $"No figure is named '{name.Text}'.{Hint(name.Text, _figures.Keys)}"
                    : $"No figure is named '{name.Text}'. Declare it (figure {name.Text}), or write the tracks directly in the section.");
            }
            else if (child.Kind == SyntaxKind.Track && hasFigures)
            {
                Error("SH1003", name.Span, _figures.ContainsKey(name.Text)
                    ? $"The block of figure '{name.Text}' holds tracks: {name.Text} {{ steps {{ ... }} }}"
                    : $"This file has figures, so a track goes inside a figure's block: {_figures.Keys.First()} {{ {name.Text} {{ ... }} }}");
            }
            else if (child.Kind == SyntaxKind.Track)
            {
                TrackChannels(child, false);
            }
            else if (child.Kind == SyntaxKind.FigureBlock)
            {
                foreach (var track in child.ChildNodes(SyntaxKind.Track))
                    TrackChannels(track, false);
            }
        }
    }

    // ---------------------------------------------------------------- clips

    private IEnumerable<SyntaxNode> TracksOf(SyntaxNode section, string figure) =>
        (figure == ""
            ? section.ChildNodes(SyntaxKind.Track)
            : section.ChildNodes(SyntaxKind.FigureBlock).Where(b => b.Token(SyntaxKind.Identifier)?.Text == figure).SelectMany(b => b.ChildNodes(SyntaxKind.Track)))
        .Where(t => t.Token(SyntaxKind.Identifier) is { } name && ChannelsOf(name.Text) is not null);

    /// <summary>A section is as long as its longest track, whoever dances it.</summary>
    private double SectionLength(string name)
    {
        if (_sectionLength.TryGetValue(name, out double known))
            return known;
        double length = 0;
        _mute++;
        foreach (var track in _sections[name].DescendantNodes().Where(n => n.Kind == SyntaxKind.Track && n.Parent!.Kind != SyntaxKind.Track))
        {
            if (track.Token(SyntaxKind.Identifier) is not { } trackName || ChannelsOf(trackName.Text) is not { } channels)
                continue;
            double time = 0, bar = 0;
            Walk(track, new Ctx(Nobody, null, [], channels, trackName.Text, [0], 0, Free: true), ref time, ref bar);
            length = Math.Max(length, time);
        }
        _mute--;
        return _sectionLength[name] = length;
    }

    /// <summary>A play of one of this file's sections, as long as the section is written.</summary>
    private static (string Name, double? Beats, double Pickup) Own(string name) => (name, null, 0);

    /// <summary>
    /// Lays the plays out one after the other. A play with Beats is a section of the song: it
    /// lasts that long whatever is written for it here — a shorter dance stands still for the
    /// rest, a section this file does not have is stood through — and the dance of it begins
    /// after the song's upbeat (Pickup).
    /// </summary>
    private void BindClip(MotionIr clip, List<(string Name, double? Beats, double Pickup)> plays)
    {
        double cursor = 0;
        int bar = 1;
        var starts = new List<double>();
        foreach (var (name, beats, pickup) in plays)
        {
            bool danced = _sections.ContainsKey(name);
            Place(beats is null ? null : cursor + pickup);
            double own = danced ? SectionLength(name) : 0, length = beats ?? own;
            int bars = Bars.Count(_meters, cursor + pickup, cursor + length);
            if (danced && beats is { } whole && own > whole - pickup + Epsilon)
            {
                var first = Bars.MeterAt(_meters, cursor + pickup);
                bool oneTime = Bars.MeterAt(_meters, cursor + whole - Epsilon) == first;
                Error("SH1903", _sections[name].Token(SyntaxKind.Identifier, 1)!.Span,
                    $"Section '{name}' is {Format(own)} beats long here, and the song's {name} has {Format(whole - pickup)} "
                    + (oneTime ? $"({Format((whole - pickup) / first.BeatsPerBar)} bars of {first.BeatsPerBar}/{first.BeatUnit})." : $"({bars} bars; it changes time on the way)."));
            }
            var span = danced ? _sections[name].Span : _musicNode!.Span;
            clip.Sections.Add(new SectionIr(name, cursor, cursor + length, span.Start, span.Length, pickup, bar));
            bar += bars;
            starts.Add(cursor + pickup);
            cursor += length;
        }
        clip.TotalBeats = cursor + (cursor > 0 && _active.Count > 0 ? _active.Max(d => d.Delay) : 0);

        foreach (var dancer in _active)
        {
            var ir = new DancerIr
            {
                Id = dancer.Id, Name = dancer.Name == "" ? dancer.Id : dancer.Name, Figure = dancer.Figure,
                X = dancer.X, Z = dancer.Z, FacingDeg = dancer.FacingDeg, Free = dancer.Free,
            };
            var sink = new Dictionary<string, List<Raw>>();
            int trackIndex = 0;
            for (int i = 0; i < plays.Count; i++)
            {
                if (!_sections.TryGetValue(plays[i].Name, out var section) || dancer.In is not null && !dancer.In.Contains(plays[i].Name))
                    continue;
                Place(plays[i].Beats is null ? null : starts[i], starts[i] + dancer.Delay);
                foreach (var track in TracksOf(section, dancer.Figure))
                    BindTrack(track, dancer, clip, sink, starts[i] + dancer.Delay, trackIndex++);
            }
            Place(null);
            foreach (string channel in Channels.All)
            {
                if (sink.TryGetValue(channel, out var raws))
                    ir.Tracks[channel] = Resolve(channel, raws);
            }
            // A mark names an arrival: one whose event lost its part to another track at that moment names nothing.
            clip.Marks.AddRange(_marks.Where(m => m.Raws.Count == 0 || m.Raws.Any(r => Arrives(r.Channel, r.Raw, m.Mark.Beat, sink[r.Channel]))).Select(m => m.Mark));
            _marks.Clear();
            CheckOppositeTargets(ir, clip.Loop);
            clip.Dancers.Add(ir);
        }
        clip.Marks.Sort((a, b) => a.Beat.CompareTo(b.Beat));
    }

    private void BindTrack(SyntaxNode track, DancerSpec dancer, MotionIr clip, Dictionary<string, List<Raw>> sink, double start, int index)
    {
        var name = track.Token(SyntaxKind.Identifier)!;
        var channels = ChannelsOf(name.Text)!;
        var ctx = new Ctx(dancer, clip, sink, dancer.Mirror ? channels.Select(Channels.Mirror).ToArray() : channels, name.Text,
            [Channels.All.Length - channels.Length, index], dancer.Delay, Mirror: dancer.Mirror, Free: dancer.Free, CheckBars: BarsKnown);

        double time = start, barStart = start;
        Walk(track, ctx, ref time, ref barStart);

        double rest = time - barStart;
        var meter = MeterAt(ctx, barStart);
        if (BarsKnown && track.DescendantNodes().Any(n => n.Kind == SyntaxKind.BarLine) && rest > Epsilon && Math.Abs(rest - meter.BeatsPerBar) > Epsilon)
        {
            Report("SH1002", DiagnosticSeverity.Warning, name.Span,
                $"The last bar of this track has {Format(rest)} beats; time {meter.BeatsPerBar}/{meter.BeatUnit} has {meter.BeatsPerBar}.");
        }
    }

    // ---------------------------------------------------------------- rows of events

    /// <summary>
    /// A bar line closes exactly one bar. One with nothing before it (at the start of a row, after
    /// a `:0` starting pose) is only punctuation. After a miscount the next bar is counted from here.
    /// </summary>
    private void CloseBar(SyntaxNode line, Ctx ctx, double time, ref double barStart)
    {
        double beats = (time - barStart) / ctx.Scale;
        var meter = MeterAt(ctx, barStart);
        int perBar = meter.BeatsPerBar;
        if (ctx.CheckBars && beats > Epsilon && Math.Abs(beats - perBar) > Epsilon)
        {
            // Say where the tie goes: the first event that starts in the bar and ends after it.
            double barEnd = barStart + perBar * ctx.Scale;
            var across = _barEvents.FirstOrDefault(e => e.T0 < barEnd - Epsilon && e.T1 > barEnd + Epsilon);
            string hint = beats < perBar ? ""
                : across.Word is null ? " An event that runs across a bar line is tied over it, as in kick:1~ | :2"
                : TieHint(across.Word, (barEnd - across.T0) / ctx.Scale, (across.T1 - barEnd) / ctx.Scale, MeterAt(ctx, barEnd).BeatsPerBar);
            string changed = meter == _meters[0] ? "" : " (the song changes time on the way: `shosa check` says where)";
            Error("SH1002", line.Span, $"The bar that ends here has {Format(beats)} beats; time {meter.BeatsPerBar}/{meter.BeatUnit} has {perBar}{changed}.{hint}");
        }
        _barEvents.Clear();
        barStart = time;
    }

    /// <summary>
    /// How to write an event that runs over the end of its bar: <paramref name="first"/> beats of
    /// it are in the bar, <paramref name="rest"/> after. One that runs over several bar lines is
    /// tied over each of them — or its row is written without bar lines.
    /// </summary>
    private static string TieHint(string word, double first, double rest, int perBar)
    {
        if (rest <= perBar + Epsilon)
            return $" '{word}' runs across the bar line: tie it over, {word}:{Format(first)}~ | :{Format(rest)}";

        int whole = (int)Math.Floor(rest / perBar + Epsilon);
        double last = rest - whole * perBar;
        if (last <= Epsilon)
        {
            whole--;
            last = perBar;
        }
        string middle = whole <= 2
            ? string.Concat(Enumerable.Repeat($":{perBar}~ | ", whole))
            : $"… {whole} bars of :{perBar}~ | … ";
        return $" '{word}' runs across {whole + 1} bar lines: tie it over each, {word}:{Format(first)}~ | {middle}:{Format(last)} — or leave the bar lines out of this row";
    }

    /// <summary>The events since the last bar line, for the hint of SH1002.</summary>
    private readonly List<(string? Word, double T0, double T1)> _barEvents = [];

    private void Walk(SyntaxNode parent, Ctx ctx, ref double time, ref double barStart)
    {
        var items = parent.ChildNodes().ToList();
        // What this row has done so far, so that `%` can do a bar of it again.
        var done = new List<(double T0, double T1, Func<double, Ctx, double> Play)>();
        double rowStart = time, written = 0;   // written: the length of the last bar a line closed in this row

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            switch (item.Kind)
            {
                case SyntaxKind.BarLine:
                    if (time - barStart > Epsilon)
                        written = time - barStart;
                    CloseBar(item, ctx, time, ref barStart);
                    break;

                case SyntaxKind.BarRepeat:
                {
                    // % is the bar before it again, %N the N bars before it; it stands alone in its bar.
                    // The bars done again are as long as the bar the sign stands in.
                    int bars = item.Token(SyntaxKind.Number) is { } n ? (int)Math.Clamp(Number(n), 1, 999) : 1;
                    // Where the song could not be read its time is not known: a bar is then as long as the one written before.
                    double bar = !BarsKnown && written > 0 ? written : MeterAt(ctx, time).BeatsPerBar * ctx.Scale;
                    double span = bars * bar, from = time - span;
                    var again = done.Where(e => e.T1 > from + Epsilon && e.T1 - e.T0 > Epsilon).ToList();
                    if (Math.Abs(time - barStart) > Epsilon)
                        Error("SH1027", item.Span, "'%' stands alone between bar lines: a b | % | % |");
                    else if (from < rowStart - Epsilon || again.Count == 0)
                        Error("SH1027", item.Span, bars == 1 ? "There is no bar before this '%' to do again." : $"There are not {bars} bars before this '%{bars}' to do again.");
                    else if (again[0].T0 < from - Epsilon)
                        Error("SH1027", item.Span, "An event is tied into the bar this '%' would do again, so that bar is not a whole. Write the bar out.");
                    else
                    {
                        var replay = ctx with { Src = ctx.Src ?? item.Span, CheckBars = false };
                        foreach (var entry in again)
                            done.Add((entry.T0 + span, entry.Play(entry.T0 + span, replay), entry.Play));
                    }
                    time += span;
                    barStart = time - bar;   // the line after it closes one bar, as after any other
                    break;
                }

                case SyntaxKind.Overlay:
                {
                    double t0 = time;
                    Overlay(item, ctx, ref time, ref barStart);
                    done.Add((t0, time, (start, c) => { double t = start, b = start; Overlay(item, c, ref t, ref b); return t; }));
                    break;
                }

                case SyntaxKind.Event:
                {
                    if (item.Child(SyntaxKind.Target) is null)
                    {
                        // A length alone: only the rest of a tied event, which the event before it has taken.
                        Error("SH1028", item.Span, "A length alone continues the event tied to it: kick:1~ | :2");
                        time += (item.Child(SyntaxKind.Duration) is { } alone ? WrittenLength(alone) ?? 1 : 1) * ctx.Scale;
                        break;
                    }

                    // kick:1~ | :2 is one event of three beats; the bar lines inside it are checked where they fall.
                    double extra = 0;
                    var inside = new List<(SyntaxNode? Line, double Length)>();
                    int last = i;
                    bool tied = item.Token(SyntaxKind.Tilde) is not null;
                    for (int j = i + 1; tied && j < items.Count; j++)
                    {
                        if (items[j].Kind == SyntaxKind.BarLine)
                        {
                            inside.Add((items[j], 0));
                        }
                        else if (items[j].Kind == SyntaxKind.Event && items[j].Child(SyntaxKind.Target) is null)
                        {
                            double more = items[j].Child(SyntaxKind.Duration) is { } d ? WrittenLength(d) ?? 0 : 0;
                            inside.Add((null, more));
                            extra += more;
                            last = j;
                            tied = items[j].Token(SyntaxKind.Tilde) is not null;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (tied)
                        Error("SH1028", (items[last].Token(SyntaxKind.Tilde) ?? item.Token(SyntaxKind.Tilde))!.Span, "'~' ties an event to the length written after it: kick:1~ | :2");

                    double t0 = time, bars0 = barStart;
                    BindEvent(item, ctx, ref time, ref barStart, extra);
                    if (ctx.CheckBars && last == i)
                        _barEvents.Add((item.Child(SyntaxKind.Target)!.ToString().Trim(), t0, time));
                    if (last > i)
                    {
                        // Walk the pieces again for their bar lines.
                        double t = time - extra * ctx.Scale;
                        barStart = bars0;
                        for (int j = i + 1; j <= last; j++)
                        {
                            var piece = inside[j - i - 1];
                            if (piece.Line is not null)
                                CloseBar(piece.Line, ctx, t, ref barStart);
                            else
                                t += piece.Length * ctx.Scale;
                        }
                        i = last;
                    }
                    done.Add((t0, time, (start, c) => { double t = start, b = start; BindEvent(item, c, ref t, ref b, extra); return t; }));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Rows that start together. The whole is as long as its longest row; a later row wins.
    /// One row alone is a group: the unit `q` performs again.
    /// </summary>
    private void Overlay(SyntaxNode overlay, Ctx ctx, ref double time, ref double barStart)
    {
        double start = time, end = time, bars = barStart;
        int row = 0;
        foreach (var sequence in overlay.ChildNodes(SyntaxKind.Sequence))
        {
            double t = start, b = barStart;   // the rows share the track's bars
            Walk(sequence, ctx with { Priority = [.. ctx.Priority, row] }, ref t, ref b);
            if (row++ == 0 || t > end + Epsilon)
                (end, bars) = (Math.Max(end, t), b);
        }
        time = end;
        barStart = bars;
    }

    /// <summary>The group `q` stands for: the nearest one in braces written before it in the same track.</summary>
    private static SyntaxNode? GroupBefore(SyntaxNode ev)
    {
        var track = ev.Parent;
        while (track is not null && !(track.Kind == SyntaxKind.Track))
            track = track.Parent;
        return track?.DescendantNodes().Where(n => n.Kind == SyntaxKind.Overlay && n.Span.End <= ev.Span.Start).MaxBy(n => n.Span.End);
    }

    /// <summary>`q`: the group before it again, stretched to the length written, mirrored if it says so.</summary>
    private void BindAgain(SyntaxNode ev, SyntaxToken word, Ctx ctx, ref double time, ref double barStart, double extra)
    {
        double? written = ev.Child(SyntaxKind.Duration) is { } duration ? WrittenLength(duration) : null;
        var group = GroupBefore(ev);
        if (group is null)
        {
            Error("SH1026", word.Span, "'q' does the group in braces before it again, and no group is written before this one in the track: { a b } q q");
            time += ((written ?? 1) + extra) * ctx.Scale;
            return;
        }

        double natural = 0, unused = 0;
        _mute++;
        Overlay(group, ctx with { Clip = null, Sink = [], Scale = 1, CheckBars = false, Depth = ctx.Depth + 1 }, ref natural, ref unused);
        _mute--;
        if (natural <= Epsilon)
            natural = 1;
        if (written is <= 0)
        {
            Error("SH1009", ev.Child(SyntaxKind.Duration)!.Span, "A group takes time; only a pose can be the ':0' a clip starts in.");
            written = null;
        }

        bool mirror = false, free = false;
        foreach (var annotation in ev.ChildNodes(SyntaxKind.Annotation))
        {
            if (annotation.Token(SyntaxKind.Identifier) is not { } name)
                continue;
            if (name.Text == "mirror")
                mirror = true;
            else if (name.Text == "free")
                free = true;
            else
                Error("SH1010", name.Span, $"'@{name.Text}' cannot be put on 'q': it takes @mirror and @free.");
        }

        double length = ((written ?? natural) + extra) * ctx.Scale;
        var src = ctx.Src ?? ev.Span;
        double t = time, b = barStart;
        Overlay(group, ctx with
        {
            Scale = length / natural, Mirror = ctx.Mirror ^ mirror, Free = ctx.Free || free, Src = src, Depth = ctx.Depth + 1,
            CheckBars = ctx.CheckBars && written is null && extra == 0,
        }, ref t, ref b);
        time += length;
        barStart = b;
        if (ev.Child(SyntaxKind.Mark)?.Token(SyntaxKind.Identifier) is { } mark && ctx.Clip is not null)
            _marks.Add((new MarkIr(time, mark.Text, ctx.Dancer.Id, src.Start, src.Length), []));
    }


    /// <summary>The length a move has when it is written without one: its longest track.</summary>
    private double Natural(string name)
    {
        if (_natural.TryGetValue(name, out double known))
            return known;
        var definition = _defs[name];
        if (definition.Kind == SyntaxKind.PoseDecl || !_expanding.Add(name))
            return 1;
        double length = 0;
        _mute++;
        foreach (var track in definition.ChildNodes(SyntaxKind.Track))
        {
            if (track.Token(SyntaxKind.Identifier) is not { } trackName || ChannelsOf(trackName.Text) is not { } channels)
                continue;
            double time = 0, bar = 0;
            Walk(track, new Ctx(Nobody, null, [], channels, trackName.Text, [0], double.NaN, Free: true, Depth: 1), ref time, ref bar);
            length = Math.Max(length, time);
        }
        _mute--;
        _expanding.Remove(name);
        return _natural[name] = length > Epsilon ? length : 1;
    }

    private double? WrittenLength(SyntaxNode duration)
    {
        var numbers = Tokens(duration, SyntaxKind.Number);
        if (numbers.Count == 0)
            return null;
        double value = Number(numbers[0]);
        if (numbers.Count >= 2)
            value = Number(numbers[1]) != 0 ? value / Number(numbers[1]) : -1;
        if (value < 0)
        {
            Error("SH1009", duration.Span, "A length is a number of beats, seconds or frames: :2, :1/2, :0.3s, :12f.");
            return 1;
        }
        // Seconds and frames become beats here, so bars are still checked.
        return Tokens(duration, SyntaxKind.Identifier).FirstOrDefault()?.Text switch
        {
            "s" => value * _unitTempo / 60,
            "f" => value / _fps * _unitTempo / 60,
            _ => value,
        };
    }

    private void BindEvent(SyntaxNode ev, Ctx ctx, ref double time, ref double barStart, double extra = 0)
    {
        var target = ev.Child(SyntaxKind.Target);
        var words = target is null ? [] : target.ChildTokens().Where(t => t.Kind is SyntaxKind.Identifier or SyntaxKind.Number && !t.IsMissing).ToList();
        var arguments = target?.Child(SyntaxKind.Arguments);
        SyntaxNode? definition = words.Count == 1 && arguments is null ? _defs.GetValueOrDefault(words[0].Text) : null;
        if (words.Count == 1 && arguments is null && words[0].Text == Vocabulary.Again && ctx.ForcedLength is null)
        {
            BindAgain(ev, words[0], ctx, ref time, ref barStart, extra);
            return;
        }

        double? written = ctx.ForcedLength is null && ev.Child(SyntaxKind.Duration) is { } duration ? WrittenLength(duration) : null;
        if (written == 0 && (ctx.Depth > 0 || !(Math.Abs(time - ctx.Origin) < Epsilon)))
        {
            Error("SH1009", ev.Child(SyntaxKind.Duration)!.Span, "':0' is the pose a clip starts in; it can only be written before anything else in the clip.");
            written = 1;
        }
        double length = ctx.ForcedLength ?? ((written ?? (definition is not null ? Natural(words[0].Text) : 1)) + extra) * ctx.Scale;
        double t0 = time, t1 = time + length;
        time = t1;

        if (target is null || words.Count == 0 || target.ChildTokens().Any(t => t.IsMissing))
            return;   // the parser has already said what is missing

        var annotations = new List<SyntaxNode>();
        foreach (var annotation in ev.ChildNodes(SyntaxKind.Annotation))
        {
            if (annotation.Token(SyntaxKind.Identifier) is not { } name)
                continue;
            if (Vocabulary.Has(Vocabulary.Annotations, name.Text))
                annotations.Add(annotation);
            else
                Error("SH1010", name.Span, $"There is no '@{name.Text}'.{Hint(name.Text, Vocabulary.Annotations.Select(w => w.Name))}");
        }
        bool Has(string name) => annotations.Any(a => a.Token(SyntaxKind.Identifier)!.Text == name);

        bool mirror = ctx.Mirror ^ Has("mirror"), free = ctx.Free || Has("free");
        string? ease = ctx.EaseOverride ?? annotations.Select(a => a.Token(SyntaxKind.Identifier)!.Text).LastOrDefault(a => a is "smooth" or "sharp" or "linear");
        var src = ctx.Src ?? ev.Span;
        var mark = ev.Child(SyntaxKind.Mark)?.Token(SyntaxKind.Identifier);

        if (words.Count == 1 && words[0].Text == Vocabulary.Skip)
            return;   // this track says nothing: a wider one may

        if (definition is not null)
        {
            ExpandDefinition(definition, words[0], annotations, ctx with { Mirror = mirror, Free = free, Src = src }, t0, length, ease);
            if (mark is not null && ctx.Clip is not null)
                _marks.Add((new MarkIr(t1, mark.Text, ctx.Dancer.Id, src.Start, src.Length), []));
            return;
        }

        bool isHold = words.Count == 1 && words[0].Text == Vocabulary.Hold;
        if (ctx.Channels.Length > 2 && !isHold)
        {
            Error("SH1019", words[0].Span, _defs.Count == 0 && Vocabulary.Reserved.Contains(words[0].Text)
                ? $"'body' takes poses and moves you have defined, hold and skip. '{words[0].Text}' belongs in the track of its part: arms, steps, torso ..."
                : $"No pose or move is named '{words[0].Text}'.{Hint(words[0].Text, _defs.Keys)}");
            return;
        }

        // What the feet do is one word and two parts: where they stand, and how high the body stands on them.
        Seg? feet = null, height = null;
        if (!isHold && ctx.Channels.Any(c => c is Channels.Support or Channels.Height))
        {
            feet = BindSupport(words, arguments, annotations, mirror, free, ctx);
            if (feet is { Kind: SegKinds.Level })
                (feet, height) = (null, feet);
            else if (feet is { Level: { } arrives })
                (feet.Level, height) = (null, new Seg { Kind = SegKinds.Level, Level = arrives });
        }

        double markBeat = t1;
        var raws = new List<(string Channel, Raw Raw)>();
        foreach (string channel in ctx.Channels)
        {
            var seg = isHold ? new Seg { Kind = SegKinds.Hold }
                : channel == Channels.Support ? feet
                : channel == Channels.Height ? height
                : BindGesture(channel, words, arguments, annotations, mirror, ctx);
            if (seg is null)
                continue;
            seg.T0 = t0;
            seg.T1 = t1;
            seg.Src = src.Start;
            seg.Len = src.Length;
            seg.Free = free;
            seg.Ease = ease ?? "smooth";
            // The moment a mark names is the arrival: half-way for a sharp event, the landing of a jump.
            if (seg.Kind == SegKinds.Jump)
                markBeat = t0 + 0.85 * length;
            else if (seg.Ease == "sharp" && channel != Channels.Support && !(channel == Channels.Height && feet is not null))
                markBeat = t0 + 0.5 * length;

            if (!ctx.Sink.TryGetValue(channel, out var list))
                ctx.Sink[channel] = list = [];
            list.Add(new Raw(seg, ctx.Priority, _order++));
            raws.Add((channel, list[^1]));
        }
        if (mark is not null && ctx.Clip is not null)
            _marks.Add((new MarkIr(markBeat, mark.Text, ctx.Dancer.Id, src.Start, src.Length), raws));
    }

    /// <summary>
    /// A defined word in a track: the definition's tracks, each cut down to what the enclosing
    /// track covers, stretched to the length written here.
    /// </summary>
    private void ExpandDefinition(SyntaxNode definition, SyntaxToken name, List<SyntaxNode> annotations, Ctx ctx, double t0, double length, string? ease)
    {
        bool isPose = definition.Kind == SyntaxKind.PoseDecl;
        foreach (var annotation in annotations)
        {
            var word = annotation.Token(SyntaxKind.Identifier)!;
            if (word.Text is "mirror" or "free" || (isPose && word.Text is "smooth" or "sharp" or "linear"))
                continue;
            Error("SH1010", word.Span, isPose
                ? $"'@{word.Text}' cannot be put on a pose: it takes @mirror, @free and @smooth, @sharp, @linear."
                : $"'@{word.Text}' cannot be put on a move: it takes @mirror and @free.");
        }
        if (_expanding.Contains(name.Text))
        {
            Error("SH1017", name.Span, $"'{name.Text}' is used inside its own definition.");
            return;
        }
        if (!isPose && length <= 0)
            Error("SH1009", name.Span, "A move takes time; only a pose can be the ':0' a clip starts in.");

        double natural = isPose ? 1 : Natural(name.Text);
        _expanding.Add(name.Text);
        bool any = false;
        foreach (var track in definition.ChildNodes(SyntaxKind.Track))
        {
            if (track.Token(SyntaxKind.Identifier) is not { } trackName || ChannelsOf(trackName.Text) is not { } channels)
                continue;
            if (ctx.Mirror)
                channels = channels.Select(Channels.Mirror).ToArray();
            var shared = channels.Where(ctx.Channels.Contains).ToArray();
            if (shared.Length == 0)
                continue;
            any = true;

            var inner = ctx with
            {
                Channels = shared, Priority = [.. ctx.Priority, Channels.All.Length - channels.Length], Depth = ctx.Depth + 1,
                CheckBars = false, ForcedLength = null, EaseOverride = null, Stance = false,
            };
            double time = t0, bar = t0;
            if (!isPose)
                Walk(track, inner with { Scale = length / natural }, ref time, ref bar);
            else if (track.ChildNodes(SyntaxKind.Event).FirstOrDefault() is { } only)
                BindEvent(only, inner with { ForcedLength = length, EaseOverride = ease, Stance = true }, ref time, ref bar);
        }
        if (!any)
            Error("SH1015", name.Span, $"'{name.Text}' has nothing for the track '{ctx.TrackName}'.");
        _expanding.Remove(name.Text);
    }

    // ---------------------------------------------------------------- who has each part, when

    private static int Compare(int[] a, int[] b)
    {
        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
            if (x != y)
                return x.CompareTo(y);
        }
        return 0;
    }

    private static bool Beats(Raw a, Raw b) => Compare(a.Priority, b.Priority) is var c && (c > 0 || (c == 0 && a.Order > b.Order));

    /// <summary>Whether an event still has its part at the moment it arrives (the same rules as <see cref="Resolve"/>).</summary>
    private static bool Arrives(string channel, Raw raw, double beat, List<Raw> all)
    {
        if (raw.Seg.T1 - raw.Seg.T0 <= Epsilon)
            return true;
        if (channel == Channels.Support)
        {
            return !all.Any(o => o != raw && o.Seg.T1 - o.Seg.T0 > Epsilon && Compare(o.Priority, raw.Priority) > 0
                && o.Seg.T0 < raw.Seg.T1 - Epsilon && o.Seg.T1 > raw.Seg.T0 + Epsilon);
        }
        double at = beat - 1e-4;   // just before it is there
        return !all.Any(o => o != raw && o.Seg.T0 < at && at < o.Seg.T1 && Beats(o, raw));
    }

    /// <summary>
    /// One channel's events from every track that wrote it, to one row. Narrower and later wins.
    /// A gesture that loses a part of its time keeps the rest (it sets out again from wherever
    /// the part then is, and still arrives when it said). A step cannot be cut: it is dropped.
    /// The height the body stands at is a part of its own and is cut like a gesture, so a row of
    /// `both.low` beside a pose or a walk takes the height and leaves the feet where they are.
    /// </summary>
    private static List<Seg> Resolve(string channel, List<Raw> raws)
    {
        var points = raws.Where(r => r.Seg.T1 - r.Seg.T0 <= Epsilon).ToList();
        var spans = raws.Where(r => r.Seg.T1 - r.Seg.T0 > Epsilon).ToList();
        var result = new List<(Seg Seg, int Rank)>();

        if (channel == Channels.Support)
        {
            foreach (var raw in spans)
            {
                bool cut = spans.Any(o => o != raw && Compare(o.Priority, raw.Priority) > 0
                    && o.Seg.T0 < raw.Seg.T1 - Epsilon && o.Seg.T1 > raw.Seg.T0 + Epsilon);
                if (!cut)
                    result.Add((raw.Seg, 1));
            }
        }
        else
        {
            var bounds = spans.SelectMany(r => new[] { r.Seg.T0, r.Seg.T1 }).OrderBy(t => t).ToList();
            for (int i = bounds.Count - 1; i > 0; i--)
            {
                if (bounds[i] - bounds[i - 1] <= Epsilon)
                    bounds.RemoveAt(i);
            }

            Raw? run = null;
            double runStart = 0;
            void Flush(double end)
            {
                if (run is null)
                    return;
                var seg = run.Seg.Clone();
                seg.T0 = runStart;
                if (seg.Kind == SegKinds.Hold)
                    seg.T1 = end;
                result.Add((seg, 1));
            }

            for (int i = 0; i + 1 < bounds.Count; i++)
            {
                double middle = (bounds[i] + bounds[i + 1]) / 2;
                Raw? winner = null;
                foreach (var raw in spans)
                {
                    if (raw.Seg.T0 < middle && middle < raw.Seg.T1 && (winner is null || Beats(raw, winner)))
                        winner = raw;
                }
                if (!ReferenceEquals(winner, run))
                {
                    Flush(bounds[i]);
                    run = winner;
                    runStart = bounds[i];
                }
            }
            if (bounds.Count > 0)
                Flush(bounds[^1]);
        }

        // Starting poses come before whatever else begins at that instant, the narrowest last.
        var ordered = points.OrderBy(p => p, Comparer<Raw>.Create((a, b) => Beats(a, b) ? 1 : -1)).ToList();
        result.AddRange(ordered.Select(p => (p.Seg, 0)));
        return result.OrderBy(r => r.Seg.T0).ThenBy(r => r.Rank).Select(r => r.Seg).ToList();
    }

    // ---------------------------------------------------------------- words

    /// <summary>The one other dancer a figure's name stands for, or null with the reason reported.</summary>
    private string? OtherDancer(SyntaxToken figure, Ctx ctx)
    {
        if (ctx.Dancer == Nobody)
            return figure.Text;
        if (figure.Text == ctx.Dancer.Figure)
        {
            Error("SH1021", figure.Span, $"'{figure.Text}' is this figure itself. A place on the own body is written without a name: hip, chest, shoulder ...");
            return null;
        }
        var dancers = _active.Where(d => d.Figure == figure.Text).ToList();
        if (dancers.Count == 1)
            return dancers[0].Id;
        Error("SH1021", figure.Span, dancers.Count == 0
            ? $"Nobody dances figure '{figure.Text}' in this score."
            : $"Figure '{figure.Text}' is danced by {dancers.Count} dancers, so which body is meant is not written.");
        return null;
    }

    private double? TurnArgument(SyntaxNode annotation, bool mirror)
    {
        var words = annotation.Child(SyntaxKind.Arguments)?.Child(SyntaxKind.Target) is { } target ? Tokens(target, SyntaxKind.Identifier) : [];
        if (words.Count == 2 && words[0].Text is "left" or "right" && !double.IsNaN(Vocabulary.TurnDegrees(words[1].Text)))
        {
            double degrees = Vocabulary.TurnDegrees(words[1].Text);
            return (words[0].Text == "left") != mirror ? degrees : -degrees;
        }
        Error("SH1014", annotation.Span, "A turn made during a step is written with its side and its amount: @turn(left.quarter), @turn(right.half).");
        return null;
    }

    private Seg? BindGesture(string channel, List<SyntaxToken> words, SyntaxNode? arguments, List<SyntaxNode> annotations, bool mirror, Ctx ctx)
    {
        int side = channel is Channels.LeftArm or Channels.LeftLeg ? 1 : channel is Channels.RightArm or Channels.RightLeg ? -1 : 0;
        bool isArm = channel is Channels.LeftArm or Channels.RightArm;
        bool isLimb = side != 0;
        var flags = new HashSet<string>();

        foreach (var annotation in annotations)
        {
            var name = annotation.Token(SyntaxKind.Identifier)!;
            flags.Add(name.Text);
            if (name.Text is "small" or "large" or "turn")
                Error("SH1010", name.Span, $"'@{name.Text}' is for a step.");
            if (name.Text == "bent" && !isLimb)
                Error("SH1010", name.Span, "'@bent' is for an arm or a leg.");
            if (name.Text == "body" && !isArm)
                Error("SH1010", name.Span, "'@body' is for an arm.");
        }
        if (arguments is not null)
            Error("SH1012", arguments.Span, "Only lift takes a number in brackets: steps { lift(0.4) }");

        if (words.Count is 2 or 3 && words[0].Kind == SyntaxKind.Identifier && _figures.ContainsKey(words[0].Text))
        {
            // b.hand.high: the height at which two hands that reach for each other meet.
            double? meet = null;
            if (words.Count == 3)
            {
                meet = words[2].Text switch { "low" => -1, "mid" => 0, "high" => 1, _ => null };
                if (meet is null || words[1].Text != "hand" || !isArm)
                {
                    Error("SH1012", words[2].Span, "Only an arm reaching for a hand takes a level, for where two hands meet: b.hand.high (a high five), b.hand.mid, b.hand.low.");
                    return null;
                }
            }
            if (!isArm && channel != Channels.Head)
            {
                Error("SH1012", words[0].Span, "A place on another figure is for an arm (reach it) or the head (look at it).");
                return null;
            }
            if (!Vocabulary.Has(Vocabulary.Parts, words[1].Text))
            {
                Error("SH1012", words[1].Span, $"'{words[1].Text}' is not a place on a figure.{Hint(words[1].Text, Vocabulary.Parts.Select(w => w.Name))}");
                return null;
            }
            return OtherDancer(words[0], ctx) is { } other ? new Seg { Kind = SegKinds.Reach, Target = other, Part = words[1].Text, Level = meet } : null;
        }

        string joined = string.Join('.', words.Select(w => w.Text));
        if (isArm && Vocabulary.Has(Vocabulary.Anchors, joined))
            return new Seg { Kind = SegKinds.Anchor, Anchor = joined };

        double x = 0, z = 0;
        double? elevation = null;
        string? pole = null;
        foreach (var word in words)
        {
            if (word.Kind == SyntaxKind.Number)
            {
                // A number where the level goes: degrees above the horizontal.
                double degrees = Number(word);
                if (elevation is not null || degrees is < -90 or > 90)
                {
                    Error("SH1013", word.Span, "A target has one level: low, mid, high, or degrees above the horizontal from -90 to 90 (fwd.30, fwd.-70).");
                    return null;
                }
                elevation = degrees * Math.PI / 180;
                continue;
            }
            switch (word.Text)
            {
                case "fwd": z += 1; break;
                case "back": z -= 1; break;
                case "side" or "across":
                    if (!isLimb)
                    {
                        Error("SH1011", word.Span, $"'{word.Text}' is for an arm or a leg; the {channel} says left or right.");
                        return null;
                    }
                    x += word.Text == "side" ? side : -side;
                    break;
                case "left" or "right":
                    if (isLimb)
                    {
                        bool outward = (word.Text == "left") == (side == 1);
                        Error("SH1011", word.Span,
                            $"An arm or a leg says side or across, so that both sides can be written alike. Here '{word.Text}' is '{(outward ? "side" : "across")}'.");
                        return null;
                    }
                    x += (word.Text == "left") != mirror ? 1 : -1;
                    break;
                case "low" or "mid" or "high":
                    if (elevation is not null)
                    {
                        Error("SH1013", word.Span, "A target has one level: low, mid, high, or degrees above the horizontal.");
                        return null;
                    }
                    elevation = word.Text switch { "high" => Math.PI / 4, "low" => -Math.PI / 4, _ => 0 };
                    break;
                case "up" or "down":
                    pole = word.Text;
                    break;
                default:
                    var known = Vocabulary.Directions.Concat(Vocabulary.Levels).Concat(Vocabulary.Poles).Select(w => w.Name)
                        .Append(Vocabulary.Hold).Append(Vocabulary.Skip).Concat(_defs.Keys);
                    if (isArm)
                        known = known.Concat(Vocabulary.Anchors.Select(w => w.Name));
                    Error("SH1012", word.Span, $"'{word.Text}' is not a direction, a level, or a pose or move of this file.{Hint(word.Text, known)}");
                    return null;
            }
        }

        Vec3 direction;
        if (pole is not null)
        {
            if (words.Count > 1)
            {
                Error("SH1013", words[0].Span, $"'{pole}' stands alone; a slanted direction is written with a level: fwd.high, side.low, fwd.30.");
                return null;
            }
            direction = new Vec3(0, pole == "up" ? 1 : -1, 0);
        }
        else if (x == 0 && z == 0)
        {
            Error("SH1013", words[0].Span, "A direction is needed: fwd, back, side, across (left or right for the torso and head), or up / down.");
            return null;
        }
        else
        {
            var flat = new Vec3(x, 0, z).Normalized();
            direction = flat * Math.Cos(elevation ?? 0) + Vec3.Up * Math.Sin(elevation ?? 0);
        }

        return new Seg
        {
            Kind = SegKinds.Dir,
            Dir = [direction.X, direction.Y, direction.Z],
            Ext = flags.Contains("bent") ? 0.72 : 1,
            Body = isArm && flags.Contains("body"),
        };
    }

    private Seg? BindSupport(List<SyntaxToken> words, SyntaxNode? arguments, List<SyntaxNode> annotations, bool mirror, bool free, Ctx ctx)
    {
        double scale = 1, turn = 0;
        SyntaxToken? turnWord = null;
        foreach (var annotation in annotations)
        {
            var name = annotation.Token(SyntaxKind.Identifier)!;
            switch (name.Text)
            {
                case "bent":
                    Error("SH1010", name.Span, "'@bent' is for an arm or a leg; bent knees under the weight are a level: both.low, left.fwd.low.");
                    break;
                case "body":
                    Error("SH1010", name.Span, "'@body' is for an arm.");
                    break;
                case "small": scale = 0.6; break;
                case "large": scale = 1.4; break;
                case "turn":
                    turnWord = name;
                    turn = TurnArgument(annotation, mirror) ?? 0;
                    break;
            }
        }

        var head = words[0];
        var rest = words.Skip(1).ToList();
        string Side(string word) => mirror ? (word == "left" ? "right" : word == "right" ? "left" : word) : word;

        if (arguments is not null && head.Text != "lift")
            Error("SH1012", arguments.Span, "Only lift takes a number in brackets: lift(0.4)");
        if (turnWord is not null && head.Text is not ("left" or "right" or "jump"))
            Error("SH1010", turnWord.Span, "'@turn(...)' is for a step or a jump. A turn on the spot is a word of its own: turn.left.quarter");

        double? LevelOf(SyntaxToken word) => word.Text switch { "low" => -1, "mid" => 0, "high" => 1, _ => null };

        switch (head.Text)
        {
            case "together":
            {
                // Like a step, a close arrives at a height, and unwritten means normal.
                var seg = new Seg { Kind = SegKinds.Together, Level = 0 };
                foreach (var word in rest)
                {
                    if (LevelOf(word) is not { } level)
                    {
                        Error("SH1012", word.Span, "A close takes only a level: together, together.low, together.high.");
                        return null;
                    }
                    seg.Level = level < 0 ? level * scale : level;
                }
                return seg;
            }

            case "turn":
                if (rest.Count == 2 && rest[0].Text is "left" or "right" && !double.IsNaN(Vocabulary.TurnDegrees(rest[1].Text)))
                {
                    double degrees = Vocabulary.TurnDegrees(rest[1].Text);
                    return new Seg { Kind = SegKinds.Turn, TurnDeg = Side(rest[0].Text) == "left" ? degrees : -degrees };
                }
                if (rest.Count == 1 && _figures.ContainsKey(rest[0].Text))
                    return OtherDancer(rest[0], ctx) is { } other ? new Seg { Kind = SegKinds.Turn, Target = other } : null;
                Error("SH1014", head.Span, "A turn is written with its side and its amount: turn.left.quarter, turn.right.half (eighth, quarter, half, full). To face another figure: turn.b");
                return null;

            case "jump":
            {
                var seg = new Seg { Kind = SegKinds.Jump, TurnDeg = turn };
                foreach (var word in rest)
                {
                    switch (word.Text)
                    {
                        case "place": break;
                        case "fwd": seg.Dz = 0.4 * scale; break;
                        case "back": seg.Dz = -0.3 * scale; break;
                        case "apart": seg.Spread = 0.6 * scale; break;
                        case "together": seg.Spread = Rig.Stance; break;
                        default:
                            Error("SH1012", word.Span, $"A jump goes place, fwd, back, apart or together.{Hint(word.Text, ["place", "fwd", "back", "apart", "together"])}");
                            return null;
                    }
                }
                return seg;
            }

            case "both":
            {
                double? level = null;
                foreach (var word in rest)
                {
                    if (word.Text == "place")
                        continue;
                    if (LevelOf(word) is not { } value)
                    {
                        Error("SH1012", word.Span, "Both feet stay where they are and change height: both.low, both.mid, both.high. To travel, step with one foot or jump.");
                        return null;
                    }
                    level = value;
                }
                if (level is null)
                {
                    Error("SH1013", head.Span, "A level is needed: both.low, both.mid, both.high.");
                    return null;
                }
                // The feet do not travel here, so the size is the size of the knee bend:
                // both.low@small is a slight bend, both.low@large a deep crouch.
                return new Seg { Kind = SegKinds.Level, Level = level < 0 ? level * scale : level };
            }

            case "shift":
                if (rest.Count == 1 && Vocabulary.Has(Vocabulary.ShiftWords, rest[0].Text))
                    return new Seg { Kind = SegKinds.Shift, Foot = Side(rest[0].Text) };
                Error("SH1012", head.Span, "The weight shifts over a foot or between them: shift.left, shift.right, shift.center.");
                return null;

            case "lift" or "land":
            {
                if (!free)
                {
                    Error("SH1022", head.Span, head.Text == "lift"
                        ? "Only a free body can hang in the air: lift(0.4)@free, or free on the dancer line. A jump that obeys gravity is: jump"
                        : "'land' brings a free body back to the floor; it follows a lift.");
                    return null;
                }
                var seg = new Seg { Kind = head.Text == "lift" ? SegKinds.Lift : SegKinds.Land };
                foreach (var word in rest)
                {
                    switch (word.Text)
                    {
                        case "place": break;
                        case "fwd": seg.Dz = 0.4 * scale; break;
                        case "back": seg.Dz = -0.3 * scale; break;
                        default:
                            Error("SH1012", word.Span, $"A {head.Text} goes place, fwd or back.{Hint(word.Text, ["place", "fwd", "back"])}");
                            return null;
                    }
                }
                if (head.Text == "lift")
                {
                    var height = arguments is null ? [] : Tokens(arguments, SyntaxKind.Number);
                    if (height.Count != 1 || arguments!.ChildNodes().Any() || Number(height[0]) < 0)
                    {
                        Error("SH1013", head.Span, "A lift says how high the feet are above the floor, in metres: lift(0.4)");
                        return null;
                    }
                    seg.Height = Number(height[0]);
                }
                return seg;
            }

            case "left" or "right":
            {
                // A step always arrives at a height: unwritten is normal height, so the step
                // after one on the toes comes down again without being told to.
                var seg = new Seg { Kind = SegKinds.Step, Foot = Side(head.Text), Dx = Rig.Stance, Level = 0, TurnDeg = turn, Stance = ctx.Stance };
                bool crossed = false, back = false;
                foreach (var word in rest)
                {
                    switch (word.Text)
                    {
                        case "place": break;
                        case "fwd": seg.Dz += Rig.Stride * scale; break;
                        case "back": seg.Dz -= Rig.Stride * scale; back = true; break;
                        case "side": seg.Dx = Rig.Stance + Rig.SideStep * scale; break;
                        case "across": seg.Dx = -0.08; crossed = true; break;
                        case "low": seg.Level = -1; break;
                        case "mid": seg.Level = 0; break;
                        case "high": seg.Level = 1; break;
                        default:
                            Error("SH1012", word.Span, word.Kind == SyntaxKind.Number
                                ? "Under the weight a level is low, mid or high; degrees are for arms, free legs, the torso and the head."
                                : $"'{word.Text}' is not a step direction or a level.{Hint(word.Text, Vocabulary.StepDirections.Concat(Vocabulary.Levels).Select(w => w.Name))}");
                            return null;
                    }
                }
                // A crossing foot goes just in front of the other one, or with `back` just behind
                // it: a cross is not a stride. (`fwd.across` is a full stride that crosses.)
                if (crossed && seg.Dz == 0)
                    seg.Dz = 0.12 * scale;
                else if (crossed && back)
                    seg.Dz = -0.12 * scale;
                return seg;
            }
        }

        var heads = Vocabulary.Feet.Concat(Vocabulary.SupportActions).Select(w => w.Name).Append(Vocabulary.Hold).Append(Vocabulary.Skip).Concat(_defs.Keys);
        string hintText = Vocabulary.Has(Vocabulary.Directions, head.Text) || Vocabulary.Has(Vocabulary.Poles, head.Text)
            ? $" A step names its foot first: left.{head.Text}, right.{head.Text}."
            : Hint(head.Text, heads);
        Error("SH1012", head.Span, $"'{head.Text}' is not something the feet do.{hintText}");
        return null;
    }

    /// <summary>
    /// SH2001. A part sent to the exact opposite of where it is can get there by any way
    /// round, so the source does not say what the movement is. (Writing a half-circle through
    /// its middle is also how a notator writes it.)
    /// </summary>
    private void CheckOppositeTargets(DancerIr dancer, bool loop)
    {
        foreach (var (channel, segs) in dancer.Tracks)
        {
            if (channel is Channels.Support or Channels.Height)
                continue;
            double[]? previous = channel switch
            {
                Channels.Torso => [0, 1, 0],
                Channels.Head => [0, 0, 1],
                _ => [0, -1, 0],
            };
            // A loop follows itself: it starts where it ends.
            if (loop && segs.LastOrDefault(s => s.Kind != SegKinds.Hold) is { } last)
                previous = last.Dir;
            foreach (var seg in segs)
            {
                if (seg.Kind is SegKinds.Anchor or SegKinds.Reach)
                {
                    previous = null;
                }
                else if (seg.Kind == SegKinds.Dir)
                {
                    var dir = seg.Dir!;
                    if (previous is not null && previous[0] * dir[0] + previous[1] * dir[1] + previous[2] * dir[2] < -0.98)
                    {
                        Report("SH2001", DiagnosticSeverity.Warning, new TextSpan(seg.Src, seg.Len),
                            "This target is the exact opposite of where the part is, so the way round is not written. Add a target in between (down, fwd, up).");
                    }
                    previous = dir;
                }
            }
        }
    }
}
