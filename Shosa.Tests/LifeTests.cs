using System.Runtime.CompilerServices;
using Shosa.Core;
using Shosa.Core.Motion;

namespace Shosa.Tests;

internal static class TestSetup
{
    /// <summary>
    /// The tests of the rules measure poses exactly, so the small movements of a living body are off for
    /// the process (DESIGN.md 94); <see cref="LifeTests"/> turn them on for themselves.
    /// </summary>
    [ModuleInitializer]
    internal static void WithoutLife() => Solver.LifeByDefault = false;
}

public class LifeTests
{
    private static MotionClip Solve(string text)
    {
        using var on = Solver.Life(true);
        var compilation = ShosaCompilation.Compile(text);
        Assert.Empty(compilation.Diagnostics);
        return compilation.Clip;
    }

    private static Vec3 At(MotionClip clip, int frame, int joint) => clip.Figures[0].Joint(frame, joint);

    [Fact]
    public void A_body_that_only_moves_its_arms_still_breathes_and_drifts_and_its_legs_and_feet_keep_their_words()
    {
        var clip = Solve(Samples.Solo("    arms { side:4 up:4 side:4 down:4 }", "tempo 100\ntime 4/4\n"));
        int frames = clip.Figures[0].Frames.Count;
        double pelvisFrom = double.MaxValue, pelvisTo = double.MinValue, headFrom = double.MaxValue, headTo = double.MinValue;
        double footMoved = 0;
        var foot = At(clip, 0, Rig.LAnkle);
        for (int f = 0; f < frames; f++)
        {
            var pelvis = At(clip, f, Rig.Pelvis);
            var nose = At(clip, f, Rig.Nose) - At(clip, f, Rig.Head);
            pelvisFrom = Math.Min(pelvisFrom, pelvis.X);
            pelvisTo = Math.Max(pelvisTo, pelvis.X);
            double yaw = Math.Atan2(nose.X, nose.Z);
            headFrom = Math.Min(headFrom, yaw);
            headTo = Math.Max(headTo, yaw);
            footMoved = Math.Max(footMoved, (At(clip, f, Rig.LAnkle) - foot).Length);
        }
        // The weight drifts a few centimetres from foot to foot, the head wanders a few degrees.
        Assert.InRange(pelvisTo - pelvisFrom, 0.03, 0.11);
        Assert.InRange((headTo - headFrom) * 180 / Math.PI, 3, 16);
        // The feet stay where they stand.
        Assert.True(footMoved < 1e-6, $"the left ankle moved {footMoved:0.0000} m");
    }

    [Fact]
    public void Life_is_the_same_every_time_and_a_loop_still_joins()
    {
        string text = Samples.Solo("    arms { side:4 down:4 }", "tempo 100\ntime 4/4\n", "loop");
        var one = Solve(text);
        var two = Solve(text);
        int last = one.Figures[0].Frames.Count - 1;
        for (int j = 0; j < Rig.Joints.Length; j++)
        {
            Assert.Equal(At(one, last / 2, j), At(two, last / 2, j));
            Assert.True((At(one, 0, j) - At(one, last, j)).Length < 0.004, $"{Rig.Joints[j]} does not join");
        }
    }

    [Fact]
    public void A_written_row_is_not_moved_by_life()
    {
        string tracks = "    torso { up:8 }\n    head { fwd:8 }\n    hips { center:8 }";
        var alive = Solve(Samples.Solo(tracks, "tempo 100\ntime 4/4\n"));
        var still = ShosaCompilation.Compile(Samples.Solo(tracks, "tempo 100\ntime 4/4\n")).Clip;
        // The weight still drifts (the steps row is not written), so measure the chest and the face against the pelvis.
        for (int f = 0; f < alive.Figures[0].Frames.Count; f += 7)
        {
            var a = At(alive, f, Rig.Chest) - At(alive, f, Rig.Pelvis);
            var s = At(still, f, Rig.Chest) - At(still, f, Rig.Pelvis);
            Assert.True(a.Normalized().Cross(s.Normalized()).Length < 1e-3, $"frame {f}: the torso leans");
        }
    }
}
