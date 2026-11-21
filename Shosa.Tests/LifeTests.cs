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

    private const string Walk = "    steps { leftFoot.fwd rightFoot.fwd leftFoot.fwd rightFoot.fwd | leftFoot.fwd rightFoot.fwd leftFoot.fwd stand }";

    /// <summary>How far each hand is ahead of its shoulder, and each ankle ahead of the pelvis, along the way the walk goes.</summary>
    private static (double LeftHand, double RightHand, double LeftAnkle, double RightAnkle) Ahead(MotionClip clip, int f)
    {
        int last = clip.Figures[0].Frames.Count - 1;
        var way = At(clip, last, Rig.Pelvis) - At(clip, 0, Rig.Pelvis);
        way = new Vec3(way.X, 0, way.Z).Normalized();
        double Along(int joint, int from) => (At(clip, f, joint) - At(clip, f, from)).Dot(way);
        return (Along(Rig.LHand, Rig.LShoulder), Along(Rig.RHand, Rig.RShoulder), Along(Rig.LAnkle, Rig.Pelvis), Along(Rig.RAnkle, Rig.Pelvis));
    }

    [Fact]
    public void An_unwritten_arm_swings_against_the_other_foot_while_walking()
    {
        // DESIGN.md 101.
        var clip = Solve(Samples.Solo(Walk, "tempo 100\ntime 4/4\n"));
        double most = double.MinValue, least = double.MaxValue;
        int checkedFrames = 0;
        for (int f = 0; f < clip.Figures[0].Frames.Count; f++)
        {
            var (leftHand, rightHand, leftAnkle, rightAnkle) = Ahead(clip, f);
            most = Math.Max(most, rightHand);
            least = Math.Min(least, rightHand);
            // The left foot well in front: the right hand is in front of the left one, and the other way round.
            if (leftAnkle - rightAnkle > 0.4)
            {
                Assert.True(rightHand - leftHand > 0.1, $"frame {f}: the right hand is {rightHand - leftHand:0.00} m ahead of the left");
                checkedFrames++;
            }
            if (rightAnkle - leftAnkle > 0.4)
                Assert.True(leftHand - rightHand > 0.1, $"frame {f}: the left hand is {leftHand - rightHand:0.00} m ahead of the right");
        }
        Assert.True(checkedFrames > 5);
        // A swing a person walks with: the hand goes some 25 to 50 cm from back to front.
        Assert.InRange(most - least, 0.25, 0.5);
        // Standing at the end, the arms hang again.
        var end = Ahead(clip, clip.Figures[0].Frames.Count - 1);
        Assert.True(Math.Abs(end.LeftHand - end.RightHand) < 0.02, $"at the end the hands are {end.LeftHand - end.RightHand:0.00} m apart");
    }

    [Fact]
    public void A_walking_arm_takes_as_long_to_swing_forward_as_back_and_does_not_stop_at_the_ends()
    {
        // DESIGN.md 106: it followed the other ankle from the pelvis, and came forward in a quarter of a second and
        // went back in nearly a second, standing still at each end while both feet were down.
        var clip = Solve(Samples.Solo(Walk, "tempo 100\ntime 4/4\n"));
        int forward = 0, back = 0, still = 0;
        // The middle of the walk: from the second step to the sixth (a beat is 0.6 s).
        for (int f = (int)(1.2 * clip.Fps); f < (int)(3.6 * clip.Fps); f++)
        {
            double change = Ahead(clip, f + 1).RightHand - Ahead(clip, f).RightHand;
            if (Math.Abs(change) < 0.002)
                still++;
            else if (change > 0)
                forward++;
            else
                back++;
        }
        Assert.True(forward > 0 && back > 0);
        Assert.InRange((double)forward / back, 0.8, 1.25);
        // A pendulum is slow only for a moment as it turns (it stood still five frames at each end, some 20 here).
        Assert.True(still <= 8, $"the hand stood still {still} frames");
    }

    /// <summary>The bend of a knee, degrees (0 straight).</summary>
    private static double KneeBend(MotionClip clip, int f, bool left)
    {
        var hip = At(clip, f, left ? Rig.LHip : Rig.RHip);
        var knee = At(clip, f, left ? Rig.LKnee : Rig.RKnee);
        var ankle = At(clip, f, left ? Rig.LAnkle : Rig.RAnkle);
        var thigh = (knee - hip).Normalized();
        var shin = (ankle - knee).Normalized();
        return Math.Acos(Math.Clamp(thigh.Dot(shin), -1, 1)) * 180 / Math.PI;
    }

    [Fact]
    public void Walking_the_knee_bends_and_straightens_smoothly_and_is_straight_before_the_foot_lands()
    {
        // DESIGN.md 106: it was pulled straight the moment before the push off and folded 34° in four frames; it bent
        // most as the foot passed under the hips and straightened in the last frame before the heel came down.
        var clip = Solve(Samples.Solo(Walk, "tempo 100\ntime 4/4\n"));
        int from = (int)(1.2 * clip.Fps), to = (int)(3.6 * clip.Fps);
        int landings = 0;
        foreach (bool left in new[] { true, false })
        {
            int ankle = left ? Rig.LAnkle : Rig.RAnkle;
            for (int f = from; f < to; f++)
            {
                double change = KneeBend(clip, f + 1, left) - KneeBend(clip, f, left);
                Assert.True(Math.Abs(change) < 10, $"frame {f}: the {(left ? "left" : "right")} knee changed {change:0.0}° in a frame");
                // The frame a foot comes down: the frame before and two frames before, the leg is near straight (it was
                // 26° and 38° then; now some 18° and 26°, with the knee most bent early in the swing).
                bool up = At(clip, f, ankle).Y > Rig.AnkleHeight + 0.001, down = At(clip, f + 1, ankle).Y <= Rig.AnkleHeight + 0.001;
                if (up && down)
                {
                    landings++;
                    Assert.True(KneeBend(clip, f, left) < 21, $"frame {f}: the {(left ? "left" : "right")} knee is bent {KneeBend(clip, f, left):0}° the frame before it lands");
                    Assert.True(KneeBend(clip, f - 1, left) < 30, $"frame {f - 1}: the {(left ? "left" : "right")} knee is bent {KneeBend(clip, f - 1, left):0}° two frames before it lands");
                }
            }
        }
        Assert.True(landings >= 3);
    }

    [Fact]
    public void Walking_the_standing_leg_straightens_as_the_other_foot_passes_and_the_hips_go_up_over_it()
    {
        // DESIGN.md 107: the standing knee stayed at the soft 13° the whole stance, and the hips were no higher
        // over the standing foot than soft knees let them be.
        var clip = Solve(Samples.Solo(Walk, "tempo 100\ntime 4/4\n"));
        int from = (int)(1.2 * clip.Fps), to = (int)(3.6 * clip.Fps);
        foreach (bool left in new[] { true, false })
        {
            // The other foot off the floor, toes and all (on its toes, pushing off, it is still standing).
            int other = left ? Rig.RToe : Rig.LToe;
            double least = double.MaxValue, lowHips = double.MaxValue, highHips = 0;
            for (int f = from; f < to; f++)
            {
                double hips = At(clip, f, Rig.Pelvis).Y;
                lowHips = Math.Min(lowHips, hips);
                if (At(clip, f, other).Y > At(clip, 0, other).Y + 0.01)
                {
                    least = Math.Min(least, KneeBend(clip, f, left));
                    highHips = Math.Max(highHips, hips);
                }
            }
            Assert.True(least < 9, $"the {(left ? "left" : "right")} knee is bent at least {least:0.0}° while it stands alone");
            Assert.True(highHips - lowHips > 0.016, $"the hips go up and down {(highHips - lowHips) * 100:0.00} cm");
        }
    }

    /// <summary>Degrees a line from the right joint to the left one is turned to the left, seen from above, from square to the way the walk goes.</summary>
    private static double Turned(MotionClip clip, int f, int left, int right)
    {
        int last = clip.Figures[0].Frames.Count - 1;
        var way = At(clip, last, Rig.Pelvis) - At(clip, 0, Rig.Pelvis);
        way = new Vec3(way.X, 0, way.Z).Normalized();
        var leftOfWay = Vec3.Up.Cross(way);
        var line = At(clip, f, left) - At(clip, f, right);
        return Math.Atan2(-line.Dot(way), line.Dot(leftOfWay)) * 180 / Math.PI;
    }

    [Fact]
    public void Walking_the_hips_go_round_with_the_leg_in_front_and_the_shoulders_the_other_way()
    {
        // DESIGN.md 102.
        var clip = Solve(Samples.Solo(Walk, "tempo 100\ntime 4/4\n"));
        int seen = 0;
        for (int f = 0; f < clip.Figures[0].Frames.Count; f++)
        {
            var (_, _, leftAnkle, rightAnkle) = Ahead(clip, f);
            double front = leftAnkle - rightAnkle;
            if (Math.Abs(front) < 0.4)
                continue;
            // The left foot in front: the left hip forward (the hips turned right) and the right shoulder forward.
            double hips = Turned(clip, f, Rig.LHip, Rig.RHip), shoulders = Turned(clip, f, Rig.LShoulder, Rig.RShoulder);
            Assert.True(hips * Math.Sign(front) < -1.5, $"frame {f}: the hips are turned {hips:0.0}°");
            Assert.True(shoulders - hips is var between && between * Math.Sign(front) > 3, $"frame {f}: the shoulders are {shoulders - hips:0.0}° from the hips");
            Assert.InRange(Math.Abs(hips), 0, 8);
            seen++;
        }
        Assert.True(seen > 5);
    }

    [Fact]
    public void Written_hips_and_shoulders_do_not_turn_with_the_walk()
    {
        var clip = Solve(Samples.Solo(Walk + "\n    hips { center }\n    shoulders { center }", "tempo 100\ntime 4/4\n"));
        // (The breathing torso leans a little and shows on the shoulder line from above: measure how far each line goes round, not where it is.)
        double hipsFrom = double.MaxValue, hipsTo = double.MinValue, shouldersFrom = double.MaxValue, shouldersTo = double.MinValue;
        for (int f = 0; f < clip.Figures[0].Frames.Count; f++)
        {
            double hips = Turned(clip, f, Rig.LHip, Rig.RHip), shoulders = Turned(clip, f, Rig.LShoulder, Rig.RShoulder);
            (hipsFrom, hipsTo) = (Math.Min(hipsFrom, hips), Math.Max(hipsTo, hips));
            (shouldersFrom, shouldersTo) = (Math.Min(shouldersFrom, shoulders), Math.Max(shouldersTo, shoulders));
        }
        Assert.True(hipsTo - hipsFrom < 0.5, $"the hips go round {hipsTo - hipsFrom:0.0}°");
        Assert.True(shouldersTo - shouldersFrom < 1, $"the shoulders go round {shouldersTo - shouldersFrom:0.0}°");
    }

    [Fact]
    public void A_written_arm_does_not_swing()
    {
        var clip = Solve(Samples.Solo(Walk + "\n    arms { down }", "tempo 100\ntime 4/4\n"));
        for (int f = 0; f < clip.Figures[0].Frames.Count; f++)
        {
            var (leftHand, rightHand, _, _) = Ahead(clip, f);
            Assert.True(Math.Abs(leftHand - rightHand) < 0.03, $"frame {f}: the hands are {leftHand - rightHand:0.00} m apart");
        }
    }
}
