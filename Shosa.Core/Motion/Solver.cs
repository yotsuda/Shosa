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

    public static MotionClip Solve(MotionIr ir, List<Diagnostic> diagnostics, int fps = 30)
    {
        var clip = new MotionClip
        {
            Title = ir.Title,
            Fps = fps,
            Tempo = ir.Tempo,
            BeatsPerBar = ir.BeatsPerBar,
            TotalBeats = ir.TotalBeats,
            Phrases = ir.Phrases,
        };

        double beatsPerFrame = ir.Tempo / (60.0 * fps);
        int frames = (int)Math.Ceiling(ir.TotalBeats / beatsPerFrame - 1e-9) + 1;

        foreach (var figure in ir.Figures)
        {
            var solver = new FigureSolver(figure, 60.0 / ir.Tempo);
            var clipFigure = new ClipFigure { Id = figure.Id, Name = figure.Name };
            var flagged = new HashSet<(Seg, string)>();
            Vec3[]? previous = null;

            void Flag(string channel, double t, string code, DiagnosticSeverity severity, string message)
            {
                var seg = SegAt(figure, channel, t);
                if (seg is not null && flagged.Add((seg, code)))
                    diagnostics.Add(new Diagnostic(code, severity, new TextSpan(seg.Src, seg.Len), message));
            }

            for (int f = 0; f < frames; f++)
            {
                double t = Math.Min(ir.TotalBeats, f * beatsPerFrame);
                var pose = solver.Pose(t, beatsPerFrame, out var problems);

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
                        "The foot is put farther away than the leg is long. Make the step smaller, or bend the knees (a low level).");
                if (problems.BothFeetOff is { } leg)
                    Flag(leg, t, "SH2004", DiagnosticSeverity.Error,
                        "This leg leaves the floor while the other one is not carrying the weight. Step onto the other foot first.");

                if (previous is not null)
                {
                    Speed(Rig.LHand, Channels.LeftArm, MaxHandSpeed, "hand");
                    Speed(Rig.RHand, Channels.RightArm, MaxHandSpeed, "hand");
                    // A foot moves because of a leg gesture if one is going on, else because of a step.
                    Speed(Rig.LAnkle, SegAt(figure, Channels.LeftLeg, t) is null ? Channels.Support : Channels.LeftLeg, MaxFootSpeed, "foot");
                    Speed(Rig.RAnkle, SegAt(figure, Channels.RightLeg, t) is null ? Channels.Support : Channels.RightLeg, MaxFootSpeed, "foot");
                }
                previous = pose;

                void Speed(int joint, string channel, double limit, string what)
                {
                    double speed = (pose[joint] - previous![joint]).Length * fps;
                    if (speed > limit)
                        Flag(channel, t, "SH2003", DiagnosticSeverity.Warning,
                            $"Too fast to perform: the {what} would move at about {speed:0} m/s. Give it more beats, or lower the tempo.");
                }
            }

            clip.Figures.Add(clipFigure);
            foreach (var (channel, segs) in figure.Tracks)
            {
                foreach (var seg in segs)
                    clip.Spans.Add(new ClipSpan(seg.T0, seg.T1, seg.Src, seg.Len, figure.Id, channel));
            }
        }

        return clip;
    }

    private static Seg? SegAt(FigureIr figure, string channel, double t)
    {
        if (!figure.Tracks.TryGetValue(channel, out var segs))
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
    }

    private struct CoreState
    {
        public double Yaw;
        public Vec3 Fwd, Left;
        public Vec3 LPos, RPos;
        public double LYaw, RYaw, LLift, RLift;
        public bool LAir, RAir;
        public double GwL, GwR;     // how far each leg is into a gesture, 0..1
        public Vec3 GoL, GoR;       // the gesturing ankle, from its hip
        public Vec3 TorsoUp;
        public Vec3 P;              // pelvis over the floor
        public double RawY, Raise;
    }

    private readonly double _secondsPerBeat;
    private readonly double _yawStart;
    private readonly Vec3 _leftStart, _rightStart;
    private readonly List<FootMove> _leftMoves = [], _rightMoves = [];
    private readonly Curve _weightX = new(), _weightZ = new(), _level = new();
    private readonly List<(double T0, double T1, double From, double To)> _turns = [];
    private readonly List<(double T0, double Up, double Land, double T1, double Height)> _jumps = [];
    private readonly List<Seg> _leftArm, _rightArm, _leftLeg, _rightLeg, _torso, _head;

    public FigureSolver(FigureIr figure, double secondsPerBeat)
    {
        _secondsPerBeat = secondsPerBeat;
        _yawStart = figure.FacingDeg * Math.PI / 180;
        var origin = new Vec3(figure.X, 0, figure.Z);
        _leftStart = origin + Vec3.Left(_yawStart) * (Rig.Stance / 2);
        _rightStart = origin - Vec3.Left(_yawStart) * (Rig.Stance / 2);

        List<Seg> Targets(string channel) =>
            figure.Tracks.TryGetValue(channel, out var segs) ? segs.Where(s => s.Kind != SegKinds.Hold).ToList() : [];

        _leftArm = Targets(Channels.LeftArm);
        _rightArm = Targets(Channels.RightArm);
        _leftLeg = Targets(Channels.LeftLeg);
        _rightLeg = Targets(Channels.RightLeg);
        _torso = Targets(Channels.Torso);
        _head = Targets(Channels.Head);

        BuildSupport(figure.Tracks.TryGetValue(Channels.Support, out var support) ? support : []);
    }

    /// <summary>
    /// Plays the support track once, in order, and writes down what it implies: where each
    /// foot moves and when, where the weight is at each landing, the facing, the height.
    /// </summary>
    private void BuildSupport(List<Seg> support)
    {
        Vec3 left = _leftStart, right = _rightStart;
        double leftYaw = _yawStart, rightYaw = _yawStart, yaw = _yawStart;
        char supporting = 'B';   // L, R or Both

        Vec3 weight = (left + right) * 0.5;
        double weightTime = 0;
        _weightX.Add(0, weight.X);
        _weightZ.Add(0, weight.Z);

        void Weight(double t0, double t1, Vec3 to)
        {
            if (t0 > weightTime)
            {
                // Nothing was written since the last key: stay there until this segment starts.
                _weightX.Add(t0, weight.X);
                _weightZ.Add(t0, weight.Z);
            }
            _weightX.Add(t1, to.X);
            _weightZ.Add(t1, to.Z);
            weight = to;
            weightTime = t1;
        }

        double level = 0, levelTime = 0;
        _level.Add(0, 0);

        void Level(double t0, double t1, double to)
        {
            if (t0 > levelTime)
                _level.Add(t0, level);
            _level.Add(t1, to);
            level = to;
            levelTime = t1;
        }

        foreach (var seg in support)
        {
            var fwd = Vec3.Forward(yaw);
            var leftward = Vec3.Left(yaw);

            switch (seg.Kind)
            {
                case SegKinds.Step:
                {
                    bool isLeft = seg.Foot == "left";
                    var from = isLeft ? left : right;
                    var other = isLeft ? right : left;
                    var target = other + fwd * seg.Dz + (isLeft ? leftward : -leftward) * seg.Dx;
                    var move = new FootMove
                    {
                        T0 = seg.T0,
                        T1 = seg.T1,
                        From = from,
                        To = target,
                        YawFrom = isLeft ? leftYaw : rightYaw,
                        YawTo = yaw,
                        Lift = Math.Clamp(0.05 + 0.12 * (target - from).Length, 0.06, 0.15),
                    };
                    if (isLeft)
                    {
                        _leftMoves.Add(move);
                        left = target;
                        leftYaw = yaw;
                        supporting = 'L';
                    }
                    else
                    {
                        _rightMoves.Add(move);
                        right = target;
                        rightYaw = yaw;
                        supporting = 'R';
                    }
                    // At the landing the weight is between the feet, a little toward the new one.
                    // Keyed only at landings, the pelvis passes over the standing foot in mid-step.
                    Weight(seg.T0, seg.T1, Vec3.Lerp((left + right) * 0.5, target, 0.3));
                    if (seg.Level is { } stepLevel)
                        Level(seg.T0, seg.T1, stepLevel);
                    break;
                }

                case SegKinds.Together:
                {
                    if (supporting != 'B')
                    {
                        bool moveLeft = supporting == 'R';
                        var from = moveLeft ? left : right;
                        var other = moveLeft ? right : left;
                        var target = other + (moveLeft ? leftward : -leftward) * Rig.Stance;
                        var move = new FootMove
                        {
                            T0 = seg.T0,
                            T1 = seg.T1,
                            From = from,
                            To = target,
                            YawFrom = moveLeft ? leftYaw : rightYaw,
                            YawTo = yaw,
                            Lift = Math.Clamp(0.05 + 0.12 * (target - from).Length, 0.06, 0.15),
                        };
                        if (moveLeft)
                        {
                            _leftMoves.Add(move);
                            left = target;
                            leftYaw = yaw;
                        }
                        else
                        {
                            _rightMoves.Add(move);
                            right = target;
                            rightYaw = yaw;
                        }
                    }
                    supporting = 'B';
                    Weight(seg.T0, seg.T1, (left + right) * 0.5);
                    Level(seg.T0, seg.T1, seg.Level ?? 0);
                    break;
                }

                case SegKinds.Level:
                    supporting = 'B';
                    Weight(seg.T0, seg.T1, (left + right) * 0.5);
                    Level(seg.T0, seg.T1, seg.Level ?? 0);
                    break;

                case SegKinds.Turn:
                {
                    double angle = seg.TurnDeg * Math.PI / 180;
                    var pivot = supporting == 'L' ? left : supporting == 'R' ? right : (left + right) * 0.5;
                    var newLeft = pivot + (left - pivot).RotateY(angle);
                    var newRight = pivot + (right - pivot).RotateY(angle);
                    _leftMoves.Add(new FootMove
                    {
                        T0 = seg.T0, T1 = seg.T1, From = left, To = newLeft, YawFrom = leftYaw, YawTo = leftYaw + angle,
                        Orbit = true, Pivot = pivot, Angle = angle, Lift = supporting == 'L' ? 0 : 0.03,
                    });
                    _rightMoves.Add(new FootMove
                    {
                        T0 = seg.T0, T1 = seg.T1, From = right, To = newRight, YawFrom = rightYaw, YawTo = rightYaw + angle,
                        Orbit = true, Pivot = pivot, Angle = angle, Lift = supporting == 'R' ? 0 : 0.03,
                    });
                    left = newLeft;
                    right = newRight;
                    leftYaw += angle;
                    rightYaw += angle;
                    _turns.Add((seg.T0, seg.T1, yaw, yaw + angle));
                    yaw += angle;
                    Weight(seg.T0, seg.T1, pivot + (weight - pivot).RotateY(angle));
                    break;
                }

                case SegKinds.Jump:
                {
                    double duration = seg.T1 - seg.T0;
                    double up = seg.T0 + 0.35 * duration, land = seg.T0 + 0.85 * duration;
                    var middle = (left + right) * 0.5;
                    var landing = middle + fwd * seg.Dz;
                    double spread = seg.Spread ?? Math.Abs((left - right).Dot(leftward));
                    var newLeft = landing + leftward * (spread / 2);
                    var newRight = landing - leftward * (spread / 2);
                    // The time in the air fixes the height: h = g T^2 / 8.
                    double airSeconds = (land - up) * _secondsPerBeat;
                    double height = 9.81 * airSeconds * airSeconds / 8;
                    _leftMoves.Add(new FootMove
                    {
                        T0 = up, T1 = land, From = left, To = newLeft, YawFrom = leftYaw, YawTo = yaw, Air = true, AirHeight = height,
                    });
                    _rightMoves.Add(new FootMove
                    {
                        T0 = up, T1 = land, From = right, To = newRight, YawFrom = rightYaw, YawTo = yaw, Air = true, AirHeight = height,
                    });
                    left = newLeft;
                    right = newRight;
                    leftYaw = rightYaw = yaw;
                    Weight(seg.T0, up, middle);
                    Weight(up, land, landing);
                    _jumps.Add((seg.T0, up, land, seg.T1, height));
                    supporting = 'B';
                    break;
                }

                default:
                    Weight(seg.T0, seg.T1, weight);
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

    /// <summary>The target being approached at t, the one before it, and how far along (eased).</summary>
    private static (Seg? Previous, Seg? Current, double E) Eval(List<Seg> targets, double t)
    {
        Seg? previous = null, current = null;
        foreach (var seg in targets)
        {
            if (seg.T0 > t)
                break;
            previous = current;
            current = seg;
        }
        if (current is null)
            return (null, null, 1);
        double u = current.T1 > current.T0 ? Math.Clamp((t - current.T0) / (current.T1 - current.T0), 0, 1) : 1;
        return (previous, current, Ease(current.Ease, u));
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

    private static Vec3 Direction(List<Seg> targets, double t, Vec3 neutral, Vec3 via)
    {
        var (previous, current, e) = Eval(targets, t);
        return Blend(DirOf(previous, neutral), DirOf(current, neutral), e, via).Normalized();
    }

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

    private void FootAt(List<FootMove> moves, Vec3 start, double t, out Vec3 pos, out double yaw, out double lift, out bool air)
    {
        pos = start;
        yaw = _yawStart;
        lift = 0;
        air = false;
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
            }
            break;
        }
    }

    private static bool IsDown(Seg seg) => seg.Dir is { } d && d[1] < -0.95;

    /// <summary>A free leg: how far it is off the floor (0..1) and where its ankle is from the hip.</summary>
    private static (double Weight, Vec3 Offset) LegGesture(List<Seg> targets, double t, Vec3 left, Vec3 fwd)
    {
        var (previous, current, e) = Eval(targets, t);
        bool previousDown = previous is null || IsDown(previous);
        bool currentDown = current is null || IsDown(current);

        Vec3 Offset(Seg seg)
        {
            var d = seg.Dir!;
            return (left * d[0] + Vec3.Up * d[1] + fwd * d[2]) * (Rig.Leg * 0.995 * seg.Ext);
        }

        if (previousDown && currentDown)
            return (0, default);
        if (previousDown)
            return (e, Offset(current!));
        if (currentDown)
            return (1 - e, Offset(previous!));
        return (1, Blend(Offset(previous!), Offset(current!), e, fwd));
    }

    private CoreState Core(double t)
    {
        var c = new CoreState { Yaw = YawAt(t) };
        c.Fwd = Vec3.Forward(c.Yaw);
        c.Left = Vec3.Left(c.Yaw);
        FootAt(_leftMoves, _leftStart, t, out c.LPos, out c.LYaw, out c.LLift, out c.LAir);
        FootAt(_rightMoves, _rightStart, t, out c.RPos, out c.RYaw, out c.RLift, out c.RAir);
        (c.GwL, c.GoL) = LegGesture(_leftLeg, t, c.Left, c.Fwd);
        (c.GwR, c.GoR) = LegGesture(_rightLeg, t, c.Left, c.Fwd);

        // The weight goes over the standing foot as far as the other leg is off the floor.
        var p = new Vec3(_weightX.Eval(t), 0, _weightZ.Eval(t));
        if (c.GwL > 0)
            p = Vec3.Lerp(p, c.RPos, 0.85 * c.GwL);
        if (c.GwR > 0)
            p = Vec3.Lerp(p, c.LPos, 0.85 * c.GwR);

        var torso = Direction(_torso, t, Vec3.Up, new Vec3(0, 0, 1));
        c.TorsoUp = (c.Left * torso.X + Vec3.Up * torso.Y + c.Fwd * torso.Z).Normalized();
        // A leaning torso is balanced by the hips going the other way.
        p -= new Vec3(c.TorsoUp.X, 0, c.TorsoUp.Z) * 0.18;
        c.P = p;

        double level = _level.Eval(t);
        c.Raise = Math.Max(0, level) * 0.07;
        double reach = Rig.Leg * 0.985;
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

    public Vec3[] Pose(double t, double dt, out PoseProblems problems)
    {
        problems = default;
        var c = Core(t);

        // Smooth the height over five frames: the reach limit has corners where a foot lands.
        double y = (Core(t - 2 * dt).RawY + Core(t - dt).RawY + c.RawY + Core(t + dt).RawY + Core(t + 2 * dt).RawY) / 5;
        y += JumpOffset(t);

        var pelvis = new Vec3(c.P.X, y, c.P.Z);
        var torsoUp = c.TorsoUp;
        var torsoLeft = (c.Left - torsoUp * c.Left.Dot(torsoUp)).Normalized();
        var torsoFwd = torsoLeft.Cross(torsoUp);

        var chest = pelvis + torsoUp * Rig.Spine;
        var head = chest + torsoUp * Rig.HeadUp;
        var gaze = Direction(_head, t, new Vec3(0, 0, 1), new Vec3(0, 0, 1));
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
            var toe = reached + footFwd * Rig.FootLength;
            toe = new Vec3(toe.X, offFloor ? reached.Y - 0.05 : 0.02, toe.Z);

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
            var shoulder = chest + torsoLeft * (Rig.ShoulderHalf * side) - torsoUp * Rig.ShoulderDrop;

            Vec3 Offset(Seg? seg)
            {
                if (seg is null)
                    return (c.Left * (0.10 * side) - Vec3.Up + c.Fwd * 0.04).Normalized() * (Rig.Arm * 0.97);
                if (seg.Kind == SegKinds.Anchor)
                {
                    var place = seg.Anchor switch
                    {
                        "hip" => pelvis + c.Left * (0.17 * side) + Vec3.Up * 0.06 + c.Fwd * 0.02,
                        "chest" => chest + torsoFwd * 0.13 - torsoUp * 0.14 + torsoLeft * (0.04 * side),
                        "head" => head + torsoLeft * (0.13 * side) + torsoUp * 0.02,
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

            var (previous, current, e) = Eval(isLeft ? _leftArm : _rightArm, t);
            var hand = shoulder + Blend(Offset(previous), Offset(current), e, c.Fwd);
            var (elbow, reachedHand) = TwoBone(shoulder, hand, Rig.UpperArm, Rig.Forearm, Rig.ElbowPole(c.Left, c.Fwd, side));

            int first = isLeft ? Rig.LShoulder : Rig.RShoulder;
            joints[first] = shoulder;
            joints[first + 1] = elbow;
            joints[first + 2] = reachedHand;
        }

        return joints;
    }
}
