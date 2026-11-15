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
    /// <param name="score">The score to bind ("" is the unnamed one); null binds every score the file has, a clip each.</param>
    /// <param name="songs">Reads the song a `music` line names; null where there is nothing to read one with.</param>
    public static (ScoreIr Score, List<Diagnostic> Diagnostics) Bind(SyntaxTree tree, string? score = null, SongSource? songs = null)
    {
        var names = tree.Root.ChildNodes(SyntaxKind.ScoreDecl).Select(s => s.Token(SyntaxKind.Identifier, 1)?.Text ?? "").Distinct().ToList();
        var result = new ScoreIr { Name = score ?? "" };
        result.Scores.AddRange(names);

        // A score is bound by itself: its tempo, its song and its dancers are its own.
        // A file with no score yet is still checked, as far as it goes.
        IEnumerable<string?> runs = score is not null ? [score] : names.Count > 0 ? names : new string?[] { null };
        var diagnostics = new List<Diagnostic>();
        var seen = new HashSet<(string, int, int)>();
        foreach (string? name in runs)
        {
            var (bound, found) = new BinderRun(tree, name, songs).Run();
            result.Clips.AddRange(bound.Clips);
            foreach (var (move, length) in bound.MoveLengths)
                result.MoveLengths.TryAdd(move, length);
            diagnostics.AddRange(found.Where(d => seen.Add((d.Code, d.Span.Start, d.Span.Length))));
        }
        return (result, diagnostics);
    }
}

/// <param name="scoreName">The score to bind; null in a file that has none.</param>
internal sealed class BinderRun(SyntaxTree tree, string? scoreName, SongSource? songs)
{
    private const double Epsilon = 1e-6;

    private sealed class DancerSpec
    {
        public string Id = "", Name = "", Figure = "";
        public double X, Z, FacingDeg, Delay;
        public bool Mirror, Free;
        public HashSet<string>? In;
    }

    /// <summary>
    /// One word on one channel before the overlaps are settled. Its segment's T0..T1 is here the
    /// word's own time — from the beat it is written at, for its length — which is when it has
    /// the part; <see cref="Resolve"/> makes the way there of it. A word that a loop wraps over its
    /// end is two of these with one Order; Start is where the word itself begins.
    /// </summary>
    /// <param name="Stance">A pose's own word: where a part is kept, not something done.</param>
    /// <param name="Rows">The `&amp;` groups it is in, and its row in each.</param>
    private sealed record Raw(Seg Seg, int[] Priority, int Order, double Start, double Since, bool Stance = false, (int Group, int Row)[]? Rows = null);

    /// <summary>
    /// How long before its beat a part sets out (DESIGN.md 50.2). The feet in the second half of
    /// the word before and no longer than this many seconds, so a foot stands on the beat and is
    /// carried off it; a sharp word in no more than these; anything else in all of the word before.
    /// </summary>
    private const double StepSeconds = 0.5, SharpSeconds = 0.15;

    /// <summary>
    /// A jump comes down on its beat. Before it — the bend and the air — it has this much of the
    /// word before (of no more than so many seconds of it), and after it, for the knees to
    /// give, the rest of as much of its own time: two jumps one after the other just fit.
    /// </summary>
    private const double JumpSeconds = 1.0, JumpBefore = 0.8;

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
        TextSpan? Src = null,
        double Since = double.NegativeInfinity,    // where the word before the next one in the row begins: the next sets out no earlier
        bool After = false,                        // done again by a `%`, the first word has another word before it than where it was written: Since is that one's
        (int Group, int Row)[]? Rows = null);      // the `&` groups the walk is inside, and the row of each

    private readonly List<Diagnostic> _diagnostics = [];
    private readonly HashSet<(string, int, int)> _reported = [];
    private int _mute;
    private string _playing = "";   // the section being bound
    private int _verse = 1;   // which play of its section, in this score, is being bound: what a `verse N` asks
    private int _order;
    private int _groups;   // the `&` groups bound so far: each has a number of its own
    private double _inner = double.NaN;   // set by a move just bound: where the last word inside it begins (NaN: it is one word)
    private readonly List<(MarkIr Mark, List<(string Channel, Raw Raw)> Raws)> _marks = [];   // of the dancer being bound

    private string _title = "";
    private double _tempo = 90;
    private int _beatsPerBar = 4, _beatUnit = 4, _fps = 30;
    private string? _music, _musicScore;
    private SyntaxNode? _musicNode, _timeNode;
    private bool _tempoWritten, _scoreTempo;    // _scoreTempo: by the score itself
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

    /// <summary>
    /// The lines a section begins with. Its tempo is in force from where it is played until a
    /// section that writes another is played; its time is its own (a section that writes none
    /// is in the file's, whatever is played before it); its upbeat is how many beats a row
    /// of it may begin before its first bar line, over the end of whatever is played before it.
    /// </summary>
    private sealed record SectionHead(TempoSpec? Tempo, (int Beats, int Unit)? Time, double Upbeat, SyntaxNode? UpbeatNode);

    /// <summary>
    /// A tempo as it is written, the same wherever it is written: `tempo 120` (Count, in beats),
    /// `tempo 4. = 60` (Count of the Note: a dotted crotchet), `tempo 80%` (Ratio, of the tempo
    /// there would be without the line; Count is then null). What a beat is, and what the tempo
    /// would have been, are only known where it is played: <see cref="Resolve"/>.
    /// A form's percentage before a section that writes a tempo is both: the section's Count, times the Ratio.
    /// </summary>
    private sealed record TempoSpec(double? Count, (int Unit, bool Dotted)? Note, double Ratio, SyntaxNode Node);

    private TempoSpec? _fileSpec, _scoreSpec;        // the `tempo` at the top of the file, and the score's own

    /// <summary>`tempo N`, `tempo N%` or `tempo NOTE = N`, wherever a tempo is written.</summary>
    private TempoSpec? ReadTempo(SyntaxNode line)
    {
        var numbers = Tokens(line, SyntaxKind.Number);
        if (Tokens(line, SyntaxKind.EqualsSign).Count > 0)
        {
            if (numbers.Count != 2)
                return null;
            double unit = Number(numbers[0]), count = Number(numbers[1]);
            if (unit is not (1 or 2 or 4 or 8 or 16 or 32 or 64))
                Error("SH1001", numbers[0].Span, "The note a tempo counts is 2 (a minim), 4 (a crotchet), 8 (a quaver) …, with a dot for a dotted one: tempo 4 = 120, tempo 4. = 60.");
            else if (!(count > 0))
                Error("SH1001", numbers[1].Span, "The tempo is how many of that note there are in a minute: tempo 4. = 60.");
            else
                return new TempoSpec(count, ((int)unit, Tokens(line, SyntaxKind.Dot).Count > 0), 1, line);
            return null;
        }
        if (numbers.Count != 1)
            return null;
        double value = Number(numbers[0]);
        if (Tokens(line, SyntaxKind.Percent).Count > 0)
        {
            if (value > 0)
                return new TempoSpec(null, null, value / 100, line);
            Error("SH1001", numbers[0].Span, "A tempo in per cent is more than none: tempo 50% is half as fast, tempo 110% a little faster.");
            return null;
        }
        if (value is < 10 or > 400)
        {
            Error("SH1001", numbers[0].Span, "The tempo is in beats per minute, from 10 to 400.");
            return null;
        }
        return new TempoSpec(value, null, 1, line);
    }

    /// <summary>
    /// The beats a minute of a tempo where it is played: <paramref name="inherited"/> is the
    /// tempo there would be without it, <paramref name="beatUnit"/> the note a beat is there,
    /// <paramref name="faster"/> how much faster the score plays everything that is written.
    /// </summary>
    private double Resolve(TempoSpec spec, double inherited, int beatUnit, double faster = 1)
    {
        double bpm = spec.Count is { } count
            ? count * (spec.Note is { } note ? (double)beatUnit / note.Unit * (note.Dotted ? 1.5 : 1) : 1) * faster
            : inherited;
        bpm *= spec.Ratio;
        double written = bpm / faster;
        if ((spec.Note is not null || spec.Ratio != 1) && written is < 10 or > 400)
        {
            Error("SH1001", spec.Node.Span, $"This comes to {Format(written)} beats a minute here; a tempo is from 10 to 400.");
            return Math.Clamp(written, 10, 400) * faster;
        }
        return bpm;
    }

    /// <summary>
    /// The tempo the score begins at, from what the top of the file and the score write.
    /// <paramref name="song"/>: the tempo of the song it is danced to, which is what there is when nothing is written.
    /// </summary>
    private void SettleTempo(double? song, int beatUnit)
    {
        double file = song ?? 90;
        if (_fileSpec is { Count: null } && song is null)
        {
            if (_music is null)
                Error("SH1038", _fileSpec.Node.Span, "A tempo in per cent is of the tempo there would be without it, and at the top of a file without a song there is none. Write the beats a minute: tempo 120");
        }
        else if (_fileSpec is { } top)
        {
            file = Resolve(top, file, beatUnit);
        }
        _fileTempo = file;
        _tempo = _scoreSpec is { } own ? Resolve(own, file, beatUnit) : file;
        _tempoWritten = _fileSpec is not null || _scoreSpec is not null;
        _scoreTempo = _scoreSpec is not null;
    }
    private readonly Dictionary<string, SectionHead> _heads = [];
    private double _fileTempo = 90;                  // the tempo at the top of the file: a score's own is so much faster or slower
    private bool _probing;                           // a row is walked only to see how long its first bar is
    private double _firstBar = double.NaN;
    private double _upbeatEnd = double.NaN;          // the walk's time at the first bar line of a row that begins with the upbeat
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
    private List<Play> Plays(SyntaxNode form)
    {
        var plays = new List<Play>();
        List<Play>? body = null;        // of the repeat that is open
        SyntaxNode? open = null;
        bool closed = false;            // its ':|' has been read: only further endings still belong to it
        int? times = null;
        var endings = new List<(int From, int To, List<Play> Names)>();
        // A `tempo` or a `time` written in the form waits for the name after it.
        TempoSpec? tempo = null;
        (int, int)? time = null;
        SyntaxNode? waiting = null;

        List<Play> Names(IEnumerable<SyntaxNode> items)
        {
            var names = new List<Play>();
            foreach (var item in items)
            {
                if (item.Kind == SyntaxKind.FormChange)
                {
                    string word = item.ChildTokens().First().Text;
                    if (_music is not null)
                        Error("SH1035", item.Span, $"The {word} is the song's: a form over a song's sections does not write it. Take it out (a score's own 'tempo N' or 'tempo N%' plays the whole song faster or slower).");
                    else
                        ReadChange(item, ref tempo, ref time);
                    waiting = item;
                    continue;
                }
                if (item.Token(SyntaxKind.Identifier) is not { } name)
                    continue;
                if (item.Token(SyntaxKind.Star) is { } star)
                    Error("SH1025", star.Span, $"A section is repeated with repeat signs: |: {name.Text} :|*{item.Token(SyntaxKind.Number)?.Text ?? "N"}");
                var known = _formNames ?? _sections.Keys;
                if (_anyFormName || known.Contains(name.Text))
                    names.Add(new Play(name.Text, tempo is null && time is null ? null : new SectionHead(tempo, time, 0, null)));
                else
                    Error("SH1005", name.Span, $"{(_formNames is null ? "No section is" : "The song has no section")} named '{name.Text}'.{Hint(name.Text, known)}");
                (tempo, time, waiting) = (null, null, null);
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
                case SyntaxKind.FormItem or SyntaxKind.FormChange:
                    if (closed)
                        Finish();
                    (body ?? plays).AddRange(Names([node]));
                    break;

                case SyntaxKind.FormEnding:
                    var numbers = node.ChildTokens().Where(t => t.Kind == SyntaxKind.Number && !t.IsMissing).ToList();
                    var names = Names(node.ChildNodes().Where(n => n.Kind is SyntaxKind.FormItem or SyntaxKind.FormChange));
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
        if (waiting is not null)
            Error("SH1037", waiting.Span, $"A '{waiting.ChildTokens().First().Text}' in a form is for the section named after it, and none is: form {{ A tempo 132 B }}");
        return plays;
    }

    /// <summary>
    /// One section played by a form. Set: the tempo and the time the form writes before its name,
    /// which it is played in this time whatever the section writes itself.
    /// </summary>
    private sealed record Play(string Name, SectionHead? Set = null);

    public (ScoreIr, List<Diagnostic>) Run()
    {
        Collect();

        var result = new ScoreIr { Name = scoreName ?? "" };

        string? formName = null;
        var cast = new List<SyntaxToken>();
        var attributes = new HashSet<string>();
        SyntaxNode? scoreNode = null;
        if (scoreName is null)
        {
            string who = _dancerNodes.Select(d => d.Token(SyntaxKind.Identifier, 1)?.Text).FirstOrDefault(n => n is not null) ?? "NAME";
            Error("SH1030", new TextSpan(tree.Text.Length, 0),
                $"A file needs a score: what is written out, and who dances in it. Add: score {{ dancer {who} }}");
        }
        else if (_scores.TryGetValue(scoreName, out scoreNode))
        {
            BindScore(scoreNode, ref formName, cast, attributes);
        }
        else
        {
            Error("SH1005", new TextSpan(0, 0), $"No score is named '{scoreName}'.{Hint(scoreName, _scores.Keys)}");
        }

        // The song first: its tempo is what seconds and frames are counted by.
        var form = formName is not null ? _forms.GetValueOrDefault(formName) : _forms.GetValueOrDefault("");
        SettleTempo(null, _beatUnit);
        ReadSong(form);
        if (_tempos.Count == 0 || _tempos[0].Beat > 0)
            _tempos.Insert(0, new TempoIr(0, _tempo));
        if (_meters.Count == 0 || _meters[0].Beat > 0)
            _meters.Insert(0, new MeterIr(0, _beatsPerBar, _beatUnit));
        Place(null);

        var dancers = BuildDancers();
        foreach (var name in cast.Where(c => !dancers.Any(d => d.Id == c.Text)))
            Error("SH1005", name.Span, $"No dancer is named '{name.Text}'.{Hint(name.Text, dancers.Select(d => d.Id))}");
        _active = dancers.Where(d => cast.Any(c => c.Text == d.Id)).ToList();
        foreach (var definition in _defs)
            CheckDefinition(definition.Key, definition.Value);
        foreach (var section in _sections.Values)
            CheckSection(section);

        // One score is one movement: what its form plays (or its song), danced by whom it lists.
        if (scoreNode is not null)
        {
            var keyword = scoreNode.ChildTokens().First();
            List<(string Name, double? Beats, double Pickup, SectionHead? Set)>? plays =
                _song is not null ? _song.Plays.Select(p => (p.Name, (double?)p.Beats, p.Pickup, (SectionHead?)null)).ToList()   // the song, every section for as long as it plays it
                : form is not null ? Plays(form).Select(Own).ToList()
                : _music is not null ? _sectionOrder.Select(n => Own(new Play(n))).ToList()                    // the song could not be read: the sections as they are written
                : null;
            if (cast.Count == 0)
            {
                string who = dancers.Count > 0 ? dancers[0].Id : "NAME";
                Error("SH1031", keyword.Span, $"A score lists who dances in it: score {(scoreName == "" ? "" : scoreName + " ")}{{ dancer {who} }}");
            }
            if (plays is null)
            {
                Error("SH1032", keyword.Span, formName is null && _forms.Count > 0
                    ? $"This score plays the unnamed form, and the file has none. Name one of its forms: form {_forms.Keys.First()}"
                    : $"A score plays a form, and the file has none. Write the order of the sections: form {{ {string.Join(' ', _sectionOrder)} }}");
            }
            else
            {
                var clip = NewClip(scoreName ?? "");
                clip.Loop = attributes.Contains("loop");
                clip.Inplace = attributes.Contains("inplace");
                BindClip(clip, plays);
                result.Clips.Add(clip);
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
                    if (name is not null && !_figures.TryAdd(name.Text, name.Text))
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
                    {
                        _sectionOrder.Add(name.Text);
                        _heads[name.Text] = ReadHead(item);
                    }
                    else
                        Error("SH1005", name.Span, $"A section named '{name.Text}' is already written above.");
                    foreach (var attribute in Tokens(item, SyntaxKind.Identifier).Skip(2))
                    {
                        Error("SH1024", attribute.Span, attribute.Text is "loop" or "inplace"
                            ? $"'{attribute.Text}' is said of what is written out, so it goes in the score: score {{ dancer NAME  {attribute.Text} }}"
                            : $"After a section's name comes its '{{'; '{attribute.Text}' does not belong here.");
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

    private SectionHead ReadHead(SyntaxNode section)
    {
        TempoSpec? tempo = null;
        (int, int)? time = null;
        double upbeat = 0;
        SyntaxNode? upbeatNode = null;
        bool late = false;
        foreach (var child in section.ChildNodes())
        {
            if (child.Kind != SyntaxKind.SectionItem)
            {
                late = true;
                continue;
            }
            var numbers = Tokens(child, SyntaxKind.Number);
            switch (child.ChildTokens().First().Text)
            {
                case "tempo" or "time":
                    ReadChange(child, ref tempo, ref time);
                    break;

                case "upbeat" when numbers.Count >= 1:
                    double length = Number(numbers[0]) / (numbers.Count > 1 ? Number(numbers[1]) : 1);
                    if (late)
                        Error("SH1036", child.Span, "The upbeat is said where the section begins, before its first block: section A { upbeat 1  solo { ... } }");
                    else if (!(length > 0) || double.IsInfinity(length))
                        Error("SH1036", child.Span, "An upbeat is a number of beats, more than none and less than a bar: upbeat 1, upbeat 1/2.");
                    else
                        (upbeat, upbeatNode) = (length, child);
                    break;
            }
        }
        return new SectionHead(tempo, time, upbeat, upbeatNode);
    }

    /// <summary>`tempo N` or `time N/N`, at the start of a section or before a name in a form.</summary>
    private void ReadChange(SyntaxNode line, ref TempoSpec? tempo, ref (int, int)? time)
    {
        var numbers = Tokens(line, SyntaxKind.Number);
        if (line.ChildTokens().First().Text == "tempo")
        {
            tempo = ReadTempo(line) ?? tempo;
        }
        else if (numbers.Count == 2)
        {
            double beats = Number(numbers[0]), unit = Number(numbers[1]);
            if (beats < 1 || beats != Math.Floor(beats) || unit < 1 || unit != Math.Floor(unit))
                Error("SH1001", line.Span, "The time is written as two whole numbers: time 4/4, time 3/4.");
            else
                time = ((int)beats, (int)unit);
        }
    }

    /// <summary>The beats of a section's upbeat where a bar has <paramref name="perBar"/>; none if it is not shorter than that.</summary>
    private double UpbeatOf(string name, int perBar) =>
        _heads.TryGetValue(name, out var head) && head.Upbeat < perBar - Epsilon ? head.Upbeat : 0;

    /// <summary>
    /// Whether a row begins with its section's upbeat: its first bar line closes a bar of just
    /// that many beats. Any other row begins at the section's first bar line.
    /// </summary>
    private bool BeginsWithUpbeat(SyntaxNode track, string[] channels, string trackName, double upbeat, double start)
    {
        if (upbeat <= Epsilon)
            return false;
        (_probing, _firstBar) = (true, double.NaN);
        _mute++;
        double time = start, bar = start;
        Walk(track, new Ctx(Nobody, null, [], channels, trackName, [0], 0, Free: true), ref time, ref bar);
        _mute--;
        _probing = false;
        return Math.Abs(_firstBar - upbeat) < Epsilon;
    }

    /// <summary>The tempo and the time there are once <paramref name="head"/> has written its own over <paramref name="from"/>.</summary>
    private (double Bpm, MeterIr Meter) Under((double Bpm, MeterIr Meter) from, SectionHead? head, double faster)
    {
        if (head is null)
            return from;
        var (bpm, meter) = from;
        if (head.Time is { } time)
        {
            // What a beat is may change with the time (4/4 to 6/8): the music goes on as fast as it went.
            bpm = bpm * time.Unit / meter.BeatUnit;
            meter = meter with { BeatsPerBar = time.Beats, BeatUnit = time.Unit };
        }
        if (head.Tempo is { } written)
            bpm = Resolve(written, bpm, meter.BeatUnit, faster);
        return (Math.Round(bpm, 3), meter);
    }

    /// <summary>
    /// Where a section begins: the tempo goes on from what was played before it, and the time is
    /// the file's again — a section's bars are written in one time, wherever it is played. The
    /// music keeps its speed where that changes what a beat is.
    /// </summary>
    private (double Bpm, MeterIr Meter) Fresh((double Bpm, MeterIr Meter) from) =>
        (Math.Round(from.Bpm * _beatUnit / from.Meter.BeatUnit, 3), from.Meter with { BeatsPerBar = _beatsPerBar, BeatUnit = _beatUnit });
    private void Put(double beat, (double Bpm, MeterIr Meter) at)
    {
        _meters.RemoveAll(m => m.Beat >= beat - Epsilon);
        if (_meters.Count == 0 || _meters[^1].BeatsPerBar != at.Meter.BeatsPerBar || _meters[^1].BeatUnit != at.Meter.BeatUnit)
            _meters.Add(at.Meter with { Beat = beat });
        _tempos.RemoveAll(t => t.Beat >= beat - Epsilon);
        if (_tempos.Count == 0 || _tempos[^1].Bpm != at.Bpm)
            _tempos.Add(new TempoIr(beat, at.Bpm));
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
                _fileSpec = ReadTempo(header) ?? _fileSpec;
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
            _songOrder = Plays(form).Select(p => p.Name).ToList();
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
        SettleTempo(Math.Clamp(song.Tempo, 10, 400), song.BeatUnit);
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

    private void BindScore(SyntaxNode score, ref string? formName, List<SyntaxToken> cast, HashSet<string> attributes)
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
                    _scoreSpec = ReadTempo(item) ?? _scoreSpec;
                    break;
                case "title":
                    if (values.Count == 1 && values[0].Kind == SyntaxKind.String)
                        _title = values[0].Text.Trim('"');
                    break;
                case "music":
                    SetMusic(item, values);
                    break;
                case "dancer":
                    if (values.Count == 0)
                        Error("SH1005", item.Span, "'dancer' is followed by the name of one of the file's dancers: dancer alice");
                    cast.AddRange(values);
                    break;
                case "loop" or "inplace":
                    attributes.Add(tokens[0].Text);
                    break;
            }
        }
    }

    private List<DancerSpec> BuildDancers()
    {
        var dancers = new List<DancerSpec>();
        var ids = new HashSet<string>();

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
                        if (values.Count == 1 && _figures.ContainsKey(values[0].Text))
                            dancer.Figure = values[0].Text;
                        else if (_figures.Count == 0)
                            Error("SH1003", clause.Span, $"'on' names the figure this dancer dances, and the file declares none. Add: figure {(values.Count == 1 ? values[0].Text : dancer.Id)}");
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

            if (!on)
                Error("SH1003", words[1].Span, $"Say which figure this dancer dances: dancer {dancer.Id} humanoid on {(_figures.Count > 0 ? _figures.Keys.First() : "FIGURE")}");
            // A dancer whose figure is not known still exists: a score may name it.
            dancers.Add(dancer);
        }
        return dancers;
    }

    // ---------------------------------------------------------------- checks made once

    private static string[]? ChannelsOf(string trackName) => trackName switch
    {
        "body" => Channels.All,
        "steps" => [Channels.Support, Channels.Height],
        "arms" => [Channels.LeftArm, Channels.RightArm],
        Channels.LeftArm or Channels.RightArm or Channels.LeftLeg or Channels.RightLeg or Channels.Hips or Channels.Torso or Channels.Twist or Channels.Head => [trackName],
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
        foreach (var child in section.ChildNodes())
        {
            var name = child.Token(SyntaxKind.Identifier);
            if (name is null)
                continue;
            if (child.Kind == SyntaxKind.SectionItem)
            {
                if (_music is not null && name.Text is "tempo" or "time")
                    Error("SH1035", child.Span, $"The {name.Text} is the song's: a file danced to a song does not write it in a section. Take this line out.");
                continue;
            }
            if (child.Kind == SyntaxKind.FigureBlock && !_figures.ContainsKey(name.Text))
            {
                Error("SH1003", name.Span, _figures.Count > 0
                    ? $"No figure is named '{name.Text}'.{Hint(name.Text, _figures.Keys)}"
                    : $"No figure is named '{name.Text}'. Declare it: figure {name.Text}");
            }
            else if (child.Kind == SyntaxKind.Track)
            {
                Error("SH1003", name.Span,
                    _figures.ContainsKey(name.Text) ? $"The block of figure '{name.Text}' holds tracks: {name.Text} {{ steps {{ ... }} }}"
                    : _figures.Count > 0 ? $"A track goes inside the block of the figure that does it: {_figures.Keys.First()} {{ {name.Text} {{ ... }} }}"
                    : $"A track goes inside the block of the figure that does it. Declare the figure and who dances it (figure hero, dancer hero humanoid on hero), then: hero {{ {name.Text} {{ ... }} }}");
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
        section.ChildNodes(SyntaxKind.FigureBlock).Where(b => b.Token(SyntaxKind.Identifier)?.Text == figure).SelectMany(b => b.ChildNodes(SyntaxKind.Track))
        .Where(t => t.Token(SyntaxKind.Identifier) is { } name && ChannelsOf(name.Text) is not null);

    /// <summary>A section is as long as its longest track, whoever dances it.</summary>
    private double SectionLength(string name)
    {
        // Where it is played says how long a bar is and how many beats a second has, so its length is of that place.
        var meter = double.IsNaN(_songShift) ? _meters[0] : Bars.MeterAt(_meters, _songShift);
        string key = FormattableString.Invariant($"{name}|{meter.BeatsPerBar}|{_unitTempo}");
        if (_sectionLength.TryGetValue(key, out double known))
            return known;
        double length = 0, upbeat = UpbeatOf(name, meter.BeatsPerBar);
        _mute++;
        foreach (var track in _sections[name].DescendantNodes().Where(n => n.Kind == SyntaxKind.Track && n.Parent!.Kind != SyntaxKind.Track))
        {
            if (track.Token(SyntaxKind.Identifier) is not { } trackName || ChannelsOf(trackName.Text) is not { } channels)
                continue;
            // The upbeat is before the section: it is not a part of its length.
            double time = BeginsWithUpbeat(track, channels, trackName.Text, upbeat, 0) ? -upbeat : 0, bar = time;
            Walk(track, new Ctx(Nobody, null, [], channels, trackName.Text, [0], 0, Free: true), ref time, ref bar);
            length = Math.Max(length, time);
        }
        _mute--;
        return _sectionLength[key] = length;
    }

    /// <summary>A play of one of this file's sections, as long as the section is written.</summary>
    private static (string Name, double? Beats, double Pickup, SectionHead? Set) Own(Play play) => (play.Name, null, 0, play.Set);


    /// <summary>
    /// Lays the plays out one after the other. A play with Beats is a section of the song: it
    /// lasts that long whatever is written for it here — a shorter dance stands still for the
    /// rest, a section this file does not have is stood through — and the dance of it begins
    /// after the song's upbeat (Pickup).
    /// </summary>
    private void BindClip(MotionIr clip, List<(string Name, double? Beats, double Pickup, SectionHead? Set)> plays)
    {
        double cursor = 0;
        int bar = 1;
        var starts = new List<double>();
        // Without a song the sections and the form say the tempo and the time themselves, each from where it is played.
        bool mapped = _music is null && plays.Any(p => p.Set is not null || _heads.GetValueOrDefault(p.Name) is { } h && (h.Tempo is not null || h.Time is not null));
        // A score's own tempo is the one it BEGINS at, and what it plays first may write a tempo
        // of its own: every section's tempo is as much faster or slower as that one is.
        double faster = 1;
        if (mapped && _scoreTempo)
        {
            var kept = (Bpm: _fileTempo, Meter: _meters[0] with { BeatUnit = _beatUnit });
            var begins = kept;
            foreach (var play in plays)
            {
                kept = Under(Fresh(kept), _heads.GetValueOrDefault(play.Name), 1);
                begins = Under(kept, play.Set, 1);
                // A section of no rows has no length: the next one begins the score too.
                if (!_sections.TryGetValue(play.Name, out var written) || written.DescendantNodes().Any(n => n.Kind == SyntaxKind.Track))
                    break;
            }
            faster = Resolve(_scoreSpec!, begins.Bpm, begins.Meter.BeatUnit) / begins.Bpm;
            _tempos[0] = new TempoIr(0, Math.Round(_fileTempo * faster, 3));
        }
        // The tempo a section writes at its start is in force until the next one that writes one; its
        // time is its own (Fresh). What the form writes before a name is over that for the one play,
        // and the next play is as if it had not been.
        var inForce = (_tempos[^1].Bpm, Meter: _meters[^1]);
        foreach (var (name, beats, pickup, set) in plays)
        {
            bool danced = _sections.ContainsKey(name);
            if (mapped)
            {
                inForce = Under(Fresh(inForce), _heads.GetValueOrDefault(name), faster);
                Put(cursor, Under(inForce, set, faster));
            }
            Place(beats is null && !mapped ? null : cursor + pickup);
            if (danced && _heads[name] is { UpbeatNode: { } said } early && early.Upbeat >= Bars.MeterAt(_meters, cursor + pickup).BeatsPerBar - Epsilon)
                Error("SH1036", said.Span, "An upbeat is shorter than a bar: upbeat 1, upbeat 1/2.");
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
        if (mapped)
        {
            (clip.Tempos, clip.Meters) = ([.. _tempos], [.. _meters]);
            (clip.Tempo, clip.BeatsPerBar, clip.BeatUnit) = (_tempos[0].Bpm, _meters[0].BeatsPerBar, _meters[0].BeatUnit);
        }

        foreach (var dancer in _active)
        {
            var ir = new DancerIr
            {
                Id = dancer.Id, Name = dancer.Name == "" ? dancer.Id : dancer.Name, Figure = dancer.Figure,
                X = dancer.X, Z = dancer.Z, FacingDeg = dancer.FacingDeg, Free = dancer.Free,
            };
            var sink = new Dictionary<string, List<Raw>>();
            int trackIndex = 0;
            double before = double.NegativeInfinity;   // where the last word the dancer has danced so far begins
            for (int i = 0; i < plays.Count; i++)
            {
                if (!_sections.TryGetValue(plays[i].Name, out var section) || dancer.In is not null && !dancer.In.Contains(plays[i].Name))
                {
                    // A section stood through is stood through: what comes after it sets out in its last bar.
                    before = Math.Max(before, LastBar(clip.Sections[i]) + dancer.Delay);
                    continue;
                }
                Place(plays[i].Beats is null && !mapped ? null : starts[i], starts[i] + dancer.Delay);
                _playing = plays[i].Name;
                _verse = plays.Take(i).Count(p => p.Name == plays[i].Name) + 1;
                double upbeat = UpbeatOf(plays[i].Name, Bars.MeterAt(_meters, starts[i]).BeatsPerBar);
                double last = before;
                foreach (var track in TracksOf(section, dancer.Figure))
                    last = Math.Max(last, BindTrack(track, dancer, clip, sink, starts[i] + dancer.Delay, trackIndex++, i, upbeat, before));
                // No earlier than the last bar either: a section whose rows end early is stood through to there.
                before = Math.Max(last, LastBar(clip.Sections[i]) + dancer.Delay);
            }
            Place(null);
            CheckTwistsAtOnce(sink);
            foreach (string channel in Channels.All)
            {
                if (sink.TryGetValue(channel, out var raws))
                    ir.Tracks[channel] = Resolve(channel, raws, clip, dancer.Delay);
            }
            // A mark names an arrival: one whose event lost its part to another track at that moment names nothing.
            clip.Marks.AddRange(_marks.Where(m => m.Raws.Count == 0 || m.Raws.Any(r => Arrives(r.Raw, m.Mark.Beat, sink[r.Channel]))).Select(m => m.Mark));
            _marks.Clear();
            CheckOppositeTargets(ir, clip.Loop);
            clip.Dancers.Add(ir);
        }
        clip.Marks.Sort((a, b) => a.Beat.CompareTo(b.Beat));
    }

    /// <summary>Where the last bar of a play of a section begins.</summary>
    private double LastBar(SectionIr section) =>
        Math.Max(section.T0, section.T1 - Bars.MeterAt(_meters, Math.Max(section.T0, section.T1 - Epsilon)).BeatsPerBar);

    /// <param name="play">Which play of the score this is: a later one wins where two meet, which is how an upbeat takes the end of what is before it.</param>
    /// <param name="before">Where the last word of what the dancer danced before this section begins: this row's first word sets out no earlier.</param>
    /// <returns>Where the last word of the row begins.</returns>
    private double BindTrack(SyntaxNode track, DancerSpec dancer, MotionIr clip, Dictionary<string, List<Raw>> sink, double start, int index, int play, double upbeat, double before)
    {
        var name = track.Token(SyntaxKind.Identifier)!;
        var channels = ChannelsOf(name.Text)!;
        var ctx = new Ctx(dancer, clip, sink, dancer.Mirror ? channels.Select(Channels.Mirror).ToArray() : channels, name.Text,
            [play, Channels.All.Length - channels.Length, index], dancer.Delay, Mirror: dancer.Mirror, Free: dancer.Free, CheckBars: BarsKnown, Since: before);

        bool early = BeginsWithUpbeat(track, channels, name.Text, upbeat, start);
        double time = early ? start - upbeat : start, barStart = time;
        _upbeatEnd = early ? start : double.NaN;
        double since = Walk(track, ctx, ref time, ref barStart);
        _upbeatEnd = double.NaN;

        double rest = time - barStart;
        var meter = MeterAt(ctx, barStart);
        if (BarsKnown && track.DescendantNodes().Any(n => n.Kind == SyntaxKind.BarLine) && rest > Epsilon && Math.Abs(rest - meter.BeatsPerBar) > Epsilon)
        {
            Report("SH1002", DiagnosticSeverity.Warning, name.Span,
                $"The last bar of this track has {Format(rest)} beats; time {meter.BeatsPerBar}/{meter.BeatUnit} has {meter.BeatsPerBar}.");
        }
        return since;
    }

    // ---------------------------------------------------------------- rows of events

    /// <summary>
    /// A bar line closes exactly one bar (the caller has made a bar with nothing in it a whole,
    /// empty one). After a miscount the next bar is counted from here.
    /// </summary>
    private void CloseBar(SyntaxNode line, Ctx ctx, double time, ref double barStart)
    {
        double beats = (time - barStart) / ctx.Scale;
        if (_probing && ctx.Depth == 0 && double.IsNaN(_firstBar))
            _firstBar = beats;
        var meter = MeterAt(ctx, barStart);
        int perBar = meter.BeatsPerBar;
        // The bar a row's upbeat is in is as short as the upbeat.
        bool upbeat = ctx.Depth == 0 && barStart < _upbeatEnd - Epsilon && Math.Abs(time - _upbeatEnd) < Epsilon;
        if (ctx.CheckBars && !upbeat && beats > Epsilon && Math.Abs(beats - perBar) > Epsilon)
        {
            // Say where the tie goes: the first event that starts in the bar and ends after it.
            double barEnd = barStart + perBar * ctx.Scale;
            var across = _barEvents.FirstOrDefault(e => e.T0 < barEnd - Epsilon && e.T1 > barEnd + Epsilon);
            string hint = beats < perBar ? ""
                : across.Word is null ? " An event that runs across a bar line is tied over it, as in kick:1~ | :2"
                : TieHint(across.Word, (barEnd - across.T0) / ctx.Scale, (across.T1 - barEnd) / ctx.Scale, MeterAt(ctx, barEnd).BeatsPerBar);
            string changed = meter == _meters[0] ? "" : $" (the {(_music is null ? "time changes" : "song changes time")} on the way: `shosa check` says where)";
            // A section that counts in another time than the file's says so itself: the one before it does not say it for it.
            var section = _sections.FirstOrDefault(s => s.Value.Span.Start <= line.Span.Start && line.Span.Start < s.Value.Span.End).Key;
            if (_music is null && section is not null && _heads.GetValueOrDefault(section) is { Time: null }
                && _heads.Values.FirstOrDefault(h => h.Time is { } other && Math.Abs(other.Beats - beats) < Epsilon) is { Time: { } meant })
                hint = $" Section '{section}' writes no time, so its bars are the file's {_beatsPerBar}/{_beatUnit}: if it is in {meant.Beats}/{meant.Unit}, begin it with `time {meant.Beats}/{meant.Unit}`.";
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

    /// <summary>Which plays of its section a `verse` is for: 2, 1,3 or 1-2 (the lexer reads -2 as one number).</summary>
    private static HashSet<int> VerseNumbers(SyntaxNode verse)
    {
        var plays = new HashSet<int>();
        int before = 0;
        foreach (var token in Tokens(verse, SyntaxKind.Number))
        {
            int n = (int)Math.Clamp(Number(token), -999, 999);
            for (int k = n < 0 ? before + 1 : n; k <= Math.Abs(n); k++)
                plays.Add(k);
            before = Math.Abs(n);
        }
        return plays;
    }

    /// <summary>
    /// Verses written next to each other share one place in the row: on a play of the section
    /// the ones that name it are done, and the place is as long as the longest of them all, so
    /// that what follows starts at the same beat every time.
    /// </summary>
    private (double Time, double BarStart) Verses(List<SyntaxNode> run, Ctx ctx, double start, double barStart,
        List<(double T0, double T1, Func<double, Ctx, double> Play)> before, double rowStart)
    {
        if (ctx.Depth > 0)
            Error("SH1034", run[0].Token(SyntaxKind.Identifier)!.Span, "A `verse` is written in a section's track: it is the section that is played a first and a second time, not a move.");
        double end = start, bars = barStart;
        foreach (var verse in run)
        {
            var numbers = VerseNumbers(verse);
            if (numbers.Contains(0))
                Error("SH1034", verse.Token(SyntaxKind.Number)!.Span, "Plays are counted from 1: verse 1 is the first time the section is played.");
            if (verse.Child(SyntaxKind.Sequence) is not { } rows)
                continue;
            double t = start, b = barStart;
            // The others are walked too, for their length and for what is wrong in them, and write nothing.
            Walk(rows, numbers.Contains(_verse) ? ctx : ctx with { Clip = null, Sink = [] }, ref t, ref b, [.. before], rowStart);
            if (t > end + Epsilon)
                (end, bars) = (t, b);
        }
        return (end, bars);
    }

    /// <returns>Where the last word of the row begins (what the row was given, if it has none): the word after it sets out no earlier.</returns>
    private double Walk(SyntaxNode parent, Ctx ctx, ref double time, ref double barStart,
        List<(double T0, double T1, Func<double, Ctx, double> Play)>? before = null, double? beganAt = null)
    {
        var items = parent.ChildNodes().ToList();
        // What this row has done so far, so that `%` can do a bar of it again.
        var done = before ?? [];
        double rowStart = beganAt ?? time, written = 0;   // written: the length of the last bar a line closed in this row
        double since = ctx.Since;                         // where the word before the next one begins
        var given = ctx;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            ctx = given with { Since = since };
            switch (item.Kind)
            {
                case SyntaxKind.BarLine:
                    if (time - barStart > Epsilon)
                    {
                        written = time - barStart;
                    }
                    else
                    {
                        // A bar with nothing in it is a bar in which this row says nothing (as in Lily#):
                        // `| |`, the line a row begins with.
                        double empty = !BarsKnown && written > 0 ? written : MeterAt(ctx, time).BeatsPerBar * ctx.Scale;
                        done.Add((time, time + empty, (start, c) => start + MeterAt(c, start).BeatsPerBar * c.Scale));
                        since = time;
                        time += empty;
                    }
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
                        // The first of them comes after the word before the sign, not after what was before it
                        // where it was written (the section before, at a row's first bar).
                        for (int k = 0; k < again.Count; k++)
                        {
                            var entry = again[k];
                            done.Add((entry.T0 + span, entry.Play(entry.T0 + span, k == 0 ? replay with { Since = since, After = true } : replay), entry.Play));
                            since = entry.T0 + span;
                        }
                    }
                    time += span;
                    barStart = time - bar;   // the line after it closes one bar, as after any other
                    break;
                }

                case SyntaxKind.Verse:
                {
                    int lastVerse = i;
                    while (lastVerse + 1 < items.Count && items[lastVerse + 1].Kind == SyntaxKind.Verse)
                        lastVerse++;
                    var run = items.GetRange(i, lastVerse - i + 1);
                    var soFar = done.ToList();
                    double t0 = time, row0 = rowStart, lag = t0 - since;
                    (time, barStart) = Verses(run, ctx, t0, barStart, soFar, rowStart);
                    // Done again by a `%`, it is the verse of the play then, with the bars before it moved along.
                    done.Add((t0, time, (start, c) =>
                        Verses(run, Again(c, start - lag), start, start, [.. soFar.Select(e => (e.T0 + start - t0, e.T1 + start - t0, e.Play))], row0 + start - t0).Time));
                    since = t0;
                    i = lastVerse;
                    break;
                }

                case SyntaxKind.Overlay:
                {
                    double t0 = time, lag = t0 - since;
                    since = Overlay(item, ctx, ref time, ref barStart);
                    done.Add((t0, time, (start, c) => { double t = start, b = start; Overlay(item, Again(c, start - lag), ref t, ref b); return t; }));
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
                    _inner = double.NaN;
                    BindEvent(item, ctx, ref time, ref barStart, extra);
                    double innerSince = _inner;
                    _inner = double.NaN;
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
                    double lag = t0 - since;
                    done.Add((t0, time, (start, c) => { double t = start, b = start; BindEvent(item, Again(c, start - lag), ref t, ref b, extra); return t; }));
                    // After a move, the word before the next one is the move's last word, not all of the move.
                    since = double.IsNaN(innerSince) ? t0 : Math.Max(t0, innerSince);
                    break;
                }
            }
        }
        return since;
    }

    /// <summary>
    /// What an item is bound in when a `%` does it again at another beat: the word before it begins as
    /// long before it as where it was written — but for the first item done again, which is told (After).
    /// </summary>
    private static Ctx Again(Ctx ctx, double since) => ctx.After ? ctx with { After = false } : ctx with { Since = since };

    /// <summary>
    /// Rows that start together. The whole is as long as its longest row; a later row wins.
    /// One row alone is a group: the unit `q` performs again.
    /// </summary>
    /// <returns>Where the last word of its rows begins, the earliest of them: the word after the group sets out no earlier.</returns>
    private double Overlay(SyntaxNode overlay, Ctx ctx, ref double time, ref double barStart)
    {
        double start = time, end = time, bars = barStart, since = double.PositiveInfinity;
        int row = 0, group = _groups++;
        foreach (var sequence in overlay.ChildNodes(SyntaxKind.Sequence))
        {
            double t = start, b = barStart;   // the rows share the track's bars
            since = Math.Min(since, Walk(sequence, ctx with { Priority = [.. ctx.Priority, row], Rows = [.. ctx.Rows ?? [], (group, row)] }, ref t, ref b));
            if (row++ == 0 || t > end + Epsilon)
                (end, bars) = (Math.Max(end, t), b);
        }
        time = end;
        barStart = bars;
        return double.IsPositiveInfinity(since) ? ctx.Since : since;
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
        if (InBars(ev.Child(SyntaxKind.Duration)))
            Error("SH1033", ev.Child(SyntaxKind.Duration)!.Span, "Only `skip` and `then` are counted in bars: skip:3bar, then:2bar. 'q' takes beats: q:8");
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
            Error("SH1009", ev.Child(SyntaxKind.Duration)!.Span, "A group takes time: give it a length, or none (then it is as long as it was written).");
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
        double began = time - length + Wrap(ctx, time - length);
        if (ev.Child(SyntaxKind.Mark)?.Token(SyntaxKind.Identifier) is { } mark && ctx.Clip is not null && began > -Epsilon)
            _marks.Add((new MarkIr(Math.Max(0, began), mark.Text, ctx.Dancer.Id, src.Start, src.Length, _playing, _verse), []));
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

    /// <summary>`:3bar`: a length counted in bars, which only a `skip` or a `then` at the start of a bar has.</summary>
    private static bool InBars(SyntaxNode? duration) =>
        duration is not null && Tokens(duration, SyntaxKind.Identifier).FirstOrDefault()?.Text == "bar";

    /// <summary>The beats of so many bars from a place: each bar is as long as the time it is in.</summary>
    private double BarsFrom(double bars, Ctx? ctx, double time, out double lastBar)
    {
        lastBar = _meters.Count > 0 ? _meters[0].BeatsPerBar : _beatsPerBar;
        if (ctx is null)
            return bars * lastBar;
        double beats = 0;
        for (double left = bars; left > Epsilon; left -= 1)
        {
            lastBar = MeterAt(ctx, time + beats * ctx.Scale).BeatsPerBar;
            beats += Math.Min(1, left) * lastBar;
        }
        return beats;
    }

    private double? WrittenLength(SyntaxNode duration, Ctx? ctx = null, double time = 0)
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
            "bar" => BarsFrom(Math.Min(value, 9999), ctx, time, out _),
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

        double? written = ctx.ForcedLength is null && ev.Child(SyntaxKind.Duration) is { } duration ? WrittenLength(duration, ctx, time) : null;
        double? lastBar = null;   // of a length in bars: how long the last of them is
        if (ctx.ForcedLength is null && ev.Child(SyntaxKind.Duration) is { } inBars && InBars(inBars))
        {
            double count = Number(Tokens(inBars, SyntaxKind.Number)[0]);
            bool rest = words.Count == 1 && arguments is null && words[0].Text is Vocabulary.Skip or Vocabulary.Hold;
            if (!rest)
                Error("SH1033", inBars.Span, "Only `skip` and `then` are counted in bars: skip:3bar, then:2bar. A movement that runs across a bar line is tied over it: kick:1~ | :2");
            else if (Tokens(inBars, SyntaxKind.Number).Count > 1 || count < 1 || Math.Abs(count - Math.Round(count)) > Epsilon)
                Error("SH1033", inBars.Span, "Bars are counted whole: skip:1bar, skip:3bar. A part of a bar is written in beats.");
            else if (Math.Abs(time - barStart) > Epsilon)
                Error("SH1033", inBars.Span, "A length in bars starts at the start of a bar: fill this bar in beats first, then `| skip:3bar |`.");
            BarsFrom(Math.Max(1, Math.Round(count)), ctx, time, out double bar);
            lastBar = bar * ctx.Scale;
        }
        // A word is written at the beat the body is there, and its length is how long it stays: nothing has none.
        if (written == 0)
        {
            Error("SH1009", ev.Child(SyntaxKind.Duration)!.Span, words.Count > 0 && words[0].Text is "skip" or "then"
                ? $"A '{words[0].Text}' of no length does nothing: take it out, or give it a length."
                : "A word takes time: its length is how long the body stays there. The first word of a score is the pose it starts in, with no ':0': guard jab");
            written = 1;
        }
        double length = ctx.ForcedLength ?? ((written ?? (definition is not null ? Natural(words[0].Text) : 1)) + extra) * ctx.Scale;
        double t0 = time, t1 = time + length;
        time = t1;
        if (lastBar is { } closing)
            barStart = t1 - closing;   // the line after it closes one bar, as after `%3`

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

        double wrap = Wrap(ctx, t0);
        if (definition is not null)
        {
            ExpandDefinition(definition, words[0], annotations, ctx with { Mirror = mirror, Free = free, Src = src }, t0, length, ease);
            if (mark is not null && ctx.Clip is not null && t0 + wrap > -Epsilon)
                _marks.Add((new MarkIr(Math.Max(0, t0 + wrap), mark.Text, ctx.Dancer.Id, src.Start, src.Length, _playing, _verse), []));
            return;
        }

        bool isHold = words.Count == 1 && words[0].Text == Vocabulary.Hold;
        if (ctx.Channels.Length > 2 && !isHold)
        {
            Error("SH1019", words[0].Span, _defs.Count == 0 && Vocabulary.Reserved.Contains(words[0].Text)
                ? $"'body' takes poses and moves you have defined, `then` and `skip`. '{words[0].Text}' belongs in the track of its part: arms, steps, torso ..."
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

        // The word's own time, in the clip. What is before beat 0 (an upbeat the score starts with) is
        // over the end of a loop, where it takes the part from the last play as it would from the one
        // before it; in any other score it is before anything is seen, and is not there.
        var pieces = new List<(double T0, double T1, bool Wrapped)>();
        if (ctx.Clip is null)
            pieces.Add((t0, t1, false));
        else if (wrap > 0)
        {
            pieces.Add((t0 + wrap, Math.Min(t1, 0) + wrap, true));
            if (t1 > Epsilon)
                pieces.Add((0, t1, false));
        }
        else if (t1 > Epsilon)
            pieces.Add((Math.Max(0, t0), t1, false));

        var raws = new List<(string Channel, Raw Raw)>();
        foreach (string channel in ctx.Channels)
        {
            var seg = isHold ? new Seg { Kind = SegKinds.Hold }
                : channel == Channels.Support ? feet
                : channel == Channels.Height ? height
                : channel is Channels.Twist or Channels.Hips ? BindTwist(channel, words, arguments, annotations, mirror)
                : BindGesture(channel, words, arguments, annotations, mirror, ctx);
            if (seg is null)
                continue;
            seg.Src = src.Start;
            seg.Len = src.Length;
            seg.Free = free;
            seg.Ease = ease ?? "smooth";

            if (!ctx.Sink.TryGetValue(channel, out var list))
                ctx.Sink[channel] = list = [];
            int order = _order++;
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = seg.Clone();
                (piece.T0, piece.T1) = (pieces[i].T0, pieces[i].T1);
                double moved = pieces[i].Wrapped ? wrap : 0;
                list.Add(new Raw(piece, pieces[i].Wrapped ? [int.MaxValue, .. ctx.Priority.Skip(1)] : ctx.Priority, order, t0 + moved, ctx.Since + moved, ctx.Stance, ctx.Rows));
                if (i == 0)
                    raws.Add((channel, list[^1]));
            }
        }
        // The moment a mark names is the beat its word is written at: the hand is there, the foot lands, the jump comes down.
        if (mark is not null && ctx.Clip is not null && t0 + wrap > -Epsilon)
            _marks.Add((new MarkIr(Math.Max(0, t0 + wrap), mark.Text, ctx.Dancer.Id, src.Start, src.Length, _playing, _verse), raws));
    }

    /// <summary>
    /// An upbeat of the first section is before the first beat of the score. In a loop it is over
    /// the end, which is what is played before the start: the beats to move such an event by.
    /// Otherwise there is nothing before it: what is all before beat 0 is not seen, and the score
    /// starts in whatever word has the part at beat 0.
    /// </summary>
    private static double Wrap(Ctx ctx, double t0) =>
        ctx.Clip is { Loop: true, TotalBeats: > 0 } clip && t0 < -Epsilon ? clip.TotalBeats : 0;

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

        double natural = isPose ? 1 : Natural(name.Text);
        double lastWord = double.PositiveInfinity;   // where the last word of its rows begins, the earliest of them
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
                lastWord = Math.Min(lastWord, Walk(track, inner with { Scale = length / natural }, ref time, ref bar));
            else if (track.ChildNodes(SyntaxKind.Event).FirstOrDefault() is { } only)
                BindEvent(only, inner with { ForcedLength = length, EaseOverride = ease, Stance = true }, ref time, ref bar);
        }
        if (!any)
            Error("SH1015", name.Span, $"'{name.Text}' has nothing for the track '{ctx.TrackName}'.");
        _expanding.Remove(name.Text);
        _inner = double.IsInfinity(lastWord) ? double.NaN : lastWord;
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

    /// <summary>
    /// SH2009. Two rows of an `&amp;` that both twist at the same time. Any part two rows write goes
    /// to the later row, and for an arm that is what was meant: one move over another. The chest
    /// is the one part two moves that look apart — a punch of each arm — both need, so the
    /// twist of one is lost without a word of the source saying so. A pose's own twist is where
    /// the chest is kept, not something done: a move that comes back to it, or a row that only
    /// stands in it, loses nothing.
    /// </summary>
    private void CheckTwistsAtOnce(Dictionary<string, List<Raw>> sink)
    {
        foreach (string channel in new[] { Channels.Twist, Channels.Hips })
        {
            if (sink.TryGetValue(channel, out var raws))
                CheckTurnsAtOnce(raws, channel == Channels.Hips ? "turn the hips" : "twist the chest", channel == Channels.Hips ? "the hips go" : "the chest goes");
        }
    }

    private void CheckTurnsAtOnce(List<Raw> raws, string both, string goes)
    {
        foreach (var lost in raws.Where(r => r is { Stance: false, Rows: not null }))
        {
            var over = raws.FirstOrDefault(o => o is { Stance: false, Rows: not null } && o.Order != lost.Order && Beats(o, lost)
                && Math.Min(o.Seg.T1, lost.Seg.T1) - Math.Max(o.Seg.T0, lost.Seg.T0) > Epsilon
                && o.Rows.Any(a => lost.Rows.Any(b => a.Group == b.Group && a.Row != b.Row)));
            if (over is not null)
                Report("SH2009", DiagnosticSeverity.Warning, new TextSpan(lost.Seg.Src, lost.Seg.Len),
                    $"This row and a later row of the `&` both {both} at the same time, and {goes} with the later one: what is written here is not seen. Write it in one of the rows only, or put the two one after the other.");
        }
    }

    /// <summary>Whether an event still has its part at the moment it arrives (the same rules as <see cref="Resolve"/>).</summary>
    private static bool Arrives(Raw raw, double beat, List<Raw> all)
    {
        double at = beat + 1e-4;   // just as its time begins
        return !all.Any(o => o.Order != raw.Order && o.Seg.T0 < at && at < o.Seg.T1 && Beats(o, raw));
    }

    /// <summary>
    /// One channel's words from every track that wrote it, to one row of segments.
    ///
    /// First, who has the part when: each word has it for its own time, and where two want it the
    /// narrower and the later wins. That makes the row a part would have if it were written alone:
    /// a run for each stretch one word has it. A word that gets the part back after a narrower
    /// one let go of it is a run of its own.
    ///
    /// Then each run is a segment: the part ARRIVES when the run begins, and makes its way there
    /// in the time of what is before it in the row (how much of it: <see cref="StepSeconds"/>).
    /// Where nothing had the part before it (a `skip`), that gap is what is before it. The first
    /// word of a score is the pose it starts in; in a loop the way to it is at the end.
    ///
    /// It never sets out before the word before it in its own row begins, so a part that nothing
    /// has written for a long time does not drift for all of that time.
    ///
    /// A step is taken or it is not: a word of the feet that only gets them back half-way through
    /// its time moves nothing. The height is a part of its own and comes back like a gesture, so a
    /// row of `both.low` beside a pose or a walk takes the height and leaves the feet where they are.
    /// </summary>
    private static List<Seg> Resolve(string channel, List<Raw> raws, MotionIr clip, double origin)
    {
        bool feet = channel == Channels.Support, steps = feet || channel == Channels.Height;
        bool loop = clip.Loop && clip.TotalBeats > 0;
        double total = clip.TotalBeats;
        var tempos = TempoMap.Of(clip.Tempos, clip.Tempo);

        var spans = raws.Where(r => r.Seg.T1 - r.Seg.T0 > Epsilon).ToList();
        var bounds = spans.SelectMany(r => new[] { r.Seg.T0, r.Seg.T1 }).OrderBy(t => t).ToList();
        for (int i = bounds.Count - 1; i > 0; i--)
        {
            if (bounds[i] - bounds[i - 1] <= Epsilon)
                bounds.RemoveAt(i);
        }

        var runs = new List<(Raw Raw, double Start, double End)>();
        for (int i = 0; i + 1 < bounds.Count; i++)
        {
            double middle = (bounds[i] + bounds[i + 1]) / 2;
            Raw? winner = null;
            foreach (var raw in spans)
            {
                if (raw.Seg.T0 < middle && middle < raw.Seg.T1 && (winner is null || Beats(raw, winner)))
                    winner = raw;
            }
            if (winner is null)
                continue;
            if (runs.Count > 0 && runs[^1].Raw.Order == winner.Order && Math.Abs(runs[^1].End - bounds[i]) <= Epsilon)
                runs[^1] = (runs[^1].Raw, runs[^1].Start, bounds[i + 1]);
            else
                runs.Add((winner, bounds[i], bounds[i + 1]));
        }

        var result = new List<Seg>();
        for (int i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            // What is before it in the row: in a loop, before the first is the last.
            (Raw Raw, double Start, double End)? before = i > 0 ? runs[i - 1]
                : loop ? (runs[^1].Raw, runs[^1].Start - total, runs[^1].End - total)
                : null;
            bool joined = before is { } b && b.End >= run.Start - Epsilon;
            // The word a loop wraps over its end goes on at the start: it is there already.
            if (joined && runs.Count > 1 && before!.Value.Raw.Order == run.Raw.Order)
                continue;
            bool starts = before is null && run.Start <= origin + Epsilon;   // the pose the score starts in
            if (feet && !starts && Math.Abs(run.Start - run.Raw.Start) > Epsilon)
                continue;

            var seg = run.Raw.Seg.Clone();
            (seg.At, seg.Until) = (run.Start, run.End);
            if (starts)
            {
                (seg.T0, seg.T1) = (0, 0);
                result.Add(seg);
                continue;
            }

            // No earlier than the word before it in its own row begins (a word after a long silence of its part).
            double floor = before is null ? 0 : joined ? before.Value.Start : before.Value.End;
            if (Math.Abs(run.Start - run.Raw.Start) <= Epsilon && run.Raw.Since < run.Start - Epsilon)
                floor = Math.Max(floor, run.Raw.Since);
            double room = run.Start - floor;
            double perSecond = TempoMap.BpmAt(tempos, Math.Max(0, (loop && run.Start <= Epsilon ? total : run.Start) - 1e-6)) / 60;
            double lead = seg.Kind == SegKinds.Hold ? 0
                : seg.Ease == "sharp" ? Math.Min(steps ? room / 2 : room, SharpSeconds * perSecond)
                : steps ? Math.Min(room / 2, StepSeconds * perSecond)
                : room;
            double t0 = run.Start - lead, t1 = run.Start;
            if (seg.Kind == SegKinds.Jump)
            {
                // It comes down on its beat: the bend and the air are before it (85% of a jump), the give after it.
                // A jump needs its time — the time in the air is how high it is — so it has what there is of the
                // word before, less what a jump before it needs to come down in, and of its own time as much after.
                double before0 = Math.Min(room, JumpSeconds * perSecond) * JumpBefore;
                double after = Math.Min(run.End - run.Start, JumpSeconds * perSecond) * (1 - JumpBefore);
                (t0, t1) = (run.Start - before0, run.Start + after);
                seg.Land = before0 / (before0 + after);
            }
            if (loop && t0 < -Epsilon)
                (t0, t1) = (t0 + total, t1 + total);
            (seg.T0, seg.T1) = (t0, t1);
            result.Add(seg);
        }
        return result.OrderBy(s => s.T0).ThenBy(s => s.T1).ToList();
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

    /// <summary>`@small` and `@large` on a knee bend: half of `low` (8 cm), and a deep crouch (40 cm). A stride is scaled by 0.6 and 1.4.</summary>
    private static double BendScale(double scale) => scale < 1 ? 0.5 : scale > 1 ? 2.5 : 1;

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

    /// <summary>
    /// A word of the `twist` track: `left`, `right.20`, `center`. How far the chest is turned on the
    /// hips, about the spine; the hips do not come along, so it is no more than <see cref="Vocabulary.MaxTwist"/> degrees.
    /// </summary>
    /// <remarks>
    /// The `hips` track says the same of the hips over the feet: the feet stay where they stand, so
    /// that too is no more than 45 degrees.
    /// </remarks>
    private Seg? BindTwist(string channel, List<SyntaxToken> words, SyntaxNode? arguments, List<SyntaxNode> annotations, bool mirror)
    {
        bool hips = channel == Channels.Hips;
        string what = hips ? "a turn of the hips" : "a twist";
        string Form = hips
            ? "The hips turn `left`, `right` (30 degrees), `left.N` / `right.N` for N degrees up to 45, or `center`: hips { right.20 left center }"
            : "A twist is `left`, `right` (30 degrees), `left.N` / `right.N` for N degrees up to 45, or `center`: twist { right.20 left center }";
        foreach (var annotation in annotations)
        {
            var name = annotation.Token(SyntaxKind.Identifier)!;
            string? belongs = name.Text switch
            {
                "small" or "large" or "turn" => "a step",
                "bent" => "an arm or a leg",
                "body" => "an arm",
                "round" => "the torso",
                _ => null,
            };
            if (belongs is not null)
                Error("SH1010", name.Span, $"'@{name.Text}' is for {belongs}. How far {what} goes is its number: left.20");
        }
        if (arguments is not null)
            Error("SH1012", arguments.Span, $"There are no brackets in {what}: how far it goes is the number after the dot, left.20");

        if (words.Count == 1 && words[0].Text == "center")
            return new Seg { Kind = SegKinds.Twist, TurnDeg = 0 };
        if (words.Count is 1 or 2 && words[0].Text is "left" or "right")
        {
            double degrees = Vocabulary.DefaultTwist;
            if (words.Count == 2)
            {
                if (words[1].Kind != SyntaxKind.Number)
                {
                    Error("SH1012", words[1].Span, Form);
                    return null;
                }
                degrees = Number(words[1]);
                if (degrees is < 0 or > Vocabulary.MaxTwist)
                {
                    Error("SH1013", words[1].Span, hips
                        ? "The hips turn up to 45 degrees over feet that stay where they stand (the chest can twist 45 more on them: twist { left.45 }). To face another way, the feet turn: steps { turn.left.quarter }"
                        : "The chest turns up to 45 degrees on the hips (the hips can turn 45 more over the feet: hips { left.45 }). To face another way, the feet turn: steps { turn.left.quarter }");
                    return null;
                }
            }
            return new Seg { Kind = SegKinds.Twist, TurnDeg = (words[0].Text == "left") != mirror ? degrees : -degrees };
        }

        var known = Vocabulary.TwistWords.Select(w => w.Name).Append(Vocabulary.Hold).Append(Vocabulary.Skip).Concat(_defs.Keys);
        Error("SH1012", words[0].Span, $"'{words[0].Text}' is not {what}. {Form}{Hint(words[0].Text, known)}");
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
            if (name.Text == "round" && channel != Channels.Torso)
                Error("SH1010", name.Span, "'@round' is for the torso: torso { fwd.80@round }");
        }
        // Brackets after the second of two directions: how far round from the first toward it.
        const string Between = "Brackets say how many degrees a direction is from the first word toward the second: fwd.left(30) is 30 degrees to the left of straight ahead, an arm's fwd.across(15) a little across (0 to 90; without brackets it is 45).";
        if (arguments is not null && (words.Any(w => w.Kind == SyntaxKind.Identifier && _figures.ContainsKey(w.Text))
            || isArm && Vocabulary.Has(Vocabulary.Anchors, string.Join('.', words.Select(w => w.Text)))))
        {
            Error("SH1012", arguments.Span, "A place takes no number. " + Between);
            return null;
        }

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
        var flats = new List<(SyntaxToken Word, double X, double Z)>();   // the horizontal directions, as written
        foreach (var word in words)
        {
            (double beforeX, double beforeZ) = (x, z);
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
            if (x != beforeX || z != beforeZ)
                flats.Add((word, x - beforeX, z - beforeZ));
        }

        if (arguments is not null)
        {
            var degrees = Tokens(arguments, SyntaxKind.Number);
            int bracketed = words.Where(w => w.Span.End <= arguments.Span.Start).Select(w => w.Span.Start).DefaultIfEmpty(-1).Last();
            bool between = flats.Count == 2 && bracketed == flats[1].Word.Span.Start &&Math.Abs(flats[0].X * flats[1].X + flats[0].Z * flats[1].Z) < 1e-9;
            if (!between || degrees.Count != 1 || arguments.ChildNodes().Any() || Number(degrees[0]) is < 0 or > 90)
            {
                Error("SH1013", arguments.Span, Between);
                return null;
            }
            double round = Number(degrees[0]) * Math.PI / 180;
            (x, z) = (flats[0].X * Math.Cos(round) + flats[1].X * Math.Sin(round), flats[0].Z * Math.Cos(round) + flats[1].Z * Math.Sin(round));
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
            Round = channel == Channels.Torso && flags.Contains("round"),
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
                case "round":
                    Error("SH1010", name.Span, "'@round' is for the torso: torso { fwd.80@round }");
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
                    seg.Level = level < 0 ? level * BendScale(scale) : level;
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
                        case "left" or "right": seg.Dx = (Side(word.Text) == "left" ? 0.4 : -0.4) * scale; break;
                        case "apart": seg.Spread = 0.6 * scale; break;
                        case "together": seg.Spread = Rig.Stance; break;
                        default:
                            Error("SH1012", word.Span, $"A jump goes place, fwd, back, left, right, apart or together.{Hint(word.Text, ["place", "fwd", "back", "left", "right", "apart", "together"])}");
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
                return new Seg { Kind = SegKinds.Level, Level = level < 0 ? level * BendScale(scale) : level };
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
                        case "left" or "right": seg.Dx = (Side(word.Text) == "left" ? 0.4 : -0.4) * scale; break;
                        default:
                            Error("SH1012", word.Span, $"A {head.Text} goes place, fwd, back, left or right.{Hint(word.Text, ["place", "fwd", "back", "left", "right"])}");
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
                    // The pose a score starts in (`:0`) is not gone to: there is no way round to write.
                    if (seg.T1 - seg.T0 > Epsilon && previous is not null && previous[0] * dir[0] + previous[1] * dir[1] + previous[2] * dir[2] < -0.98)
                    {
                        Report("SH2001", DiagnosticSeverity.Warning, new TextSpan(seg.Src, seg.Len),
                            $"This target is the exact opposite of where the part is ({channel}), so the way round is not written. Add a target in between: one that is neither end (fwd or side between down and up; up or down between fwd and back).");
                    }
                    previous = dir;
                }
            }
        }
    }
}
