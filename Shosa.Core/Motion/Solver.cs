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
            clip.Marks.Add(new ClipMark(mark.Beat, Math.Round(clip.SecondsAt(mark.Beat), 4), mark.Name, mark.Dancer));

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

        // Head, chest and pelvis of every dancer at every frame, where they really are (before `inplace`).
        var bodies = new Dictionary<string, List<(Vec3 Head, Vec3 Chest, Vec3 Pelvis)>>();
        var unreached = new HashSet<int>();

        foreach (var dancer in ir.Dancers)
        {
            var solver = solvers[dancer.Id];
            var clipFigure = new ClipFigure { Id = dancer.Id, Name = dancer.Name };
            var flagged = new HashSet<(Seg, string)>();
            Vec3[]? previous = null;
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

            // `inplace`: the even part of the travel is taken out and handed to the engine as a velocity.
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
                if (seg is not null && (evenIfFree || !seg.Free) && flagged.Add((seg, code)))
                    diagnostics.Add(new Diagnostic(code, severity, new TextSpan(seg.Src, seg.Len), message));
            }

            for (int f = 0; f < frames; f++)
            {
                double t = beatOfFrame[f];
                var pose = solver.Pose(t, out var problems);
                body.Add((pose[Rig.Head], pose[Rig.Chest], pose[Rig.Pelvis]));
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
                if (problems.BothFeetOff is { } leg)
                    Flag(leg, t, "SH2004", DiagnosticSeverity.Error,
                        "This leg leaves the floor while the other one is not carrying the weight. Step onto the other foot first.");

                if (previous is not null)
                {
                    Speed(Rig.LHand, Channels.LeftArm, MaxHandSpeed, "hand");
                    Speed(Rig.RHand, Channels.RightArm, MaxHandSpeed, "hand");
                    // A foot moves because of a leg gesture if one is going on, else because of a step.
                    Speed(Rig.LAnkle, SegAt(dancer, Channels.LeftLeg, t) is null ? Channels.Support : Channels.LeftLeg, MaxFootSpeed, "foot");
                    Speed(Rig.RAnkle, SegAt(dancer, Channels.RightLeg, t) is null ? Channels.Support : Channels.RightLeg, MaxFootSpeed, "foot");
                }
                previous = pose;

                void Speed(int joint, string channel, double limit, string what)
                {
                    // A hand is judged against the body it hangs on: an arm held out through a turn or
                    // a jump is not a fast arm. (The feet, which make the turn, are judged against the floor.)
                    double speed = what == "hand"
                        ? (InBody(pose, joint) - InBody(previous!, joint)).Length * fps
                        : (pose[joint] - previous![joint]).Length * fps;
                    if (speed > limit)
                        Flag(channel, t, "SH2003", DiagnosticSeverity.Warning,
                            $"Too fast to perform: the {what} would move at about {speed:0} m/s. Give it more time, or lower the tempo.");
                }
            }

            clip.Figures.Add(clipFigure);
            foreach (var (channel, segs) in dancer.Tracks)
            {
                foreach (var seg in segs)
                    clip.Spans.Add(new ClipSpan(seg.T0, seg.T1, seg.Src, seg.Len, dancer.Id, channel));
            }
        }

        Collisions(ir, bodies, beatOfFrame, diagnostics);
        return clip;
    }

    private const double HeadRadius = 0.10, TrunkRadius = 0.11;

    /// <summary>
    /// SH2007. Two dancers in the same place: a head or a trunk inside another's. Arms and legs
    /// are left out — hands are meant to meet, and a kick that lands is the point of it.
    /// Said once for each pair, at the first moment.
    /// </summary>
    private static void Collisions(MotionIr ir, Dictionary<string, List<(Vec3 Head, Vec3 Chest, Vec3 Pelvis)>> bodies, double[] beatOfFrame, List<Diagnostic> diagnostics)
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
                        (Between(p.Head, p.Head, q.Pelvis, q.Chest) - HeadRadius - TrunkRadius, $"{a.Id}'s head and {b.Id}'s body"),
                        (Between(q.Head, q.Head, p.Pelvis, p.Chest) - HeadRadius - TrunkRadius, $"{b.Id}'s head and {a.Id}'s body"),
                        (Between(p.Pelvis, p.Chest, q.Pelvis, q.Chest) - 2 * TrunkRadius, "bodies"),
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
                    string what = worst.What is "heads" or "bodies" ? $"the {worst.What} of {a.Id} and {b.Id}" : worst.What;
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

    /// <summary>A joint as seen from the pelvis: sideways along the hips, up, and forward.</summary>
    private static Vec3 InBody(Vec3[] pose, int joint)
    {
        var across = pose[Rig.LHip] - pose[Rig.RHip];
        var left = new Vec3(across.X, 0, across.Z).Normalized();
        var fwd = left.Cross(Vec3.Up);
        var d = pose[joint] - pose[Rig.Pelvis];
        return new Vec3(d.Dot(left), d.Y, d.Dot(fwd));
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
        /// <summary>How much of a stride the step goes the way the foot points: 1 forward, -1 back, 0 sideways.</summary>
        public double Along;
    }

    /// <summary>Where the feet and the weight are between two events of the support track.</summary>
    private sealed class Stand
    {
        public Vec3 Left, Right, Weight;
        public double LeftYaw, RightYaw, Yaw, Float;
        public char On = 'B';   // the supporting foot: L, R or Both
        public Stand Clone() => (Stand)MemberwiseClone();
    }

    private struct CoreState
    {
        public double Yaw;
        public Vec3 Fwd, Left;
        public Vec3 LPos, RPos;
        public double LYaw, RYaw, LLift, RLift;
        public double LPitch, RPitch;   // the foot about its ankle, radians; positive lifts the toes
        public bool LAir, RAir;
        public double GwL, GwR;     // how far each leg is into a gesture, 0..1
        public Vec3 GoL, GoR;       // the gesturing ankle, from its hip
        public Vec3 TorsoUp;
        public Vec3 P;              // pelvis over the floor
        public double RawY, Raise, Float;
    }

    /// <summary>The spine and what hangs on it, before arms and legs.</summary>
    private struct Trunk
    {
        public CoreState C;
        public Vec3 Pelvis, Chest, Head, Up, Left, Fwd;
        public readonly Vec3 Shoulder(int side) => Chest + Left * (Rig.ShoulderHalf * side) - Up * Rig.ShoulderDrop;
    }

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
    private readonly List<(double T0, double Up, double Land, double T1, double Height)> _jumps = [];
    private readonly List<Seg> _height, _leftArm, _rightArm, _leftLeg, _rightLeg, _torso, _head;
    private bool _built;
    private int _posing;

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
        _torso = Row(Channels.Torso);
        _head = Row(Channels.Head);
    }

    /// <summary>What has to hold for a loop and does not, in words for the author; null when it joins.</summary>
    public string? LoopProblem { get; private set; }

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
            return segs;
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
            Left = _origin + Vec3.Left(_yawStart) * (Rig.Stance / 2),
            Right = _origin - Vec3.Left(_yawStart) * (Rig.Stance / 2),
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
        _built = true;
    }

    /// <summary>
    /// Plays the support track once, in order, and writes down what it implies: where each
    /// foot moves and when, where the weight is at each landing, the facing, the height.
    /// </summary>
    private void Play(Stand s, List<Seg> support, double offset, bool record)
    {
        void Weight(double t0, double t1, Vec3 to)
        {
            if (record)
            {
                if (t0 > _weightTime)
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

        void Move(bool isLeft, FootMove move)
        {
            if (record)
                (isLeft ? _leftMoves : _rightMoves).Add(move);
            if (isLeft)
            {
                s.Left = move.To;
                s.LeftYaw = move.YawTo;
            }
            else
            {
                s.Right = move.To;
                s.RightYaw = move.YawTo;
            }
        }

        void Turn(double t0, double t1, double angle)
        {
            if (record)
                _turns.Add((t0, t1, s.Yaw, s.Yaw + angle));
            s.Yaw += angle;
        }

        static double StepLift(Vec3 from, Vec3 to) => Math.Clamp(0.05 + 0.12 * (to - from).Length, 0.06, 0.15);

        foreach (var seg in support)
        {
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
                    });
                    s.On = isLeft ? 'L' : 'R';
                    // At the landing the weight is between the feet, a little toward the new one.
                    // Keyed only at landings, the pelvis passes over the standing foot in mid-step.
                    Weight(t0, t1, Vec3.Lerp((s.Left + s.Right) * 0.5, target, 0.3));
                    break;
                }

                case SegKinds.Together:
                {
                    if (s.On != 'B')
                    {
                        bool moveLeft = s.On == 'R';
                        var from = moveLeft ? s.Left : s.Right;
                        var other = moveLeft ? s.Right : s.Left;
                        var target = other + (moveLeft ? leftward : -leftward) * Rig.Stance;
                        Move(moveLeft, new FootMove
                        {
                            T0 = t0, T1 = t1, From = from, To = target,
                            YawFrom = moveLeft ? s.LeftYaw : s.RightYaw, YawTo = s.Yaw, Lift = StepLift(from, target),
                        });
                    }
                    s.On = 'B';
                    Weight(t0, t1, (s.Left + s.Right) * 0.5);
                    break;
                }

                case SegKinds.Shift:
                {
                    var middle = (s.Left + s.Right) * 0.5;
                    s.On = seg.Foot == "left" ? 'L' : seg.Foot == "right" ? 'R' : 'B';
                    Weight(t0, t1, s.On == 'L' ? Vec3.Lerp(middle, s.Left, 0.85) : s.On == 'R' ? Vec3.Lerp(middle, s.Right, 0.85) : middle);
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
                    Move(true, new FootMove
                    {
                        T0 = t0, T1 = t1, From = s.Left, To = newLeft, YawFrom = s.LeftYaw, YawTo = s.LeftYaw + angle,
                        Orbit = true, Pivot = pivot, Angle = angle, Lift = s.On == 'L' ? 0 : 0.03,
                    });
                    Move(false, new FootMove
                    {
                        T0 = t0, T1 = t1, From = s.Right, To = newRight, YawFrom = s.RightYaw, YawTo = s.RightYaw + angle,
                        Orbit = true, Pivot = pivot, Angle = angle, Lift = s.On == 'R' ? 0 : 0.03,
                    });
                    Turn(t0, t1, angle);
                    Weight(t0, t1, weight);
                    break;
                }

                case SegKinds.Jump:
                {
                    double duration = t1 - t0;
                    double up = t0 + 0.35 * duration, land = t0 + 0.85 * duration;
                    var middle = (s.Left + s.Right) * 0.5;
                    var landing = middle + fwd * seg.Dz;
                    double spread = seg.Spread ?? Math.Abs((s.Left - s.Right).Dot(leftward));
                    double angle = seg.TurnDeg * Math.PI / 180;
                    var leftAfter = Vec3.Left(s.Yaw + angle);
                    // The time in the air fixes the height: h = g T^2 / 8.
                    double airSeconds = TempoMap.Seconds(_tempos, land) - TempoMap.Seconds(_tempos, up);
                    double height = 9.81 * airSeconds * airSeconds / 8;
                    Move(true, new FootMove
                    {
                        T0 = up, T1 = land, From = s.Left, To = landing + leftAfter * (spread / 2),
                        YawFrom = s.LeftYaw, YawTo = s.Yaw + angle, Air = true, AirHeight = height,
                    });
                    Move(false, new FootMove
                    {
                        T0 = up, T1 = land, From = s.Right, To = landing - leftAfter * (spread / 2),
                        YawFrom = s.RightYaw, YawTo = s.Yaw + angle, Air = true, AirHeight = height,
                    });
                    if (angle != 0)
                        Turn(up, land, angle);
                    Weight(t0, up, middle);
                    Weight(up, land, landing);
                    if (record)
                        _jumps.Add((t0, up, land, t1, height));
                    s.On = 'B';
                    break;
                }

                case SegKinds.Lift or SegKinds.Land:
                {
                    // A free body hangs where it is told to: no take-off, no arc.
                    if (seg.Dz != 0)
                    {
                        var glide = fwd * seg.Dz;
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
    }

    private static double Smooth(double u) => u * u * (3 - 2 * u);

    private static double Ease(string kind, double u) => kind switch
    {
        "linear" => u,
        // All of the way in the first half, then still. Eased at both ends: a movement that
        // starts at full speed is not one a body makes, and it doubled the peak speed.
        "sharp" => Smooth(Math.Min(1, u / 0.5)),
        _ => Smooth(u),
    };

    /// <summary>
    /// Where a part is at t, given its row of segments. A segment goes from wherever the part
    /// was when it began — the one before may not have arrived, if a narrower track took the
    /// part over — to its own target. A hold keeps the part where it was when the hold began.
    /// </summary>
    private static T Chain<T>(List<Seg> segs, double t, Func<Seg?, T> target, Func<T, T, double, T> blend)
    {
        int current = -1;
        for (int i = 0; i < segs.Count && segs[i].T0 <= t; i++)
            current = i;
        return ChainAt(segs, current, t, target, blend, 0);
    }

    private static T ChainAt<T>(List<Seg> segs, int index, double t, Func<Seg?, T> target, Func<T, T, double, T> blend, int depth)
    {
        if (index < 0 || depth > 200)
            return target(null);
        var seg = segs[index];
        if (seg.Kind == SegKinds.Hold)
            return ChainAt(segs, index - 1, seg.T0, target, blend, depth + 1);
        double u = seg.T1 > seg.T0 ? Math.Clamp((t - seg.T0) / (seg.T1 - seg.T0), 0, 1) : 1;
        double e = Ease(seg.Ease, u);
        return e >= 1 ? target(seg) : blend(ChainAt(segs, index - 1, seg.T0, target, blend, depth + 1), target(seg), e);
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

    private static Vec3 DirOf(Seg? seg, Vec3 neutral) => seg?.Dir is { } d ? new Vec3(d[0], d[1], d[2]) : neutral;

    private static Vec3 Direction(List<Seg> targets, double t, Vec3 neutral, Vec3 via) =>
        Chain(targets, t, seg => DirOf(seg, neutral), (a, b, e) => Blend(a, b, e, via)).Normalized();

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
                continue;
            }
            if (t <= move.T0)
                break;

            double u = (t - move.T0) / (move.T1 - move.T0), s = Smooth(u);
            if (move.Orbit)
            {
                pos = move.Pivot + (move.From - move.Pivot).RotateY(move.Angle * s);
                yaw = move.YawFrom + move.Angle * s;
                lift = move.Lift * Math.Sin(Math.PI * u);
            }
            else if (move.Air)
            {
                pos = Vec3.Lerp(move.From, move.To, u);
                yaw = move.YawFrom + (move.YawTo - move.YawFrom) * s;
                lift = move.AirHeight * 4 * u * (1 - u);
                air = true;
            }
            else
            {
                pos = Vec3.Lerp(move.From, move.To, s);
                yaw = move.YawFrom + (move.YawTo - move.YawFrom) * s;
                lift = move.Lift * Math.Sin(Math.PI * u);
                // A step forward rolls through the foot: it leaves heel first, toes last, and
                // lands on the heel with the toes up. A step back does the reverse.
                pitch = -move.Along * (u < 0.5 ? 0.6 : 0.5) * Math.Sin(2 * Math.PI * u);
            }
            break;
        }
    }

    private static bool IsDown(Seg seg) => seg.Dir is { } d && d[1] < -0.95;

    /// <summary>A free leg: how far it is off the floor (0..1) and where its ankle is from the hip.</summary>
    private static (double Weight, Vec3 Offset) LegGesture(List<Seg> targets, double t, Vec3 left, Vec3 fwd)
    {
        (double, Vec3) Target(Seg? seg)
        {
            if (seg is null || IsDown(seg) || seg.Dir is not { } d)
                return (0, default);
            return (1, (left * d[0] + Vec3.Up * d[1] + fwd * d[2]) * (Rig.Leg * 0.995 * seg.Ext));
        }

        return Chain(targets, t, Target, (a, b, e) =>
        {
            double weight = a.Item1 + (b.Item1 - a.Item1) * e;
            // Coming off the floor, or going back to it, the leg keeps the one shape it has.
            if (a.Item1 < 1e-6)
                return (weight, b.Item2);
            if (b.Item1 < 1e-6)
                return (weight, a.Item2);
            return (weight, Blend(a.Item2, b.Item2, e, fwd));
        });
    }

    private double Clamp(double t) => _loop ? t : Math.Clamp(t, 0, _length);

    private CoreState Core(double t)
    {
        var c = new CoreState { Yaw = YawAt(t) };
        c.Fwd = Vec3.Forward(c.Yaw);
        c.Left = Vec3.Left(c.Yaw);
        FootAt(_leftMoves, _leftStart, _leftYawStart, t, out c.LPos, out c.LYaw, out c.LLift, out c.LAir, out c.LPitch);
        FootAt(_rightMoves, _rightStart, _rightYawStart, t, out c.RPos, out c.RYaw, out c.RLift, out c.RAir, out c.RPitch);
        (c.GwL, c.GoL) = LegGesture(_leftLeg, t, c.Left, c.Fwd);
        (c.GwR, c.GoR) = LegGesture(_rightLeg, t, c.Left, c.Fwd);
        c.Float = _float.Eval(t);

        // The weight goes over the standing foot as far as the other leg is off the floor
        // (a body that hangs in the air has no weight to shift).
        var p = new Vec3(_weightX.Eval(t), 0, _weightZ.Eval(t));
        if (c.Float < 0.01)
        {
            if (c.GwL > 0)
                p = Vec3.Lerp(p, c.RPos, 0.85 * c.GwL);
            if (c.GwR > 0)
                p = Vec3.Lerp(p, c.LPos, 0.85 * c.GwR);
        }

        var torso = Direction(_torso, t, Vec3.Up, new Vec3(0, 0, 1));
        c.TorsoUp = (c.Left * torso.X + Vec3.Up * torso.Y + c.Fwd * torso.Z).Normalized();
        // A leaning torso is balanced by the hips going the other way.
        p -= new Vec3(c.TorsoUp.X, 0, c.TorsoUp.Z) * 0.18;
        c.P = p;

        // The height is a row of its own, read like a gesture: it can be taken over half-way.
        double level = Chain(_height, t, seg => seg?.Level ?? 0, (a, b, e) => a + (b - a) * e);
        c.Raise = Math.Max(0, level) * 0.07;
        double reach = Rig.Leg * 0.995;
        double y = Rig.AnkleHeight + reach - 0.30 * Math.Max(0, -level) + c.Raise;

        // The pelvis is as high as it can be with both planted feet still in reach.
        double Cap(Vec3 hip, Vec3 foot, bool free, double raise)
        {
            if (free)
                return double.MaxValue;
            double distance = (new Vec3(hip.X, 0, hip.Z) - new Vec3(foot.X, 0, foot.Z)).Length;
            return Rig.AnkleHeight + raise + Math.Sqrt(Math.Max(0.04, reach * reach - distance * distance));
        }

        y = Math.Min(y, Cap(p + c.Left * Rig.HipHalf, c.LPos, c.LAir || c.GwL > 0.05, c.Raise));
        y = Math.Min(y, Cap(p - c.Left * Rig.HipHalf, c.RPos, c.RAir || c.GwR > 0.05, c.Raise));
        c.RawY = y;
        return c;
    }

    private double JumpOffset(double t)
    {
        foreach (var jump in _jumps)
        {
            if (t < jump.T0 || t > jump.T1)
                continue;
            if (t < jump.Up)
                return -0.12 * Math.Sin(Math.PI * (t - jump.T0) / (jump.Up - jump.T0));
            if (t < jump.Land)
            {
                double u = (t - jump.Up) / (jump.Land - jump.Up);
                return jump.Height * 4 * u * (1 - u);
            }
            return -0.08 * Math.Sin(Math.PI * (t - jump.Land) / (jump.T1 - jump.Land));
        }
        return 0;
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

    private Trunk TrunkAt(double t)
    {
        var c = Core(t);

        // Smooth the height over five frames: the reach limit has corners where a foot lands.
        double y = (Core(Clamp(t - 2 * _dt)).RawY + Core(Clamp(t - _dt)).RawY + c.RawY + Core(Clamp(t + _dt)).RawY + Core(Clamp(t + 2 * _dt)).RawY) / 5;
        y += JumpOffset(t);

        var trunk = new Trunk { C = c, Pelvis = new Vec3(c.P.X, y, c.P.Z), Up = c.TorsoUp };
        trunk.Left = (c.Left - trunk.Up * c.Left.Dot(trunk.Up)).Normalized();
        trunk.Fwd = trunk.Left.Cross(trunk.Up);
        trunk.Chest = trunk.Pelvis + trunk.Up * Rig.Spine;
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
        var c = trunk.C;
        Vec3 pelvis = trunk.Pelvis, chest = trunk.Chest, head = trunk.Head;
        Vec3 torsoUp = trunk.Up, torsoLeft = trunk.Left, torsoFwd = trunk.Fwd;
        var lifted = Vec3.Up * c.Float;

        Vec3 Gaze(Seg? seg)
        {
            if (seg?.Kind == SegKinds.Reach && _others(seg.Target!) is { } other)
            {
                // Look at it: the direction from this head, said in the chest's own frame.
                var toward = (other.PlaceAt(seg.Part!, 1, t, this) - (head + lifted)).Normalized();
                return new Vec3(toward.Dot(torsoLeft), toward.Dot(torsoUp), toward.Dot(torsoFwd));
            }
            return DirOf(seg, new Vec3(0, 0, 1));
        }

        var gaze = Chain(_head, t, Gaze, (a, b, e) => Blend(a, b, e, new Vec3(0, 0, 1))).Normalized();
        var nose = head + (torsoLeft * gaze.X + torsoUp * gaze.Y + torsoFwd * gaze.Z) * 0.12;

        var joints = new Vec3[Rig.Joints.Length];
        joints[Rig.Pelvis] = pelvis;
        joints[Rig.Chest] = chest;
        joints[Rig.Head] = head;
        joints[Rig.Nose] = nose;

        // Legs.
        for (int side = 1; side >= -1; side -= 2)
        {
            bool isLeft = side == 1;
            var hip = pelvis + c.Left * (Rig.HipHalf * side);
            var floor = isLeft ? c.LPos : c.RPos;
            double lift = isLeft ? c.LLift : c.RLift;
            bool air = isLeft ? c.LAir : c.RAir;
            double gesture = isLeft ? c.GwL : c.GwR;
            double otherLift = isLeft ? c.RLift : c.LLift;
            double otherGesture = isLeft ? c.GwR : c.GwL;

            var ankle = new Vec3(floor.X, Rig.AnkleHeight + lift + (air ? 0 : c.Raise), floor.Z);
            if (gesture > 0)
            {
                ankle = Vec3.Lerp(ankle, hip + (isLeft ? c.GoL : c.GoR), gesture);
                if (gesture > 0.5 && (otherLift > 0.01 || otherGesture > 0.5))
                    problems.BothFeetOff = isLeft ? Channels.LeftLeg : Channels.RightLeg;
            }
            else if (!air && (ankle - hip).Length > Rig.Leg + 0.02)
            {
                problems.Overreach = true;
            }

            var (knee, reached) = TwoBone(hip, ankle, Rig.Thigh, Rig.Shin, Rig.KneePole(c.Left, c.Fwd, side));
            bool offFloor = reached.Y > Rig.AnkleHeight + c.Raise + 0.005;
            var footFwd = Vec3.Forward(isLeft ? c.LYaw : c.RYaw);
            // A gesturing leg has taken the foot over: the roll of a step fades as it does.
            double pitch = (isLeft ? c.LPitch : c.RPitch) * (1 - gesture);
            var toe = reached + footFwd * (Rig.FootLength * Math.Cos(pitch));
            toe = new Vec3(toe.X, offFloor ? Math.Max(0.02, reached.Y - 0.05 + Rig.FootLength * Math.Sin(pitch)) : 0.02, toe.Z);

            int first = isLeft ? Rig.LHip : Rig.RHip;
            joints[first] = hip;
            joints[first + 1] = knee;
            joints[first + 2] = reached;
            joints[first + 3] = toe;
        }

        // Arms.
        for (int side = 1; side >= -1; side -= 2)
        {
            bool isLeft = side == 1;
            var shoulder = trunk.Shoulder(side);

            Vec3 Offset(Seg? seg)
            {
                if (seg is null)
                    return (c.Left * (0.10 * side) - Vec3.Up + c.Fwd * 0.04).Normalized() * (Rig.Arm * 0.97);
                if (seg.Kind == SegKinds.Reach)
                    return _others(seg.Target!) is { } other ? other.PlaceAt(seg.Part!, side, t, this, seg.Level) - (shoulder + lifted) : Offset(null);
                if (seg.Kind == SegKinds.Anchor)
                {
                    var place = seg.Anchor switch
                    {
                        "hip" => pelvis + c.Left * (0.17 * side) + Vec3.Up * 0.06 + c.Fwd * 0.02,
                        "chest" => chest + torsoFwd * 0.13 - torsoUp * 0.14 + torsoLeft * (0.04 * side),
                        "head" => head + torsoLeft * (0.13 * side) + torsoUp * 0.02,
                        "chin" => head + torsoFwd * 0.14 - torsoUp * 0.12 + torsoLeft * (0.06 * side),
                        "belly" => pelvis + torsoUp * 0.18 + torsoFwd * 0.13 + torsoLeft * (0.03 * side),
                        "across.shoulder" => chest - torsoLeft * (Rig.ShoulderHalf * 0.8 * side) - torsoUp * Rig.ShoulderDrop + torsoFwd * 0.09,
                        "clap" => chest + torsoFwd * 0.28 - torsoUp * 0.12 + torsoLeft * (0.012 * side),
                        _ => shoulder + torsoUp * 0.06 + torsoFwd * 0.05,
                    };
                    return place - shoulder;
                }
                var d = seg.Dir!;
                // The room's frame by default (a notator's "standard cross": down is the floor
                // even when the torso leans); the torso's own frame when the event says @body.
                var direction = seg.Body
                    ? torsoLeft * d[0] + torsoUp * d[1] + torsoFwd * d[2]
                    : c.Left * d[0] + Vec3.Up * d[1] + c.Fwd * d[2];
                return direction * (Rig.Arm * 0.999 * seg.Ext);
            }

            var hand = shoulder + Chain(isLeft ? _leftArm : _rightArm, t, Offset, (a, b, e) => Blend(a, b, e, c.Fwd));
            var (elbow, reachedHand) = TwoBone(shoulder, hand, Rig.UpperArm, Rig.Forearm, Rig.ElbowPole(c.Left, c.Fwd, side));

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
