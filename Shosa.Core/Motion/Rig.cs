using Shosa.Core.Semantics;

namespace Shosa.Core.Motion;

/// <summary>The humanoid the solver moves: a 1.7 m figure, in metres.</summary>
public static class Rig
{
    public const double AnkleHeight = 0.08;
    public const double Thigh = 0.45;
    public const double Shin = 0.45;
    public const double Leg = Thigh + Shin;
    public const double HipHalf = 0.10;
    public const double Spine = 0.50;
    public const double ShoulderHalf = 0.19;
    /// <summary>How far the shoulders sit below the top of the spine.</summary>
    public const double ShoulderDrop = 0.03;
    public const double UpperArm = 0.29;
    public const double Forearm = 0.31;
    public const double Arm = UpperArm + Forearm;
    public const double HeadUp = 0.24;
    public const double FootLength = 0.17;

    /// <summary>Distance between the feet when standing, and what the step words mean in metres.</summary>
    public const double Stance = 0.20;
    public const double Stride = 0.50;
    public const double SideStep = 0.35;

    public const int Pelvis = 0, Chest = 1, Head = 2, Nose = 3,
        LShoulder = 4, LElbow = 5, LHand = 6, RShoulder = 7, RElbow = 8, RHand = 9,
        LHip = 10, LKnee = 11, LAnkle = 12, LToe = 13, RHip = 14, RKnee = 15, RAnkle = 16, RToe = 17;

    /// <summary>Where an elbow points when it bends: down, back and a little out. Side is 1 for left, -1 for right.</summary>
    public static Vec3 ElbowPole(Vec3 left, Vec3 fwd, int side) => -Vec3.Up - fwd * 0.6 + left * (0.35 * side);

    /// <summary>Where a knee points when it bends: forward, a little out and up.</summary>
    public static Vec3 KneePole(Vec3 left, Vec3 fwd, int side) => fwd + left * (0.15 * side) + Vec3.Up * 0.1;

    public static readonly string[] Joints =
    [
        "pelvis", "chest", "head", "nose",
        "leftShoulder", "leftElbow", "leftHand", "rightShoulder", "rightElbow", "rightHand",
        "leftHip", "leftKnee", "leftAnkle", "leftToe", "rightHip", "rightKnee", "rightAnkle", "rightToe",
    ];

    /// <summary>Joint pairs to draw, each with the side it belongs to: c, l or r.</summary>
    public static readonly (int A, int B, char Side)[] Bones =
    [
        (Pelvis, Chest, 'c'), (Chest, Head, 'c'), (Head, Nose, 'c'),
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
    public string Title { get; set; } = "";
    public int Fps { get; set; }
    public double Tempo { get; set; }
    public int BeatsPerBar { get; set; }
    public double TotalBeats { get; set; }
    public string[] Joints { get; set; } = Rig.Joints;
    public int[][] Bones { get; set; } = Rig.Bones.Select(b => new[] { b.A, b.B }).ToArray();
    public string BoneSides { get; set; } = new(Rig.Bones.Select(b => b.Side).ToArray());
    public List<ClipFigure> Figures { get; } = [];
    /// <summary>Which source span is being performed when: the link between text and movement.</summary>
    public List<ClipSpan> Spans { get; } = [];
    public List<PhraseIr> Phrases { get; set; } = [];

    public double BeatOfFrame(int frame) => Math.Min(TotalBeats, frame * Tempo / (60.0 * Fps));
    public int FrameOfBeat(double beat) => (int)Math.Round(Math.Clamp(beat, 0, TotalBeats) * 60.0 * Fps / Tempo);
    /// <summary>A beat as a position between frames: a beat seldom falls exactly on one.</summary>
    public double FrameAt(double beat) => Math.Clamp(beat, 0, TotalBeats) * 60.0 * Fps / Tempo;
}

public sealed class ClipFigure
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>One array per frame: x, y, z of each joint in <see cref="Rig.Joints"/> order.</summary>
    public List<float[]> Frames { get; } = [];

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

public sealed record ClipSpan(double T0, double T1, int Start, int Length, string Figure, string Channel);
