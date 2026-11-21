using System.Globalization;
using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Output;

namespace Shosa.Tests;

/// <summary>
/// The skeleton with rotations says the same thing as the joint positions the solver made:
/// turning the bones and walking down the hierarchy puts every joint back where it was.
/// </summary>
public class SkeletonTests
{
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Rotations_put_every_joint_back_where_the_solver_had_it(string name)
    {
        var clip = Samples.Compile(name).Clip;
        foreach (var figure in clip.Figures)
        {
            Assert.Equal(figure.Frames.Count, figure.Rotations.Count);
            for (int f = 0; f < figure.Frames.Count; f++)
            {
                var local = Enumerable.Range(0, Skeleton.Bones.Length).Select(b => figure.Rotation(f, b)).ToArray();
                var (heads, ends) = Skeleton.Positions(figure.Joint(f, Rig.Pelvis), local);
                Compare(name, f, figure, heads, ends);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void A_foot_in_the_air_never_pulls_its_toes_further_up_than_an_ankle_can(string name)
    {
        // DESIGN.md 104: swinging a step, the toes went up from level while the bent knee leaned the shin back, and the ankle closed to 50°.
        var clip = Samples.Compile(name).Clip;
        foreach (var figure in clip.Figures)
        {
            for (int f = 0; f < figure.Frames.Count; f++)
            {
                foreach (var (knee, ankle, toe) in new[] { (Rig.LKnee, Rig.LAnkle, Rig.LToe), (Rig.RKnee, Rig.RAnkle, Rig.RToe) })
                {
                    Vec3 k = figure.Joint(f, knee), a = figure.Joint(f, ankle), t = figure.Joint(f, toe);
                    if (t.Y < 0.03 || a.Y < Rig.AnkleHeight + 0.01)
                        continue;   // on the floor: the foot lies as the floor has it
                    double angle = Math.Acos(Math.Clamp((k - a).Normalized().Dot((t - a).Normalized()), -1, 1)) * 180 / Math.PI;
                    Assert.True(angle > 99.5, $"{name} {figure.Id} frame {f}: the ankle is {angle:0.0}°");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void A_reader_of_the_bvh_file_finds_the_same_joints(string name)
    {
        var clip = Samples.Compile(name).Clip;
        foreach (var figure in clip.Figures)
        {
            var bvh = BvhFile.Parse(Bvh.Render(clip, figure));

            Assert.Equal(Skeleton.Bones.Select(b => b.Name), bvh.Bones.Select(b => b.Name));
            Assert.Equal(figure.Frames.Count, bvh.Frames.Count);
            Assert.Equal(1.0 / clip.Fps, bvh.FrameTime, 5);
            for (int f = 0; f < bvh.Frames.Count; f++)
            {
                var (heads, ends) = bvh.Positions(f);
                Compare(name, f, figure, heads, ends);
            }
        }
    }

    [Fact]
    public void Standing_still_is_the_rest_pose_with_the_arms_down()
    {
        // Nothing is twisted that need not be: the hips, torso and head have no rotation at all,
        // and an arm hanging at the side is the T-pose arm turned about the forward axis only.
        var figure = ShosaCompilation.Compile(Samples.Solo("    steps { then:4 }", "tempo 100\ntime 4/4\n")).Clip.Figures[0];

        foreach (int bone in new[] { Skeleton.Hips, Skeleton.Spine, Skeleton.Head })
            Assert.True(Math.Abs(figure.Rotation(0, bone).W) > 0.9999, $"{Skeleton.Bones[bone].Name} is turned");

        var (z, x, y) = figure.Rotation(0, Skeleton.LUpperArm).ToEulerZxy();
        // (Not exactly: the arm hangs a little out, and the elbow points back and some 30 degrees out.)
        Assert.True(Math.Abs(z + 90) < 15 && Math.Abs(x) < 35 && Math.Abs(y) < 20, $"left upper arm: z {z:0} x {x:0} y {y:0}");
    }

    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void No_bone_flips_between_two_frames(string name)
    {
        // Positions can be right while a limb is twisted half a turn about its own length from
        // one frame to the next (the bending plane read the other way round). A stick figure
        // does not show it; a mesh does.
        var clip = Samples.Compile(name).Clip;
        foreach (var figure in clip.Figures)
        {
            for (int f = 1; f < figure.Rotations.Count; f++)
            {
                for (int b = 0; b < Skeleton.Bones.Length; b++)
                {
                    Quat before = figure.Rotation(f - 1, b), after = figure.Rotation(f, b);
                    double dot = Math.Abs(before.X * after.X + before.Y * after.Y + before.Z * after.Z + before.W * after.W);
                    double degrees = 2 * Math.Acos(Math.Min(1, dot)) * 180 / Math.PI;
                    Assert.True(degrees < 45, $"{name}: {Skeleton.Bones[b].Name} turns {degrees:0} degrees at frame {f}");
                }
            }
        }
    }

    [Fact]
    public void Clapping_hands_lie_palm_to_palm_and_do_not_run_into_each_other()
    {
        // The forearms come in at a slant from the shoulders; hands carried on along them crossed each
        // other's fingers on a character (DESIGN.md 95). Facing +z, the figure's left is +x.
        var figure = ShosaCompilation.Compile(Samples.Solo("    arms { down:2 clap:2 then }", "tempo 60\ntime 4/4\n")).Clip.Figures[0];
        int frame = 2 * 30;
        var (heads, ends) = Skeleton.Positions(figure.Joint(frame, Rig.Pelvis), Enumerable.Range(0, Skeleton.Bones.Length).Select(b => figure.Rotation(frame, b)).ToList());
        Vec3 leftWrist = heads[Skeleton.LHand], rightWrist = heads[Skeleton.RHand];
        Vec3 leftTip = ends[Skeleton.LHand]!.Value, rightTip = ends[Skeleton.RHand]!.Value;
        Assert.True(leftTip.X - rightTip.X > 0.03, $"the fingers cross: {leftTip.X - rightTip.X:0.000} m apart");
        // Side by side: the tips as far apart as the wrists, and the hands pointing the same way.
        Assert.InRange((leftTip - rightTip).Length - (leftWrist - rightWrist).Length, -0.01, 0.01);
        Assert.True((leftTip - leftWrist).Normalized().Dot((rightTip - rightWrist).Normalized()) > 0.99);
    }

    [Fact]
    public void Scale_changes_lengths_and_leaves_angles_alone()
    {
        var clip = ShosaCompilation.Compile(Samples.Walk).Clip;
        var metres = BvhFile.Parse(Bvh.Render(clip, clip.Figures[0]));
        var centimetres = BvhFile.Parse(Bvh.Render(clip, clip.Figures[0], 100));

        Assert.Equal(metres.Bones[Skeleton.LLowerLeg].Offset.Y * 100, centimetres.Bones[Skeleton.LLowerLeg].Offset.Y, 6);
        Assert.Equal(metres.Frames[10][1] * 100, centimetres.Frames[10][1], 6);
        Assert.Equal(metres.Frames[10][3..], centimetres.Frames[10][3..]);
    }

    internal static void Compare(string name, int frame, ClipFigure figure, Vec3[] heads, Vec3?[] ends)
    {
        for (int b = 0; b < Skeleton.Bones.Length; b++)
        {
            var bone = Skeleton.Bones[b];
            if (bone.Joint >= 0)
                Near(bone.Name, heads[b], figure.Joint(frame, bone.Joint), 1e-3);
            if (bone.EndJoint == Rig.Nose)
                Near(bone.Name + " end", ends[b]!.Value, figure.Joint(frame, bone.EndJoint), 1e-3);
            // The solver draws a foot longer or shorter as the heel lifts, and a bone cannot
            // stretch: the rotation carries where the foot points, so that is what is compared.
            else if (bone.EndJoint >= 0)
                Assert.True(
                    (ends[b]!.Value - heads[b]).Normalized().Dot((figure.Joint(frame, bone.EndJoint) - figure.Joint(frame, bone.Joint)).Normalized()) > 0.9999,
                    $"{name}: {bone.Name} points the wrong way at frame {frame}");
        }

        void Near(string what, Vec3 actual, Vec3 expected, double tolerance) =>
            Assert.True((actual - expected).Length < tolerance,
                $"{name}: {what} at frame {frame} is {(actual - expected).Length:0.0000} m off");
    }

    /// <summary>A BVH reader written from the format's definition, not from the writer.</summary>
    private sealed class BvhFile
    {
        public sealed record Bone(string Name, int Parent, Vec3 Offset, string[] Channels)
        {
            public Vec3? End { get; set; }
        }

        public List<Bone> Bones { get; } = [];
        public List<double[]> Frames { get; } = [];
        public double FrameTime { get; private set; }

        public static BvhFile Parse(string text)
        {
            var file = new BvhFile();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToArray();
            var open = new Stack<int>();
            int current = -1, i = 0;
            bool inEnd = false;

            for (; lines[i] != "MOTION"; i++)
            {
                var words = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                switch (words[0])
                {
                    case "ROOT" or "JOINT":
                        file.Bones.Add(new Bone(words[1], current, default, []));
                        open.Push(current);
                        current = file.Bones.Count - 1;
                        break;
                    case "End":
                        inEnd = true;
                        break;
                    case "OFFSET":
                        var offset = new Vec3(Number(words[1]), Number(words[2]), Number(words[3]));
                        if (inEnd)
                            file.Bones[current].End = offset;
                        else
                            file.Bones[current] = file.Bones[current] with { Offset = offset };
                        break;
                    case "CHANNELS":
                        file.Bones[current] = file.Bones[current] with { Channels = words[2..] };
                        break;
                    case "}":
                        if (inEnd)
                            inEnd = false;
                        else
                            current = open.Pop();
                        break;
                }
            }

            Assert.StartsWith("Frames:", lines[i + 1]);
            file.FrameTime = Number(lines[i + 2].Split(':')[1]);
            for (i += 3; i < lines.Length; i++)
                file.Frames.Add(lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Number).ToArray());
            Assert.Equal(int.Parse(lines[^(file.Frames.Count + 2)].Split(':')[1], CultureInfo.InvariantCulture), file.Frames.Count);
            return file;
        }

        private static double Number(string word) => double.Parse(word, CultureInfo.InvariantCulture);

        /// <summary>Each bone's matrix is its parent's, then its offset, then its channels in the order written.</summary>
        public (Vec3[] Heads, Vec3?[] Ends) Positions(int frame)
        {
            var values = Frames[frame];
            var heads = new Vec3[Bones.Count];
            var ends = new Vec3?[Bones.Count];
            var turns = new Func<Vec3, Vec3>[Bones.Count];
            int at = 0;

            for (int b = 0; b < Bones.Count; b++)
            {
                var bone = Bones[b];
                var position = bone.Offset;
                var steps = new List<(char Axis, double Radians)>();
                foreach (string channel in bone.Channels)
                {
                    double value = values[at++];
                    if (channel.EndsWith("position"))
                    {
                        position += channel[0] switch
                        {
                            'X' => new Vec3(value, 0, 0),
                            'Y' => new Vec3(0, value, 0),
                            _ => new Vec3(0, 0, value),
                        };
                    }
                    else
                    {
                        steps.Add((channel[0], value * Math.PI / 180));
                    }
                }

                var parentTurn = bone.Parent < 0 ? v => v : turns[bone.Parent];
                heads[b] = bone.Parent < 0 ? position : heads[bone.Parent] + parentTurn(position);
                // The channels multiply left to right, so the last one written turns the vector first.
                turns[b] = v =>
                {
                    for (int s = steps.Count - 1; s >= 0; s--)
                        v = Turn(v, steps[s].Axis, steps[s].Radians);
                    return parentTurn(v);
                };
                if (bone.End is { } end)
                    ends[b] = heads[b] + turns[b](end);
            }
            Assert.Equal(values.Length, at);
            return (heads, ends);
        }

        private static Vec3 Turn(Vec3 v, char axis, double a)
        {
            double cos = Math.Cos(a), sin = Math.Sin(a);
            return axis switch
            {
                'X' => new Vec3(v.X, v.Y * cos - v.Z * sin, v.Y * sin + v.Z * cos),
                'Y' => new Vec3(v.X * cos + v.Z * sin, v.Y, -v.X * sin + v.Z * cos),
                _ => new Vec3(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos, v.Z),
            };
        }
    }
}
