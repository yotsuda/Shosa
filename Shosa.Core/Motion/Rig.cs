using Shosa.Core.Semantics;

namespace Shosa.Core.Motion;

/// <summary>The humanoid the solver moves: a 1.7 m figure, in metres.</summary>
public static class Rig
{
    public const double AnkleHeight = 0.08;
    public const double Thigh = 0.45;
    public const double Shin = 0.45;
    public const double Leg = Thigh + Shin;
    /// <summary>
    /// How much of its length a straight leg reaches, standing or in the air: a knee bent about 3.6°, which the
    /// eye reads as straight (a ballet dancer's standing leg), and still enough for the knee to know which way
    /// it bends. Was 0.995 (11.5°, a soft knee everywhere) until writing trial 19 (DESIGN.md 78.4).
    /// </summary>
    public const double StraightLeg = 0.9995;
    /// <summary>
    /// Degrees the knees are bent standing at `mid`, the height a row nobody wrote stands at: a soft knee, which
    /// reads as natural on a game character (the user chose 12° looking at 4°, 10°, 15° and 24° side by side,
    /// 2026-11-15, DESIGN.md 79.4). `straight` (and `high`, on the toes) stands on the straight leg.
    /// </summary>
    public const double SoftKnee = 12;
    public const double HipHalf = 0.10;
    public const double Spine = 0.50;
    public const double ShoulderHalf = 0.19;
    /// <summary>How far the shoulders sit below the top of the spine.</summary>
    public const double ShoulderDrop = 0.03;
    public const double UpperArm = 0.29;
    public const double Forearm = 0.31;
    public const double Arm = UpperArm + Forearm;
    /// <summary>Degrees the elbow is bent in an arm that just hangs.</summary>
    public const double RestElbow = 15;
    /// <summary>How far the hips come down at the level `low`, in metres.</summary>
    public const double LowDrop = 0.16;
    /// <summary>Degrees each half of a rounded back (`@round`) turns from the line between the pelvis and the chest.</summary>
    public const double RoundAngle = 15;
    public const double HeadUp = 0.24;
    public const double FootLength = 0.17;

    /// <summary>Distance between the feet when standing, and what the step words mean in metres.</summary>
    public const double Stance = 0.20;
    /// <summary>Distance between the ankles with the heels touching: the feet closed with the legs turned out all the way.</summary>
    public const double HeelsTogether = 0.08;
    /// <summary>Distance between the ankles with the feet side by side, touching (`together`, parallel).</summary>
    public const double Together = 0.10;
    /// <summary>How high a foot left in the air after landing on the other one is held, ankle above the floor.</summary>
    public const double HeldFoot = 0.15;
    public const double Stride = 0.50;
    public const double SideStep = 0.35;

    /// <summary>The waist is the middle of the spine: on the line from the pelvis to the chest unless the back is rounded.</summary>
    public const int Pelvis = 0, Waist = 1, Chest = 2, Head = 3, Nose = 4,
        LShoulder = 5, LElbow = 6, LHand = 7, RShoulder = 8, RElbow = 9, RHand = 10,
        LHip = 11, LKnee = 12, LAnkle = 13, LToe = 14, RHip = 15, RKnee = 16, RAnkle = 17, RToe = 18;

    /// <summary>Where an elbow points when it bends: down, back and a little out. Side is 1 for left, -1 for right.</summary>
    /// <summary>
    /// How far from root to end a limb of two bones reaches with its middle joint bent `bend`
    /// degrees (0 straight, as `@bent(N)` counts), as a share of its whole length: the law of cosines.
    /// </summary>
    public static double BentShare(double upper, double lower, double bend)
    {
        double angle = bend * Math.PI / 180;
        return Math.Sqrt(upper * upper + lower * lower + 2 * upper * lower * Math.Cos(angle)) / (upper + lower);
    }

    public static Vec3 ElbowPole(Vec3 left, Vec3 fwd, int side) => -Vec3.Up - fwd * 0.6 + left * (0.35 * side);

    /// <summary>
    /// Where a knee points when it bends: forward, a little out and up, in the frame of its own
    /// FOOT (the way the toes point on the floor), so a leg turned out bends its knee over the toes.
    /// The solver and <see cref="Skeleton"/> both take it so.
    /// </summary>
    public static Vec3 KneePole(Vec3 left, Vec3 fwd, int side) => fwd + left * (0.15 * side) + Vec3.Up * 0.1;

    public static readonly string[] Joints =
    [
        "pelvis", "waist", "chest", "head", "nose",
        "leftShoulder", "leftElbow", "leftHand", "rightShoulder", "rightElbow", "rightHand",
        "leftHip", "leftKnee", "leftAnkle", "leftToe", "rightHip", "rightKnee", "rightAnkle", "rightToe",
    ];

    /// <summary>Joint pairs to draw, each with the side it belongs to: c, l or r.</summary>
    public static readonly (int A, int B, char Side)[] Bones =
    [
        (Pelvis, Waist, 'c'), (Waist, Chest, 'c'), (Chest, Head, 'c'), (Head, Nose, 'c'),
        // The shoulder line belongs to the torso: coloured as an arm it reads as a bent upper arm.
        (Chest, LShoulder, 'c'), (LShoulder, LElbow, 'l'), (LElbow, LHand, 'l'),
        (Chest, RShoulder, 'c'), (RShoulder, RElbow, 'r'), (RElbow, RHand, 'r'),
        (Pelvis, LHip, 'l'), (LHip, LKnee, 'l'), (LKnee, LAnkle, 'l'), (LAnkle, LToe, 'l'),
        (Pelvis, RHip, 'r'), (RHip, RKnee, 'r'), (RKnee, RAnkle, 'r'), (RAnkle, RToe, 'r'),
    ];
}

/// <summary>The solved movement: every joint of every figure at every frame, in world metres.</summary>
public sealed class MotionClip
{
    /// <summary>The section this clip is, or the score or form it was made from ("" if unnamed).</summary>
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>The last frame is the first one again (moved on by the travel, unless <see cref="Inplace"/>).</summary>
    public bool Loop { get; set; }
    /// <summary>The even travel over the floor is taken out; each figure says what it was (<see cref="ClipFigure.Velocity"/>).</summary>
    public bool Inplace { get; set; }
    public int Fps { get; set; }
    /// <summary>Beats per minute at the start; <see cref="Tempos"/> has every one.</summary>
    public double Tempo { get; set; }
    /// <summary>Beats to the bar at the start; <see cref="Meters"/> has every time.</summary>
    public int BeatsPerBar { get; set; }
    /// <summary>Every tempo, the first at beat 0: a clip danced to a song that changes tempo has several.</summary>
    public List<TempoIr> Tempos { get; set; } = [];
    /// <summary>Every time, the first at beat 0.</summary>
    public List<MeterIr> Meters { get; set; } = [];
    public double TotalBeats { get; set; }
    public string[] Joints { get; set; } = Rig.Joints;
    public int[][] Bones { get; set; } = Rig.Bones.Select(b => new[] { b.A, b.B }).ToArray();
    public string BoneSides { get; set; } = new(Rig.Bones.Select(b => b.Side).ToArray());
    public List<ClipFigure> Figures { get; } = [];
    /// <summary>Which source span is being performed when: the link between text and movement.</summary>
    public List<ClipSpan> Spans { get; } = [];
    public List<SectionIr> Sections { get; set; } = [];
    /// <summary>The song this is danced to; null for a dance of its own.</summary>
    public MusicIr? Music { get; set; }
    /// <summary>The moments the author named (`!hit`), in time order.</summary>
    public List<ClipMark> Marks { get; } = [];

    private IReadOnlyList<TempoIr> Map => TempoMap.Of(Tempos, Tempo);

    /// <summary>The second a beat falls on. Frames are even in seconds; beats are not, where the tempo changes.</summary>
    public double SecondsAt(double beat) => TempoMap.Seconds(Map, beat);
    public double BeatOfFrame(int frame) => Math.Min(TotalBeats, TempoMap.Beat(Map, (double)frame / Fps));
    public int FrameOfBeat(double beat) => (int)Math.Round(FrameAt(beat));
    /// <summary>A beat as a position between frames: a beat seldom falls exactly on one.</summary>
    public double FrameAt(double beat) => SecondsAt(Math.Clamp(beat, 0, TotalBeats)) * Fps;
}

public sealed class ClipFigure
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>For an `inPlace` clip: the travel that was taken out, in metres per second (x, z).</summary>
    public double[]? Velocity { get; set; }
    /// <summary>
    /// Which way the figure faces at the first frame and at the last, radians: where its feet and
    /// its turns put it. The line of the hips does not say it (the hips can be turned over the feet).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double[] Facing { get; set; } = [0, 0];
    /// <summary>Which way the feet and the turns face the figure at every frame, radians (0 the front, more to its left).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<double> Yaws { get; } = [];
    /// <summary>One array per frame: x, y, z of each joint in <see cref="Rig.Joints"/> order.</summary>
    public List<float[]> Frames { get; } = [];

    /// <summary>
    /// How the body is turned at a position between frames, as the language counts it: the hips
    /// (`hips`), the line of the shoulders (`shoulders`) and the face, each from
    /// the way the feet face, measured on the floor.
    /// </summary>
    public BodyAngles Angles(double frame)
    {
        static double Degrees(double radians) => radians * 180 / Math.PI;
        int before = (int)Math.Floor(frame);
        double Yaw(int at) => Yaws.Count == 0 ? Facing[0] : Yaws[Math.Clamp(at, 0, Yaws.Count - 1)];
        double yaw = Yaw(before) + (Yaw(before + 1) - Yaw(before)) * (frame - before);
        Vec3 fwd = Vec3.Forward(yaw), left = Vec3.Left(yaw);

        // A line from the right joint to the left one points to the figure's left when it is square over the feet.
        double Line(int leftJoint, int rightJoint)
        {
            var across = Joint(frame, leftJoint) - Joint(frame, rightJoint);
            return Degrees(Math.Atan2(-across.Dot(fwd), across.Dot(left)));
        }
        // The face is turned as the line of the ears is, like the hips and the shoulders: the nose
        // on the floor would swing wide for a face looking down, a small turn of the head pointing
        // it far across the floor below. The ears are square to the nose and to the torso's up, as
        // the head is posed (Skeleton), so their line says the head's left and right whatever its up and down.
        var nose = Joint(frame, Rig.Nose) - Joint(frame, Rig.Head);
        var torsoUp = (Joint(frame, Rig.Head) - Joint(frame, Rig.Chest)).Normalized();
        var ears = torsoUp.Cross(nose.Normalized());
        double face = ears.Length < 1e-6 ? Line(Rig.LShoulder, Rig.RShoulder) : Degrees(Math.Atan2(-ears.Dot(fwd), ears.Dot(left)));
        double ahead = nose.Dot(fwd), aside = nose.Dot(left);
        // A joint's bend as `@bent(N)` counts it: how far the second bone turns away from the line of the first.
        double Bend(int root)
        {
            var upper = Joint(frame, root + 1) - Joint(frame, root);
            var lower = Joint(frame, root + 2) - Joint(frame, root + 1);
            return Degrees(Math.Acos(Math.Clamp(upper.Normalized().Dot(lower.Normalized()), -1, 1)));
        }
        return new BodyAngles(Degrees(Math.Atan2(Math.Sin(yaw), Math.Cos(yaw))), Line(Rig.LHip, Rig.RHip), Line(Rig.LShoulder, Rig.RShoulder),
            face, Degrees(Math.Atan2(nose.Y, Math.Sqrt(ahead * ahead + aside * aside))),
            Bend(Rig.LShoulder), Bend(Rig.RShoulder), Bend(Rig.LHip), Bend(Rig.RHip));
    }

    /// <summary>
    /// One array per frame: x, y, z, w of each bone of <see cref="Skeleton.Bones"/>, relative to
    /// its parent. With the pelvis position this is the same pose as <see cref="Frames"/>, in the
    /// form exporters need. Not in the JSON: the preview draws positions, and is sent every edit.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<float[]> Rotations { get; } = [];

    public Quat Rotation(int frame, int bone)
    {
        var f = Rotations[Math.Clamp(frame, 0, Rotations.Count - 1)];
        return new Quat(f[bone * 4], f[bone * 4 + 1], f[bone * 4 + 2], f[bone * 4 + 3]);
    }

    public Vec3 Joint(int frame, int joint)
    {
        var f = Frames[Math.Clamp(frame, 0, Frames.Count - 1)];
        return new Vec3(f[joint * 3], f[joint * 3 + 1], f[joint * 3 + 2]);
    }

    /// <summary>A joint at a position between two frames.</summary>
    public Vec3 Joint(double frame, int joint)
    {
        int before = (int)Math.Floor(frame);
        return Vec3.Lerp(Joint(before, joint), Joint(before + 1, joint), frame - before);
    }
}

/// <summary>
/// Degrees, more to the figure's left. <paramref name="Feet"/> is from the front of the room; the
/// hips, the shoulders and the face are from the way the feet face; <paramref name="FaceUp"/> is above level.
/// The elbows and the knees are how far each is bent, 0 straight (as `@bent(N)` counts).
/// </summary>
public readonly record struct BodyAngles(double Feet, double Hips, double Shoulders, double Face, double FaceUp,
    double LeftElbow = 0, double RightElbow = 0, double LeftKnee = 0, double RightKnee = 0);

/// <param name="Section">The section that writes the mark, and <paramref name="Play"/>: which play of it in the score (1 the first). With them an engine tells the first `hit` of a section played twice from the second.</param>
public sealed record ClipMark(double Beat, double Seconds, string Name, string Figure, string Section = "", int Play = 1);

public sealed record ClipSpan(double T0, double T1, int Start, int Length, string Figure, string Channel);
