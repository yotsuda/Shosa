using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

namespace Shosa.Core.Motion;

/// <summary>
/// <see cref="MotionIr"/> to <see cref="MotionClip"/>: the part that corresponds to engraving.
/// The source says WHAT each part does; this decides how the body gets there — where a
/// stepping foot lands and how high it lifts, how the weight travels between the feet, how
/// far the pelvis must sink for both legs to reach, where elbows and knees go.
///
/// A deliberately plain first version: closed-form curves and two-bone IK, no physics and no
/// learned model. It answers "can the vocabulary be solved into watchable movement at all",
/// and it reports what it had to force (SH2xxx), which is what an author — a person or an AI —
/// needs in order to fix the source.
/// </summary>
public static class Solver
{
    private const double MaxHandSpeed = 10;   // m/s; a fast punch
    private const double MaxFootSpeed = 12;   // m/s; a snap kick

    /// <summary>
    /// Whether unwritten rows are given the small movements of a living body (DESIGN.md 94). On for every
    /// use; the tests of the other rules turn it off for the process, to measure poses exactly, and the tests
    /// of this one turn it back on for themselves with <see cref="Life(bool)"/>.
    /// </summary>
    public static bool LifeByDefault { get; set; } = true;
    private static readonly AsyncLocal<bool?> LifeHere = new();
    internal static bool LifeOn => LifeHere.Value ?? LifeByDefault;

    /// <summary>Life on or off for what this flow of execution solves, until the result is disposed.</summary>
    public static IDisposable Life(bool on)
    {
        var before = LifeHere.Value;
        LifeHere.Value = on;
        return new Restore(() => LifeHere.Value = before);
    }

    private sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
    public static MotionClip Solve(MotionIr ir, List<Diagnostic> diagnostics)
    {
        int fps = ir.Fps;
        var clip = new MotionClip
        {
            Name = ir.Name,
            Title = ir.Title,
            Fps = fps,
            Tempo = ir.Tempo,
            BeatsPerBar = ir.BeatsPerBar,
            Tempos = [.. TempoMap.Of(ir.Tempos, ir.Tempo)],
            Meters = [.. Bars.Of(ir.Meters, ir.BeatsPerBar, ir.BeatUnit)],
            TotalBeats = ir.TotalBeats,
            Loop = ir.Loop,
            Inplace = ir.Inplace,
            Sections = ir.Sections,
            Music = ir.Music,
        };
        foreach (var mark in ir.Marks)
            clip.Marks.Add(new ClipMark(mark.Beat, Math.Round(clip.SecondsAt(mark.Beat), 4), mark.Name, mark.Dancer, mark.Section, mark.Play));

        // Frames are even in seconds; where the tempo changes, the beats between two of them change.
        double totalSeconds = clip.SecondsAt(ir.TotalBeats);
        int frames = (int)Math.Ceiling(totalSeconds * fps - 1e-9) + 1;
        var beatOfFrame = new double[frames];
        for (int f = 0; f < frames; f++)
            beatOfFrame[f] = clip.BeatOfFrame(f);
        // The solver's own small step of time, for smoothing: a frame at the first tempo.
        double beatsPerFrame = ir.Tempo / (60.0 * fps);

        var solvers = new Dictionary<string, FigureSolver>();
        foreach (var dancer in ir.Dancers)
            solvers[dancer.Id] = new FigureSolver(dancer, ir, beatsPerFrame, id => solvers.GetValueOrDefault(id));
        foreach (var dancer in ir.Dancers)
            solvers[dancer.Id].Build();

        // Head, chest and pelvis of every dancer at every frame, where they really are (before `inPlace`).
        var bodies = new Dictionary<string, List<Body>>();
        var unreached = new HashSet<int>();

        foreach (var dancer in ir.Dancers)
        {
            var solver = solvers[dancer.Id];
            var clipFigure = new ClipFigure { Id = dancer.Id, Name = dancer.Name, Model = dancer.Model, ModelHeight = dancer.ModelHeight };
            // The mouth sings with the song, on its clock: frames are even in seconds.
            if (dancer.Sings is { } sung)
                clipFigure.Mouth = Mouth.Frames(sung, Enumerable.Range(0, frames).Select(f => (double)f / fps).ToList(), clip.SecondsAt);
            // A mic in a hand (DESIGN.md 117): how far it is up at the mouth, for the preview to draw it.
            if (dancer.Mic is { } micHand)
                clipFigure.Mic = new ClipMic(micHand, beatOfFrame.Select(b => (float)Math.Round(solver.MicUp(b), 3)).ToList());
            foreach (var (seg, channel) in solver.MicHidden())
                diagnostics.Add(new Diagnostic("SH2018", DiagnosticSeverity.Warning, new TextSpan(seg.Src, seg.Len),
                    $"The mic is at the mouth here ({dancer.Id} is singing), and the hand that holds it wins: this word of the {channel} row is not seen. Move it to where {dancer.Id} does not sing, or give it to the other arm{(dancer.Mic == "both" ? " (with mic both, both hands are on the mic while singing; mic right frees the left)" : "")}."));
            if (solver.Juggling is { } juggle)
                JuggleProblems(dancer, juggle, clip, diagnostics, solver);
            var flagged = new HashSet<(Seg, string)>();
            Vec3[]? previous = null;
            double previousFacing = 0;
            var body = bodies[dancer.Id] = [];

            // A hand sent to a place on another dancer that is farther away than the arm is long.
            foreach (var (channel, side) in new[] { (Channels.LeftArm, 1), (Channels.RightArm, -1) })
            {
                if (!dancer.Tracks.TryGetValue(channel, out var arm))
                    continue;
                foreach (var seg in arm)
                {
                    // Taken over before it arrives: it was never going to get there.
                    if (seg.Kind != SegKinds.Reach || arm.Any(s => s.T0 > seg.T0 + 1e-9 && s.T0 < seg.T1 - 1e-9))
                        continue;
                    double shortfall = solver.ReachShortfall(seg, side);
                    if (shortfall > 0.03 && unreached.Add(seg.Src))
                        diagnostics.Add(new Diagnostic("SH2006", DiagnosticSeverity.Warning, new TextSpan(seg.Src, seg.Len), FormattableString.Invariant(
                            $"The hand does not get there: {seg.Target}'s {seg.Part} is about {shortfall:0.00} m farther than the arm reaches. Start the two closer together (`at`), step toward it first, or lean (`torso`).")));
                }
            }

            if (solver.LoopProblem is { } problem && ir.Sections.Count > 0)
                diagnostics.Add(new Diagnostic("SH2005", DiagnosticSeverity.Warning, new TextSpan(ir.Sections[0].Src, "section".Length), problem));
            // What the feet were asked to do that the way they stood does not allow (a hop from both feet).
            foreach (var (seg, code, message) in solver.SupportProblems)
                if (flagged.Add((seg, code)))
                    diagnostics.Add(new Diagnostic(code, code == "SH2016" ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error, new TextSpan(seg.Src, seg.Len), message));
            // A foot the steps put down and its leg's row holds up: the leg row wins, and the foot stays in the air.
            foreach (var (seg, leg, at) in solver.FeetHeldUp())
                if (flagged.Add((seg, "SH2013")))
                    diagnostics.Add(new Diagnostic("SH2013", DiagnosticSeverity.Warning, new TextSpan(seg.Src, seg.Len), FormattableString.Invariant(
                        $"This puts the {(leg == Channels.LeftLeg ? "left" : "right")} foot on the floor at beat {at:0.##}, but the {leg} row still holds that leg up, and a leg's own row wins: the foot stays in the air. Put the leg down in its row by then (`down`, beside the other foot), or let it go to the steps (`skip`, landing where they put it){(seg.Kind == SegKinds.Jump ? ": the leg a jump lands on, shaped in the air, needs it" : "")}.")));

            // `inPlace`: the even part of the travel is taken out and handed to the engine as a velocity.
            clipFigure.Facing = [solver.FacingAt(0), solver.FacingAt(ir.TotalBeats)];
            var travel = ir.Inplace && ir.TotalBeats > 0 ? solver.Travel : default;
            if (ir.Inplace && ir.TotalBeats > 0)
            {
                double seconds = totalSeconds;
                clipFigure.Velocity = [Math.Round(travel.X / seconds, 4), Math.Round(travel.Z / seconds, 4)];
            }

            void Flag(string channel, double t, string code, DiagnosticSeverity severity, string message, bool evenIfFree = false)
            {
                // A knee bend is written in the same row as the steps: what the feet cannot do may be its doing.
                var seg = SegAt(dancer, channel, t) ?? (channel == Channels.Support ? SegAt(dancer, Channels.Height, t) : null);
                // A sharp word goes in the same seconds at any tempo: more time is not what helps it.
                if (code == "SH2003" && seg is { Ease: "sharp" })
                    message = message.Replace("Give it more time, or lower the tempo.",
                        "A @sharp word goes in 0.15 s at any tempo: write a target nearer to it before it, or take the @sharp off.");
                if (seg is not null && (evenIfFree || !seg.Free) && flagged.Add((seg, code)))
                    diagnostics.Add(new Diagnostic(code, severity, new TextSpan(seg.Src, seg.Len), message));
            }

            for (int f = 0; f < frames; f++)
            {
                double t = beatOfFrame[f];
                var pose = solver.Pose(t, out var problems);
                body.Add(new Body(pose[Rig.Head], pose[Rig.Chest], pose[Rig.Waist], pose[Rig.Pelvis],
                    [pose[Rig.LHip], pose[Rig.LKnee], pose[Rig.LAnkle]], [pose[Rig.RHip], pose[Rig.RKnee], pose[Rig.RAnkle]]));
                if (ir.Inplace && ir.TotalBeats > 0)
                {
                    var back = travel * (t / ir.TotalBeats);
                    for (int j = 0; j < pose.Length; j++)
                        pose[j] -= back;
                }

                var data = new float[pose.Length * 3];
                for (int j = 0; j < pose.Length; j++)
                {
                    data[j * 3] = (float)Math.Round(pose[j].X, 4);
                    data[j * 3 + 1] = (float)Math.Round(pose[j].Y, 4);
                    data[j * 3 + 2] = (float)Math.Round(pose[j].Z, 4);
                }
                clipFigure.Frames.Add(data);
                clipFigure.Yaws.Add(solver.FacingAt(t));

                var rotations = Skeleton.Rotations(pose);
                var turns = new float[rotations.Length * 4];
                for (int b = 0; b < rotations.Length; b++)
                {
                    turns[b * 4] = (float)rotations[b].X;
                    turns[b * 4 + 1] = (float)rotations[b].Y;
                    turns[b * 4 + 2] = (float)rotations[b].Z;
                    turns[b * 4 + 3] = (float)rotations[b].W;
                }
                clipFigure.Rotations.Add(turns);

                if (problems.Overreach)
                    Flag(Channels.Support, t, "SH2002", DiagnosticSeverity.Warning,
                        "The foot is put farther away than the leg is long. Make the step smaller, or bend the knees (a low level).", evenIfFree: true);
                if (problems.HandShort is { } arm)
                    Flag(arm, t, "SH2015", DiagnosticSeverity.Warning,
                        "The hand stays where it was in the room (`stay`), and the arm does not reach it here: the body has moved too far from it. Lean or sink less while it stays, or let the hand go (a word after `stay`).");
                if (problems.BothFeetOff is { } leg)
                    Flag(leg, t, "SH2004", DiagnosticSeverity.Error,
                        "This leg leaves the floor while the other one is not carrying the weight. Step onto the other foot first.");
                if (problems.ShouldersShort is { } shoulders)
                {
                    static string Side(double degrees) => FormattableString.Invariant($"{Math.Abs(degrees):0.#}° {(degrees >= 0 ? "left" : "right")}");
                    // Without a word of their own the shoulders are square to the feet: it is the hips that turned them away.
                    string row = SegAt(dancer, Channels.Shoulders, t) is null ? Channels.Hips : Channels.Shoulders;
                    // Said of where the word arrives (on the way the numbers are between two words), if it is short there too.
                    if (SegAt(dancer, row, t) is { } word && solver.ShouldersShortAt(word.T1) is { } arrived)
                        shoulders = arrived;
                    bool leaning = shoulders.Lean >= 5;
                    string lean = leaning ? FormattableString.Invariant($", and over a lean of {shoulders.Lean:0}° a turn of the chest shows less from above") : "";
                    string wanted = Math.Abs(shoulders.Wanted) < 0.05 ? "square to the feet" : $"{Side(shoulders.Wanted)} of the feet";
                    string stand = leaning ? ", stand up more" : "";
                    // Bent nearly level, a turn about the spine hardly shows from above (at level, not at
                    // all): what turns the shoulders there is the way the bend points (writing trial 18).
                    string way = shoulders.Wanted >= 0 ? "left" : "right";
                    string help = row == Channels.Shoulders && shoulders.Lean >= 75
                        ? FormattableString.Invariant($"Bent this far, a turn about the spine hardly shows from above: bend toward that side instead (`torso {{ fwd.{way}({Math.Abs(shoulders.Wanted):0.#}) }}`), the hips further that way, or stand up more.")
                        : row == Channels.Shoulders
                        ? $"Turn the hips further that way{stand}, or write what they reach."
                        : $"With no `shoulders` row they stay square to the feet over these hips: turn them with the hips (`shoulders {{ {(shoulders.Reached >= 0 ? "left" : "right")}(20) }}`){stand}, or turn the hips less.";
                    Flag(row, t, "SH2010", DiagnosticSeverity.Warning, FormattableString.Invariant(
                        $"The shoulders {(Math.Abs(shoulders.Reached) < 0.05 ? "stay square to the feet" : $"get to {Side(shoulders.Reached)} of the feet")} here, not {wanted}: the chest turns at most {Vocabulary.MaxChestTurn:0}° on the hips{lean}. {help}"));
                }

                if (previous is not null)
                {
                    Speed(Rig.LHand, Channels.LeftArm, MaxHandSpeed, "hand");
                    Speed(Rig.RHand, Channels.RightArm, MaxHandSpeed, "hand");
                    // A foot moves because of a leg gesture if one is going on, else because of a step.
                    Speed(Rig.LAnkle, SegAt(dancer, Channels.LeftLeg, t) is null ? Channels.Support : Channels.LeftLeg, MaxFootSpeed, "foot");
                    Speed(Rig.RAnkle, SegAt(dancer, Channels.RightLeg, t) is null ? Channels.Support : Channels.RightLeg, MaxFootSpeed, "foot");
                }
                previous = pose;
                previousFacing = solver.FacingAt(t);

                void Speed(int joint, string channel, double limit, string what)
                {
                    // A hand is judged against the body it hangs on: an arm held out through a turn or
                    // a jump is not a fast arm. (The feet, which make the turn, are judged against the floor.)
                    double speed = what == "hand"
                        ? (InBody(pose, joint, solver.FacingAt(t)) - InBody(previous!, joint, previousFacing)).Length * fps
                        : (pose[joint] - previous![joint]).Length * fps;
                    if (speed > limit)
                        Flag(channel, t, "SH2003", DiagnosticSeverity.Warning,
                            $"Too fast to perform: the {what} would move at about {speed:0} m/s. Give it more time, or lower the tempo.");
                }
            }

            // The balls (DESIGN.md 118): drawn from the hands as they are, at the throw and at the catch.
            if (solver.Juggling is { } juggling)
                clipFigure.Juggle = JuggleFrames(juggling, clipFigure.Frames, fps);
            clip.Figures.Add(clipFigure);
            foreach (var (channel, segs) in dancer.Tracks)
            {
                foreach (var seg in segs)
                {
                    // When its word is the one being performed: from the beat it is written at, for its length
                    // (an IR written by hand may say only when the part moves).
                    var (from, until) = seg.Until > seg.At ? (seg.At, seg.Until) : (seg.T0, seg.T1);
                    clip.Spans.Add(new ClipSpan(from, until, seg.Src, seg.Len, dancer.Id, channel));
                }
            }
        }

        Collisions(ir, bodies, beatOfFrame, diagnostics);
        return clip;
    }

    /// <summary>A throw higher than this over the hand is a warning (SH2020): a 7 at a lively tempo is 2.5 m.</summary>
    private const double HighestThrow = 3.0;
    /// <summary>Seconds from one throw to the next below which the hands cannot keep up (SH2020).</summary>
    private const double QuickestBeat = 0.12;
    /// <summary>Metres a ring has to rise over the hands not to meet the others in the air (SH2020): they are 32 cm across.</summary>
    private const double LowestRing = 0.6;
    /// <summary>A juggling ring across, metres: a flat band 3.4 cm wide and 3.5 mm thick (the user).</summary>
    private const double RingAcross = 0.324;
    /// <summary>Metres a club has to rise over the hands to turn once clear of the others (SH2020): it is half a metre long.</summary>
    private const double LowestClub = 0.45;

    /// <summary>A juggle word's `every`, times a factor, as it is written: 1/2, 1, 3/2.</summary>
    private static string Every(Seg seg, double times)
    {
        double beats = (seg.Every ?? 0.5) * times;
        foreach (int d in new[] { 1, 2, 3, 4, 8 })
            if (Math.Abs(beats * d - Math.Round(beats * d)) < 1e-6)
                return d == 1 ? $"{Math.Round(beats)}" : $"{Math.Round(beats * d)}/{d}";
        return beats.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>What the juggle row asks that cannot be done or can hardly be, and the arm words it hides.</summary>
    /// <summary>Juggling.ClawCarry, as the warning says it.</summary>
    private const string ClawCarryText = "0.12";

    private static void JuggleProblems(DancerIr dancer, Juggling juggling, MotionClip clip, List<Diagnostic> diagnostics, FigureSolver solver)
    {
        var said = new HashSet<(int, string)>();
        void Say(Seg seg, string code, DiagnosticSeverity severity, string message)
        {
            if (said.Add((seg.Src, code)))
                diagnostics.Add(new Diagnostic(code, severity, new TextSpan(seg.Src, seg.Len), message));
        }
        foreach (var problem in juggling.Problems)
            Say(problem.Seg, problem.Code, problem.IsError ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning, problem.Message);

        var segs = dancer.Tracks[Channels.Juggle];
        foreach (var seg in segs)
        {
            double from = clip.SecondsAt(seg.T0), to = clip.SecondsAt(seg.T1);
            double beat = clip.SecondsAt(seg.T0 + (seg.Every ?? 0.5)) - from;
            if (beat < QuickestBeat)
                Say(seg, "SH2020", DiagnosticSeverity.Warning, FormattableString.Invariant(
                    $"A throw every {beat:0.00} s: the hands cannot keep up (each one throws and catches every {2 * beat:0.00} s; quick jugglers take 0.25 s or more). Throw less often (every 1/2 rather than 1/4), or lower the tempo."));
            var high = juggling.Rises().Where(r => r.Thrown >= from - 1e-6 && r.Thrown < to - 1e-6).Select(r => r.Rise).DefaultIfEmpty(0).Max();
            // Rings are 32.4 cm across: thrown lower than this they meet in the air and fall (the user). Higher is longer in the
            // air, which is a throw less often: the beats decide, not the hands.
            var low = juggling.Flights.Where(fl => fl.Value >= 3 && fl.Thrown >= from - 1e-6 && fl.Thrown < to - 1e-6)
                .Select(fl => 9.81 * Math.Pow(fl.Caught - fl.Thrown, 2) / 8).DefaultIfEmpty(double.PositiveInfinity).Min();
            if (juggling.Prop == "clubs" && low < LowestClub)
                Say(seg, "SH2020", DiagnosticSeverity.Warning, FormattableString.Invariant(
                    $"Clubs thrown {low:0.00} m high meet in the air: they are half a metre long and turn once on the way, and want {LowestClub:0.00} m or more. A throw goes higher for longer in the air, which is less often: every {Every(seg, 1.5)} rather than every {Every(seg, 1)}, or a slower tempo."));
            if (juggling.Prop == "rings" && low < LowestRing)
                Say(seg, "SH2020", DiagnosticSeverity.Warning, FormattableString.Invariant(
                    $"Rings thrown {low:0.00} m high meet in the air and fall: they are {RingAcross:0.000} m across, and want {LowestRing:0.0} m or more. A throw goes higher for longer in the air, which is less often: every {Every(seg, 2)} rather than every {Every(seg, 1)}, or a slower tempo."));
            if (high > HighestThrow)
                Say(seg, "SH2020", DiagnosticSeverity.Warning, FormattableString.Invariant(
                    $"The highest throw here goes {high:0.0} m above the hand: few can throw that high and catch it. Throw more often (every 1/4), raise the tempo, or use lower numbers."));
        }

        // Tricks the body has to make room for (DESIGN.md 118.12): under the leg, the leg lifted; behind the back, over the shoulder.
        // How high a ball thrown or caught behind the back has to rise to go past the shoulder, metres (SH2022).
        const double BackClear = 0.55;
        foreach (var flight in juggling.Flights.Where(f => f.Trick is not null && f.Seg is not null))
        {
            double beat = TempoMap.Beat(clip.Tempos, flight.Thrown);
            var pose = solver.Pose(beat, out _);
            string side = flight.From == 0 ? "right" : "left";
            if (flight.Trick == "underThrow")
            {
                var hip = pose[flight.From == 0 ? Rig.RHip : Rig.LHip];
                var knee = pose[flight.From == 0 ? Rig.RKnee : Rig.LKnee];
                if (knee.Y < hip.Y - 0.2)
                    Say(flight.Seg!, "SH2022", DiagnosticSeverity.Warning, FormattableString.Invariant(
                        $"The {side} hand throws from under the {side} leg at beat {beat:0.##}, and the leg is not lifted there: lift it in its row by then (`{side}Leg {{ fwd.bent }}`, the thigh level, the knee at the height of the hip), or the hand goes through the thigh."));
            }
            else if (flight.Trick == "backThrow")
            {
                double rise = 9.81 * Math.Pow(flight.Caught - flight.Thrown, 2) / 8;
                if (rise < BackClear)
                    Say(flight.Seg!, "SH2022", DiagnosticSeverity.Warning, FormattableString.Invariant(
                        $"Thrown from behind the back, it rises {rise:0.00} m: it has to clear the other shoulder, some {BackClear:0.00} m above the hand there. Throw a higher number (a 5), or less often."));
            }
        }
        // Caught behind the back (DESIGN.md 118.39): the ball comes down over the shoulder, so it has to go up past it.
        foreach (var flight in juggling.Flights.Where(f => f.BackCatch is not null))
        {
            double rise = 9.81 * Math.Pow(flight.Caught - flight.Thrown, 2) / 8;
            if (rise < BackClear)
                Say(flight.BackCatch!, "SH2022", DiagnosticSeverity.Warning, FormattableString.Invariant(
                    $"Caught behind the back at beat {TempoMap.Beat(clip.Tempos, flight.Caught):0.##}, it rises {rise:0.00} m: it has to come down over the shoulder, some {BackClear:0.00} m above the hand behind the back. Throw it higher (a 5), or less often."));
        }

        // A penguin's balls go up above the shoulders (the user): thrown in front of the belly and caught low behind the hip,
        // a ball has to be long in the air for it, which is a throw less often. Said once at each penguin word, of the balls
        // thrown in it (one thrown into it from a word before is thrown at that word's pace).
        foreach (var flight in juggling.PenguinFlights)
        {
            double caughtBeat = TempoMap.Beat(clip.Tempos, flight.Caught);
            if (flight.Seg is not { } word || word.Tricks?.Contains("penguinCatch") != true)
                continue;
            int hand = flight.From == 0 ? Rig.RHand : Rig.LHand, to = flight.To == 0 ? Rig.RHand : Rig.LHand;
            var atThrow = solver.Pose(TempoMap.Beat(clip.Tempos, flight.Thrown), out _);
            var atCatch = solver.Pose(caughtBeat, out _);
            double air = flight.Caught - flight.Thrown, y0 = atThrow[hand].Y, y1 = atCatch[to].Y;
            double up = (y1 - y0) / air + 9.81 * air / 2;
            double top = y0 + (up > 0 ? up * up / (2 * 9.81) : 0);
            double shoulders = Math.Max(atThrow[Rig.LShoulder].Y, atThrow[Rig.RShoulder].Y);
            if (top < shoulders)
                Say(word, "SH2020", DiagnosticSeverity.Warning, FormattableString.Invariant(
                    $"A penguin's ball goes up to {top:0.00} m here, below the shoulders ({shoulders:0.00} m): caught low behind the hip and thrown in front, it wants to go above them, and so to be longer in the air, which is a throw less often: every {Every(word, 2)} rather than every {Every(word, 1)}, or a slower tempo."));
        }

        // A claw catch too slow to be on the ball's way up (the user: snatched at the face before its top; at a quicker tempo).
        foreach (var (seg, air, most, reach, slowest, quickest, tooQuick) in juggling.ClawTiming)
        {
            double bpm = 60 / Math.Max(1e-6, clip.SecondsAt(seg.T0 + 1) - clip.SecondsAt(seg.T0));
            double low = Math.Ceiling(bpm * slowest), high = Math.Floor(bpm * quickest);
            string range = low <= high
                ? FormattableString.Invariant($"It suits a tempo of about {low:0} to {high:0} (it is {bpm:0}).")
                : "No tempo suits it with these throws.";
            string what = tooQuick
                ? FormattableString.Invariant($"Clawed at the face, the catching hand has not the time to come up there ({reach:0.00} s after its own throw) and bring the ball down again ({ClawCarryText} s) before it throws it: it is hurried.")
                : FormattableString.Invariant($"Clawed at the face near its top, a ball has to get there within {most:0.00} s of the throw; here the catching hand is up at the face only {air:0.00} s after it (its own throw comes first, then {reach:0.00} s to come up), so the ball has fallen well past its top.");
            Say(seg, "SH2023", DiagnosticSeverity.Warning, what + " " + range);
        }

        // An arm's word that arrives while the juggling has the arms is not seen.
        foreach (var channel in new[] { Channels.LeftArm, Channels.RightArm })
        {
            if (!dancer.Tracks.TryGetValue(channel, out var arm))
                continue;
            foreach (var seg in arm.Where(s => s.Kind is not (SegKinds.Hold or SegKinds.Stay)))
            {
                if (juggling.Busy.Any(b => seg.At > b.From + 1e-6 && seg.At < b.To - 1e-6))
                    Say(seg, "SH2021", DiagnosticSeverity.Warning,
                        $"{dancer.Id} is juggling here, and the juggle row has both arms: this word of the {channel} row is not seen. Move it to where the juggle row has no pattern (`skip` there), or juggle later.");
            }
        }
    }

    /// <summary>
    /// The balls at each frame, for the preview: where each one is (x y z), the hand it left and the one it
    /// goes to (0 right, 1 left; the same while held), how far through its flight (0..1), and a club's turn.
    /// </summary>
    private static ClipJuggle JuggleFrames(Juggling juggling, List<float[]> frames, int fps)
    {
        Vec3 Joint(int frame, int joint)
        {
            var f = frames[Math.Clamp(frame, 0, frames.Count - 1)];
            return new Vec3(f[joint * 3], f[joint * 3 + 1], f[joint * 3 + 2]);
        }
        // A ball lies in the hand toward the fingers, so they can close on it: past the wrist along the forearm, and on top
        // of the hand (the user: at 5 cm it was too near the body to be held). A ring is gripped in the fist, nearer.
        double along = juggling.Prop == "rings" ? 0.05 : 0.085;
        Vec3 Palm(int hand, double sec)
        {
            double at = sec * fps;
            int f = (int)Math.Floor(at);
            double u = at - f;
            int wrist = hand == 0 ? Rig.RHand : Rig.LHand, elbow = wrist - 1;
            var w = Vec3.Lerp(Joint(f, wrist), Joint(f + 1, wrist), u);
            var e = Vec3.Lerp(Joint(f, elbow), Joint(f + 1, elbow), u);
            // A claw holds it under the palm, turned down (DESIGN.md 118.12). A penguin's palm is up again, the wrist bent in
            // and the fingers out (118.18): as the others', so the preview, which puts the ball on the character's own palm
            // and adds only how far this is from that, does not push it behind the hand.
            var style = juggling.StyleAt(hand, sec);
            // Turned out (takeouts' swipe), the ball is beside the palm, which the preview finds on the hand itself.
            double up = style == Juggling.Style.Claw ? -0.05 : style == Juggling.Style.Outward ? 0 : 0.04;
            return w + (w - e).Normalized() * along + Vec3.Up * up;
        }
        int Stacked(int ball, double sec)
        {
            int hand = juggling.HandOf(ball, sec), below = 0;
            for (int b = 0; b < ball; b++)
                if (juggling.HandOf(b, sec) == hand)
                    below++;
            return below;
        }

        var result = new ClipJuggle(juggling.Prop, juggling.Balls, [], [],
            [.. juggling.Flights.Select(fl => new[] { fl.Ball, fl.From, fl.To, (float)Math.Round(fl.Thrown, 4), (float)Math.Round(fl.Caught, 4), fl.Mills ? 1f : 0f, fl.MillsThird })], [], [], [], [], [], []);
        for (int f = 0; f < frames.Count; f++)
        {
            double sec = (double)f / fps;
            var data = new float[juggling.Balls * 7];
            for (int b = 0; b < juggling.Balls; b++)
            {
                var (at, from, to, u, spin) = juggling.Ball(b, sec, Palm, Stacked);
                data[b * 7] = (float)Math.Round(at.X, 4);
                data[b * 7 + 1] = (float)Math.Round(at.Y, 4);
                data[b * 7 + 2] = (float)Math.Round(at.Z, 4);
                data[b * 7 + 3] = from;
                data[b * 7 + 4] = to;
                data[b * 7 + 5] = (float)Math.Round(u, 3);
                data[b * 7 + 6] = (float)Math.Round(spin, 3);
            }
            result.Frames.Add(data);
            result.Weight.Add((float)Math.Round(juggling.Weight(sec), 3));
            result.Styles.Add([(int)juggling.StyleAt(0, sec), (int)juggling.StyleAt(1, sec)]);
            result.Mills.Add((float)Math.Round(juggling.MillsAt(sec) * juggling.Weight(sec), 3));
            result.Follow!.Add((float)Math.Round(juggling.TakeoutAt(sec) * juggling.Weight(sec), 3));
            result.Plane!.Add([(float)Math.Round(juggling.RoundPlane(0, sec) * juggling.Weight(sec), 3), (float)Math.Round(juggling.RoundPlane(1, sec) * juggling.Weight(sec), 3)]);
            // How much the windmill's top arm has its forearm drawn in, [right, left]: the preview, which solves a
            // character's arms again in Mills' Mess and the windmill, lets its hand in to the trunk as much (DESIGN.md 118.33).
            result.Pull!.Add([(float)Math.Round(juggling.WindmillPull(0, sec) * juggling.Weight(sec), 3), (float)Math.Round(juggling.WindmillPull(1, sec) * juggling.Weight(sec), 3)]);
            // How much the windmill's hand from under the arm goes round its wheel, its elbow held, [right, left]: the preview
            // keeps a character's elbow where the figure's is, scaled, as much (DESIGN.md 118.34).
            float HeldWheel(int hand) => juggling.Wheel(hand, sec) is { Held: true } w ? (float)Math.Round(w.Weight * juggling.Weight(sec), 3) : 0f;
            result.Wheel!.Add([HeldWheel(0), HeldWheel(1)]);
        }
        return result;
    }

    private const double HeadRadius = 0.10, TrunkRadius = 0.11, LegRadius = 0.05, StandingAnkle = 0.15;

    /// <summary>Where a dancer's head, trunk and legs (hip, knee, ankle) really are at a frame.</summary>
    private readonly record struct Body(Vec3 Head, Vec3 Chest, Vec3 Waist, Vec3 Pelvis, Vec3[] Left, Vec3[] Right);

    /// <summary>From a line segment to a dancer's spine, which is two: pelvis to waist, waist to chest.</summary>
    private static double TrunkGap(Vec3 a0, Vec3 a1, Body body) =>
        Math.Min(Between(a0, a1, body.Pelvis, body.Waist), Between(a0, a1, body.Waist, body.Chest));

    /// <summary>How far the legs two dancers stand on are from each other (less than nothing: one is through the other).</summary>
    private static double LegGap(Body p, Body q)
    {
        double gap = double.PositiveInfinity;
        foreach (var mine in new[] { p.Left, p.Right })
        {
            foreach (var theirs in new[] { q.Left, q.Right })
            {
                if (mine[2].Y > StandingAnkle || theirs[2].Y > StandingAnkle)
                    continue;
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                        gap = Math.Min(gap, Between(mine[i], mine[i + 1], theirs[j], theirs[j + 1]) - 2 * LegRadius);
                }
            }
        }
        return gap;
    }

    /// <summary>
    /// SH2007. Two dancers in the same place: a head or a trunk inside another's, or a leg one
    /// stands on through a leg the other stands on (two who bend their knees too near each
    /// other: the 10th writing trial). Arms and a leg in the air are left out — hands are meant
    /// to meet, and a kick that lands is the point of it. Said once for each pair, at the first moment.
    /// </summary>
    private static void Collisions(MotionIr ir, Dictionary<string, List<Body>> bodies, double[] beatOfFrame, List<Diagnostic> diagnostics)
    {
        for (int i = 0; i < ir.Dancers.Count; i++)
        {
            for (int j = i + 1; j < ir.Dancers.Count; j++)
            {
                DancerIr a = ir.Dancers[i], b = ir.Dancers[j];
                var framesA = bodies[a.Id];
                var framesB = bodies[b.Id];
                for (int f = 0; f < framesA.Count; f++)
                {
                    var (p, q) = (framesA[f], framesB[f]);
                    (double Gap, string What)[] pairs =
                    [
                        ((p.Head - q.Head).Length - 2 * HeadRadius, "heads"),
                        (TrunkGap(p.Head, p.Head, q) - HeadRadius - TrunkRadius, $"{a.Id}'s head and {b.Id}'s body"),
                        (TrunkGap(q.Head, q.Head, p) - HeadRadius - TrunkRadius, $"{b.Id}'s head and {a.Id}'s body"),
                        (Math.Min(TrunkGap(p.Pelvis, p.Waist, q), TrunkGap(p.Waist, p.Chest, q)) - 2 * TrunkRadius, "bodies"),
                        (LegGap(p, q), "legs"),
                    ];
                    var worst = pairs.MinBy(x => x.Gap);
                    if (worst.Gap >= 0)
                        continue;

                    double t = beatOfFrame[f];
                    var span = new[] { Channels.Torso, Channels.Support, Channels.Height }
                        .SelectMany(channel => new[] { SegAt(a, channel, t), SegAt(b, channel, t) })
                        .Where(seg => seg is not null && seg.Kind != SegKinds.Hold)
                        .Select(seg => new TextSpan(seg!.Src, seg.Len))
                        .FirstOrDefault(ir.Sections.LastOrDefault(s => s.T0 <= t) is { } section ? new TextSpan(section.Src, "section".Length) : default);
                    string what = worst.What is "heads" or "bodies" or "legs" ?$"the {worst.What} of {a.Id} and {b.Id}" : worst.What;
                    diagnostics.Add(new Diagnostic("SH2007", DiagnosticSeverity.Warning, span, FormattableString.Invariant(
                        $"Two dancers are in the same place: at beat {t:0.##} {what} run into each other. Start them farther apart (`at`), or make the movement smaller.")));
                    break;
                }
            }
        }
    }

    /// <summary>The shortest distance between two line segments (either may be a point).</summary>
    private static double Between(Vec3 a0, Vec3 a1, Vec3 b0, Vec3 b1)
    {
        Vec3 u = a1 - a0, v = b1 - b0, w = a0 - b0;
        double uu = u.Dot(u), uv = u.Dot(v), vv = v.Dot(v), uw = u.Dot(w), vw = v.Dot(w);
        double det = uu * vv - uv * uv;
        double s = det > 1e-12 ? Math.Clamp((uv * vw - vv * uw) / det, 0, 1) : 0;
        double r = vv > 1e-12 ? Math.Clamp((uv * s + vw) / vv, 0, 1) : 0;
        s = uu > 1e-12 ? Math.Clamp((uv * r - uw) / uu, 0, 1) : 0;
        return (w + u * s - v * r).Length;
    }

    /// <summary>
    /// A joint as seen from the pelvis by a dancer facing <paramref name="yaw"/>: to its left, up, and forward.
    /// The facing is the feet's, not the line of the hips: a direction word of an arm is measured from the
    /// feet, so a hand held out stays where it is in the room while the hips whip round under it (`hips`).
    /// </summary>
    private static Vec3 InBody(Vec3[] pose, int joint, double yaw)
    {
        var d = pose[joint] - pose[Rig.Pelvis];
        return new Vec3(d.Dot(Vec3.Left(yaw)), d.Y, d.Dot(Vec3.Forward(yaw)));
    }

    private static Seg? SegAt(DancerIr dancer, string channel, double t)
    {
        if (!dancer.Tracks.TryGetValue(channel, out var segs))
            return null;
        return segs.LastOrDefault(s => s.Kind != SegKinds.Hold && s.T0 < t && t <= s.T1 + 1e-9)
            ?? segs.LastOrDefault(s => s.T0 < t && t <= s.T1 + 1e-9);
    }
}

internal struct PoseProblems
{
    public bool Overreach;
    /// <summary>The channel of a leg gesture made while the other foot is not planted.</summary>
    public string? BothFeetOff;
    /// <summary>The shoulders do not get where their row says: degrees from the feet reached and wanted, and the lean.</summary>
    public (double Reached, double Wanted, double Lean)? ShouldersShort;
    /// <summary>The channel of an arm whose hand `stay` keeps in the room out of its reach.</summary>
    public string? HandShort;
}

/// <summary>Shape-preserving cubic through keys: no overshoot, and at rest wherever a value repeats.</summary>
internal sealed class Curve
{
    private readonly List<double> _t = [];
    private readonly List<double> _v = [];
    private double[]? _slopes;

    public void Add(double t, double v)
    {
        if (_t.Count > 0 && t <= _t[^1] + 1e-9)
        {
            _v[^1] = v;
        }
        else
        {
            _t.Add(t);
            _v.Add(v);
        }
        _slopes = null;
    }

    public double Eval(double t)
    {
        int n = _t.Count;
        if (n == 0)
            return 0;
        if (n == 1 || t <= _t[0])
            return _v[0];
        if (t >= _t[n - 1])
            return _v[n - 1];

        _slopes ??= Slopes();
        int i = 0;
        while (i < n - 2 && t > _t[i + 1])
            i++;

        double h = _t[i + 1] - _t[i], u = (t - _t[i]) / h, u2 = u * u, u3 = u2 * u;
        return (2 * u3 - 3 * u2 + 1) * _v[i] + (u3 - 2 * u2 + u) * h * _slopes[i]
            + (-2 * u3 + 3 * u2) * _v[i + 1] + (u3 - u2) * h * _slopes[i + 1];
    }

    private double[] Slopes()
    {
        int n = _t.Count;
        var slopes = new double[n];
        for (int i = 1; i < n - 1; i++)
        {
            double before = (_v[i] - _v[i - 1]) / (_t[i] - _t[i - 1]);
            double after = (_v[i + 1] - _v[i]) / (_t[i + 1] - _t[i]);
            slopes[i] = before * after <= 0 ? 0 : 2 * before * after / (before + after);
        }
        return slopes;
    }
}

internal sealed class FigureSolver
{
    private bool _toForehead, _fromForehead;   // while an arm is on its way to the forehead, or away from it (ArmBlend)

    private sealed class FootMove
    {
        public double T0, T1;
        public Vec3 From, To;
        public double YawFrom, YawTo;
        public double Lift;
        public bool Orbit;
        public Vec3 Pivot;
        public double Angle;
        public bool Air;
        public double AirHeight;
        /// <summary>
        /// How far the foot bows aside at the middle of its way and back. In the air, the feet of a
        /// changement pass each other apart; on the floor, a foot whose straight way would run through
        /// the standing foot goes round it (<see cref="GoRound"/>).
        /// </summary>
        public Vec3 Open;
        /// <summary>How high the foot is held off the floor when the move begins and when it ends (a foot left in the air after a hop or a leap stays at LiftTo).</summary>
        public double LiftFrom, LiftTo;
        /// <summary>How much of a stride the step goes the way the foot points: 1 forward, -1 back, 0 sideways.</summary>
        public double Along;
        /// <summary>The word of the support track that made the move.</summary>
        public Seg? Seg;
    }

    /// <summary>Where the feet and the weight are between two events of the support track.</summary>
    private sealed class Stand
    {
        public Vec3 Left, Right, Weight;
        public double LeftYaw, RightYaw, Yaw, Float;
        /// <summary>How high each foot is held off the floor between moves: 0 on the floor, more after landing a hop or a leap on the other foot.</summary>
        public double LeftUp, RightUp;
        public char On = 'B';   // the supporting foot: L, R or Both
        /// <summary>A shift that did not get the weight over its foot (a wide stance): the other foot still carries some, and slides to close.</summary>
        public bool Short;
        public Stand Clone() => (Stand)MemberwiseClone();
    }

    private struct CoreState
    {
        public double Yaw;
        public Vec3 Fwd, Left;
        public Vec3 LPos, RPos;
        public double LYaw, RYaw, LLift, RLift;
        public double LPitch, RPitch;   // the foot about its ankle, radians; positive lifts the toes
        /// <summary>How far each foot may rise onto its toes for its leg to reach, 0..1 of <see cref="RollMost"/>: a foot about to step, and on its way up.</summary>
        public double LRoll, RRoll;
        public bool LAir, RAir;
        public double GwL, GwR;     // how far each leg is into a gesture, 0..1
        public Vec3 TorsoUp;
        /// <summary>How far the back is rounded: 0 a straight spine, 1 the whole of `@round`.</summary>
        public double Round;
        /// <summary>Where the `shoulders` row puts the line of the shoulders on the floor: radians from the feet, positive to the left.</summary>
        public double Shoulders;
        /// <summary>How far the hips are turned over the feet about the vertical, radians, positive to the left; and their own left and forward.</summary>
        public double Hips;
        public Vec3 HipLeft, HipFwd;
        public Vec3 P;             // pelvis over the floor
        public double WantY, Deficit, CeilingY, Raise, Float;
        /// <summary>How far the height row stands below standing straight (soft knees, a plié).</summary>
        public double Drop;
    }

    /// <summary>The spine and what hangs on it, before arms and legs.</summary>
    private struct Trunk
    {
        public CoreState C;
        /// <summary>Up, Left and Fwd are the chest's own: with a rounded back the chest leans further than the line from the pelvis to it.</summary>
        public Vec3 Pelvis, Waist, Chest, Head, Up, Left, Fwd;
        /// <summary>
        /// The chest's frame square to the feet: what the head is measured against. The hips and the
        /// shoulders turn under the head, so the face keeps looking where the head's own word says.
        /// </summary>
        public Vec3 FaceLeft, FaceFwd;
        /// <summary>
        /// When the chest would have to turn on the hips further than it can to put the shoulders where
        /// their row says: the degrees from the feet they reach instead, and the lean that takes the rest.
        /// </summary>
        public (double Reached, double Wanted, double Lean)? Short;
        public readonly Vec3 Shoulder(int side) => Chest + Left * (Rig.ShoulderHalf * side) - Up * Rig.ShoulderDrop;
    }

    /// <summary>A share of the way from the middle to a foot at which the weight is over that foot, and the other may lift.</summary>
    private const double OverTheFoot = 0.8;

    private readonly DancerIr _dancer;
    private readonly Func<string, FigureSolver?> _others;
    private readonly IReadOnlyList<TempoIr> _tempos;
    private readonly double _dt, _length;
    private readonly bool _loop;
    private readonly Vec3 _origin;
    private double _yawStart, _leftYawStart, _rightYawStart;
    private Vec3 _leftStart, _rightStart;
    private readonly List<FootMove> _leftMoves = [], _rightMoves = [];
    private readonly Curve _weightX = new(), _weightZ = new(), _float = new();
    private double _weightTime, _floatTime;
    private readonly List<(double T0, double T1, double From, double To)> _turns = [];
    private readonly List<(double T0, double Up, double Land, double T1, double Height, double Bend, double Give)> _jumps = [];
    /// <summary>Sissonnes: the leg thrown out, when both feet leave the floor, and when the other lands (DESIGN.md 85.6).</summary>
    private readonly List<(bool Left, double Up, double Land)> _thrown = [];
    /// <summary>Where in the room each `stay` keeps its hand (found once), and the ones being found.</summary>
    private readonly Dictionary<Seg, Vec3> _stayHands = [];
    private readonly HashSet<Seg> _staying = [];
    private readonly List<Seg> _height, _leftArm, _rightArm, _leftLeg, _rightLeg, _turnout, _hips, _torso, _shoulders, _head;
    private bool _built;
    private int _posing;

    // A body nobody tells to move is not still (DESIGN.md 94): it breathes, its weight drifts from foot to
    // foot, the chest and the head go a little with the arms. Only rows the dancer has no words in, and
    // only as much as the feet stand still. No chance in it: the same text moves the same way, and a loop
    // joins (the periods divide it). Each dancer has its own phase, so a line does not breathe as one.
    private const double BreathSeconds = 3.6, SwaySeconds = 7.2;
    private double _breath, _sway, _wander, _phase, _lifeShare = 1;
    private bool _alive;

    private void SetUpLife()
    {
        _alive = Solver.LifeOn;
        _lifeShare = _alive ? 1 : 0;
        double perSecond = TempoMap.BpmAt(_tempos, 0) / 60;
        double Period(double seconds)
        {
            double beats = seconds * perSecond;
            if (!_loop)
                return beats;
            int times = (int)Math.Round(_length / beats);
            if (times < 1)
            {
                // A loop shorter than half a breath: it would breathe at the loop's pace, so it does not sway at all.
                if (_length < beats * 0.5)
                    _lifeShare = 0;
                times = 1;
            }
            return _length / times;
        }
        _breath = Period(BreathSeconds);
        _sway = Period(SwaySeconds);
        _wander = Period(SwaySeconds * 1.5);
        uint hash = 2166136261;
        foreach (char ch in _dancer.Id)
            hash = (hash ^ ch) * 16777619;
        _phase = hash % 1000 / 1000.0;
    }

    private double Wave(double t, double period, double shift = 0) =>
        _lifeShare * Math.Sin(2 * Math.PI * (t / period + _phase + shift));

    /// <summary>How still the feet are around t: 0 while a foot moves, 1 from 0.8 s away from any move of either foot.</summary>
    private double FeetStill(double t)
    {
        double ramp = 0.8 * PerSecond(t), nearest = double.MaxValue;
        foreach (var moves in new[] { _leftMoves, _rightMoves })
        {
            foreach (var move in moves)
            {
                double d = t < move.T0 ? move.T0 - t : t > move.T1 ? t - move.T1 : 0;
                nearest = Math.Min(nearest, d);
            }
        }
        return nearest >= ramp ? 1 : Smooth(nearest / ramp);
    }

    /// <summary>How far an arm is raised, 0 hanging to 1 overhead, and how far it is held forward (0..1), as its row carries it.</summary>
    private static (double Up, double Fwd) ArmRaise(List<Seg> arm, double t) =>
        Chain(arm, t, seg => seg switch
        {
            null => (0.0, 0.0),
            { Dir: { } d } => ((d[1] + 1) / 2, Math.Max(0, d[2])),
            { Kind: SegKinds.Anchor, Anchor: "head" or "forehead" } => (0.85, 0.3),
            { Kind: SegKinds.Anchor } => (0.4, 0.5),
            _ => (0.5, 0.5),
        }, (a, b, e) => (a.Item1 + (b.Item1 - a.Item1) * e, a.Item2 + (b.Item2 - a.Item2) * e));

    public FigureSolver(DancerIr dancer, MotionIr ir, double beatsPerFrame, Func<string, FigureSolver?> others)
    {
        _dancer = dancer;
        _others = others;
        _tempos = TempoMap.Of(ir.Tempos, ir.Tempo);
        _dt = beatsPerFrame;
        _length = ir.TotalBeats;
        _loop = ir.Loop && ir.TotalBeats > 0;
        _yawStart = dancer.FacingDeg * Math.PI / 180;
        _origin = new Vec3(dancer.X, 0, dancer.Z);

        _height = Row(Channels.Height);
        _leftArm = Row(Channels.LeftArm);
        _rightArm = Row(Channels.RightArm);
        _leftLeg = Row(Channels.LeftLeg);
        _rightLeg = Row(Channels.RightLeg);
        _turnout = Row(Channels.Turnout);
        _torso = Row(Channels.Torso);
        _shoulders = Row(Channels.Shoulders);
        _hips = Row(Channels.Hips);
        _head = Row(Channels.Head);
        SetUpLife();
        SetUpMic();
        Juggling = Juggling.Build(dancer, _tempos);
    }

    /// <summary>The juggle row timed (DESIGN.md 118), or null for a dancer that does not juggle.</summary>
    public Juggling? Juggling { get; }

    /// <summary>The farthest a juggling hand goes from its shoulder, metres: the elbow bent some 40 degrees (0.60 is straight).</summary>
    private const double JuggleReach = 0.575;
    /// <summary>Where a juggling hand's reach starts to be held in, metres: beyond, it comes ever more slowly up to <see cref="JuggleReach"/>.</summary>
    private const double JuggleReachSoft = 0.50;

    /// <summary>
    /// A juggling hand's way out from the shoulder, held in softly: as far as <see cref="JuggleReachSoft"/> as it is, and beyond
    /// ever less, never past <see cref="JuggleReach"/>. (Stopped hard there, the hand stood still where it went beyond and
    /// turned off at a sudden angle as it came back: the user, Mills' crossed throws.)
    /// </summary>
    /// <summary>How far a penguin's hand goes from the shoulder at the most, metres: the arm 0.998 of its length, the elbow bent some 7 degrees.</summary>
    private const double PenguinReach = 0.998 * (Rig.UpperArm + Rig.Forearm);
    /// <summary>How far out from the pelvis a penguin's hand is kept by the hips round its catch, metres (it catches 0.34 out).</summary>
    private const double PenguinHipClear = 0.40;
    /// <summary>Where a penguin's reach starts to be held in, metres.</summary>
    private const double PenguinReachSoft = 0.50;

    private static Vec3 SoftReach(Vec3 out_) => SoftReach(out_, JuggleReachSoft, JuggleReach);

    private static Vec3 SoftReach(Vec3 out_, double soft, double most)
    {
        double r = out_.Length;
        if (r <= soft)
            return out_;
        double room = most - soft;
        return out_ * ((soft + room * Math.Tanh((r - soft) / room)) / r);
    }
    /// <summary>How far in front of the feet Mills' balls go, metres: all the same.</summary>
    private const double MillsPlane = 0.33;
    /// <summary>Where Mills' elbows point, besides down: out to their side (in, below 0) and back, per unit down.</summary>
    /// (In 0.3 and back 0.2: the elbow went 0.16 m side to side rather than 0.25, the forearm out past it; further in, the
    /// forearms came closer than 7 cm in more frames.) Then in only 0.1 (the user: the upper arms turned in at the shoulder).
    private const double MillsElbowOut = -0.1, MillsElbowBack = 0.2;
    /// <summary>Where Mills' elbow is kept side to side: metres out from the shoulder (in, below 0), and how firmly (0..1).</summary>
    /// (12 cm in: the elbow went 0.08 m side to side rather than 0.16. Held exactly, without the turn below, it went far up
    /// and forward instead, 1.8 m a round. Then 5 cm in, held a little more loosely (MillsElbowSlack 0.045): the upper arm
    /// 15 degrees in from straight down on the whole rather than 25, the user asking for it less turned in, DESIGN.md 118.26.)
    private const double MillsElbowSide = -0.05, MillsElbowHold = 1.0;
    /// <summary>Where a takeouts' swiping elbow is kept side to side: metres out from the shoulder.</summary>
    private const double SwipeElbowSide = 0.12;
    /// <summary>Takeouts' round (DESIGN.md 118.49): the elbow's way, down and this much forward.</summary>
    private const double RoundElbowAhead = 0.6;
    /// <summary>How far off its place side to side Mills' elbow may be (metres) against how far round its circle it goes (radians).</summary>
    private const double MillsElbowSlack = 0.045, MillsElbowTurn = 0.25;
    /// <summary>How far the pelvis goes to the side with Mills' sway, per unit of the back's lean (against it, the hips out under a leaning back: with it, the body went away from the throws).</summary>
    private const double MillsLegSway = -0.4;
    /// <summary>Seconds between the nine looks either side at how a hand is held behind the back or under the leg (some 0.16 s each way).</summary>
    private const double TrickPoleStep = 0.04;
    /// <summary>How far the windmill's top hand (and the ball it holds) is drawn in toward the body, metres, as the other hand throws from under it.</summary>
    private const double WindmillDrawIn = 0.18;
    /// <summary>The head tipped back after a high ball: from how far above the head the ball begins to tip it, at how far it
    /// is tipped all the way, and how far that is (radians); and the time it is eased over (seconds, the Gaussian's spread).</summary>
    private const double HeadBackFrom = 0.3, HeadBackFull = 1.2, HeadBackMost = 20 * Math.PI / 180, HeadBackSoften = 0.15;

    // A mic in a hand (`mic right`, DESIGN.md 117). While the dancer sings, the hand is at the mouth, whatever its
    // row says; between phrases the row has the arm again, the mic in its hand. Sung by the phrase, not the
    // syllable: a singer does not take the mic down for a breath. Up a little before the first syllable (the
    // breath before it), down a little after the last.
    private const double MicJoinSeconds = 1.6, MicLeadSeconds = 0.25, MicRaiseSeconds = 0.5, MicStaySeconds = 0.3, MicLowerSeconds = 0.6;
    /// <summary>The phrases sung, in seconds: from the first syllable to the end of the last.</summary>
    private readonly List<(double From, double To)> _micPhrases = [];

    private void SetUpMic()
    {
        if (_dancer.Mic is null || _dancer.Sings is not { Count: > 0 } sung)
            return;
        foreach (var syllable in sung.OrderBy(s => s.Beat))
        {
            double from = Seconds(syllable.Beat), to = Seconds(syllable.Beat + syllable.Beats);
            if (_micPhrases.Count > 0 && from - _micPhrases[^1].To < MicJoinSeconds)
                _micPhrases[^1] = (_micPhrases[^1].From, Math.Max(_micPhrases[^1].To, to));
            else
                _micPhrases.Add((from, to));
        }
    }

    private double Seconds(double beat) => TempoMap.Seconds(_tempos, Math.Max(0, beat));

    /// <summary>How far the mic is up at the mouth at beat t: 0 in the hand, 1 at the mouth.</summary>
    public double MicUp(double t)
    {
        double s = Seconds(t), up = 0;
        foreach (var (from, to) in _micPhrases)
        {
            double there = from - MicLeadSeconds, leaves = to + MicStaySeconds;
            double w = s <= there - MicRaiseSeconds || s >= leaves + MicLowerSeconds ? 0
                : s < there ? Smooth((s - there + MicRaiseSeconds) / MicRaiseSeconds)
                : s <= leaves ? 1
                : Smooth(1 - (s - leaves) / MicLowerSeconds);
            up = Math.Max(up, w);
        }
        return up;
    }

    /// <summary>
    /// Words of an arm that holds the mic that arrive while it is at the mouth: they are not seen (the mic wins).
    /// With `mic both` both arms; otherwise the one hand.
    /// </summary>
    public IEnumerable<(Seg Seg, string Channel)> MicHidden()
    {
        if (_micPhrases.Count == 0)
            return [];
        var channels = _dancer.Mic switch
        {
            "both" => new[] { Channels.LeftArm, Channels.RightArm },
            "left" => [Channels.LeftArm],
            _ => [Channels.RightArm],
        };
        var full = _micPhrases.Select(p => (From: TempoMap.Beat(_tempos, p.From - MicLeadSeconds), To: TempoMap.Beat(_tempos, p.To + MicStaySeconds))).ToList();
        return from channel in channels
               where _dancer.Tracks.ContainsKey(channel)
               from seg in _dancer.Tracks[channel]
               where seg.Kind is not (SegKinds.Hold or SegKinds.Stay)
               where full.Any(p => seg.At > p.From + 1e-6 && seg.At < p.To - 1e-6)
               select (seg, channel);
    }

    /// <summary>What has to hold for a loop and does not, in words for the author; null when it joins.</summary>
    public string? LoopProblem { get; private set; }
    /// <summary>Words of the support track that the way the feet stood did not allow, with the error to give.</summary>
    public List<(Seg Seg, string Code, string Message)> SupportProblems { get; } = [];

    /// <summary>
    /// Words of the support track that put a foot down on the floor while its leg's own row still
    /// holds it up (the narrower row wins, so the foot stays in the air): the landing leg of a split
    /// leap that kept its shape, a `together` under a leg still lifted behind (writing trial 19).
    /// </summary>
    public IEnumerable<(Seg Seg, string Leg, double At)> FeetHeldUp()
    {
        foreach (var (moves, legRow, leg) in new[] { (_leftMoves, _leftLeg, Channels.LeftLeg), (_rightMoves, _rightLeg, Channels.RightLeg) })
        {
            foreach (var move in moves)
            {
                if (move.Seg is null || move.LiftTo > 0 || move.Orbit)
                    continue;
                double at = move.T1;
                if (_loop && (at < -1e-9 || at > _length + 1e-9))
                    continue;
                double yaw = YawAt(at);
                if (LegShape(legRow, at, Vec3.Left(yaw), Vec3.Forward(yaw), SomewhereBelow, Vec3.Up).Weight > 0.5)
                    yield return (move.Seg, leg, at);
            }
        }
    }

    /// <summary>How far the weight goes over the floor from the first beat to the last.</summary>
    public Vec3 Travel => WeightAt(_length) - WeightAt(0);

    public Vec3 WeightAt(double t) => _built ? new Vec3(_weightX.Eval(t), 0, _weightZ.Eval(t)) : _origin;

    /// <summary>
    /// A channel's segments. A loop follows itself, so its row is laid out three times — the
    /// turn before, this one, the turn after — and only the middle one is looked at: whatever
    /// is still arriving, or already leaving, at the join comes out right without being told.
    /// </summary>
    private List<Seg> Row(string channel)
    {
        var segs = _dancer.Tracks.TryGetValue(channel, out var found) ? found : [];
        if (!_loop)
            return [.. segs];    // its own list: Build may put a seg it shortened in it
        var row = new List<Seg>();
        for (int turn = -1; turn <= 1; turn++)
        {
            foreach (var seg in segs)
            {
                var copy = seg.Clone();
                copy.T0 += turn * _length;
                copy.T1 += turn * _length;
                row.Add(copy);
            }
        }
        return row;
    }

    public void Build()
    {
        var support = _dancer.Tracks.TryGetValue(Channels.Support, out var found) ? found : [];
        var stand = new Stand
        {
            Left = _origin + Vec3.Left(_yawStart) * (ClosedWidth(0) / 2),
            Right = _origin - Vec3.Left(_yawStart) * (ClosedWidth(0) / 2),
            Weight = _origin,
            LeftYaw = _yawStart, RightYaw = _yawStart, Yaw = _yawStart,
        };

        double start = 0;
        if (_loop)
        {
            // Let the stance settle, then see what one turn of the loop does to it.
            for (int i = 0; i < 3; i++)
                Play(stand, support, 0, record: false);
            var after = stand.Clone();
            Play(after, support, 0, record: false);

            var travel = after.Weight - stand.Weight;
            if (Math.Abs(Math.IEEERemainder(after.Yaw - stand.Yaw, 2 * Math.PI)) > 1e-3)
                LoopProblem = "This loop does not join: it ends facing another way than it starts. The turns in it must add up to nothing, or to a full turn.";
            else if (((after.Left - stand.Left) - travel).Length > 0.02 || ((after.Right - stand.Right) - travel).Length > 0.02
                || Math.Abs(after.Float - stand.Float) > 1e-6)
                LoopProblem = "This loop does not join: the feet do not end as they start.";

            // Start one turn early, placed so that the weight is over the dancer's own place at beat 0.
            var back = _origin - stand.Weight - travel;
            stand.Left += back;
            stand.Right += back;
            stand.Weight += back;
            start = -_length;
        }

        _yawStart = stand.Yaw;
        _leftYawStart = stand.LeftYaw;
        _rightYawStart = stand.RightYaw;
        _leftStart = stand.Left;
        _rightStart = stand.Right;
        _weightX.Add(start, stand.Weight.X);
        _weightZ.Add(start, stand.Weight.Z);
        _float.Add(start, stand.Float);
        _weightTime = _floatTime = start;

        if (_loop)
        {
            for (int turn = -1; turn <= 1; turn++)
                Play(stand, support, turn * _length, record: true);
        }
        else
        {
            Play(stand, support, 0, record: true);
        }
        // A leg let go (`skip` in its row) over a step of its foot is all let go when that step lands: the foot
        // cannot be on the floor and still a third held up. A skip longer than the landing is the time the leg
        // had, not the time it takes (writing trial 24: `side.down(45):1/2 skip:3/2` over an assemblé's landing).
        foreach (var (moves, legRow) in new[] { (_leftMoves, _leftLeg), (_rightMoves, _rightLeg) })
        {
            for (int i = 0; i < legRow.Count; i++)
            {
                var seg = legRow[i];
                if (seg.Kind != SegKinds.Release)
                    continue;
                var landing = moves.FirstOrDefault(m => m.Seg is not null && m.LiftTo <= 0 && !m.Orbit
                    && m.T1 > seg.T0 + 1e-9 && m.T1 < seg.T1 - 1e-9);
                if (landing is null)
                    continue;
                var early = seg.Clone();
                early.T1 = landing.T1;
                legRow[i] = early;
            }
        }
        // A sissonne pushes off both feet: the leg thrown out leaves the floor with the other, and opens in
        // the air. Its word into the shape, begun on the floor before the take-off, starts at the take-off
        // and arrives as written (it slid the foot 30 cm out along the floor through the bend: DESIGN.md 85.6).
        foreach (var (left, up, land) in _thrown)
        {
            var legRow = left ? _leftLeg : _rightLeg;
            for (int i = 0; i < legRow.Count; i++)
            {
                var seg = legRow[i];
                if (seg.Kind is SegKinds.Hold or SegKinds.Release or SegKinds.Stay || seg.T0 >= up - 1e-9 || seg.T1 <= up + 1e-9 || seg.T1 > land + 1e-9)
                    continue;
                // Only a leg that comes off the floor: one already held up was never pushing.
                if (LegShape(legRow, seg.T0, Vec3.Left(_yawStart), Vec3.Forward(_yawStart), SomewhereBelow, Vec3.Up).Weight > 1e-6)
                    continue;
                var late = seg.Clone();
                late.T0 = up;
                legRow[i] = late;
            }
        }
        _built = true;
    }

    /// <summary>
    /// Plays the support track once, in order, and writes down what it implies: where each
    /// foot moves and when, where the weight is at each landing, the facing, the height.
    /// </summary>
    private void Play(Stand s, List<Seg> support, double offset, bool record)
    {
        // A step straight after a step: the body goes on over the feet, not stopping at each landing (DESIGN.md 90).
        bool flowing = false;
        void Weight(double t0, double t1, Vec3 to)
        {
            if (record)
            {
                if (t0 > _weightTime && !flowing)
                {
                    // Nothing was written since the last key: stay there until this segment starts.
                    _weightX.Add(t0, s.Weight.X);
                    _weightZ.Add(t0, s.Weight.Z);
                }
                _weightX.Add(t1, to.X);
                _weightZ.Add(t1, to.Z);
                _weightTime = t1;
            }
            s.Weight = to;
        }

        void Float(double t0, double t1, double to)
        {
            if (record)
            {
                if (t0 > _floatTime)
                    _float.Add(t0, s.Float);
                _float.Add(t1, to);
                _floatTime = t1;
            }
            s.Float = to;
        }

        // A move sets out from as high as the foot is held, and ends on the floor, or held at `held`.
        Seg? current = null;
        void Move(bool isLeft, FootMove move, double held = 0)
        {
            move.LiftFrom = isLeft ? s.LeftUp : s.RightUp;
            move.LiftTo = held;
            move.Seg = current;
            if (record)
                (isLeft ? _leftMoves : _rightMoves).Add(move);
            if (isLeft)
            {
                s.Left = move.To;
                s.LeftYaw = move.YawTo;
                s.LeftUp = held;
            }
            else
            {
                s.Right = move.To;
                s.RightYaw = move.YawTo;
                s.RightUp = held;
            }
        }

        void Turn(double t0, double t1, double angle)
        {
            if (record)
                _turns.Add((t0, t1, s.Yaw, s.Yaw + angle));
            s.Yaw += angle;
        }

        // How far (0..0.85 of the way from the middle) the weight goes toward one foot. Only as far as the hips sink
        // 3 cm below where the dancer stands (DESIGN.md 86.4): in a wide stance the other leg is all but straight
        // already, and every cm toward the foot costs height (`shift.right` 0.7 m apart was a side lunge, the knee at
        // 75°). A lunge is written in the height row, and then the hips go on over the foot.
        double ShareOver(bool onLeft, double at)
        {
            var middle = (s.Left + s.Right) * 0.5;
            double share = 0.85;
            var hipLeft = Vec3.Left(s.Yaw) * Rig.HipHalf;
            double full = Rig.Leg * Rig.StraightLeg;
            double Reach(Vec3 hip, Vec3 foot)
            {
                double d = (new Vec3(hip.X, 0, hip.Z) - new Vec3(foot.X, 0, foot.Z)).Length;
                return Math.Sqrt(Math.Max(0.04, full * full - d * d));
            }
            double StandAt(Vec3 q) => Math.Min(Reach(q + hipLeft, s.Left), Reach(q - hipLeft, s.Right));
            // The far leg straightens out of the bend the height row stands in then (soft knees, a `low`: Core),
            // and then the hips may sink 3 cm more below where they stand.
            double level = Chain(_height, at, x => x?.Level ?? 0, (a, b, e) => a + (b - a) * e, Dist);
            double softness = Chain(_height, at, x => x is { Straight: true } ? 0.0 : 1.0, (a, b, e) => a + (b - a) * e, Dist);
            double drop = Math.Max(softness * Rig.Leg * (Rig.StraightLeg - Rig.BentShare(Rig.Thigh, Rig.Shin, Rig.SoftKnee)), Rig.LowDrop * Math.Max(0, -level));
            double floor = StandAt(middle) - drop - 0.03;
            var foot = onLeft ? s.Left : s.Right;
            if (StandAt(Vec3.Lerp(middle, foot, share)) < floor)
            {
                double lo = 0, hi = share;
                for (int i = 0; i < 20; i++)
                {
                    double mid = (lo + hi) / 2;
                    if (StandAt(Vec3.Lerp(middle, foot, mid)) < floor) hi = mid; else lo = mid;
                }
                share = lo;
            }
            return share;
        }

        static double StepLift(Vec3 from, Vec3 to) => Math.Clamp(0.05 + 0.12 * (to - from).Length, 0.06, 0.15);
        Vec3 Outward(bool isLeft) => Vec3.Left(s.Yaw) * (isLeft ? 1 : -1);

        // A leg's `down` puts a foot left in the air (after a hop or a leap) on the floor, beside the
        // foot it stands on at standing width; the weight stays where it is (DESIGN.md 78.4). Its
        // row's `down` words, in this play's time, in order.
        var downs = new[] { (Left: true, Row: _leftLeg), (Left: false, Row: _rightLeg) }
            .SelectMany(leg => leg.Row.Where(d => IsDown(d) && d.T1 > offset + 1e-9 && d.T1 <= offset + _length + 1e-9)
                                      .Select(d => (leg.Left, Seg: d)))
            .OrderBy(d => d.Seg.T1).ToList();
        int nextDown = 0;
        void PutDown(double until)
        {
            for (; nextDown < downs.Count && downs[nextDown].Seg.T1 <= until + 1e-9; nextDown++)
            {
                var (isLeft, down) = downs[nextDown];
                if ((isLeft ? s.LeftUp : s.RightUp) <= 0 || s.On == 'B' || (s.On == 'L') == isLeft)
                    continue;
                var standing = isLeft ? s.Right : s.Left;
                var place = standing + Vec3.Left(s.Yaw) * (isLeft ? Rig.Stance : -Rig.Stance);
                current = null;
                // Not before the foot has finished being put in the air.
                var moves = isLeft ? _leftMoves : _rightMoves;
                double from = record && moves.Count > 0 ? Math.Max(down.T0, Math.Min(down.T1, moves[^1].T1)) : down.T0;
                Move(isLeft, new FootMove
                {
                    T0 = from, T1 = down.T1, From = isLeft ? s.Left : s.Right, To = place,
                    YawFrom = isLeft ? s.LeftYaw : s.RightYaw, YawTo = s.Yaw,
                });
            }
        }

        // In a loop the word before the first is the last (of the turn before).
        Seg? previous = _loop && support.Count > 0 ? support[^1] : null;
        foreach (var seg in support)
        {
            // A `down` that has arrived before this word sets out has put its foot on the floor.
            PutDown(seg.T0 + offset);
            double gap = previous is null ? 1 : seg.At - previous.Until;
            if (_loop && gap < -1e-6)
                gap += _length;
            double previousEnd = previous is null || (_loop && seg == support[0]) ? offset + Math.Min(0, seg.T0) : previous.T1 + offset;
            flowing = previous is not null && BinderRun.Walks(previous) && BinderRun.Walks(seg) && seg.T1 > seg.T0 + 1e-9 && Math.Abs(gap) < 1e-6;
            var previous2 = previous;   // the word before this one
            previous = seg;
            current = seg;
            double t0 = seg.T0 + offset, t1 = seg.T1 + offset;
            var fwd = Vec3.Forward(s.Yaw);
            var leftward = Vec3.Left(s.Yaw);

            switch (seg.Kind)
            {
                case SegKinds.Step:
                {
                    bool isLeft = seg.Foot == "left";
                    if (seg.Stance)
                    {
                        // A pose is a way of standing: a body that already stands so moves nothing.
                        var apart = (isLeft ? s.Left : s.Right) - (isLeft ? s.Right : s.Left);
                        if (Math.Abs(apart.Dot(fwd) - seg.Dz) < 0.02 && Math.Abs(apart.Dot(isLeft ? leftward : -leftward) - seg.Dx) < 0.02)
                        {
                            Weight(t0, t1, s.Weight);
                            break;
                        }
                    }
                    if (seg.Kept is { } kept)
                    {
                        // A loop's first step, already made at its start: standing so already, no foot moves, and
                        // the weight goes back between the feet over all of the word before, as a shift does.
                        var apart = (isLeft ? s.Left : s.Right) - (isLeft ? s.Right : s.Left);
                        if (Math.Abs(apart.Dot(fwd) - seg.Dz) < 0.02 && Math.Abs(apart.Dot(isLeft ? leftward : -leftward) - seg.Dx) < 0.02
                            && Math.Abs((isLeft ? s.LeftYaw : s.RightYaw) - s.Yaw) < 0.02)
                        {
                            s.On = 'B';
                            Weight(kept + offset, t1, (s.Left + s.Right) * 0.5);
                            break;
                        }
                    }
                    if (seg.TurnDeg != 0)
                    {
                        // Turning during the step: the standing foot swivels where it is, and the
                        // step lands where it would have after the turn.
                        double angle = seg.TurnDeg * Math.PI / 180;
                        var standing = isLeft ? s.Right : s.Left;
                        double standingYaw = isLeft ? s.RightYaw : s.LeftYaw;
                        Move(!isLeft, new FootMove
                        {
                            T0 = t0, T1 = t1, From = standing, To = standing, YawFrom = standingYaw, YawTo = standingYaw + angle,
                            Orbit = true, Pivot = standing, Angle = angle,
                        });
                        Turn(t0, t1, angle);
                        fwd = Vec3.Forward(s.Yaw);
                        leftward = Vec3.Left(s.Yaw);
                    }
                    var from = isLeft ? s.Left : s.Right;
                    var other = isLeft ? s.Right : s.Left;
                    var target = other + fwd * seg.Dz + (isLeft ? leftward : -leftward) * seg.Dx;
                    Move(isLeft, new FootMove
                    {
                        T0 = t0, T1 = t1, From = from, To = target,
                        YawFrom = isLeft ? s.LeftYaw : s.RightYaw, YawTo = s.Yaw, Lift = StepLift(from, target),
                        Along = Math.Clamp((target - from).Dot(fwd) / Rig.Stride, -1, 1),
                        Open = GoRound(from, target, other, Outward(isLeft)),
                    });
                    if (t1 - t0 <= 1e-9 || seg.Stance)
                    {
                        // A step already made (the first word of a score) is a way of standing, and so is a
                        // pose's step: one stands on both feet, the weight between them (DESIGN.md 82.6; trial 23:
                        // the same guard stood 58% and 43% toward the front foot in a loop and out of one). To
                        // stand on the foot it stepped onto, a `shift` follows.
                        s.On = 'B';
                        Weight(t0, t1, (s.Left + s.Right) * 0.5);
                        break;
                    }
                    s.On = isLeft ? 'L' : 'R';
                    // At the landing the weight is between the feet, a little toward the new one.
                    // Keyed only at landings, the pelvis passes over the standing foot in mid-step.
                    // A step out of standing still: the body sets off before the foot does, as long again before it
                    // (in a `then` too: it is what a lead-in is written with): in the foot's own half second it went
                    // 1.3 to 1.6 m/s, a burst out of every start (trial 28).
                    double sets = flowing ? t0 : Math.Max(previous2 is { Kind: SegKinds.Hold } ? previous2.T0 + offset : previousEnd, t0 - (t1 - t0));
                    Weight(sets, t1, Vec3.Lerp((s.Left + s.Right) * 0.5, target, 0.3));
                    break;
                }

                case SegKinds.Together:
                {
                    // `together`: touching (the heels, turned out); `stand`: the width it says.
                    double closed = seg.Spread ?? TouchWidth(t1);
                    if (s.On != 'B')
                    {
                        bool moveLeft = s.On == 'R';
                        var from = moveLeft ? s.Left : s.Right;
                        var other = moveLeft ? s.Right : s.Left;
                        var target = other + (moveLeft ? leftward : -leftward) * closed;
                        // A shift that did not get over its foot (a wide stance: DESIGN.md 86.4) leaves the other foot
                        // with weight on it: it slides in along the floor, the weight coming with it (trial 28).
                        Move(moveLeft, new FootMove
                        {
                            T0 = t0, T1 = t1, From = from, To = target,
                            YawFrom = moveLeft ? s.LeftYaw : s.RightYaw, YawTo = s.Yaw, Lift = s.Short ? 0 : StepLift(from, target),
                            Open = GoRound(from, target, other, Outward(moveLeft)),
                        });
                    }
                    else
                    {
                        // The weight is on both feet (after a jump, a `shift.center`): neither is the free one.
                        // Two feet do not slide together at once (DESIGN.md 90; they did, trial 23 to here): the
                        // weight goes toward one first, the one it is nearer (the right when it is between them), from
                        // a little before the word sets out, as far as a `shift` goes (no deeper than 3 cm: trial 28, a
                        // 15 cm lunge out of second), and the other closes to it: lifted when the weight got over the
                        // standing foot, slid along the floor when it did not (the user, DESIGN.md 90).
                        var apart = s.Left - s.Right;
                        if (Math.Abs(apart.Dot(leftward) - closed) > 0.02 || Math.Abs(apart.Dot(fwd)) > 0.02)
                        {
                            bool moveLeft = (s.Weight - s.Left).Length > (s.Weight - s.Right).Length - 1e-6;
                            var standing = moveLeft ? s.Right : s.Left;
                            var from = moveLeft ? s.Left : s.Right;
                            var target = standing + (moveLeft ? leftward : -leftward) * closed;
                            double d = t1 - t0;
                            double over = Math.Max(previousEnd, t0 - d / 2), sets = t0 + 0.25 * d;
                            double share = ShareOver(!moveLeft, sets);
                            Weight(over, sets, Vec3.Lerp((s.Left + s.Right) * 0.5, standing, share));
                            Move(moveLeft, new FootMove
                            {
                                T0 = sets, T1 = t1, From = from, To = target,
                                YawFrom = moveLeft ? s.LeftYaw : s.RightYaw, YawTo = s.Yaw, Lift = share < OverTheFoot ? 0 : StepLift(from, target),
                                Open = GoRound(from, target, standing, Outward(moveLeft)),
                            });
                        }
                    }
                    s.Short = false;
                    s.On = 'B';
                    Weight(t0, t1, (s.Left + s.Right) * 0.5);
                    break;
                }

                case SegKinds.Shift:
                {
                    var middle = (s.Left + s.Right) * 0.5;
                    s.On = seg.Foot == "left" ? 'L' : seg.Foot == "right" ? 'R' : 'B';
                    double share = s.On == 'B' ? 0 : ShareOver(s.On == 'L', t1);
                    s.Short = s.On != 'B' && share < OverTheFoot;
                    Weight(t0, t1, s.On == 'L' ? Vec3.Lerp(middle, s.Left, share) : s.On == 'R' ? Vec3.Lerp(middle, s.Right, share) : middle);
                    break;
                }

                case SegKinds.Turn:
                {
                    var pivot = s.On == 'L' ? s.Left : s.On == 'R' ? s.Right : (s.Left + s.Right) * 0.5;
                    double angle = seg.TurnDeg * Math.PI / 180;
                    if (seg.Target is not null)
                    {
                        // To face another dancer: where it is when this turn ends (where it starts, if it is not solved yet).
                        var toward = (_others(seg.Target)?.WeightAt(seg.T1) ?? pivot) - pivot;
                        angle = toward.Length > 1e-6 ? Math.IEEERemainder(Math.Atan2(toward.X, toward.Z) - s.Yaw, 2 * Math.PI) : 0;
                    }
                    var newLeft = pivot + (s.Left - pivot).RotateY(angle);
                    var newRight = pivot + (s.Right - pivot).RotateY(angle);
                    var weight = pivot + (s.Weight - pivot).RotateY(angle);
                    // A foot held in the air goes round held. On both feet they swivel on the floor (lifted together, the body hung: trial 23).
                    Move(true, new FootMove
                    {
                        T0 = t0, T1 = t1, From = s.Left, To = newLeft, YawFrom = s.LeftYaw, YawTo = s.LeftYaw + angle,
                        Orbit = true, Pivot = pivot, Angle = angle, Lift = s.On is 'L' or 'B' || s.LeftUp > 0 ? 0 : 0.03,
                    }, s.LeftUp);
                    Move(false, new FootMove
                    {
                        T0 = t0, T1 = t1, From = s.Right, To = newRight, YawFrom = s.RightYaw, YawTo = s.RightYaw + angle,
                        Orbit = true, Pivot = pivot, Angle = angle, Lift = s.On is 'R' or 'B' || s.RightUp > 0 ? 0 : 0.03,
                    }, s.RightUp);
                    Turn(t0, t1, angle);
                    Weight(t0, t1, weight);
                    break;
                }

                case SegKinds.Jump:
                {
                    double duration = t1 - t0;
                    // Bend, air, give: the feet come down at Land. Of the time before it, the bend takes as long as it
                    // is deep, and that is as deep as the jump goes high (JumpShape, DESIGN.md 84.5).
                    double lands = seg.Land ?? 0.85, land = t0 + lands * duration;
                    double beforeSeconds = TempoMap.Seconds(_tempos, land) - TempoMap.Seconds(_tempos, t0);
                    double airSeconds = JumpShape.AirIn(beforeSeconds);
                    double up = beforeSeconds > 1e-9 ? land - (land - t0) * airSeconds / beforeSeconds : t0;
                    double angle = seg.TurnDeg * Math.PI / 180;
                    var leftAfter = Vec3.Left(s.Yaw + angle);
                    // The time in the air fixes the height: h = g T^2 / 8.
                    double height = JumpShape.Height(airSeconds);
                    // A height written (`hop.up(5cm)`) was given the time it needs (Binder), unless there was not so much.
                    if (seg.Rise is { } rise && duration > 1e-9)
                    {
                        if (record && height < rise - 0.005)
                            SupportProblems.Add((seg, "SH2016", FormattableString.Invariant(
                                $"There is not time enough before this {seg.Lands switch { "same" => "hop", "other" => "leap", _ => "jump" }} to rise {rise * 100:0.#} cm: it rises {height * 100:0.#} cm. Give the word before it more time.")));
                        height = Math.Min(height, rise);
                    }
                    // A jump that takes no time (in the upbeat a score starts with) has been made: the body stands as it landed.
                    bool made = duration <= 1e-9;
                    var travel = fwd * seg.Dz + leftward * seg.Dx;

                    // Onto one foot: the one it stands on (`hop`) or the one that carried no weight (`leap`).
                    // Standing on both, a `hop` lands on the foot whose leg its row does not hold in a shape
                    // as it lands (a sissonne: the other leg is thrown out; DESIGN.md 85.5). With neither leg
                    // or both held, or for a `leap`, nothing says which: the writer shifts the weight first.
                    string onto = seg.Lands ?? "both";
                    bool fromBoth = false, sissonneLeft = false;
                    if (onto != "both" && s.On == 'B')
                    {
                        bool Held(List<Seg> row) => LegShape(row, land, leftward, fwd, SomewhereBelow, Vec3.Up).Weight > 0.5;
                        bool leftHeld = Held(_leftLeg), rightHeld = Held(_rightLeg);
                        if (onto == "same" && leftHeld != rightHeld)
                        {
                            fromBoth = true;
                            sissonneLeft = rightHeld;
                        }
                        else
                        {
                            if (record)
                                SupportProblems.Add((seg, "SH2012", onto == "same"
                                    ? "Standing on both feet, a hop does not know which foot it lands on. Throw one leg out in its row as it lands (`rightLeg { side.down(45) }`: it lands on the left foot, a sissonne), or put the weight on one first (`shift.left`), or jump onto both: `jump`."
                                    : "Standing on both feet, a leap does not know which foot it lands on. Put the weight on one first: `shift.left` (a leap lands on the right foot), or for a jump from both feet onto one, a `hop` with the other leg thrown out in its row."));
                            onto = "both";
                        }
                    }

                    if (onto == "both")
                    {
                        var middle = (s.Left + s.Right) * 0.5;
                        var landing = middle + travel;
                        // A plain `jump` keeps how the feet stand to each other: apart, one ahead, crossed (a sauté in
                        // fifth lands in fifth; trial 21: it landed side by side). `apart` and `together` set the width.
                        var stood = s.Left - s.Right;
                        var fwdAfter = Vec3.Forward(s.Yaw + angle);
                        // `jump.change` mirrors it left for right: each foot keeps its side, and the one behind lands as
                        // far in front (a changement: fifth, right front, lands in fifth, left front; still crossed).
                        var half = seg.Spread is null
                            ? (leftAfter * stood.Dot(leftward) + fwdAfter * (seg.Change ? -1 : 1) * stood.Dot(fwd)) * 0.5
                            : leftAfter * ((seg.Spread is -1 ? TouchWidth(t1) : seg.Spread.Value) / 2);
                        // Changing, the feet open in the air to pass each other at standing width, each to its own side.
                        double open = seg.Change ? Math.Max(0, (Rig.Stance - stood.Dot(leftward)) / 2) : 0;
                        Move(true, new FootMove
                        {
                            T0 = up, T1 = land, From = s.Left, To = landing + half,
                            YawFrom = s.LeftYaw, YawTo = s.Yaw + angle, Air = !made, AirHeight = height, Open = leftward * open,
                        });
                        Move(false, new FootMove
                        {
                            T0 = up, T1 = land, From = s.Right, To = landing - half,
                            YawFrom = s.RightYaw, YawTo = s.Yaw + angle, Air = !made, AirHeight = height, Open = leftward * -open,
                        });
                        Weight(t0, up, middle);
                        Weight(up, land, landing);
                        s.On = 'B';
                    }
                    else if (fromBoth)
                    {
                        // Off both feet together, onto one; the other leg goes where its row throws it, and when
                        // the row lets it go it is held low by the landing foot, as after a hop.
                        bool landLeft = sissonneLeft;
                        var middle = (s.Left + s.Right) * 0.5;
                        var target = (landLeft ? s.Left : s.Right) + travel;
                        var way = travel.Length > 1e-6 ? travel.Normalized() : fwd;
                        var held = target - way * 0.12 + (landLeft ? -leftAfter : leftAfter) * 0.08;
                        Move(landLeft, new FootMove
                        {
                            T0 = up, T1 = land, From = landLeft ? s.Left : s.Right, To = target,
                            YawFrom = landLeft ? s.LeftYaw : s.RightYaw, YawTo = s.Yaw + angle, Air = !made, AirHeight = height,
                        });
                        Move(!landLeft, new FootMove
                        {
                            T0 = up, T1 = land, From = landLeft ? s.Right : s.Left, To = held,
                            YawFrom = landLeft ? s.RightYaw : s.LeftYaw, YawTo = s.Yaw + angle, Air = !made, AirHeight = height,
                        }, made ? 0 : Rig.HeldFoot);
                        Weight(t0, up, middle);
                        Weight(up, land, target);
                        s.On = landLeft ? 'L' : 'R';
                        if (record && !made)
                            _thrown.Add((!landLeft, up, land));
                    }
                    else
                    {
                        bool takeoffLeft = s.On == 'L';
                        bool landLeft = onto == "same" ? takeoffLeft : !takeoffLeft;
                        var takeoff = takeoffLeft ? s.Left : s.Right;
                        // A hop comes down where it went from; a leap puts the other foot down a stride on,
                        // at its own side of standing width.
                        var target = takeoff + travel + (landLeft == takeoffLeft ? default : (landLeft ? leftAfter : -leftAfter) * Rig.Stance);
                        // The foot that does not land is held low, behind the way it went, by the landing foot.
                        var way = travel.Length > 1e-6 ? travel.Normalized() : fwd;
                        var held = target - way * 0.12 + (landLeft ? -leftAfter : leftAfter) * 0.08;
                        // The foot that carries no weight does not wait for the take-off: it leaves the floor as the
                        // bend begins (the back foot of a skip, the swinging leg of a run). Left on the floor while the
                        // body went on over the other foot, a stride behind, it was farther than the leg is long (trial 19).
                        double freeUp = made ? up : t0;
                        Move(landLeft, new FootMove
                        {
                            T0 = landLeft == takeoffLeft ? up : freeUp, T1 = land, From = landLeft ? s.Left : s.Right, To = target,
                            YawFrom = landLeft ? s.LeftYaw : s.RightYaw, YawTo = s.Yaw + angle, Air = !made, AirHeight = height,
                        });
                        Move(!landLeft, new FootMove
                        {
                            T0 = landLeft == takeoffLeft ? freeUp : up, T1 = land, From = landLeft ? s.Right : s.Left, To = held,
                            YawFrom = landLeft ? s.RightYaw : s.LeftYaw, YawTo = s.Yaw + angle, Air = !made, AirHeight = height,
                        }, made ? 0 : Rig.HeldFoot);
                        Weight(t0, up, takeoff);
                        Weight(up, land, target);
                        s.On = landLeft ? 'L' : 'R';
                    }
                    if (angle != 0)
                        Turn(up, land, angle);
                    if (record && !made)
                    {
                        // The knees bend before it as deep as the jump is high (12 cm for 16 cm of air and more; a
                        // skip of a rope bends a little, writing trial 24: 12 cm under 2.7 cm of air was a heavy
                        // bob), and give on landing up to 8 cm, less where there is no time for it. Both are
                        // measured down from standing straight: a plié the height row stands in is part of them.
                        double giveSeconds = TempoMap.Seconds(_tempos, t1) - TempoMap.Seconds(_tempos, land);
                        _jumps.Add((t0, up, land, t1, height, JumpShape.Bend(height), JumpShape.Give(height) * Math.Min(1, giveSeconds / 0.15)));
                    }
                    break;
                }


                case SegKinds.Lift or SegKinds.Land:
                {
                    // A free body hangs where it is told to: no take-off, no arc.
                    if (seg.Dz != 0 || seg.Dx != 0)
                    {
                        var glide = fwd * seg.Dz + leftward * seg.Dx;
                        Move(true, new FootMove { T0 = t0, T1 = t1, From = s.Left, To = s.Left + glide, YawFrom = s.LeftYaw, YawTo = s.LeftYaw });
                        Move(false, new FootMove { T0 = t0, T1 = t1, From = s.Right, To = s.Right + glide, YawFrom = s.RightYaw, YawTo = s.RightYaw });
                        Weight(t0, t1, s.Weight + glide);
                    }
                    else
                    {
                        Weight(t0, t1, s.Weight);
                    }
                    Float(t0, t1, seg.Kind == SegKinds.Lift ? seg.Height : 0);
                    break;
                }

                default:
                    Weight(t0, t1, s.Weight);
                    break;
            }
        }
        PutDown(double.MaxValue);
    }

    private static double Smooth(double u) => u * u * (3 - 2 * u);

    private static double Ease(string kind, double u) => kind switch
    {
        "linear" => u,
        // A sharp word is sharp in WHEN it moves: the binder gives it a short segment just before
        // its beat. Within that it is eased at both ends like any other: a movement that starts
        // at full speed is not one a body makes.
        _ => Smooth(u),
    };

    /// <summary>
    /// Where a part is at t, given its row of segments. A segment goes from wherever the part
    /// was when it began — the one before may not have arrived, if a narrower track took the
    /// part over — to its own target. A hold keeps the part where it was when the hold began.
    /// </summary>

    private static T Chain<T>(List<Seg> segs, double t, Func<Seg?, T> target, Func<T, T, double, T> blend, Func<T, T, double>? distance = null)
    {
        int current = -1;
        for (int i = 0; i < segs.Count && segs[i].T0 <= t; i++)
            current = i;
        return ChainAt(segs, current, t, target, blend, 0, distance);
    }

    private static double Dist(double a, double b) => Math.Abs(b - a);

    private static double Dist(Vec3 a, Vec3 b)
    {
        // How far the far end goes when Blend carries one offset to the other: round the arc, and in or out.
        double la = a.Length, lb = b.Length;
        if (la < 1e-9 || lb < 1e-9)
            return Math.Abs(lb - la);
        double angle = Math.Acos(Math.Clamp(a.Dot(b) / (la * lb), -1, 1));
        return angle * (la + lb) / 2 + Math.Abs(lb - la);
    }

    /// <summary>
    /// The slopes (in each segment's own eased 0..1) at the word passed through between segs[k] and segs[k + 1],
    /// so that the part goes through it at ONE speed: 1.5 times its mean speed over both segments, as one eased
    /// way over both would at its middle, but no more than either side can take without overshooting (slope 3).
    /// </summary>
    private static (double End, double Start) ThroughSlopes<T>(List<Seg> segs, int k, Func<Seg?, T> target, Func<T, T, double, T> blend, int depth, Func<T, T, double>? distance)
    {
        const double m = 1.5;
        if (distance is null)
            return (m, m);
        Seg a = segs[k], b = segs[k + 1];
        T from = ChainAt(segs, k - 1, a.T0, target, blend, depth + 1, distance), at = target(a), to = target(b);
        double da = distance(from, at), db = distance(at, to), ta = a.T1 - a.T0, tb = b.T1 - b.T0;
        if (da < 1e-9 || db < 1e-9)
            return (0, 0);
        double v = Math.Min(m * (da + db) / (ta + tb), Math.Min(3 * da / ta, 3 * db / tb));
        return (v * ta / da, v * tb / db);
    }

    private static T ChainAt<T>(List<Seg> segs, int index, double t, Func<Seg?, T> target, Func<T, T, double, T> blend, int depth, Func<T, T, double>? distance = null)
    {
        if (index < 0 || depth > 200)
            return target(null);
        var seg = segs[index];
        if (seg.Kind == SegKinds.Hold)
            return ChainAt(segs, index - 1, seg.T0, target, blend, depth + 1, distance);
        // A hand that stays (`stay`) is held where it was from the start of the word: nothing to go to.
        if (seg.Kind == SegKinds.Stay)
            return target(seg);
        double u = seg.T1 > seg.T0 ? Math.Clamp((t - seg.T0) / (seg.T1 - seg.T0), 0, 1) : 1;
        double e = Ease(seg.Ease, u);
        // A word passed through (`@through`, DESIGN.md 86.4, 87): the way to it arrives at speed, and the way on
        // from it leaves at that same speed (ThroughSlopes). Each side is a cubic; neither overshoots.
        // Only where the next word sets out from it at once.
        {
            static bool Joined(Seg a, Seg b) => Math.Abs(a.T1 - b.T0) < 1e-9 && b.Kind is not (SegKinds.Hold or SegKinds.Stay or SegKinds.Release) && b.T1 > b.T0 + 1e-9;
            bool through = seg.Ease == "through" && index + 1 < segs.Count && Joined(seg, segs[index + 1]);
            bool fromThrough = index > 0 && segs[index - 1] is { Ease: "through" } before && Joined(before, seg);
            if ((through || fromThrough) && u > 0 && u < 1)
            {
                double s0 = fromThrough ? ThroughSlopes(segs, index - 1, target, blend, depth, distance).Start : 0;
                double s1 = through ? ThroughSlopes(segs, index, target, blend, depth, distance).End : 0;
                e = (u * u * u - 2 * u * u + u) * s0 + (-2 * u * u * u + 3 * u * u) + (u * u * u - u * u) * s1;
            }
        }
        return e >= 1 ? target(seg) : blend(ChainAt(segs, index - 1, seg.T0, target, blend, depth + 1, distance), target(seg), e);
    }

    /// <summary>From one offset to another: the direction swings, the length changes evenly.</summary>
    private static Vec3 Blend(Vec3 a, Vec3 b, double e, Vec3 via)
    {
        if (e <= 0)
            return a;
        if (e >= 1)
            return b;
        double lengthA = a.Length, lengthB = b.Length;
        Vec3 from = a.Normalized(), to = b.Normalized(), direction;
        if (from.Dot(to) < -0.98 && Math.Abs(from.Dot(via)) < 0.9)
            direction = e < 0.5 ? Vec3.Slerp(from, via, e * 2) : Vec3.Slerp(via, to, (e - 0.5) * 2);
        else
            direction = Vec3.Slerp(from, to, e);
        return direction * (lengthA + (lengthB - lengthA) * e);
    }

    /// <summary>
    /// A leg from one offset from the hip to another: it swings about the hip, the length changing
    /// evenly (a straight leg stays straight). A leg does not go over the top of the hip from behind
    /// to the front (or back): where the shortest way rises above both ends, it passes under, through
    /// down (a ballet dancer's leg brushes through first position).
    /// </summary>
    private static Vec3 LegSwing(Vec3 a, Vec3 b, double e)
    {
        if (e <= 0)
            return a;
        if (e >= 1)
            return b;
        double lengthA = a.Length, lengthB = b.Length, length = lengthA + (lengthB - lengthA) * e;
        if (lengthA < 1e-6 || lengthB < 1e-6)
            return Vec3.Lerp(a, b, e);
        Vec3 from = a.Normalized(), to = b.Normalized();
        bool over = from.Dot(to) < 0 && (from + to).Normalized().Y > Math.Max(from.Y, to.Y) + 0.05
            || from.Dot(to) < -0.98;
        if (!over)
            return Vec3.Slerp(from, to, e) * length;
        // Through down, each half in time as long as its angle.
        var down = -Vec3.Up;
        double first = Math.Acos(Math.Clamp(from.Dot(down), -1, 1)), second = Math.Acos(Math.Clamp(down.Dot(to), -1, 1));
        double split = first + second > 1e-6 ? first / (first + second) : 0.5;
        var direction = e < split ? Vec3.Slerp(from, down, e / split) : Vec3.Slerp(down, to, (e - split) / (1 - split));
        return direction * length;
    }

    /// <summary>The part of a vector square to a unit axis, as a unit vector; zero if there is none.</summary>
    private static Vec3 Across(Vec3 v, Vec3 axis)
    {
        var across = v - axis * v.Dot(axis);
        return across.Length < 1e-6 ? default : across * (1 / across.Length);
    }

    /// <summary>
    /// A direction square to one unit axis, turned with that axis onto another by the shortest
    /// turn. Null where there is no shortest turn (the axes are opposite) or nothing to carry.
    /// </summary>
    private static Vec3? Carry(Vec3 v, Vec3 from, Vec3 to)
    {
        double d = 1 + from.Dot(to);
        if (d < 1e-3 || v.Length < 1e-6)
            return null;
        return v - (from + to) * (v.Dot(to) / d);
    }

    private static Vec3 DirOf(Seg? seg, Vec3 neutral) => seg?.Dir is { } d ? new Vec3(d[0], d[1], d[2]) : neutral;

    private static Vec3 Direction(List<Seg> targets, double t, Vec3 neutral, Vec3 via) =>
        Chain(targets, t, seg => DirOf(seg, neutral), (a, b, e) => Blend(a, b, e, via), Dist).Normalized();

    /// <summary>Which way the figure faces, radians: what its turns have made of where it began.</summary>
    public double FacingAt(double t) => YawAt(t);

    private double YawAt(double t)
    {
        double yaw = _yawStart;
        foreach (var turn in _turns)
        {
            if (t >= turn.T1)
            {
                yaw = turn.To;
                continue;
            }
            if (t > turn.T0)
                yaw = turn.From + (turn.To - turn.From) * Smooth((t - turn.T0) / (turn.T1 - turn.T0));
            break;
        }
        return yaw;
    }

    private static void FootAt(List<FootMove> moves, Vec3 start, double startYaw, double t, out Vec3 pos, out double yaw, out double lift, out bool air, out double pitch)
    {
        pos = start;
        yaw = startYaw;
        lift = 0;
        air = false;
        pitch = 0;
        foreach (var move in moves)
        {
            if (t >= move.T1)
            {
                pos = move.To;
                yaw = move.YawTo;
                lift = move.LiftTo;   // a foot left in the air stays up until its next move
                continue;
            }
            if (t <= move.T0)
                break;

            double u = (t - move.T0) / (move.T1 - move.T0), s = Smooth(u);
            if (move.Orbit)
            {
                pos = move.Pivot + (move.From - move.Pivot).RotateY(move.Angle * s);
                yaw = move.YawFrom + move.Angle * s;
                lift = move.Lift * Math.Sin(Math.PI * u) + move.LiftFrom + (move.LiftTo - move.LiftFrom) * s;
            }
            else if (move.Air)
            {
                pos = Vec3.Lerp(move.From, move.To, u) + move.Open * Math.Sin(Math.PI * u);
                yaw = move.YawFrom + (move.YawTo - move.YawFrom) * s;
                lift = move.AirHeight * 4 * u * (1 - u) + move.LiftFrom + (move.LiftTo - move.LiftFrom) * u;
                air = true;
                // Off the floor the foot points: the push off the toes leaves it pointed at once, and it comes back
                // under the leg only to land, toes first (DESIGN.md 90: it hung level through the air). The body has
                // risen onto the toes to leave the floor, so the ankle is higher by what the pointed foot reaches down:
                // the height of the jump is how far the toes clear the floor (trial 28: a 5 cm skip never left it).
                if (move.AirHeight > 1e-6)
                {
                    pitch = AirPitch(u, move.AirHeight);
                    lift += OntoTheToes(pitch);
                }
            }
            else
            {
                pos = Vec3.Lerp(move.From, move.To, s) + move.Open * Math.Sin(Math.PI * s);
                yaw = move.YawFrom + (move.YawTo - move.YawFrom) * s;
                // The foot comes up early in the step and goes on low to land, as a leg swings: the heel has risen
                // and the weight has gone over before the step begins, so the toes leave the floor at once (DESIGN.md 103.
                // Even, the ankle rose slower than the toes pointed down, and the toes stayed on the floor a third of the step).
                double w = Math.Pow(u, SwingEarly);
                // Highest a third of the way, and low again as the foot passes under the body: it goes on forward
                // just over the floor and the leg straightens before it lands (DESIGN.md 106. Even, the foot was still high
                // passing under the hips, and the knee bent most there and straightened in the last frame).
                double high = Math.Sin(Math.PI * Math.Pow(u, LiftEarly));
                lift = move.Lift * high * high * high + move.LiftFrom + (move.LiftTo - move.LiftFrom) * s;
                // A step forward rolls through the foot: it leaves heel first, toes last, and
                // lands on the heel with the toes up. A step back does the reverse.
                // Leaving, the toes point down only a little: the heel rose before the step (RollShare), and pointed further
                // the toes would hold the floor while the ankle rose (DESIGN.md 103).
                pitch = -move.Along * (w < 0.5 ? 0.3 : 0.5) * Math.Sin(2 * Math.PI * w);
            }
            break;
        }
    }

    /// <summary>How far a foot about to step may rise onto its toes for its leg to reach, radians (the heel 11 cm up).</summary>
    private const double RollMost = 0.75;

    /// <summary>
    /// An unwritten arm's swing while walking (DESIGN.md 101): radians per metre the other foot is ahead of the
    /// pelvis, the most either way, and the degrees the elbow bends further at the front of the swing.
    /// </summary>
    private const double SwingPerMetre = 1.2, SwingMost = 0.4, SwingElbow = 20;

    /// <summary>
    /// How far back of hanging the swing of an arm is centred, radians, and over how long either side the stride
    /// it follows is taken, seconds (DESIGN.md 106).
    /// </summary>
    private const double SwingBack = 0.12, ArmLag = 0.1;

    /// <summary>Half the stride between the feet along <paramref name="fwd"/> at <paramref name="t"/>: how far the right foot is ahead of the middle of the two.</summary>
    private double HalfStride(double t, Vec3 fwd)
    {
        FootAt(_leftMoves, _leftStart, _leftYawStart, t, out var l, out _, out _, out _, out _);
        FootAt(_rightMoves, _rightStart, _rightYawStart, t, out var r, out _, out _, out _, out _);
        return 0.5 * (r - l).Dot(fwd);
    }

    /// <summary>
    /// Unwritten hips and shoulders turning while walking (DESIGN.md 102): radians per metre the left foot is
    /// ahead of the right. A stride of 0.6 m turns the hips 4° with the leg in front and the shoulders 3° the other way.
    /// </summary>
    private const double HipsPerMetre = 0.12, ShouldersPerMetre = 0.09;
    /// <summary>
    /// How far the toes are below the ankle in a level foot: a foot lifted level has its toes as far above the floor
    /// as its ankle has risen (it was 0.05, and a foot that began to lift flicked its toes up 1 cm and down again. DESIGN.md 103).
    /// </summary>
    private const double ToeBelowAnkle = Rig.AnkleHeight - FloorToe;

    /// <summary>
    /// The least angle at the ankle, in degrees, between the shin (to the knee) and the foot (to the toes) of a foot off
    /// the floor. Standing it is 109° (the toes are below the ankle); 100° is the toes pulled up some 9°.
    /// </summary>
    private const double LeastAnkle = 100;

    /// <summary>The toe of a foot off the floor, turned down about its ankle as far as the ankle needs to open to <see cref="LeastAnkle"/>; not below the floor.</summary>
    private static Vec3 AnkleKept(Vec3 knee, Vec3 ankle, Vec3 toe)
    {
        var shin = knee - ankle;
        var foot = toe - ankle;
        if (shin.Length < 1e-9 || foot.Length < 1e-9)
            return toe;
        double angle = Math.Acos(Math.Clamp(shin.Normalized().Dot(foot.Normalized()), -1, 1));
        double least = LeastAnkle * Math.PI / 180;
        if (angle >= least)
            return toe;
        var axis = shin.Cross(foot);
        if (axis.Length < 1e-9)
            return toe;
        axis = axis.Normalized();
        // Turned about the line across the ankle, away from the shin (Rodrigues).
        double turn = least - angle, cos = Math.Cos(turn), sin = Math.Sin(turn);
        var turned = foot * cos + axis.Cross(foot) * sin + axis * (axis.Dot(foot) * (1 - cos));
        var kept = ankle + turned;
        return kept.Y < FloorToe ? new Vec3(kept.X, FloorToe, kept.Z) : kept;
    }

    /// <summary>How early in a step on the floor the foot comes up: the step's time is raised to this power (1 is even).</summary>
    private const double SwingEarly = 0.7;

    /// <summary>How early in a step on the floor the foot is highest: its time to this power is half-way at the top (a third of the step).</summary>
    private const double LiftEarly = 0.55;

    /// <summary>How long before a step its foot may begin to rise onto its toes, seconds.</summary>
    private const double RollAhead = 0.3;

    /// <summary>
    /// How far a foot may rise onto its toes at <paramref name="t"/>, 0..1: as the body goes on over the
    /// other foot, the leg behind stays long by lifting its heel instead of pulling the hips down (DESIGN.md 100).
    /// From a little before a step on the floor until it is half-way; not a slide, not a foot in the air or turning.
    /// </summary>
    private double RollShare(List<FootMove> moves, double t)
    {
        foreach (var move in moves)
        {
            if (t >= move.T1)
                continue;
            if (move.Orbit || move.Air || move.Lift <= 0 || move.LiftFrom > 0)
                return 0;
            // (t is in beats. It was divided by the seconds as they were, so the heel had 0.18 s at tempo 100. DESIGN.md 105.)
            if (t <= move.T0)
                return Smooth(Math.Clamp(1 - (move.T0 - t) / (RollAhead * PerSecond(t)), 0, 1));
            double u = (t - move.T0) / (move.T1 - move.T0);
            return 1 - Smooth(Math.Clamp((u - 0.4) / 0.2, 0, 1));
        }
        return 0;
    }

    /// <summary>
    /// How far the standing leg straightens out of soft knees at <paramref name="t"/>, 0..1 (DESIGN.md 107): the body
    /// goes up over the standing foot as the other foot passes it, most half-way through a step on the floor.
    /// </summary>
    private double Vault(double t)
    {
        double most = 0;
        foreach (var moves in new[] { _leftMoves, _rightMoves })
            foreach (var move in moves)
            {
                if (t < move.T0 || t >= move.T1)
                    continue;
                if (move.Orbit || move.Air || move.Lift <= 0 || move.LiftFrom > 0)
                    break;
                double s = Math.Sin(Math.PI * (t - move.T0) / (move.T1 - move.T0));
                most = Math.Max(most, s * s);
                break;
            }
        return most;
    }

    /// <summary>How far past the reach the heel goes up as a foot pushes off, radians, and from how long before its step (seconds).</summary>
    private const double PushMore = 0.4, PushAhead = 0.45;

    /// <summary>
    /// How much a foot is pushing off at <paramref name="t"/>, 0..1 (DESIGN.md 105): rising over the last moments before its step on
    /// the floor, and gone as the step is a third of the way. The same steps as <see cref="RollShare"/>.
    /// </summary>
    private double PushShare(List<FootMove> moves, double t)
    {
        foreach (var move in moves)
        {
            if (t >= move.T1)
                continue;
            if (move.Orbit || move.Air || move.Lift <= 0 || move.LiftFrom > 0)
                return 0;
            // A step near the start pushes off only as much as it has had time to since the start: the body stood
            // still before it, and the first frame is the stance as written (DESIGN.md 106. Its heel was up at the
            // first frame, and `check` measured a walk from where that foot landed).
            // (A loop has the end of its last time round before it.)
            double ahead = PushAhead * PerSecond(t), had = _loop ? ahead : Math.Min(ahead, move.T0);
            if (had <= 1e-9)
                return 0;
            double ready = had / ahead;
            if (t <= move.T0)
                return ready * Smooth(Math.Clamp(1 - (move.T0 - t) / had, 0, 1));
            double u = (t - move.T0) / (move.T1 - move.T0);
            return ready * (1 - Smooth(Math.Clamp((u - 0.1) / 0.25, 0, 1)));
        }
        return 0;
    }

    /// <summary>Whether the hip has gone on past a foot the way it points (a foot behind rises onto its toes; one ahead or aside does not).</summary>
    private static bool Behind(Vec3 hip, Vec3 foot, Vec3 fwd) => (hip.X - foot.X) * fwd.X + (hip.Z - foot.Z) * fwd.Z > 0.05;

    /// <summary>Where a foot's toes are with its ankle over <paramref name="floor"/>, <paramref name="lift"/> up and pitched by <paramref name="pitch"/> (a step's roll), as the leg draws it.</summary>
    private static Vec3 ToeOf(Vec3 floor, Vec3 fwd, double lift, double pitch)
    {
        double ankle = Rig.AnkleHeight + lift;
        var toe = new Vec3(floor.X, 0, floor.Z) + fwd * (Rig.FootLength * Math.Cos(pitch));
        return new Vec3(toe.X, lift > 0.005 ? Math.Max(FloorToe, ankle - ToeBelowAnkle + Rig.FootLength * Math.Sin(pitch)) : FloorToe, toe.Z);
    }

    /// <summary>
    /// The ankle of a foot (its ankle over <paramref name="floor"/>, <paramref name="lift"/> up, pitched by <paramref name="pitch"/>)
    /// turned on by <paramref name="angle"/> about its toes, the heel going up. At 0 it is where the step has it.
    /// </summary>
    private static Vec3 RolledAnkle(Vec3 floor, Vec3 fwd, double angle, double lift = 0, double pitch = 0)
    {
        var toe = ToeOf(floor, fwd, lift, pitch);
        var v = new Vec3(floor.X, Rig.AnkleHeight + lift, floor.Z) - toe;
        double along = v.X * fwd.X + v.Z * fwd.Z;
        var aside = new Vec3(v.X - fwd.X * along, 0, v.Z - fwd.Z * along);
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        return toe + aside + fwd * (along * cos + v.Y * sin) + Vec3.Up * (-along * sin + v.Y * cos);
    }

    /// <summary>How far a foot in the air points, radians (about 35°: the toe 9 cm further below the ankle than standing).</summary>
    private const double AirPoint = 0.6;

    /// <summary>The foot about its ankle a part <paramref name="u"/> of the way through the air: pointed soon after the take-off, back to land.</summary>
    private static double AirPitch(double u, double height) =>
        -AirPoint * Math.Clamp(height / 0.12, 0.4, 1) * Smooth(Math.Min(1, u / 0.35)) * Smooth(Math.Min(1, (1 - u) / 0.4));

    /// <summary>How much higher the ankle is when the foot points so far down (<paramref name="pitch"/>, negative) with its toe where it was.</summary>
    private static double OntoTheToes(double pitch) => Rig.FootLength * Math.Sin(Math.Max(0, -pitch));

    /// <summary>How far apart two ankles pass at the least, metres: a foot's width and a little.</summary>
    private const double AnkleClearance = 0.09;
    /// <summary>How near the standing ankle both ends of a leg's way must be for it to go round it, metres.</summary>
    private const double NearTheStandingFoot = 0.25;

    /// <summary>
    /// A leg's ankle on its way between the floor (<paramref name="floor"/>, at 0) and a shape
    /// (<paramref name="shape"/>, at 1), seen from above as turning about the standing ankle: the
    /// angle the shorter way round, the distance from one end's to the other's. The height is the
    /// swing's. Only for a way whose two ends are both by the standing foot (writing trial 24,
    /// DESIGN.md 85: from a coupé behind into fifth the foot went through the standing ankle).
    /// </summary>
    private static Vec3 RoundTheStandingFoot(Vec3 swung, Vec3 floor, Vec3 shape, Vec3 standing, double gesture)
    {
        double fx = floor.X - standing.X, fz = floor.Z - standing.Z, sx = shape.X - standing.X, sz = shape.Z - standing.Z;
        double rf = Math.Sqrt(fx * fx + fz * fz), rs = Math.Sqrt(sx * sx + sz * sz);
        if (rf > NearTheStandingFoot || rs > NearTheStandingFoot || rf < 0.01 || rs < 0.01)
            return swung;
        double af = Math.Atan2(fz, fx), turn = Math.Atan2(sz, sx) - af;
        turn = Math.IEEERemainder(turn, 2 * Math.PI);
        double angle = af + turn * gesture, r = rf + (rs - rf) * gesture;
        return new Vec3(standing.X + r * Math.Cos(angle), swung.Y, standing.Z + r * Math.Sin(angle));
    }

    /// <summary>
    /// The bow a foot's way on the floor takes so as not to run through the standing foot: closing
    /// from behind into fifth in front, or from a coupé behind to fifth behind, the straight line
    /// goes through the standing ankle (writing trials 23 and 24, DESIGN.md 85). Nothing for a way
    /// that passes clear of it, or that only begins or ends near it (fifth itself is close).
    /// The bow goes to the side the standing foot is not on, or outward when it is on the line.
    /// </summary>
    private static Vec3 GoRound(Vec3 from, Vec3 to, Vec3 standing, Vec3 outward)
    {
        Vec3 Flat(Vec3 v) => new(v.X, 0, v.Z);
        Vec3 a = Flat(from), b = Flat(to), p = Flat(standing), way = b - a;
        double length2 = way.Dot(way);
        if (length2 < 1e-6)
            return default;
        double u = Math.Clamp((p - a).Dot(way) / length2, 0, 1);
        if (u < 0.15 || u > 0.85)
            return default;
        var nearest = a + way * u;
        var aside = nearest - p;
        double apart = aside.Length;
        if (apart >= AnkleClearance)
            return default;
        var across = new Vec3(-way.Z, 0, way.X).Normalized();
        var away = apart > 0.01 ? (aside.Dot(across) >= 0 ? across : across * -1) : (outward.Dot(across) >= 0 ? across : across * -1);
        // The bow is a half sine over the way: at the nearest point it has grown to sin(π u) of itself.
        return away * Math.Min(0.15, (AnkleClearance - apart) / Math.Sin(Math.PI * u));
    }

    /// <summary>When the leg's word that takes it back to the floor (a `skip`, a `down`) at <paramref name="t"/> ends; null if it is not on one.</summary>
    private static double? FadeEnd(List<Seg> row, double t)
    {
        foreach (var seg in row)
        {
            if (t >= seg.T0 && t < seg.T1)
                return seg.Kind == SegKinds.Release || IsDown(seg) ? seg.T1 : null;
        }
        return null;
    }

    private static bool IsDown(Seg seg) => seg.Dir is { } d && d[1] < -0.95;

    /// <summary>How far a limb reaches, root to end. A straight one a hair short of its length, so the joint still knows which way it bends.</summary>
    private static double Reach(Seg seg, double upper, double lower, double straight) =>
        (seg.Bend is double bend && bend > 0 ? Math.Min(straight, Rig.BentShare(upper, lower, bend)) : straight) * (upper + lower);

    /// <summary>
    /// A free leg: how far it is off the floor (0..1), where its ankle is from the hip, and which way
    /// the knee is sent where the text says so (`@knee(...)`; null: the way its foot points, read at
    /// the moment). A toe sent to the other leg (`otherKnee`, `otherAnkle`) is put there by `place`.
    /// Which way the knee points is decided at the poses the text names and carried between them
    /// with the leg, as an elbow's is.
    /// </summary>
    private static LegPose LegShape(List<Seg> targets, double t, Vec3 left, Vec3 fwd, Func<Seg, Vec3> place, Vec3 kneePole, (Vec3 Left, Vec3 Up, Vec3 Fwd)? torso = null)
    {
        LegPose Target(Seg? seg)
        {
            // The room's frame, or with `.body` the torso's.
            var (l, u, f) = seg is { Body: true } && torso is { } body ? body : (left, Vec3.Up, fwd);
            Vec3 offset;
            if (seg is { Kind: SegKinds.Anchor } or { Floor: true })
                offset = place(seg);
            else if (seg is null || IsDown(seg) && !seg.Body || seg.Dir is not { } d)
                return default;
            else
                offset = (l * d[0] + u * d[1] + f * d[2]) * Reach(seg, Rig.Thigh, Rig.Shin, Rig.StraightLeg);
            Vec3? bend = seg.Pole is { } q ? Across(l * q[0] + u * q[1] + f * q[2], offset.Normalized()) : null;
            return new LegPose(1, offset, bend, seg.Kind == SegKinds.Anchor || seg.Floor ? 1 : 0);
        }

        return Chain(targets, t, Target, (a, b, e) =>
        {
            double weight = a.Weight + (b.Weight - a.Weight) * e;
            double point = a.Point + (b.Point - a.Point) * e;
            // Coming off the floor, or going back to it, the leg keeps the one shape it has.
            if (a.Weight < 1e-6)
                return b with { Weight = weight, Point = point };
            if (b.Weight < 1e-6)
                return a with { Weight = weight, Point = point };
            var offset = LegSwing(a.Offset, b.Offset, e);
            if (a.Bend is null && b.Bend is null)
                return new LegPose(weight, offset, null, point);
            var axis = offset.Normalized();
            Vec3 Own(Vec3? bend, Vec3 at) => bend ?? Across(kneePole, at.Normalized());
            Vec3? from = Carry(Own(a.Bend, a.Offset), a.Offset.Normalized(), axis), to = Carry(Own(b.Bend, b.Offset), b.Offset.Normalized(), axis);
            var between = from is null || to is null ? from ?? to ?? kneePole : Vec3.Slerp(from.Value, to.Value, e);
            return new LegPose(weight, offset, Across(between, axis), point);
        }, (a, b) => Dist(a.Offset, b.Offset) + Math.Abs(b.Weight - a.Weight));
    }

    /// <summary>
    /// A free leg at a moment: how far it is off the floor (0..1), where its ankle is from the hip,
    /// which way the knee is sent (null: where the foot points), and how far the foot is pointed on
    /// along the shin (1 for a toe sent to the other leg) rather than lying along the way it faces.
    /// </summary>
    private readonly record struct LegPose(double Weight, Vec3 Offset, Vec3? Bend, double Point);

    /// <summary>Where a toe sent to the other leg is, before that leg is solved: only to say the leg is off the floor.</summary>
    /// <summary>How high a toe on the floor is drawn, metres (the toe joint above the sole).</summary>
    private const double FloorToe = 0.02;

    private static Vec3 SomewhereBelow(Seg seg) => -Vec3.Up * (Rig.Leg * 0.6);

    private double Clamp(double t) => _loop ? t : Math.Clamp(t, 0, _length);

    /// <summary>How far each leg is turned out in its socket, radians (negative: in).</summary>
    private double TurnoutAt(double t) => Chain(_turnout, t, seg => (seg?.TurnDeg ?? 0) * Math.PI / 180, (a, b, e) => a + (b - a) * e, Dist);

    /// <summary>
    /// How far apart the ankles stand when the feet close: standing width with the legs parallel,
    /// the heels together (<see cref="Rig.HeelsTogether"/>) with them turned out all the way, as a
    /// dancer's first position.
    /// </summary>
    private double ClosedWidth(double t)
    {
        // (The width a row begins at: standing width, the heels together as far as it is turned out.)
        double turnout = Math.Max(0, TurnoutAt(t));
        return Rig.Stance - (Rig.Stance - Rig.HeelsTogether) * Math.Sin(turnout);
    }

    /// <summary>How far apart the ankles are when the feet close touching (`together`): side by side, the heels together as far as the legs are turned out.</summary>
    private double TouchWidth(double t)
    {
        double turnout = Math.Max(0, TurnoutAt(t));
        return Rig.Together - (Rig.Together - Rig.HeelsTogether) * Math.Sin(turnout);
    }

    private CoreState Core(double t)
    {
        var c = new CoreState { Yaw = YawAt(t) };
        c.Fwd = Vec3.Forward(c.Yaw);
        c.Left = Vec3.Left(c.Yaw);
        FootAt(_leftMoves, _leftStart, _leftYawStart, t, out c.LPos, out c.LYaw, out c.LLift, out c.LAir, out c.LPitch);
        FootAt(_rightMoves, _rightStart, _rightYawStart, t, out c.RPos, out c.RYaw, out c.RLift, out c.RAir, out c.RPitch);
        // A leg turned in its socket turns its foot about the heel: the left one to the left.
        double turnout = TurnoutAt(t);
        c.LYaw += turnout;
        c.RYaw -= turnout;
        c.GwL = LegShape(_leftLeg, t, c.Left, c.Fwd, SomewhereBelow, Vec3.Up).Weight;
        c.GwR = LegShape(_rightLeg, t, c.Left, c.Fwd, SomewhereBelow, Vec3.Up).Weight;
        c.LRoll = RollShare(_leftMoves, t) * (1 - c.GwL);
        c.RRoll = RollShare(_rightMoves, t) * (1 - c.GwR);
        c.Float = _float.Eval(t);

        // The weight goes over the standing foot as far as the other leg is off the floor
        // (a body that hangs in the air has no weight to shift).
        var p = new Vec3(_weightX.Eval(t), 0, _weightZ.Eval(t));
        // Standing still on both feet, the weight drifts from one to the other and back (DESIGN.md 94).
        double still = _alive && c.Float < 0.01 && !c.LAir && !c.RAir ? FeetStill(t) * (1 - c.GwL) * (1 - c.GwR) : 0;
        double drift = Wave(t, _sway) * still;
        if (still > 0)
        {
            var across = c.LPos - c.RPos;
            double apart = across.Length;
            // As far as a third of the way to each foot, and no more than 5 cm.
            if (apart > 1e-6)
                p += across * (Math.Min(0.05, apart / 3) * drift / apart);
        }
        // Mills' Mess: the body sways from the feet as well as at the waist (the user: the legs too, a little from the
        // floor): the pelvis goes over to the side the back leans to, the legs leaning from the ankles.
        if (Juggling is { } juggler && juggler.LegLean(Seconds(t)) is var legSway && legSway != 0)
            p += c.Left * (legSway * MillsLegSway);
        if (c.Float < 0.01)
        {
            if (c.GwL > 0)
                p = Vec3.Lerp(p, c.RPos, 0.85 * c.GwL);
            if (c.GwR > 0)
                p = Vec3.Lerp(p, c.LPos, 0.85 * c.GwR);
        }

        var torso = Direction(_torso, t, Vec3.Up, new Vec3(0, 0, 1));
        if (_alive && _torso.Count == 0)
        {
            // An unwritten torso breathes (a degree each way), goes a little back and up as the arms rise or
            // reach forward, leans away from the one arm raised, and over the foot the weight drifts to.
            var (upL, fwdL) = ArmRaise(_leftArm, t);
            var (upR, fwdR) = ArmRaise(_rightArm, t);
            double back = 0.05 * (upL + upR) / 2 + 0.03 * (fwdL + fwdR) / 2;
            double sideways = 0.05 * (upR - upL) + 0.02 * drift;
            torso = new Vec3(sideways, 1, 0.02 * Wave(t, _breath) - back).Normalized();
        }
        // Mills' Mess: the back sways at the waist to the side of the arm that crosses below, the shoulder of the one above
        // up, so the two have room (DESIGN.md 118.12). Where the torso's row says nothing; after the unwritten torso's own
        // sway, which set it afresh and had undone this.
        if (_torso.Count == 0 && Juggling is { } swaying && swaying.SideLean(Seconds(t)) is var sway && sway != 0)
            torso = new Vec3(torso.X + sway, torso.Y, torso.Z).Normalized();
        c.TorsoUp = (c.Left * torso.X + Vec3.Up * torso.Y + c.Fwd * torso.Z).Normalized();
        c.Round = Chain(_torso, t, seg => seg is { Round: true } ? 1.0 : 0.0, (a, b, e) => a + (b - a) * e, Dist);
        c.Shoulders = Chain(_shoulders, t, seg => (seg?.TurnDeg ?? 0) * Math.PI / 180, (a, b, e) => a + (b - a) * e, Dist);
        // The hips turn over the feet: the sockets go round the pelvis, the feet stay, and the knees follow.
        c.Hips = Chain(_hips, t, seg => (seg?.TurnDeg ?? 0) * Math.PI / 180, (a, b, e) => a + (b - a) * e, Dist);
        // Walking, unwritten hips go round with the leg in front (its hip forward) and unwritten shoulders the
        // other way, with the arms that swing against the legs (DESIGN.md 102). Only as much as the feet move.
        double stride = 0;
        if (_alive && (_hips.Count == 0 || _shoulders.Count == 0))
            stride = Math.Clamp((c.LPos - c.RPos).Dot(c.Fwd), -0.8, 0.8) * (1 - FeetStill(t));
        if (_shoulders.Count == 0)
            c.Shoulders += ShouldersPerMetre * stride;
        // Mills' Mess: the shoulders go with the arm that crosses (DESIGN.md 118.12).
        if (_shoulders.Count == 0 && Juggling is { } juggled)
            c.Shoulders += juggled.ShoulderTurn(Seconds(t));
        // Unwritten hips turn a little with the drift of the weight, toward the leg that takes it (3.4°).
        if (_alive && _hips.Count == 0)
            c.Hips -= 0.06 * drift + HipsPerMetre * stride;
        // Juggling behind the back: the hips go round with the shoulders (DESIGN.md 118.38).
        if (_hips.Count == 0 && Juggling is { } juggledHips)
            c.Hips += juggledHips.HipTurn(Seconds(t));
        c.HipLeft = c.Left * Math.Cos(c.Hips) - c.Fwd * Math.Sin(c.Hips);
        c.HipFwd = c.Fwd * Math.Cos(c.Hips) + c.Left * Math.Sin(c.Hips);
        // A leaning torso is balanced by the hips going the other way. Sideways only as far as it costs the
        // legs that stand next to no height: over crossed feet (a fifth) the hips going aside take one
        // hip away from its foot, the pelvis comes down to that leg's reach, and the other knee bent 23° in a
        // side bend written on straight legs (writing trial 22). A real body tilts its pelvis there; this one
        // keeps the hip sockets level, so it keeps the hips over the feet instead.
        bool freeL = c.LAir || c.GwL > 0.05, freeR = c.RAir || c.GwR > 0.05;
        var lean = new Vec3(c.TorsoUp.X, 0, c.TorsoUp.Z) * 0.18;
        var aside = c.Left * lean.Dot(c.Left);
        p -= lean - aside;
        if (aside.Length > 1e-6 && !freeL && !freeR)
        {
            double full = Rig.Leg * Rig.StraightLeg;
            double StandOver(Vec3 q) => Math.Min(Cap(q + c.HipLeft * Rig.HipHalf, c.LPos, false, 0, full), Cap(q - c.HipLeft * Rig.HipHalf, c.RPos, false, 0, full));
            // A knee is quick to show what a leg has to spare: 0.5 mm is 4°, 3 mm already 10°.
            const double spare = 0.0005;
            double before = StandOver(p);
            double share = 1;
            if (StandOver(p - aside) < before - spare)
            {
                double lo = 0, hi = 1;
                for (int i = 0; i < 14; i++)
                {
                    double mid = (lo + hi) / 2;
                    if (StandOver(p - aside * mid) < before - spare)
                        hi = mid;
                    else
                        lo = mid;
                }
                share = lo;
            }
            aside *= share;
        }
        p -= aside;
        c.P = p;

        // The height is a row of its own, read like a gesture: it can be taken over half-way.
        double level = Chain(_height, t, seg => seg?.Level ?? 0, (a, b, e) => a + (b - a) * e, Dist);
        c.Raise = Math.Max(0, level) * 0.07;
        double reach = Rig.Leg * Rig.StraightLeg;
        double top = Rig.AnkleHeight + reach + c.Raise;
        // `mid` stands on soft knees, `straight` and `high` on straight legs; `low` is measured from straight.
        double soft = Chain(_height, t, seg => seg is { Straight: true } ? 0.0 : 1.0, (a, b, e) => a + (b - a) * e, Dist);
        double softDrop = soft * (1 - Vault(t)) * Rig.Leg * (Rig.StraightLeg - Rig.BentShare(Rig.Thigh, Rig.Shin, Rig.SoftKnee));

        // The pelvis is as high as it can be with both planted feet still in reach.
        double Cap(Vec3 hip, Vec3 foot, bool free, double raise, double reach, double yaw = 0, double roll = 0, double lift = 0, double pitch = 0)
        {
            if (free)
                return double.MaxValue;
            double distance = (new Vec3(hip.X, 0, hip.Z) - new Vec3(foot.X, 0, foot.Z)).Length;
            double flat = Rig.AnkleHeight + raise + Math.Sqrt(Math.Max(0.04, reach * reach - distance * distance));
            if (roll <= 0 || raise > 1e-6 || !Behind(hip, foot, Vec3.Forward(yaw)))
                return flat;
            // A foot about to step may rise onto its toes: the hips can be as high as the leg reaches to its ankle then.
            var rolled = RolledAnkle(foot, Vec3.Forward(yaw), RollMost * roll, lift, pitch);
            double far = (new Vec3(hip.X, 0, hip.Z) - new Vec3(rolled.X, 0, rolled.Z)).Length;
            return Math.Max(flat, rolled.Y + raise + Math.Sqrt(Math.Max(0.04, reach * reach - far * far)));
        }

        double stand = Math.Min(top, Math.Min(Cap(p + c.HipLeft * Rig.HipHalf, c.LPos, freeL, c.Raise, reach, c.LYaw, c.LRoll, c.LLift, c.LPitch),
                                              Cap(p - c.HipLeft * Rig.HipHalf, c.RPos, freeR, c.Raise, reach, c.RYaw, c.RRoll, c.RLift, c.RPitch)));
        // How far the feet hold the hips under the top (a stance, a stride): this has corners where a
        // foot lands, and only this is smoothed (TrunkAt). The height row is smooth already, and is
        // measured down from where the dancer stands: `low` is bent knees, not a squat, the hips
        // 16 cm lower (a deep crouch is `low(40cm)`), and `low(12cm)` is 12 cm below standing straight in the same place.
        c.Drop = Math.Max(softDrop, Rig.LowDrop * Math.Max(0, -level));
        c.Deficit = stand - top;
        // The weight carried off the middle of two planted feet (a shift, a lunge, DESIGN.md 87): the far leg
        // straightens out of the bend the height row has it in before the hips must go lower. `low(15cm)` over
        // the right foot is 15 cm down with the left leg straight, not 15 cm and the shift's sink again.
        if (!freeL && !freeR && c.Drop > 0)
        {
            var q = (c.LPos + c.RPos) * 0.5;
            double standMid = Math.Min(top, Math.Min(Cap(q + c.HipLeft * Rig.HipHalf, c.LPos, false, c.Raise, reach),
                                                     Cap(q - c.HipLeft * Rig.HipHalf, c.RPos, false, c.Raise, reach)));
            if (stand < standMid)
                c.Deficit += Math.Min(c.Drop, standMid - stand);
        }
        c.WantY = top - c.Drop;
        // The most the legs reach at all: the smoothed height may not go over it, or a planted foot would come up.
        c.CeilingY = Math.Min(Cap(p + c.HipLeft * Rig.HipHalf, c.LPos, freeL, c.Raise, (Rig.Leg - 2e-4), c.LYaw, c.LRoll, c.LLift, c.LPitch),
                              Cap(p - c.HipLeft * Rig.HipHalf, c.RPos, freeR, c.Raise, (Rig.Leg - 2e-4), c.RYaw, c.RRoll, c.RLift, c.RPitch));
        return c;
    }

    /// <summary>
    /// What a jump does to the height of the hips at a moment, on top of the height row. `drop` is how far
    /// the row stands below standing straight (a plié, soft knees): the bend and the give are measured from
    /// standing straight, so a plié already stood in is part of them, and the legs are straight from the
    /// take-off to the landing (DESIGN.md 84.5: a plié written before a jump was a second bend under it).
    /// </summary>
    /// <summary>A landing's give and the next jump's bend are one when no more than this many seconds stand between them.</summary>
    private const double JoinGap = 0.15;
    private double PerSecond(double t) => TempoMap.BpmAt(_tempos, Math.Max(0, t)) / 60;

    private double JumpOffset(double t, double drop)
    {
        static double Smooth(double x) => x <= 0 ? 0 : x >= 1 ? 1 : x * x * (3 - 2 * x);
        for (int i = 0; i < _jumps.Count; i++)
        {
            var jump = _jumps[i];
            // Jump after jump (jumping jacks, a skip of a rope): the give of one landing is the bend of the next
            // take-off, one going down and up between them. Two (the give, back up, the bend) shook the knees
            // twice in a quarter of a second (DESIGN.md 90).
            if (i + 1 < _jumps.Count && _jumps[i + 1] is var next && next.T0 <= jump.T1 + 1e-9 + JoinGap * PerSecond(jump.T1)
                && t >= jump.Land && t < next.Up && next.Up > jump.Land + 1e-9)
            {
                double w = (t - jump.Land) / (next.Up - jump.Land);
                return drop - Math.Max(drop, Math.Max(jump.Give, next.Bend)) * Math.Sin(Math.PI * w);
            }
            if (i > 0 && _jumps[i - 1] is var last && jump.T0 <= last.T1 + 1e-9 + JoinGap * PerSecond(last.T1) && t >= last.Land && t < jump.Up)
                continue;
            if (t < jump.T0 || t > jump.T1)
                continue;
            if (t < jump.Up)
            {
                // Down to the bend (only what the plié does not give already), then up onto straight legs.
                double u = (t - jump.T0) / (jump.Up - jump.T0);
                return -Math.Max(0, jump.Bend - drop) * Math.Sin(Math.PI * u) + drop * Smooth(2 * u - 1);
            }
            if (t < jump.Land)
            {
                double u = (t - jump.Up) / (jump.Land - jump.Up);
                // Up onto the pointed toes, as the feet are (FootAt): the height is the toes' off the floor.
                return drop + jump.Height * 4 * u * (1 - u) + (jump.Height > 1e-6 ? OntoTheToes(AirPitch(u, jump.Height)) : 0);
            }
            // Straight legs touch, and give into the height row's plié, and as much more as the give is deeper.
            if (jump.T1 - jump.Land < 1e-9)
                return 0;
            double v = (t - jump.Land) / (jump.T1 - jump.Land);
            return drop * (1 - Smooth(v)) - Math.Max(0, jump.Give - drop) * Math.Sin(Math.PI * v);
        }
        return 0;
    }

    /// <summary>
    /// The pole that puts the elbow, on its circle round the line from the shoulder to the hand, as near `lateral` metres out
    /// from the shoulder along `outward` as it goes without going far round from where `natural` puts it: the least of
    /// (miss / MillsElbowSlack)² + (turn / MillsElbowTurn)². Put exactly there, the elbow went far up and forward and
    /// jumped from one side of the circle to the other.
    /// </summary>
    private static Vec3 ElbowOut(Vec3 shoulder, Vec3 hand, double length1, double length2, Vec3 outward, double lateral, Vec3 natural)
    {
        var toTarget = hand - shoulder;
        double distance = Math.Clamp(toTarget.Length, Math.Abs(length1 - length2) + 1e-3, length1 + length2 - 1e-4);
        var axis = toTarget.Normalized();
        double along = (length1 * length1 - length2 * length2 + distance * distance) / (2 * distance);
        double height = Math.Sqrt(Math.Max(0, length1 * length1 - along * along));
        var u = natural - axis * natural.Dot(axis);
        if (u.Length < 1e-6 || height < 1e-6)
            return natural;
        u = u.Normalized();
        var v = axis.Cross(u);
        // The elbow's way out: (centre - shoulder)·outward + height (u·outward cos θ + v·outward sin θ), θ = 0 the natural.
        double c0 = axis.Dot(outward) * along, a = height * u.Dot(outward), b = height * v.Dot(outward);
        double r = Math.Sqrt(a * a + b * b);
        if (r < 1e-6)
            return u;
        double Cost(double t)
        {
            double miss = (c0 + a * Math.Cos(t) + b * Math.Sin(t) - lateral) / MillsElbowSlack, turn = t / MillsElbowTurn;
            return miss * miss + turn * turn;
        }
        double best = double.MaxValue, theta = 0;
        for (int i = -180; i <= 180; i++)
        {
            double t = i * Math.PI / 180, cost = Cost(t);
            if (cost < best)
            {
                best = cost;
                theta = t;
            }
        }
        // Between the degrees, the bottom of the parabola through the best and its neighbours: on whole degrees the elbow
        // stepped a few millimetres from frame to frame (the user: it shook).
        theta = Refine(Cost, theta, Math.PI / 180);
        return u * Math.Cos(theta) + v * Math.Sin(theta);
    }

    /// <summary>The least of `cost` near `at`, found on a grid `step` wide: the bottom of the parabola through it and its neighbours.</summary>
    private static double Refine(Func<double, double> cost, double at, double step)
    {
        double l = cost(at - step), m = cost(at), r = cost(at + step), curve = l - 2 * m + r;
        return curve > 1e-12 ? at + step * Math.Clamp((l - r) / (2 * curve), -1, 1) : at;
    }

    /// <summary>Two bones from root toward target; the joint between them bends toward the pole.</summary>
    private static (Vec3 Mid, Vec3 End) TwoBone(Vec3 root, Vec3 target, double length1, double length2, Vec3 pole)
    {
        var toTarget = target - root;
        double distance = Math.Clamp(toTarget.Length, Math.Abs(length1 - length2) + 1e-3, length1 + length2 - 1e-4);
        var axis = toTarget.Normalized();
        double along = (length1 * length1 - length2 * length2 + distance * distance) / (2 * distance);
        double height = Math.Sqrt(Math.Max(0, length1 * length1 - along * along));
        var bend = pole - axis * pole.Dot(axis);
        if (bend.Length < 1e-6)
            bend = Math.Abs(axis.Y) < 0.9 ? Vec3.Up.Cross(axis) : new Vec3(1, 0, 0);
        return (root + axis * along + bend.Normalized() * height, root + axis * distance);
    }

    /// <summary>Whether the shoulders get where their row says at a moment (null) or how far they get (SH2010).</summary>
    public (double Reached, double Wanted, double Lean)? ShouldersShortAt(double t) => TrunkAt(Clamp(t)).Short;

    private Trunk TrunkAt(double t)
    {
        var c = Core(t);

        // Smooth over five frames what the feet take off the height: the reach limit has corners where
        // a foot lands. The height row itself is not smoothed again, so it starts and arrives on its beats.
        double y = c.WantY + (Core(Clamp(t - 2 * _dt)).Deficit + Core(Clamp(t - _dt)).Deficit + c.Deficit + Core(Clamp(t + _dt)).Deficit + Core(Clamp(t + 2 * _dt)).Deficit) / 5;
        y = Math.Min(y, c.CeilingY);
        y += JumpOffset(t, c.Drop);

        // The torso's word says the line from the pelvis to the chest. A rounded back bows
        // behind that line: the lower half stands up from it, the upper half (and with it the
        // shoulders and the head) leans further forward. The two halves keep their length, so
        // the chest comes a little nearer the pelvis.
        var trunk = new Trunk { C = c, Pelvis = new Vec3(c.P.X, y, c.P.Z) };
        var line = c.TorsoUp;
        // The spine stands on the hips: across is the hips' own left (wherever they are turned
        // to over the feet), square to the line, and the back rounds forward of that.
        var across = (c.HipLeft - line * c.HipLeft.Dot(line)).Normalized();
        var ahead = across.Cross(line);
        double bow = c.Round * Rig.RoundAngle * Math.PI / 180, cos = Math.Cos(bow), sin = Math.Sin(bow);
        trunk.Waist = trunk.Pelvis + (line * cos - ahead * sin) * (Rig.Spine / 2);
        trunk.Chest = trunk.Pelvis + line * (Rig.Spine * cos);
        trunk.Up = line * cos + ahead * sin;
        var before = across.Cross(trunk.Up);
        // The shoulders' row says where their line lies on the floor, from the feet. The chest turns
        // about its own up, on from where the hips have it, to put it there: upright that is the
        // difference of the two rows, under a lean more (a turn about a slanting spine shows less
        // from above). The shoulders, and what is said against the chest (`@body`, a hand on the
        // chest), go round with it. The head does not: its frame is the hips' turned back.
        var aim = c.Left * Math.Cos(c.Shoulders) - c.Fwd * Math.Sin(c.Shoulders);
        var square = Vec3.Up.Cross(aim);
        // A level spine that points square to the aim (bent over, turned toward it with the
        // torso's own direction): every turn about it shows the same line from above, so the
        // row is already met and the chest stays where the hips have it (atan2 of two rounding
        // errors would pick any turn, and warn of a shortfall that is not there).
        double a = across.Dot(square), b = before.Dot(square);
        double turn = Math.Abs(a) < 1e-9 && Math.Abs(b) < 1e-9 ? 0 : Math.Atan2(a, b);
        if ((across * Math.Cos(turn) - before * Math.Sin(turn)).Dot(aim) < 0)
            turn += Math.PI;
        turn = Math.Atan2(Math.Sin(turn), Math.Cos(turn));
        double most = Vocabulary.MaxChestTurn * Math.PI / 180;
        if (Math.Abs(turn) > most + 1e-9)
        {
            turn = Math.Sign(turn) * most;
            var reached = across * Math.Cos(turn) - before * Math.Sin(turn);
            trunk.Short = (Math.Atan2(-reached.Dot(c.Fwd), reached.Dot(c.Left)) * 180 / Math.PI, c.Shoulders * 180 / Math.PI,
                Math.Acos(Math.Clamp(line.Y, -1, 1)) * 180 / Math.PI);
        }
        double hipsCos = Math.Cos(c.Hips), hipsSin = Math.Sin(c.Hips), turnCos = Math.Cos(turn), turnSin = Math.Sin(turn);
        trunk.FaceLeft = across * hipsCos + before * hipsSin;
        trunk.FaceFwd = before * hipsCos - across * hipsSin;
        trunk.Left = across * turnCos - before * turnSin;
        trunk.Fwd = before * turnCos + across * turnSin;
        trunk.Head = trunk.Chest + trunk.Up * Rig.HeadUp;
        return trunk;
    }

    /// <summary>A place on this body, for another dancer to reach or look at. Heights include a lift.</summary>
    private Vec3 PlaceAt(string part, int side, double t, FigureSolver asker, double? level = null)
    {
        var trunk = TrunkAt(t);
        var lifted = Vec3.Up * trunk.C.Float;
        switch (part)
        {
            case "head": return trunk.Head + lifted;
            case "chest": return trunk.Chest + trunk.Fwd * 0.10 + lifted;
            case "shoulder": return trunk.Shoulder(side) + lifted;
        }

        // A hand. If this arm is reaching for the asker's hand at the same moment, neither hand
        // is anywhere yet: the two meet half-way between the shoulders, at the height of a handshake.
        var arm = side == 1 ? _leftArm : _rightArm;
        // The segment that is arriving at t, not one that starts there: at the instant the hands meet they are still reaching.
        var current = arm.LastOrDefault(s => s.T0 < t - 1e-9 && s.Kind != SegKinds.Hold) ?? arm.FirstOrDefault(s => s.T0 <= t && s.Kind != SegKinds.Hold);
        bool mutual = current is { Kind: SegKinds.Reach, Part: "hand" } && current.Target == asker._dancer.Id;
        if (mutual || _posing > 0)
        {
            var mine = trunk.Shoulder(side) + lifted;
            var theirs = asker.TrunkAt(t) is var other ? other.Shoulder(side) + Vec3.Up * other.C.Float : mine;
            var middle = (mine + theirs) * 0.5;
            // With a level (b.hand.high) the hands meet above the heads, at the shoulders or by the hips.
            double y = level switch
            {
                > 0 => Math.Max(mine.Y, theirs.Y) + 0.40,
                0 => middle.Y,
                < 0 => Math.Min(mine.Y, theirs.Y) - 0.45,
                _ => Math.Min(mine.Y, theirs.Y) - 0.25,
            };
            return new Vec3(middle.X, y, middle.Z);
        }
        return Pose(t, out _)[side == 1 ? Rig.LHand : Rig.RHand];
    }

    /// <summary>How far beyond the arm's length the place a reach is sent to lies when it arrives (negative: within reach).</summary>
    public double ReachShortfall(Seg seg, int side)
    {
        if (seg.Target is null || _others(seg.Target) is not { } other)
            return 0;
        _posing++;
        try
        {
            var trunk = TrunkAt(seg.T1);
            var shoulder = trunk.Shoulder(side) + Vec3.Up * trunk.C.Float;
            return (other.PlaceAt(seg.Part!, side, seg.T1, this, seg.Level) - shoulder).Length - Rig.Arm;
        }
        finally
        {
            _posing--;
        }
    }

    public Vec3[] Pose(double t, out PoseProblems problems)
    {
        _posing++;
        try
        {
            return PoseCore(t, out problems);
        }
        finally
        {
            _posing--;
        }
    }

    private Vec3[] PoseCore(double t, out PoseProblems problems)
    {
        problems = default;
        var trunk = TrunkAt(t);
        problems.ShouldersShort = trunk.Short;
        var c = trunk.C;
        Vec3 pelvis = trunk.Pelvis, chest = trunk.Chest, head = trunk.Head;
        Vec3 torsoUp = trunk.Up, torsoLeft = trunk.Left, torsoFwd = trunk.Fwd;
        Vec3 faceLeft = trunk.FaceLeft, faceFwd = trunk.FaceFwd;
        var lifted = Vec3.Up * c.Float;

        // Where the face looks, in the room. A written direction is measured as every part's is: from
        // the way the feet face, level (with `.body`, from the chest). A head nobody wrote looks
        // along the chest, so it bows with a bow; it is not turned with the shoulders.
        Vec3 Gaze(Seg? seg)
        {
            if (seg?.Kind == SegKinds.Reach && _others(seg.Target!) is { } other)
                return (other.PlaceAt(seg.Part!, 1, t, this) - (head + lifted)).Normalized();
            if (seg?.Dir is { } d)
                return seg.Body ? faceLeft * d[0] + torsoUp * d[1] + faceFwd * d[2] : c.Left * d[0] + Vec3.Up * d[1] + c.Fwd * d[2];
            return faceFwd;
        }

        var gaze = Chain(_head, t, Gaze, (a, b, e) => Blend(a, b, e, faceFwd), Dist).Normalized();
        if (_alive && _head.Count == 0)
        {
            // An unwritten head is not fixed on the chest either (DESIGN.md 94): it wanders a few degrees
            // side to side, slower than the weight, nods with the breath, and looks up a little with raised arms.
            double up = (ArmRaise(_leftArm, t).Up + ArmRaise(_rightArm, t).Up) / 2;
            double yaw = 0.08 * Wave(t, _wander, 0.3), pitch = 0.025 * Wave(t, _breath, 0.25) + 0.07 * up;
            gaze = (gaze + faceLeft * yaw + Vec3.Up * pitch).Normalized();
        }
        // Juggling, a ball thrown well above the head: the head tips back a little to follow it (the user; the eyes, which
        // the figure has not, follow the highest ball in the preview). Eased over a little time either side, so it does
        // not nod at every catch.
        if (Juggling is { } juggler && juggler.Weight(Seconds(t)) is var juggled && juggled > 0)
        {
            double sec0 = Seconds(t), middle = pelvis.Y + 0.15, back = 0, total = 0;
            for (int i = -6; i <= 6; i++)
            {
                double k = Math.Exp(-0.5 * (i / 3.0) * (i / 3.0));
                double above = juggler.HighestInAir(sec0 + i * HeadBackSoften / 3) is { } y ? middle + y - head.Y : 0;
                back += k * Smooth(Math.Clamp((above - HeadBackFrom) / (HeadBackFull - HeadBackFrom), 0, 1));
                total += k;
            }
            double lift = HeadBackMost * juggled * back / total;
            if (lift > 0)
            {
                var level = new Vec3(gaze.X, 0, gaze.Z);
                if (level.Length > 1e-6)
                {
                    double rise = Math.Min(Math.Asin(Math.Clamp(gaze.Y, -1, 1)) + lift, 1.2);
                    gaze = level.Normalized() * Math.Cos(rise) + Vec3.Up * Math.Sin(rise);
                }
            }
        }

        // A tilt (`tilt(left(15))`) is the line of the eyes from level, in the room: the neck bends
        // sideways about the gaze until the ears' line is that far from level. Unwritten, the head sits
        // square on the neck, and the eyes lean as the torso leans; between the two the angle goes evenly.
        var neck = head - chest;
        var g = gaze;
        double Roll(Vec3 up)
        {
            var ears = up.Cross(g);
            return ears.Length < 1e-6 ? 0 : Math.Asin(Math.Clamp(-ears.Normalized().Dot(Vec3.Up), -1, 1));
        }
        double square = Roll(neck);
        double roll = Chain(_head, t, seg => seg?.Tilt is { } d ? d * Math.PI / 180 : square, (a, b, e) => a + (b - a) * e, Dist);
        if (Math.Abs(roll - square) > 1e-9 && neck.Cross(g).Length > 1e-6)
        {
            // Turn the neck about the gaze by the angle that brings the ears to the roll: the ears go
            // round with it, e(θ) = e0 cos θ + (g × e0) sin θ, and the roll is -asin(e · up).
            var e0 = neck.Cross(g).Normalized();
            double a = e0.Dot(Vec3.Up), b = g.Cross(e0).Dot(Vec3.Up), r = Math.Sqrt(a * a + b * b);
            if (r > 1e-6)
            {
                double phi = Math.Atan2(b, a), spread = Math.Acos(Math.Clamp(-Math.Sin(roll) / r, -1, 1));
                static double Wrap(double x) => Math.Atan2(Math.Sin(x), Math.Cos(x));
                double one = Wrap(phi + spread), other = Wrap(phi - spread);
                double theta = Math.Abs(one) < Math.Abs(other) ? one : other;
                double cos = Math.Cos(theta), sin = Math.Sin(theta);
                neck = neck * cos + g.Cross(neck) * sin + g * (g.Dot(neck) * (1 - cos));
                head = chest + neck;
            }
        }
        var nose = head + gaze * 0.12;

        var joints = new Vec3[Rig.Joints.Length];
        joints[Rig.Pelvis] = pelvis;
        joints[Rig.Waist] = trunk.Waist;
        joints[Rig.Chest] = chest;
        joints[Rig.Head] = head;
        joints[Rig.Nose] = nose;

        // Legs. A toe sent to the other leg (`otherKnee`, `otherAnkle`) goes where that leg is, so a leg with
        // such a word is solved again once the other one has been.
        for (int side = 1; side >= -1; side -= 2)
        {
            int at = side;
            Leg(side, seg => seg.Floor ? ToeOnTheFloor(seg, at) : SomewhereBelow(seg), ref problems);
        }
        for (int side = 1; side >= -1; side -= 2)
        {
            if ((side == 1 ? _leftLeg : _rightLeg).Any(s => s.Kind == SegKinds.Anchor))
            {
                int other = side == 1 ? Rig.RHip : Rig.LHip, at = side;
                Leg(side, seg => seg.Floor ? ToeOnTheFloor(seg, at) : ToeAt(seg, at, joints[other + 1], joints[other + 2]), ref problems);
            }
        }

        // `side.floor`: the leg straight out its way, the foot pointed on along it, the toe on the
        // floor (a tendu). How far down it slopes follows from how high the hip is: the leg and the
        // foot together reach from the hip to the floor (DESIGN.md 85.5).
        Vec3 ToeOnTheFloor(Seg seg, int side)
        {
            var hip = pelvis + c.HipLeft * (Rig.HipHalf * side);
            var d = seg.Dir!;
            var way = c.Left * d[0] + c.Fwd * d[2];
            way = way.Length > 1e-6 ? way.Normalized() : c.Fwd;
            double leg = Rig.StraightLeg * Rig.Leg, reach = leg + Rig.FootLength;
            double sin = Math.Clamp((hip.Y - FloorToe) / reach, 0, 1), cos = Math.Sqrt(1 - sin * sin);
            return (way * cos - Vec3.Up * sin) * leg;
        }

        // The toe of the leg on `side` at the other leg's knee or ankle. The foot is pointed on along
        // the shin, so the ankle stands back from the place along the shin, and the shin depends on
        // where the ankle is: a few rounds of solving the leg find the two together.
        Vec3 ToeAt(Seg seg, int side, Vec3 otherKnee, Vec3 otherAnkle)
        {
            var hip = pelvis + c.HipLeft * (Rig.HipHalf * side);
            double ahead = seg.Anchor!.EndsWith(".back") ? -0.06 : 0.06;
            var place = seg.Anchor.StartsWith("otherKnee") ? otherKnee + c.HipFwd * ahead : otherAnkle + c.HipFwd * ahead + Vec3.Up * 0.05;
            double footYaw = side == 1 ? c.LYaw : c.RYaw;
            var pole = seg.Pole is { } q ? c.Left * q[0] + Vec3.Up * q[1] + c.Fwd * q[2] : Rig.KneePole(Vec3.Left(footYaw), Vec3.Forward(footYaw), side);
            var ankle = place + Vec3.Up * Rig.FootLength;
            for (int round = 0; round < 6; round++)
            {
                var (knee, reached) = TwoBone(hip, ankle, Rig.Thigh, Rig.Shin, pole);
                ankle = place - (reached - knee).Normalized() * Rig.FootLength;
            }
            return ankle - hip;
        }

        void Leg(int side, Func<Seg, Vec3> place, ref PoseProblems problems)
        {
            bool isLeft = side == 1;
            var hip = pelvis + c.HipLeft * (Rig.HipHalf * side);
            var floor = isLeft ? c.LPos : c.RPos;
            double lift = isLeft ? c.LLift : c.RLift;
            bool air = isLeft ? c.LAir : c.RAir;
            double otherLift = isLeft ? c.RLift : c.LLift;
            double otherGesture = isLeft ? c.GwR : c.GwL;

            // The knee bends the way its foot points: over the toes, turned out with them (unless the text sends it elsewhere).
            double footYaw = isLeft ? c.LYaw : c.RYaw;
            var footFwd = Vec3.Forward(footYaw);
            var kneePole = Rig.KneePole(Vec3.Left(footYaw), footFwd, side);
            var (gesture, offset, bend, point) = LegShape(isLeft ? _leftLeg : _rightLeg, t, c.Left, c.Fwd, place, kneePole, (torsoLeft, torsoUp, torsoFwd));

            var ankle = new Vec3(floor.X, Rig.AnkleHeight + lift + (air ? 0 : c.Raise), floor.Z);
            Vec3? onToes = null;   // where the toes stay while the heel comes up (a foot about to step)
            if (gesture > 0)
            {
                // The leg swings about the hip from the floor to its shape (and back): a straight leg
                // lowered from the front stays straight on the way down, as it does between two shapes.
                var onFloor = ankle;
                ankle = hip + LegSwing(ankle - hip, offset, gesture);
                // In the air of a jump both legs may take a shape (a split leap); standing, one leg carries the weight.
                bool flight = c.LAir && c.RAir;
                // On its way between the floor and a shape low by the standing foot (lifting to a coupé
                // behind, closing from it into fifth in front) the swing about the hip runs the foot
                // through the standing ankle. Seen from above it goes round it instead: the way turns
                // about the standing ankle the shorter way, and comes no nearer than its two ends.
                // Letting go, the floor end is where `steps` has the foot when the leg has let go (the
                // place it is in now would swing the way from one side round to the other as a step
                // sets out late in the word); lifting, it is where the foot stood.
                // Swung from a hip that sinks (a plié, the bend before a jump) the foot would go into the floor.
                if (ankle.Y < Rig.AnkleHeight)
                    ankle = new Vec3(ankle.X, Rig.AnkleHeight, ankle.Z);
                if (!flight && gesture < 1)
                {
                    var goal = onFloor;
                    if (FadeEnd(isLeft ? _leftLeg : _rightLeg, t) is { } end)
                    {
                        FootAt(isLeft ? _leftMoves : _rightMoves, isLeft ? _leftStart : _rightStart, 0, end, out var there, out _, out _, out _, out _);
                        goal = new Vec3(there.X, onFloor.Y, there.Z);
                    }
                    ankle = RoundTheStandingFoot(ankle, goal, hip + offset, isLeft ? c.RPos : c.LPos, gesture);
                }
                if (!flight && gesture > 0.5 && (otherLift > 0.01 || otherGesture > 0.5))
                    problems.BothFeetOff = isLeft ? Channels.LeftLeg : Channels.RightLeg;
            }
            else if (!air)
            {
                // A foot about to step, the body gone on past it: the heel comes up, the toes stay, and the
                // leg reaches its ankle there instead of bending (DESIGN.md 100). No higher than it must.
                double roll = isLeft ? c.LRoll : c.RRoll;
                double straight = Rig.Leg * Rig.StraightLeg;
                // Just before it leaves, the heel goes on up past what the reach needs, and the knee folds: a leg
                // pushing off is bent some 40° as its toes leave the floor (DESIGN.md 105; it left straight).
                // It begins before the heel may rise for the reach: the hips are as high as the leg behind reaches, and
                // waiting for that, the leg went quite straight (2°) the moment before it folded (DESIGN.md 106).
                // Only as the hip goes on past the foot: from the first step, the push would begin before the foot is
                // behind, and the heel jumped up as it came to be (7° of knee in a frame).
                double past = (hip.X - floor.X) * footFwd.X + (hip.Z - floor.Z) * footFwd.Z;
                double push = PushShare(isLeft ? _leftMoves : _rightMoves, t) * (1 - (isLeft ? c.GwL : c.GwR))
                    * Smooth(Math.Clamp((past - 0.05) / 0.1, 0, 1));
                bool reaching = (ankle - hip).Length > straight;
                if ((roll > 0 || push > 0) && c.Raise < 1e-6 && Behind(hip, floor, footFwd) && (reaching || push > 0))
                {
                    // From where the step has the foot (lifted, rolling through it): on about the toes.
                    double stepPitch = isLeft ? c.LPitch : c.RPitch;
                    double lo = 0, hi = reaching ? RollMost * roll : 0;
                    if (reaching && (RolledAnkle(floor, footFwd, hi, lift, stepPitch) - hip).Length <= straight)
                    {
                        for (int i = 0; i < 20; i++)
                        {
                            double mid = (lo + hi) / 2;
                            if ((RolledAnkle(floor, footFwd, mid, lift, stepPitch) - hip).Length > straight)
                                lo = mid;
                            else
                                hi = mid;
                        }
                    }
                    ankle = RolledAnkle(floor, footFwd, hi + PushMore * push, lift, stepPitch);
                    onToes = ToeOf(floor, footFwd, lift, stepPitch);
                }
                if ((ankle - hip).Length > Rig.Leg + 0.02)
                    problems.Overreach = true;
            }

            var (knee, reached) = TwoBone(hip, ankle, Rig.Thigh, Rig.Shin, bend is { } sent && sent.Length > 1e-6 ? sent : kneePole);
            bool offFloor = reached.Y > Rig.AnkleHeight + c.Raise + 0.005;
            // A gesturing leg has taken the foot over: the roll of a step fades as it does.
            double pitch = (isLeft ? c.LPitch : c.RPitch) * (1 - gesture);
            var toe = reached + footFwd * (Rig.FootLength * Math.Cos(pitch));
            toe = new Vec3(toe.X, offFloor ? Math.Max(0.02, reached.Y - ToeBelowAnkle + Rig.FootLength * Math.Sin(pitch)) : 0.02, toe.Z);
            if (onToes is { } ball)
                toe = ball;
            // A foot off the floor is turned on its ankle no further toward the shin than a person can pull it (DESIGN.md 104).
            // The step's roll says the foot's angle in the room; with the knee bent through the swing the shin leans
            // back, and toes up from level made the ankle 50° (a person's goes to some 90° with this foot, 100° in a swing).
            // A foot on its toes is held by the floor, until they leave it.
            if (offFloor && toe.Y > FloorToe + 0.005)
                toe = AnkleKept(knee, reached, toe);
            // In the air of a jump a leg in a shape points its foot too, as the one the steps carry does (DESIGN.md 90).
            if (gesture > 0 && c.LAir && c.RAir)
                point = Math.Max(point, Math.Clamp(-(isLeft ? c.LPitch : c.RPitch) / AirPoint, 0, 1));
            // A toe sent to the other leg: the foot pointed on along the shin.
            if (point > 0)
            {
                toe = Vec3.Lerp(toe, reached + (reached - knee).Normalized() * Rig.FootLength, point);
                // Sliding out to a tendu, a toe pointed along the shin would dip into the floor.
                if (toe.Y < FloorToe)
                    toe = new Vec3(toe.X, FloorToe, toe.Z);
            }

            int first = isLeft ? Rig.LHip : Rig.RHip;
            joints[first] = hip;
            joints[first + 1] = knee;
            joints[first + 2] = reached;
            joints[first + 3] = toe;
        }

        // A mic (DESIGN.md 117): the hand that holds it, and with `mic both` the other one while singing.
        double micUp = _micPhrases.Count > 0 ? MicUp(t) : 0;
        // Juggling: both hands, while a pattern is thrown. The point the hands go round: before the belly, a little above the hips.
        double juggleSec = Seconds(t), juggleWeight = Juggling?.Weight(juggleSec) ?? 0;
        var juggleFwd = new Vec3(torsoFwd.X, 0, torsoFwd.Z);
        juggleFwd = juggleFwd.Length > 1e-6 ? juggleFwd.Normalized() : c.Fwd;
        // Mills' Mess keeps its balls in one plane before the body: measured from the way the feet face, not the chest,
        // which turns with the crossing arms.
        if (Juggling?.MillsAt(juggleSec) > 0)
            juggleFwd = c.Fwd;
        var juggleLeft = Vec3.Up.Cross(juggleFwd);
        var juggleOrigin = pelvis + Vec3.Up * 0.15 + juggleFwd * 0.30;
        // Mills' Mess: the hands' places are measured across from between the feet, not from the pelvis, which sways
        // from side to side (it took the throws in with it: the user, throw from further out).
        if (Juggling?.MillsAt(juggleSec) is { } millsOrigin && millsOrigin > 0)
            juggleOrigin += juggleLeft * (((c.LPos + c.RPos) * 0.5 - pelvis).Dot(juggleLeft) * millsOrigin);

        // Arms.
        for (int side = 1; side >= -1; side -= 2)
        {
            bool isLeft = side == 1;
            var shoulder = trunk.Shoulder(side);
            bool holdsMic = _dancer.Mic is { } micHand && (micHand == "left") == isLeft;
            bool helpsMic = _dancer.Mic == "both" && isLeft;

            // An arm that hangs: the upper arm straight down, the elbow a little bent, so the hand
            // is in front of the shoulder. The elbow bends back from the line to the hand; with
            // the hand under the shoulder it is the elbow that goes behind the body, and on a
            // character the whole arm reads as held back (and is hidden in the hip and the clothes).
            // Swung about the shoulder's left-right line by `swing` radians (forward is positive), the elbow
            // bending a little more as the arm comes in front.
            Vec3 Hang(Vec3 left, Vec3 up, Vec3 fwd, double swing = 0)
            {
                double bent = (Rig.RestElbow + SwingElbow * Math.Max(0, swing) / SwingMost) * Math.PI / 180;
                double below = Rig.UpperArm + Rig.Forearm * Math.Cos(bent), front = Rig.Forearm * Math.Sin(bent);
                double cos = Math.Cos(swing), sin = Math.Sin(swing);
                return left * (0.06 * side) - up * (below * cos - front * sin) + fwd * (front * cos + below * sin);
            }

            // An arm nobody wrote swings against the legs while they walk (DESIGN.md 101): forward as the other
            // foot is ahead of the pelvis, back as it is behind, and only as much as the feet are moving. It is a
            // movement of a living body, so it goes with the rest of them (the tests of exact poses turn it off).
            double swing = 0;
            if (_alive)
            {
                // Half the stride between the feet where they stand or step, not the other ankle from the pelvis: the pelvis
                // goes on all the time, so that came forward with the quick swing of the foot and went back slowly with
                // the body passing over it (DESIGN.md 106). The feet take turns, so the arm takes as long each way.
                // Taken over a little time either side, as an arm is a pendulum: it does not stop at the end of its
                // swing while both feet are on the floor. The upper arm goes further back than forward.
                double ahead = 0;
                for (int k = -2; k <= 2; k++)
                    ahead += HalfStride(Clamp(t + k * ArmLag / 2 * PerSecond(t)), c.Fwd) / 5;
                if (!isLeft)
                    ahead = -ahead;
                swing = (Math.Clamp(ahead * SwingPerMetre, -SwingMost, SwingMost) - SwingBack) * (1 - FeetStill(t));
            }

            Vec3 Offset(Seg? seg)
            {
                // An arm nobody wrote hangs, and swings while the feet walk. One that holds a mic holds it before
                // the belly, the elbow bent: a singer between phrases does not let it dangle.
                if (seg is null)
                    return holdsMic
                        ? chest + torsoFwd * 0.22 - torsoUp * 0.35 + torsoLeft * (0.10 * side) - shoulder
                        : Hang(c.Left, Vec3.Up, c.Fwd, swing);
                if (seg.Kind == SegKinds.Reach)
                    return _others(seg.Target!) is { } other ? other.PlaceAt(seg.Part!, side, t, this, seg.Level) - (shoulder + lifted) : Offset(null);
                if (seg.Kind == SegKinds.Anchor)
                {
                    var place = seg.Anchor switch
                    {
                        "hip" => pelvis + c.HipLeft * (0.17 * side) + Vec3.Up * 0.06 + c.HipFwd * 0.02,
                        "chest" => chest + torsoFwd * 0.13 - torsoUp * 0.14 + torsoLeft * (0.04 * side),
                        // The head and the belly are not turned with the chest on the hips: the head is over it, the belly under it.
                        "head" => head + faceLeft * (0.13 * side) + torsoUp * 0.02,
                        "chin" => head + faceFwd * 0.14 - torsoUp * 0.12 + faceLeft * (0.06 * side),
                        // On the head as it is (looking up, tilted): in front of the brow, a little to its own side.
                        "forehead" => head + gaze * 0.13 + neck.Normalized() * 0.07 + faceLeft * (0.04 * side),
                        // The belly is on the hips: it goes round with them, and no further.
                        "belly" => pelvis + torsoUp * 0.18
                            + (faceFwd * Math.Cos(c.Hips) + faceLeft * Math.Sin(c.Hips)) * 0.13
                            + (faceLeft * Math.Cos(c.Hips) - faceFwd * Math.Sin(c.Hips)) * (0.03 * side),
                        "across.shoulder" => chest - torsoLeft * (Rig.ShoulderHalf * 0.8 * side) - torsoUp * Rig.ShoulderDrop + torsoFwd * 0.09,
                        "clap" => chest + torsoFwd * 0.28 - torsoUp * 0.12 + torsoLeft * (0.025 * side),   // the wrists 5 cm apart: a hand is that thick (DESIGN.md 95)
                        _ => shoulder + torsoUp * 0.06 + torsoFwd * 0.05,
                    };
                    return place - shoulder;
                }
                var d = seg.Dir!;
                // `down` is the arm hanging, the same shape as an arm nobody wrote: on a body with
                // flesh the hand is not under the shoulder joint. (`down.bent` is still the short arm straight down, and `down.bent(0)` the straight arm.)
                if (d[1] < -0.9999 && seg.Bend is null && seg.Pole is null)
                    return seg.Body ? Hang(torsoLeft, torsoUp, torsoFwd) : Hang(c.Left, Vec3.Up, c.Fwd);
                return Frame(seg, d) * Reach(seg, Rig.UpperArm, Rig.Forearm, 0.999);
            }

            // The room's frame by default (a notator's "standard cross": down is the floor
            // even when the torso leans); the torso's own frame when the event says @body.
            Vec3 Frame(Seg seg, double[] d) => seg.Body
                ? torsoLeft * d[0] + torsoUp * d[1] + torsoFwd * d[2]
                : c.Left * d[0] + Vec3.Up * d[1] + c.Fwd * d[2];

            // Where the elbow points is decided at the poses the text names, and carried between
            // them with the arm. Read off the pole at every instant instead, it spins half a turn
            // in a frame when the hand passes the one direction that lies along the pole
            // (overhead and a little forward, on the way from `up` to `clap`). `@elbow(...)` sends it
            // its own way.
            var pole = Rig.ElbowPole(c.Left, c.Fwd, side);
            var armSegs = isLeft ? _leftArm : _rightArm;
            (Vec3 Offset, Vec3 Bend) Posed(Seg? seg)
            {
                if (seg?.Kind == SegKinds.Stay)
                {
                    // The arm as the words before the `stay` have it (when its place is being found), or
                    // reaching for that place in the room; the elbow pointing as the word before sent it.
                    var before = armSegs.Take(armSegs.IndexOf(seg)).ToList();
                    if (_staying.Contains(seg))
                        return Chain(before, seg.T0, Posed, ArmBlend, (a, b) => Dist(a.Offset, b.Offset));
                    var kept = StayHand(seg) - (shoulder + lifted);
                    var sent = before.LastOrDefault(s => s.Kind is not (SegKinds.Hold or SegKinds.Stay));
                    return (kept, Across(sent?.Pole is { } p ? Frame(sent, p) : pole, kept.Normalized()));
                }
                var offset = Offset(seg);
                // A hand at the forehead holds its elbow out to the side (a salute), unless told otherwise.
                var sentTo = seg?.Pole is { } towards ? Frame(seg, towards)
                    : seg is { Kind: SegKinds.Anchor, Anchor: "forehead" } ? torsoLeft * side : pole;
                return (offset, Across(sentTo, offset.Normalized()));
            }

            (Vec3 Offset, Vec3 Bend) ArmBlend((Vec3 Offset, Vec3 Bend) a, (Vec3 Offset, Vec3 Bend) b, double e)
            {
                var offset = Blend(a.Offset, b.Offset, e, c.Fwd);
                // On the way to the forehead the hand comes in across late, rising on its own side, and the elbow goes
                // out at once, so the hand does not cross the chest (DESIGN.md 87.4).
                // Away from it the other way round: the hand goes out to its side at once and the elbow comes in last
                // (trial 27: down from the salute the hand crossed 17 cm past the middle of the chest).
                bool toBrow = _toForehead, fromBrow = _fromForehead;
                if (toBrow || fromBrow)
                {
                    double la = a.Offset.Dot(torsoLeft), lb = b.Offset.Dot(torsoLeft), now = offset.Dot(torsoLeft);
                    double side = toBrow ? e * e : 1 - (1 - e) * (1 - e);
                    offset += torsoLeft * ((la + (lb - la) * side) - now);
                }
                var axis = offset.Normalized();
                Vec3? from = Carry(a.Bend, a.Offset.Normalized(), axis), to = Carry(b.Bend, b.Offset.Normalized(), axis);
                double eb = toBrow ? Math.Min(1, 3 * e) : fromBrow ? Math.Max(0, 3 * e - 2) : e;
                var between = from is null || to is null ? from ?? to ?? pole : AroundTheElbow(from.Value, to.Value, eb, axis);
                return (offset, Across(between, axis));
            }

            // From where the elbow points at one word to where it points at the next, both carried to the arm as it
            // is now (square to it). The shorter way round can lead the elbow through pointing up and in, the way an
            // elbow does not go, and where the two are opposite it picked one side and then the other: down from
            // overhead to a clap, the elbow went from above the shoulder to below it in a frame (DESIGN.md 95). The
            // angles are measured from where an elbow points by itself (the pole, square to the arm), so the way
            // goes round that side, and never through its opposite.
            Vec3 AroundTheElbow(Vec3 a, Vec3 b, double e, Vec3 axis)
            {
                var natural = Across(pole, axis);
                if (natural.Length < 1e-6 || a.Length < 1e-6 || b.Length < 1e-6)
                    return Vec3.Slerp(a, b, e);
                a = a.Normalized();
                b = b.Normalized();
                var turned = axis.Cross(natural);
                double Angle(Vec3 v) => Math.Atan2(v.Dot(turned), v.Dot(natural));
                double angle = Angle(a) + (Angle(b) - Angle(a)) * e;
                return natural * Math.Cos(angle) + turned * Math.Sin(angle);
            }

            // Where a `stay` keeps the hand: where the arm had it as the word began, in the room.
            Vec3 StayHand(Seg seg)
            {
                if (_stayHands.TryGetValue(seg, out var kept))
                    return kept;
                _staying.Add(seg);
                try
                {
                    kept = Pose(seg.T0, out _)[isLeft ? Rig.LHand : Rig.RHand];
                }
                finally
                {
                    _staying.Remove(seg);
                }
                _stayHands[seg] = kept;
                return kept;
            }

            int going = armSegs.FindLastIndex(s => s.T0 <= t);
            bool moving = going >= 0 && t < armSegs[going].T1;
            static bool Brow(Seg s) => s is { Kind: SegKinds.Anchor, Anchor: "forehead" };
            int came = going - 1;
            while (came >= 0 && armSegs[came].Kind == SegKinds.Hold)
                came--;
            _toForehead = moving && Brow(armSegs[going]);
            _fromForehead = moving && !_toForehead && armSegs[going].Kind != SegKinds.Hold && came >= 0 && Brow(armSegs[came]);
            var (reach, bend) = Chain(armSegs, t, Posed, ArmBlend, (a, b) => Dist(a.Offset, b.Offset));
            _toForehead = _fromForehead = false;
            if (armSegs.LastOrDefault(s => s.T0 <= t + 1e-9) is { Kind: SegKinds.Stay } staying && !_staying.Contains(staying)
                && reach.Length > Rig.UpperArm + Rig.Forearm + 0.01)
                problems.HandShort = isLeft ? Channels.LeftArm : Channels.RightArm;
            var hand = shoulder + reach;
            if (micUp > 0 && (holdsMic || helpsMic))
            {
                // At the mouth: the wrist under the chin and a little in front, so the mic in the fist rises to the
                // lips; the other hand of `mic both` lower on the handle. On the head as it is, so the mic goes with a
                // nod or a turn. The elbow down and out. Over the row's arm, as far as the mic is up.
                var neckUp = (head - chest).Normalized();
                var mouth = head + gaze * 0.09 + neckUp * 0.02;
                var wrist = holdsMic
                    ? mouth + gaze * 0.05 - neckUp * 0.15 + faceLeft * (0.03 * side)
                    : mouth + gaze * 0.06 - neckUp * 0.21 + faceLeft * (0.02 * side);
                hand = Vec3.Lerp(hand, wrist, micUp);
                var micPole = (-Vec3.Up + torsoLeft * (0.6 * side) - torsoFwd * 0.2).Normalized();
                bend = Vec3.Slerp(bend.Length < 1e-6 ? pole.Normalized() : bend.Normalized(), micPole, micUp);
            }
            if (juggleWeight > 0)
            {
                // Juggling (DESIGN.md 118): the hand goes round its small loop before the belly, the elbow down by the
                // side, the forearm forward. In the frame of the chest as it faces, level: the throws go up, whatever the lean.
                var local = Juggling!.Hand(isLeft ? 1 : 0, juggleSec);
                var wrist = juggleOrigin + juggleLeft * local.X + Vec3.Up * local.Y + juggleFwd * local.Z;
                // Never so far from the shoulder that the arm goes straight: the elbow stays bent some 40 degrees (the
                // user: going up with the ball, the arm turning about the elbow). A juggler's elbow is never locked.
                var reachOut = wrist - shoulder;
                // A penguin's arm is let out nearly straight (the user: the elbow straight at the catch), bent some 7 degrees
                // so that the way the elbow points, and the upper arm turned in with it, still holds.
                // How much of a penguin it is, over a little time either side, so the reach does not change in a frame where
                // the hand's way turns to or from one.
                double penguin = 0;
                for (int k = -4; k <= 4; k++)
                    penguin += Juggling!.StyleAt(isLeft ? 1 : 0, juggleSec + k * 0.03) == Juggling.Style.Penguin ? 1.0 / 9 : 0;
                // Fully straight round the catch: from a little before it, for the wrist to turn in, until the wrist has
                // turned back after it (DESIGN.md 118.19, 118.20).
                double held = Juggling!.PenguinHeld(isLeft ? 1 : 0, juggleSec);
                penguin = Math.Max(penguin, held);
                // Held in softly as it comes to the straight arm, not stopped there (the user: it jerked at the catch).
                var straight = SoftReach(reachOut, PenguinReachSoft, PenguinReach);
                wrist = shoulder + Vec3.Lerp(SoftReach(reachOut), straight, penguin);
                // The windmill's wheel (DESIGN.md 118.34): the elbow held from the shoulder as it is (the wheel is drawn for the
                // figure's shoulder standing square; the back's lean moves the shoulder), and the wrist going round from it,
                // not drawn in by the reach.
                var wheel = Juggling.Wheel(isLeft ? 1 : 0, juggleSec);
                Vec3 axle = default;
                if (wheel is { Held: true } wh)
                {
                    Vec3 Axes(Vec3 v) => juggleLeft * v.X + Vec3.Up * v.Y + juggleFwd * v.Z;
                    var squareShoulder = new Vec3(Math.Sign(wh.Elbow.X) * Rig.ShoulderHalf, Rig.Spine - Rig.ShoulderDrop - 0.15, -0.30);
                    axle = shoulder + Axes(wh.Elbow - squareShoulder);
                    wrist = Vec3.Lerp(wrist, axle + Axes(wh.Wrist - wh.Elbow), wh.Weight);
                }
                // Held straight, the arm goes out to its reach the way it points, wherever the hand's way is (the user: the
                // wrist cannot turn on a bent elbow, and before the catch the elbow was still straightening as it turned).
                if (held > 0 && (wrist - shoulder).Length > 1e-6)
                    wrist = Vec3.Lerp(wrist, shoulder + (wrist - shoulder).Normalized() * PenguinReach, held);
                // Clear of the hip round a penguin's catch (the user: swinging on from the catch, the hand went into the
                // hip): down by the hips and not out in front, the hand is kept PenguinHipClear out from the pelvis, the
                // arm swung out about the shoulder, as long as it was.
                double clear = Juggling!.PenguinClear(isLeft ? 1 : 0, juggleSec);
                if (clear > 0)
                {
                    var hipOut = trunk.C.HipLeft * side;
                    var hipFwd = Vec3.Up.Cross(trunk.C.HipLeft).Normalized() * -1;
                    if (hipFwd.Dot(trunk.Fwd) < 0)
                        hipFwd = hipFwd * -1;
                    var d = wrist - pelvis;
                    double outNow = d.Dot(hipOut), ahead = d.Dot(hipFwd), above = d.Y;
                    // As much as it is down by the hips (below the waist) and not in front of them (eased over 0.3 m each
                    // way, so it does not jump out in a frame as the hand comes down to the catch).
                    double where = Smooth(Math.Clamp((0.25 - above) / 0.3, 0, 1)) * Smooth(Math.Clamp((0.35 - ahead) / 0.3, 0, 1));
                    double push = (PenguinHipClear - outNow) * clear * where;
                    if (push > 0)
                    {
                        double length = (wrist - shoulder).Length;
                        var moved = wrist + hipOut * push - shoulder;
                        if (moved.Length > 1e-6)
                            wrist = shoulder + moved.Normalized() * length;
                    }
                }
                // The windmill's top arm, as the other hand throws from under it, draws its forearm in against the body (the
                // user: the hand pulled in, the forearm as if against the body), the ball it holds with it, so the ball from
                // under goes up in front of it.
                if (Juggling.WindmillPull(isLeft ? 1 : 0, juggleSec) is var drawIn && drawIn > 0)
                    wrist -= juggleFwd * (WindmillDrawIn * drawIn);
                hand = Vec3.Lerp(hand, wrist, juggleWeight);
                // The elbows down by the ribs and a little back, so the forearms point forward: out to the side they
                // turned the forearms in, and on a character with narrow shoulders the hands crossed the middle.
                // Rings: the elbows straight down by the ribs, a touch in, the upper arm not turned (the user: the shoulder turned in
                // looked wrong); with a ball a little back as well.
                // Clubs: the elbows open out to the side, so the hands catch well out (the user).
                // A trick holds the arm its own way (DESIGN.md 118.12): behind the back and under the leg the elbow goes out,
                // a penguin's arm hangs straight with the elbow back.
                var ownPole = Juggling.Prop switch
                {
                    "rings" => -Vec3.Up - juggleLeft * (0.05 * side) - juggleFwd * 0.15,
                    "clubs" => -Vec3.Up + juggleLeft * (0.6 * side) - juggleFwd * 0.15,
                    _ => -Vec3.Up + juggleLeft * (0.08 * side) - juggleFwd * 0.45,
                };
                // Behind the back and under the leg, as much as the hand's way is so over a little time either side (DESIGN.md
                // 118.31: taken at the moment, the elbow turned out and back in a frame each side of the throw, at 240-300 m/s²).
                double backShare = 0, underShare = 0;
                for (int k = -4; k <= 4; k++)
                {
                    var near = Juggling!.StyleAt(isLeft ? 1 : 0, juggleSec + k * TrickPoleStep);
                    backShare += near == Juggling.Style.Back ? 1.0 / 9 : 0;
                    underShare += near == Juggling.Style.Under ? 1.0 / 9 : 0;
                }
                var jugglePole = ownPole.Normalized();
                if (backShare > 0)
                    jugglePole = Vec3.Slerp(jugglePole, (-Vec3.Up * 0.3 + juggleLeft * (1.0 * side) - juggleFwd * 0.2).Normalized(), Smooth(backShare));
                if (underShare > 0)
                    jugglePole = Vec3.Slerp(jugglePole, (-Vec3.Up * 0.2 + juggleLeft * (1.0 * side) + juggleFwd * 0.1).Normalized(), Smooth(underShare));
                // A penguin's arm is turned in at the shoulder (the user: the shoulder put in): the elbow out to the side. As much
                // as it is a penguin's over a little time (above), so the elbow does not turn over in a frame (it swung the
                // forearm round at 1600 degrees a second).
                // Only as the arm comes out straight: bent (after the throw, the hand up before the chest) the elbow out to the
                // side stood out like a hand on the hip (the user's eye on the swing down to the catch).
                double straightness = Smooth(Math.Clamp(((wrist - shoulder).Length / (Rig.UpperArm + Rig.Forearm) - 0.8) / 0.15, 0, 1));
                if (penguin * straightness > 0)
                    jugglePole = Vec3.Slerp(jugglePole, (-Vec3.Up * 0.1 + juggleLeft * (1.0 * side) - juggleFwd * 0.2).Normalized(), penguin * straightness);
                // Mills' Mess: the elbow in toward the ribs, so the forearm swings out round the body from it rather than the
                // whole arm going to and fro (the user).
                double mills = Juggling.MillsAt(juggleSec);
                if (mills > 0)
                    jugglePole = Vec3.Slerp(jugglePole, (-Vec3.Up + juggleLeft * (MillsElbowOut * side) - juggleFwd * MillsElbowBack).Normalized(), mills);
                bend = Vec3.Slerp(bend.Length < 1e-6 ? pole.Normalized() : bend.Normalized(), jugglePole, juggleWeight);
                // And the elbow kept at one place side to side, under the shoulder, as far as the arm lets it: only the
                // forearm goes round (the user: the elbow's x fixed, the arm past it swung wide).
                if (mills > 0)
                    bend = Vec3.Slerp(bend, ElbowOut(shoulder, hand, Rig.UpperArm, Rig.Forearm, juggleLeft * side, MillsElbowSide, bend), mills * juggleWeight * MillsElbowHold);
                // Takeouts' swipe the same way: the elbow kept out on its own side while the forearm turns round it in to the
                // body (the user: the elbow's x moved too much; DESIGN.md 118.49).
                double swipe = Juggling.SwipeHold(isLeft ? 1 : 0, juggleSec);
                if (swipe > 0)
                    bend = Vec3.Slerp(bend, ElbowOut(shoulder, hand, Rig.UpperArm, Rig.Forearm, juggleLeft * side, SwipeElbowSide, bend), swipe * juggleWeight);
                // Takeouts' round: the forearm on an upright plane before the face (the user: wiping to and fro as in The Karate
                // Kid's training): the elbow below the hand, as far forward as it (less a little), not back by the ribs.
                double round = Juggling.RoundPlane(isLeft ? 1 : 0, juggleSec) * juggleWeight;
                if (round > 0)
                {
                    // (One way, not sought on the elbow's circle as ElbowOut does: with the swipe's and the throw's holds coming and
                    // going, two places on it came out about as good and the elbow leapt 20 cm from one to the other in a frame.)
                    var planePole = (-Vec3.Up + juggleFwd * RoundElbowAhead).Normalized();
                    bend = Vec3.Slerp(bend.Length < 1e-6 ? pole.Normalized() : bend.Normalized(), planePole, round);
                }
                // The windmill's wheel: the elbow held where the wheel's axle is (DESIGN.md 118.34).
                if (wheel is { Held: true } held2)
                {
                    var toward = axle - (shoulder + hand) * 0.5;
                    if (toward.Length > 1e-6)
                        bend = Vec3.Slerp(bend.Length < 1e-6 ? pole.Normalized() : bend.Normalized(), toward.Normalized(), held2.Weight * juggleWeight);
                }
            }
            var (elbow, reachedHand) = TwoBone(shoulder, hand, Rig.UpperArm, Rig.Forearm, bend.Length < 1e-6 ? pole : bend);
            // Mills' Mess: the balls all the same distance in front of the body (the user: exactly). The ball lies past the
            // wrist along the forearm, and the forearm points differently crossed and uncrossed: the wrist is moved until
            // the palm, where the ball is, is at that distance (twice, the arm solved again each time). Only while it holds a
            // ball: empty, the hands are free to pass each other in front and behind.
            // (Not while the windmill's top arm draws its hand and the ball in with its elbow pulled back: DESIGN.md 118.33.)
            if (juggleWeight > 0 && Juggling!.MillsAt(juggleSec) * Juggling.HoldShare(isLeft ? 1 : 0, juggleSec) * (1 - Juggling.WindmillPull(isLeft ? 1 : 0, juggleSec)) * (1 - (Juggling.Wheel(isLeft ? 1 : 0, juggleSec)?.Weight ?? 0)) is var millsShare && millsShare > 0)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    var palm = reachedHand + (reachedHand - elbow).Normalized() * 0.085 + Vec3.Up * 0.04;
                    // From the feet, which stand still, not the pelvis, which sways a little as a living body does.
                    double off = MillsPlane - (palm - (c.LPos + c.RPos) * 0.5).Dot(juggleFwd);
                    hand += juggleFwd * (off * millsShare * juggleWeight);
                    var fromShoulder = hand - shoulder;
                    hand = shoulder + SoftReach(fromShoulder);
                    (elbow, reachedHand) = TwoBone(shoulder, hand, Rig.UpperArm, Rig.Forearm, bend.Length < 1e-6 ? pole : bend);
                }
            }

            int first = isLeft ? Rig.LShoulder : Rig.RShoulder;
            joints[first] = shoulder;
            joints[first + 1] = elbow;
            joints[first + 2] = reachedHand;
        }

        // A lifted body is the same pose, higher: nothing in it stands on the floor.
        if (c.Float != 0)
        {
            for (int j = 0; j < joints.Length; j++)
                joints[j] += lifted;
            problems.Overreach = false;
        }
        return joints;
    }
}
