using System.Globalization;
using Shosa.Core.Motion;
using Shosa.Core.Syntax;

namespace Shosa.Core.Semantics;

/// <summary>
/// Syntax tree to <see cref="MotionIr"/>: resolves names, gives every word its meaning,
/// lays the form out on one time line, and reports what does not make sense (SH1xxx).
/// It reads the red tree and never throws on a broken one: an event it cannot understand
/// still takes its time, so one bad word does not shift everything after it.
/// </summary>
public static class Binder
{
    public static (MotionIr Ir, List<Diagnostic> Diagnostics) Bind(SyntaxTree tree) => new BinderRun(tree).Run();
}

internal sealed class BinderRun(SyntaxTree tree)
{
    private const double Epsilon = 1e-9;

    private readonly MotionIr _ir = new();
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly HashSet<(string, int, int)> _reported = [];
    private readonly Dictionary<string, FigureIr> _figures = [];
    private readonly Dictionary<string, SyntaxNode> _phrases = [];

    private void Report(string code, DiagnosticSeverity severity, TextSpan span, string message)
    {
        // A phrase played twice is bound twice; say each thing once.
        if (_reported.Add((code, span.Start, span.Length)))
            _diagnostics.Add(new Diagnostic(code, severity, span, message));
    }

    private void Error(string code, TextSpan span, string message) => Report(code, DiagnosticSeverity.Error, span, message);

    private static double Number(SyntaxToken token) => double.Parse(token.Text, CultureInfo.InvariantCulture);

    private static List<SyntaxToken> Tokens(SyntaxNode node, SyntaxKind kind) =>
        node.ChildTokens().Where(t => t.Kind == kind && !t.IsMissing).ToList();

    private static string Hint(string name, IEnumerable<string> candidates) =>
        Vocabulary.Nearest(name, candidates) is { } nearest ? $" Did you mean '{nearest}'?" : "";

    public (MotionIr, List<Diagnostic>) Run()
    {
        SyntaxNode? form = null;
        var phraseOrder = new List<string>();

        foreach (var item in tree.Root.ChildNodes())
        {
            switch (item.Kind)
            {
                case SyntaxKind.Header:
                    BindHeader(item);
                    break;
                case SyntaxKind.FigureDecl:
                    BindFigure(item);
                    break;
                case SyntaxKind.PhraseDecl:
                    if (item.Token(SyntaxKind.Identifier, 1) is { } phraseName)
                    {
                        if (_phrases.TryAdd(phraseName.Text, item))
                            phraseOrder.Add(phraseName.Text);
                        else
                            Error("SH1005", phraseName.Span, $"A phrase named '{phraseName.Text}' is already written above.");
                    }
                    break;
                case SyntaxKind.FormDecl:
                    if (form is null)
                        form = item;
                    else
                        Error("SH1005", item.Span, "There is already a form above; a file has one.");
                    break;
            }
        }

        if (_figures.Count == 0 && _phrases.Count > 0)
            Error("SH1000", new TextSpan(0, 0), "No figure is declared. Add one: figure a { rig humanoid }");

        var plays = new List<SyntaxNode>();
        if (form is not null)
        {
            foreach (var entry in form.ChildNodes(SyntaxKind.FormItem))
            {
                var name = entry.Token(SyntaxKind.Identifier);
                if (name is null)
                    continue;
                if (!_phrases.TryGetValue(name.Text, out var phrase))
                {
                    Error("SH1005", name.Span, $"No phrase is named '{name.Text}'.{Hint(name.Text, _phrases.Keys)}");
                    continue;
                }
                int count = entry.Token(SyntaxKind.Number) is { } times ? (int)Math.Clamp(Number(times), 1, 999) : 1;
                for (int i = 0; i < count; i++)
                    plays.Add(phrase);
            }
        }
        else
        {
            plays.AddRange(phraseOrder.Select(name => _phrases[name]));
        }

        double cursor = 0;
        foreach (var phrase in plays)
            BindPhrase(phrase, ref cursor);
        _ir.TotalBeats = cursor;

        foreach (var figure in _ir.Figures)
            CheckOppositeTargets(figure);

        return (_ir, _diagnostics);
    }

    private void BindHeader(SyntaxNode header)
    {
        var keyword = header.ChildTokens().First();
        var numbers = Tokens(header, SyntaxKind.Number);
        switch (keyword.Text)
        {
            case "title":
                if (header.Token(SyntaxKind.String) is { } title)
                    _ir.Title = title.Text.Trim('"');
                break;
            case "tempo":
                if (numbers.Count == 1)
                {
                    double tempo = Number(numbers[0]);
                    if (tempo is < 10 or > 400)
                        Error("SH1001", numbers[0].Span, "The tempo is in beats per minute, from 10 to 400.");
                    else
                        _ir.Tempo = tempo;
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
                        _ir.BeatsPerBar = (int)beats;
                        _ir.BeatUnit = (int)unit;
                    }
                }
                break;
        }
    }

    private void BindFigure(SyntaxNode node)
    {
        var id = node.Token(SyntaxKind.Identifier, 1);
        if (id is null)
            return;

        var figure = new FigureIr { Id = id.Text, Name = node.Token(SyntaxKind.String)?.Text.Trim('"') ?? id.Text };
        if (!_figures.TryAdd(id.Text, figure))
        {
            Error("SH1003", id.Span, $"A figure named '{id.Text}' is already declared above.");
            return;
        }
        _ir.Figures.Add(figure);

        foreach (var property in node.ChildNodes(SyntaxKind.Property))
        {
            var tokens = property.ChildTokens().Where(t => !t.IsMissing).ToList();
            var name = tokens[0];
            var values = tokens.Skip(1).ToList();
            switch (name.Text)
            {
                case "rig":
                    if (values.Count != 1 || values[0].Text != "humanoid")
                        Error("SH1006", property.Span, "The only rig so far is: rig humanoid");
                    else
                        figure.Rig = values[0].Text;
                    break;

                case "at":
                    if (values.Count == 2 && values.All(v => v.Kind == SyntaxKind.Number))
                    {
                        figure.X = Number(values[0]);
                        figure.Z = Number(values[1]);
                    }
                    else
                    {
                        Error("SH1006", property.Span, "A position is two numbers in metres: at 1.5 0 (x to stage left, z toward the audience).");
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
                                _ => (double?)null,
                            }
                        : null;
                    if (degrees is { } facing)
                        figure.FacingDeg = facing;
                    else
                        Error("SH1006", property.Span, "facing takes front, back, stageLeft, stageRight, or degrees (positive turns to the figure's left).");
                    break;

                default:
                    Error("SH1006", name.Span, $"A figure has no property '{name.Text}'.{Hint(name.Text, ["rig", "at", "facing"])}");
                    break;
            }
        }
    }

    private static string[]? ChannelsOf(string trackName) => trackName switch
    {
        "steps" => [Channels.Support],
        "arms" => [Channels.LeftArm, Channels.RightArm],
        Channels.LeftArm or Channels.RightArm or Channels.LeftLeg or Channels.RightLeg or Channels.Torso or Channels.Head => [trackName],
        _ => null,
    };

    private void BindPhrase(SyntaxNode phrase, ref double cursor)
    {
        double length = 0;
        foreach (var block in phrase.ChildNodes(SyntaxKind.FigureBlock))
        {
            var figureName = block.Token(SyntaxKind.Identifier);
            if (figureName is null)
                continue;
            if (!_figures.TryGetValue(figureName.Text, out var figure))
            {
                string hint = ChannelsOf(figureName.Text) is not null
                    ? $" A track goes inside a figure's block: a {{ {figureName.Text} {{ ... }} }}"
                    : Hint(figureName.Text, _figures.Keys);
                Error("SH1003", figureName.Span, $"No figure is named '{figureName.Text}'.{hint}");
                continue;
            }

            var written = new HashSet<string>();
            foreach (var track in block.ChildNodes(SyntaxKind.Track))
            {
                var trackName = track.Token(SyntaxKind.Identifier);
                if (trackName is null)
                    continue;
                var channels = ChannelsOf(trackName.Text);
                if (channels is null)
                {
                    Error("SH1004", trackName.Span,
                        $"There is no track '{trackName.Text}'.{Hint(trackName.Text, Vocabulary.Tracks.Select(w => w.Name))}");
                    continue;
                }
                foreach (var channel in channels)
                {
                    if (!written.Add(channel))
                        Error("SH1008", trackName.Span, $"'{trackName.Text}' writes a part of the body that another track of this phrase already writes.");
                }

                double time = cursor, barStart = cursor;
                WalkItems(track, figure, channels, ref time, ref barStart);

                double rest = time - barStart;
                if (track.ChildNodes(SyntaxKind.BarLine).Any() && rest > Epsilon && Math.Abs(rest - _ir.BeatsPerBar) > Epsilon)
                {
                    Report("SH1002", DiagnosticSeverity.Warning, trackName.Span,
                        $"The last bar of this track has {Format(rest)} beats; time {_ir.BeatsPerBar}/{_ir.BeatUnit} has {_ir.BeatsPerBar}.");
                }
                length = Math.Max(length, time - cursor);
            }
        }

        var name = phrase.Token(SyntaxKind.Identifier, 1);
        _ir.Phrases.Add(new PhraseIr(name?.Text ?? "", cursor, cursor + length, phrase.Span.Start, phrase.Span.Length));
        cursor += length;
    }

    private static string Format(double value) => Math.Round(value, 3).ToString(CultureInfo.InvariantCulture);

    private void WalkItems(SyntaxNode parent, FigureIr figure, string[] channels, ref double time, ref double barStart)
    {
        foreach (var item in parent.ChildNodes())
        {
            switch (item.Kind)
            {
                case SyntaxKind.BarLine:
                    double beats = time - barStart;
                    if (Math.Abs(beats - _ir.BeatsPerBar) > Epsilon)
                    {
                        Error("SH1002", item.Span,
                            $"The bar that ends here has {Format(beats)} beats; time {_ir.BeatsPerBar}/{_ir.BeatUnit} has {_ir.BeatsPerBar}.");
                    }
                    barStart = time;
                    break;

                case SyntaxKind.Repeat:
                    int count = item.Token(SyntaxKind.Number) is { } times ? (int)Math.Clamp(Number(times), 1, 999) : 1;
                    for (int i = 0; i < count; i++)
                        WalkItems(item, figure, channels, ref time, ref barStart);
                    break;

                case SyntaxKind.Event:
                    BindEvent(item, figure, channels, ref time);
                    break;
            }
        }
    }

    private void BindEvent(SyntaxNode ev, FigureIr figure, string[] channels, ref double time)
    {
        double beats = 1;
        if (ev.Child(SyntaxKind.Duration) is { } duration)
        {
            var numbers = Tokens(duration, SyntaxKind.Number);
            if (numbers.Count >= 1)
                beats = Number(numbers[0]);
            if (numbers.Count >= 2)
                beats = Number(numbers[1]) != 0 ? beats / Number(numbers[1]) : 0;
            if (beats <= 0)
            {
                Error("SH1009", duration.Span, "A duration is a positive number of beats: :2, :1/2.");
                beats = 1;
            }
        }

        double t0 = time, t1 = time + beats;
        time = t1;

        var target = ev.Child(SyntaxKind.Target);
        if (target is null || target.ChildTokens().Any(t => t.IsMissing))
            return;   // the parser has already said what is missing
        var words = Tokens(target, SyntaxKind.Identifier);

        var annotations = new List<SyntaxToken>();
        foreach (var annotation in ev.ChildNodes(SyntaxKind.Annotation))
        {
            if (annotation.Token(SyntaxKind.Identifier) is not { } name)
                continue;
            if (Vocabulary.Has(Vocabulary.Annotations, name.Text))
                annotations.Add(name);
            else
                Error("SH1010", name.Span, $"There is no '@{name.Text}'.{Hint(name.Text, Vocabulary.Annotations.Select(w => w.Name))}");
        }

        foreach (var channel in channels)
        {
            var seg = channel == Channels.Support ? BindSupport(words, annotations) : BindGesture(channel, words, annotations);
            if (seg is null)
                continue;
            seg.T0 = t0;
            seg.T1 = t1;
            seg.Src = ev.Span.Start;
            seg.Len = ev.Span.Length;
            foreach (var annotation in annotations)
            {
                if (annotation.Text is "smooth" or "sharp" or "linear")
                    seg.Ease = annotation.Text;
            }
            if (!figure.Tracks.TryGetValue(channel, out var list))
                figure.Tracks[channel] = list = [];
            list.Add(seg);
        }
    }

    private Seg? BindGesture(string channel, List<SyntaxToken> words, List<SyntaxToken> annotations)
    {
        int side = channel is Channels.LeftArm or Channels.LeftLeg ? 1 : channel is Channels.RightArm or Channels.RightLeg ? -1 : 0;
        bool isArm = channel is Channels.LeftArm or Channels.RightArm;
        bool isLimb = side != 0;

        foreach (var annotation in annotations)
        {
            if (annotation.Text is "small" or "large")
                Error("SH1010", annotation.Span, $"'@{annotation.Text}' is for a step.");
            if (annotation.Text == "bent" && !isLimb)
                Error("SH1010", annotation.Span, "'@bent' is for an arm or a leg.");
            if (annotation.Text == "body" && !isArm)
                Error("SH1010", annotation.Span, "'@body' is for an arm.");
        }

        if (words.Count == 1 && words[0].Text == Vocabulary.Hold)
            return new Seg { Kind = SegKinds.Hold };

        if (isArm && words.Count == 1 && Vocabulary.Has(Vocabulary.Anchors, words[0].Text))
            return new Seg { Kind = SegKinds.Anchor, Anchor = words[0].Text };

        double x = 0, z = 0;
        string? level = null, pole = null;
        foreach (var word in words)
        {
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
                    x += word.Text == "left" ? 1 : -1;
                    break;
                case "low" or "mid" or "high":
                    if (level is not null)
                    {
                        Error("SH1013", word.Span, "A target has one level: low, mid or high.");
                        return null;
                    }
                    level = word.Text;
                    break;
                case "up" or "down":
                    pole = word.Text;
                    break;
                default:
                    var known = Vocabulary.Directions.Concat(Vocabulary.Levels).Concat(Vocabulary.Poles).Select(w => w.Name).Append(Vocabulary.Hold);
                    if (isArm)
                        known = known.Concat(Vocabulary.Anchors.Select(w => w.Name));
                    Error("SH1012", word.Span, $"'{word.Text}' is not a direction or a level.{Hint(word.Text, known)}");
                    return null;
            }
        }

        Vec3 direction;
        if (pole is not null)
        {
            if (words.Count > 1)
            {
                Error("SH1013", words[0].Span, $"'{pole}' stands alone; a slanted direction is written with a level: fwd.high, side.low.");
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
            double elevation = level switch { "high" => Math.PI / 4, "low" => -Math.PI / 4, _ => 0 };
            var flat = new Vec3(x, 0, z).Normalized();
            direction = flat * Math.Cos(elevation) + Vec3.Up * Math.Sin(elevation);
        }

        return new Seg
        {
            Kind = SegKinds.Dir,
            Dir = [direction.X, direction.Y, direction.Z],
            Ext = annotations.Any(a => a.Text == "bent") ? 0.72 : 1,
            Body = isArm && annotations.Any(a => a.Text == "body"),
        };
    }

    private Seg? BindSupport(List<SyntaxToken> words, List<SyntaxToken> annotations)
    {
        foreach (var annotation in annotations)
        {
            if (annotation.Text == "bent")
                Error("SH1010", annotation.Span, "'@bent' is for an arm or a leg; bent knees under the weight are a level: both.low, left.fwd.low.");
            if (annotation.Text == "body")
                Error("SH1010", annotation.Span, "'@body' is for an arm.");
        }

        var head = words[0];
        var rest = words.Skip(1).ToList();
        double scale = annotations.Any(a => a.Text == "small") ? 0.6 : annotations.Any(a => a.Text == "large") ? 1.4 : 1;

        switch (head.Text)
        {
            case Vocabulary.Hold when rest.Count == 0:
                return new Seg { Kind = SegKinds.Hold };

            case "together":
            {
                // Like a step, a close arrives at a height, and unwritten means normal.
                var seg = new Seg { Kind = SegKinds.Together, Level = 0 };
                foreach (var word in rest)
                {
                    switch (word.Text)
                    {
                        case "low": seg.Level = -1; break;
                        case "mid": seg.Level = 0; break;
                        case "high": seg.Level = 1; break;
                        default:
                            Error("SH1012", word.Span, "A close takes only a level: together, together.low, together.high.");
                            return null;
                    }
                }
                return seg;
            }

            case "turn":
                if (rest.Count == 2 && rest[0].Text is "left" or "right" && !double.IsNaN(Vocabulary.TurnDegrees(rest[1].Text)))
                {
                    double degrees = Vocabulary.TurnDegrees(rest[1].Text);
                    return new Seg { Kind = SegKinds.Turn, TurnDeg = rest[0].Text == "left" ? degrees : -degrees };
                }
                Error("SH1014", head.Span, "A turn is written with its side and its amount: turn.left.quarter, turn.right.half (eighth, quarter, half, full).");
                return null;

            case "jump":
            {
                var seg = new Seg { Kind = SegKinds.Jump };
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
                    switch (word.Text)
                    {
                        case "place": break;
                        case "low": level = -1; break;
                        case "mid": level = 0; break;
                        case "high": level = 1; break;
                        default:
                            Error("SH1012", word.Span, "Both feet stay where they are and change height: both.low, both.mid, both.high. To travel, step with one foot or jump.");
                            return null;
                    }
                }
                if (level is null)
                {
                    Error("SH1013", head.Span, "A level is needed: both.low, both.mid, both.high.");
                    return null;
                }
                return new Seg { Kind = SegKinds.Level, Level = level };
            }

            case "left" or "right":
            {
                // A step always arrives at a height: unwritten is normal height, so the step
                // after one on the toes comes down again without being told to.
                var seg = new Seg { Kind = SegKinds.Step, Foot = head.Text, Dx = Rig.Stance, Level = 0 };
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
                            Error("SH1012", word.Span,
                                $"'{word.Text}' is not a step direction or a level.{Hint(word.Text, Vocabulary.StepDirections.Concat(Vocabulary.Levels).Select(w => w.Name))}");
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

        var heads = Vocabulary.Feet.Concat(Vocabulary.SupportActions).Select(w => w.Name).Append(Vocabulary.Hold);
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
    private void CheckOppositeTargets(FigureIr figure)
    {
        foreach (var (channel, segs) in figure.Tracks)
        {
            if (channel == Channels.Support)
                continue;
            double[]? previous = channel switch
            {
                Channels.Torso => [0, 1, 0],
                Channels.Head => [0, 0, 1],
                _ => [0, -1, 0],
            };
            foreach (var seg in segs)
            {
                if (seg.Kind == SegKinds.Anchor)
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
