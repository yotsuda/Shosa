using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

public class MotionTests
{
    private static ShosaCompilation Compile(string text) => ShosaCompilation.Compile(text);

    private static string Body(string tracks) =>
        "tempo 100\ntime 4/4\nfigure a { rig humanoid }\nphrase P {\n  a {\n" + tracks + "\n  }\n}\n";

    [Fact]
    public void A_bar_with_the_wrong_number_of_beats_is_reported_at_its_bar_line()
    {
        string text = Body("    steps { left.fwd right.fwd left.fwd | right.fwd:4 }");
        var diagnostic = Assert.Single(Compile(text).Diagnostics);

        Assert.Equal("SH1002", diagnostic.Code);
        Assert.Equal('|', text[diagnostic.Span.Start]);
    }

    [Fact]
    public void Left_on_a_limb_is_refused_with_the_word_to_write_instead()
    {
        var diagnostic = Assert.Single(Compile(Body("    rightArm { left }")).Diagnostics);

        Assert.Equal("SH1011", diagnostic.Code);
        Assert.Contains("'across'", diagnostic.Message);
    }

    [Fact]
    public void A_misspelt_word_gets_a_suggestion()
    {
        var diagnostic = Assert.Single(Compile(Body("    steps { left.forwd }")).Diagnostics);

        Assert.Equal("SH1012", diagnostic.Code);
        Assert.Contains("'fwd'", diagnostic.Message);
    }

    [Fact]
    public void A_target_opposite_to_where_the_part_is_asks_for_one_in_between()
    {
        string text = Body("    arms { up }");
        var diagnostic = Assert.Single(Compile(text).Diagnostics);

        Assert.Equal("SH2001", diagnostic.Code);
        Assert.Equal("up", text.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
    }

    [Fact]
    public void A_gesture_with_the_leg_that_carries_the_weight_is_an_error()
    {
        // After left.fwd the weight is on the left foot; the right foot then steps while the
        // left leg is told to kick.
        var compilation = Compile(Body("    steps { left.fwd right.fwd:2 hold }\n    leftLeg { hold fwd.low hold:2 }"));

        Assert.Contains(compilation.Diagnostics, d => d.Code == "SH2004");
    }

    [Fact]
    public void A_planted_foot_does_not_slide()
    {
        var clip = Compile(Samples.Walk).Clip;
        var figure = clip.Figures[0];

        foreach (int ankle in new[] { Rig.LAnkle, Rig.RAnkle })
        {
            for (int f = 1; f < figure.Frames.Count; f++)
            {
                Vec3 before = figure.Joint(f - 1, ankle), after = figure.Joint(f, ankle);
                if (before.Y <= Rig.AnkleHeight + 1e-4 && after.Y <= Rig.AnkleHeight + 1e-4)
                    Assert.True((after - before).Length < 1e-3, $"ankle {ankle} slid at frame {f}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Limbs_keep_their_length(string name)
    {
        var clip = Compile(Samples.Read(name)).Clip;
        foreach (var figure in clip.Figures)
        {
            for (int f = 0; f < figure.Frames.Count; f++)
            {
                void Bone(int a, int b, double length) =>
                    Assert.True(Math.Abs((figure.Joint(f, a) - figure.Joint(f, b)).Length - length) < 2e-3, $"{name}: bone {a}-{b} at frame {f}");

                Bone(Rig.LHip, Rig.LKnee, Rig.Thigh);
                Bone(Rig.LKnee, Rig.LAnkle, Rig.Shin);
                Bone(Rig.RHip, Rig.RKnee, Rig.Thigh);
                Bone(Rig.RKnee, Rig.RAnkle, Rig.Shin);
                Bone(Rig.LShoulder, Rig.LElbow, Rig.UpperArm);
                Bone(Rig.LElbow, Rig.LHand, Rig.Forearm);
                Bone(Rig.RShoulder, Rig.RElbow, Rig.UpperArm);
                Bone(Rig.RElbow, Rig.RHand, Rig.Forearm);
                Bone(Rig.Pelvis, Rig.Chest, Rig.Spine);
            }
        }
    }

    [Fact]
    public void Three_steps_and_a_quarter_turn_four_times_come_back_to_the_start()
    {
        // Every step is placed from the other foot, so a side of the square is a rigid motion
        // of (right foot, facing): a quarter turn about some point. Four of them are the
        // identity. (The left foot ends one step ahead: it took the last step.)
        var figure = Compile(Samples.Read("square")).Clip.Figures[0];
        Vec3 first = figure.Joint(0, Rig.RAnkle), last = figure.Joint(figure.Frames.Count - 1, Rig.RAnkle);

        Assert.True((first - last).Length < 0.01, $"ended {(first - last).Length:0.000} m from the start");
    }

    [Fact]
    public void Changing_one_word_of_an_arm_changes_nothing_below_the_waist()
    {
        // The point of writing movement as text: an edit stays where it was made.
        var before = Compile(Samples.Walk).Clip.Figures[0];
        var after = Compile(Samples.Walk.Replace("side:2", "fwd:2")).Clip.Figures[0];

        Assert.Equal(before.Frames.Count, after.Frames.Count);
        bool armsDiffer = false;
        for (int f = 0; f < before.Frames.Count; f++)
        {
            for (int joint = Rig.LHip; joint <= Rig.RToe; joint++)
                Assert.Equal(before.Joint(f, joint), after.Joint(f, joint));
            Assert.Equal(before.Joint(f, Rig.Pelvis), after.Joint(f, Rig.Pelvis));
            armsDiffer |= before.Joint(f, Rig.LHand) != after.Joint(f, Rig.LHand);
        }
        Assert.True(armsDiffer);
    }

    [Fact]
    public void Every_event_of_the_source_is_linked_to_the_time_it_is_performed()
    {
        var compilation = Compile(Samples.Walk);
        var events = compilation.Tree.Root.DescendantNodes().Where(n => n.Kind == SyntaxKind.Event).ToList();

        foreach (var ev in events)
            Assert.Contains(compilation.Clip.Spans, s => s.Start == ev.Span.Start && s.Length == ev.Span.Length);
    }

    // ---- What the first authoring trial (two AIs, the spec alone) got wrong or could not say.

    private static Vec3 At(FuriClip clip, double beat, int joint) => clip.Figure.Joint(clip.Clip.FrameAt(beat), joint);

    private sealed record FuriClip(MotionClip Clip, ClipFigure Figure);

    private static FuriClip Solve(string tracks)
    {
        var compilation = Compile(Body(tracks));
        Assert.Empty(compilation.Diagnostics);
        return new FuriClip(compilation.Clip, compilation.Clip.Figures[0]);
    }

    [Fact]
    public void A_step_with_no_level_comes_down_from_the_toes()
    {
        var clip = Solve("    steps { left.fwd right.side.high left.place.high right.back }");

        Assert.True(At(clip, 3, Rig.LAnkle).Y > Rig.AnkleHeight + 0.05, "on the toes after .high");
        Assert.Equal(Rig.AnkleHeight, At(clip, 4, Rig.LAnkle).Y, 3);
    }

    [Fact]
    public void A_close_takes_a_level()
    {
        var clip = Solve("    steps { left.fwd together.high together }");

        Assert.True(At(clip, 2, Rig.RAnkle).Y > Rig.AnkleHeight + 0.05);
        Assert.Equal(Rig.AnkleHeight, At(clip, 3, Rig.RAnkle).Y, 3);
    }

    [Fact]
    public void Two_steps_forward_and_two_back_end_where_they_began()
    {
        var clip = Solve("    steps { left.fwd right.fwd together hold | right.back left.back together hold }");

        Assert.True((At(clip, 0, Rig.Pelvis) - At(clip, 8, Rig.Pelvis)).Length < 0.01);
    }

    [Fact]
    public void A_cross_behind_goes_just_behind_the_other_foot_not_a_stride_behind()
    {
        var clip = Solve("    steps { right.side left.back.across }");
        Vec3 crossing = At(clip, 2, Rig.LAnkle), standing = At(clip, 2, Rig.RAnkle);

        Assert.Equal(-0.12, crossing.Z - standing.Z, 2);          // behind (the figure faces +z)
        Assert.True(crossing.X < standing.X, "crossed past the right foot (the figure's left is +x)");
    }

    [Fact]
    public void From_both_feet_a_leg_can_lift_without_a_step_first()
    {
        var clip = Solve("    rightLeg { fwd.low hold down hold }");

        Assert.True(At(clip, 1.5, Rig.RAnkle).Y > 0.2, "the leg is up");
        Assert.True(Math.Abs(At(clip, 1.5, Rig.Pelvis).X - At(clip, 1.5, Rig.LAnkle).X) < 0.05, "the hips are over the standing foot");
        Assert.True((At(clip, 0, Rig.RAnkle) - At(clip, 4, Rig.RAnkle)).Length < 1e-3, "and it lands where it stood");
    }

    [Fact]
    public void An_arm_leans_with_the_torso_only_when_it_says_body()
    {
        var room = Solve("    arms { fwd up hold:2 }\n    torso { hold:2 left.high:2 }");
        var body = Solve("    arms { fwd up up:2@body }\n    torso { hold:2 left.high:2 }");

        Vec3 Arm(FuriClip clip) => (At(clip, 4, Rig.LHand) - At(clip, 4, Rig.LShoulder)).Normalized();
        Vec3 Spine(FuriClip clip) => (At(clip, 4, Rig.Chest) - At(clip, 4, Rig.Pelvis)).Normalized();

        Assert.True(Arm(room).Dot(Vec3.Up) > 0.999, "measured against the room, the arm stays vertical");
        Assert.True(Arm(body).Dot(Spine(body)) > 0.999, "with @body it is in line with the spine");
    }

    [Fact]
    public void A_sharp_event_has_not_moved_at_its_first_instant()
    {
        // It used to start at full speed, so a pose taken a hair after the beat showed the arm
        // already a fifth of the way there.
        var clip = Solve("    arms { side:2 fwd:2@sharp }");
        Vec3 at = At(clip, 2, Rig.LHand), justAfter = At(clip, 2.03, Rig.LHand);

        Assert.True((at - justAfter).Length < 0.02, $"moved {(at - justAfter).Length:0.000} m in 0.03 beats");
        Assert.True((At(clip, 3, Rig.LHand) - At(clip, 4, Rig.LHand)).Length < 1e-3, "there by the middle of the event");
    }
}
