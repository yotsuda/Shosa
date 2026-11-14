using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

public class MotionTests
{
    private static ShosaCompilation Compile(string text) => ShosaCompilation.Compile(text);

    private static string Body(string tracks) =>
        "tempo 100\ntime 4/4\nsection P {\n" + tracks + "\n}\n";

    [Fact]
    public void A_bar_with_the_wrong_number_of_beats_is_reported_at_its_bar_line()
    {
        string text = Body("    steps { left.fwd right.fwd left.fwd | right.fwd:4 }");
        var diagnostic = Assert.Single(Compile(text).Diagnostics);

        Assert.Equal("SH1002", diagnostic.Code);
        Assert.Equal('|', text[diagnostic.Span.Start]);
    }

    [Fact]
    public void A_bar_line_closes_exactly_one_bar_and_an_event_across_it_is_tied()
    {
        // 2 + 3 + 3 is two bars, but the line after them closes a bar of eight beats.
        string loose = Body("    arms { side:2 fwd:3 side:3 | }");
        var diagnostic = Assert.Single(Compile(loose).Diagnostics);
        Assert.Equal("SH1002", diagnostic.Code);
        Assert.Contains("fwd:2~ | :1", diagnostic.Message);   // where the tie goes, worked out from the event

        // Tied over the line it is one event of three beats: the same movement as without bar lines.
        var tied = Compile(Body("    arms { side:2 fwd:2~ | :1 side:3 | }"));
        Assert.Empty(tied.Diagnostics);
        Same(Compile(Body("    arms { side:2 fwd:3 side:3 }")), tied);
    }

    [Fact]
    public void A_tie_with_no_length_after_it_and_a_length_with_no_tie_before_it_are_errors()
    {
        Assert.Equal("SH1028", Assert.Single(Compile(Body("    arms { side:2~ fwd:2 }")).Diagnostics).Code);
        Assert.Equal("SH1028", Assert.Single(Compile(Body("    arms { side:2 :2 }")).Diagnostics).Code);
    }

    [Fact]
    public void A_row_with_bar_lines_that_ends_inside_a_bar_is_warned_about()
    {
        var diagnostic = Assert.Single(Compile(Body("    arms { side:4 | fwd:2 }")).Diagnostics);

        Assert.Equal("SH1002", diagnostic.Code);
        Assert.Contains("2 beats", diagnostic.Message);
    }

    [Fact]
    public void A_percent_sign_is_the_bar_before_it_again()
    {
        var written = Compile(Body("    steps { left.side together right.side together | left.side together right.side together | left.side together right.side together }\n    arms { side:2 up:2 | fwd:2 down:2 | side:2 up:2 | fwd:2 down:2 }"));
        var signs = Compile(Body("    steps { left.side together right.side together | % | % | }\n    arms { side:2 up:2 | fwd:2 down:2 | %2 | }"));

        Assert.Empty(signs.Diagnostics);
        Same(written, signs);
    }

    [Fact]
    public void A_percent_sign_needs_a_whole_bar_before_it_and_a_bar_to_itself()
    {
        Assert.Equal("SH1027", Assert.Single(Compile(Body("    arms { % | side:4 | }")).Diagnostics).Code);
        Assert.Equal("SH1027", Assert.Single(Compile(Body("    arms { side:2 fwd:2~ | :2 side:2 | % | }")).Diagnostics).Code);
        Assert.Contains(Compile(Body("    arms { side:4 | fwd:2 % | }")).Diagnostics, d => d.Code == "SH1027");
    }

    [Fact]
    public void Q_is_the_group_in_braces_before_it_again()
    {
        var written = Compile(Body("    rightArm { fwd:1 up:1/2 side:1/2 up:1/2 side:1/2 hold:1 up:1 side:1 }"));
        var again = Compile(Body("    rightArm { fwd:1 { up:1/2 side:1/2 } q hold:1 q:2 }"));

        Assert.Empty(again.Diagnostics);
        Same(written, again);
    }

    [Fact]
    public void Q_mirrored_steps_to_the_other_side_and_q_without_a_group_is_an_error()
    {
        Same(Compile(Body("    steps { left.side together right.side together }")), Compile(Body("    steps { { left.side together } q@mirror }")));

        Assert.Equal("SH1026", Assert.Single(Compile(Body("    arms { side q }")).Diagnostics).Code);
    }

    [Fact]
    public void The_old_repeat_is_refused_with_what_to_write_instead()
    {
        var diagnostic = Assert.Single(Compile(Body("    arms { repeat 2 { side up } }")).Diagnostics);

        Assert.Equal("SH0012", diagnostic.Code);
        Assert.Contains("q", diagnostic.Message);
    }

    [Fact]
    public void A_form_repeats_sections_with_repeat_signs_and_numbered_endings()
    {
        const string sections = "section A { arms { side } }\nsection B { arms { fwd } }\nsection C { arms { up } }\n";
        string[] Order(string form) => Compile(sections + form).Clip.Sections.Select(s => s.Name).ToArray();

        Assert.Equal(new[] { "A", "A", "B" }, Order("form { |: A :| B }"));
        Assert.Equal(new[] { "A", "A", "A" }, Order("form { |: A :|*3 }"));
        Assert.Equal(new[] { "A", "B", "A", "C" }, Order("form { |: A [1. B] :| [2. C] }"));
        Assert.Equal(new[] { "A", "B", "A", "B", "A", "C", "B" }, Order("form { |: A [1-2. B] :| [3. C] B }"));
        Assert.Equal(new[] { "A", "A", "B", "B" }, Order("form { |: A :|: B :| }"));

        Assert.Contains(Compile(sections + "form { A*2 }").Diagnostics, d => d.Code == "SH1025");
        Assert.Contains(Compile(sections + "form { A :| B }").Diagnostics, d => d.Code == "SH1025");
        Assert.Contains(Compile(sections + "form { |: A :| [2. C] }").Diagnostics, d => d.Code == "SH1025");
    }

    /// <summary>Two texts are the same movement: every joint at every frame.</summary>
    private static void Same(ShosaCompilation expected, ShosaCompilation actual)
    {
        var a = expected.Clip.Figures[0].Frames;
        var b = actual.Clip.Figures[0].Frames;
        Assert.Equal(a.Count, b.Count);
        for (int frame = 0; frame < a.Count; frame++)
        {
            for (int i = 0; i < a[frame].Length; i++)
                Assert.True(Math.Abs(a[frame][i] - b[frame][i]) < 1e-4, $"frame {frame}, value {i}: {a[frame][i]} and {b[frame][i]}");
        }
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
        foreach (var figure in Compile(Samples.Read(name)).Clips.SelectMany(clip => clip.Figures))
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

    private static string Two(double apart, string a, string b = "head { hold:2 }") => FormattableString.Invariant(
        $"tempo 100\ntime 4/4\nfigure a\nfigure b\ndancer ann humanoid on a at {-apart / 2} 0 facing stageLeft\ndancer ben humanoid on b at {apart / 2} 0 facing stageRight\nsection P {{\n  a {{ {a} }}\n  b {{ {b} }}\n}}\n");

    [Fact]
    public void A_hand_sent_to_a_place_it_cannot_reach_is_warned_about_with_the_distance()
    {
        Assert.Empty(Compile(Two(0.5, "rightArm { b.chest:2 }")).Diagnostics);

        string text = Two(2, "rightArm { b.chest:2 }");
        var diagnostic = Assert.Single(Compile(text).Diagnostics);
        Assert.Equal("SH2006", diagnostic.Code);
        Assert.Equal("b.chest:2", text.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
        Assert.Contains("ben's chest", diagnostic.Message);

        // Two hands that reach for each other from too far: each is told.
        Assert.Equal(2, Compile(Two(2, "rightArm { b.hand:2 }", "rightArm { a.hand:2 }")).Diagnostics.Count(d => d.Code == "SH2006"));
        // A step toward it is enough.
        Assert.Empty(Compile(Two(1.0, "steps { left.fwd hold }\n rightArm { hold b.chest }")).Diagnostics);
    }

    [Fact]
    public void Two_dancers_in_the_same_place_are_warned_about_once()
    {
        Assert.Empty(Compile(Two(1, "torso { fwd.high:2 }", "torso { hold:2 }")).Diagnostics);

        // A metre apart and both bow to horizontal: the heads meet.
        string text = Two(1, "torso { hold fwd }", "torso { hold fwd }");
        var diagnostic = Assert.Single(Compile(text).Diagnostics);
        Assert.Equal("SH2007", diagnostic.Code);
        Assert.Equal("fwd", text.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
        Assert.Contains("ann and ben", diagnostic.Message);

        // Standing in one spot from the start: nothing is moving, so the section is pointed at.
        string same = Two(0.1, "head { hold:2 }");
        diagnostic = Assert.Single(Compile(same).Diagnostics);
        Assert.Equal("SH2007", diagnostic.Code);
        Assert.Equal("section", same.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
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
