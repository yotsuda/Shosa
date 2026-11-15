using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

public class MotionTests
{
    private static ShosaCompilation Compile(string text) => ShosaCompilation.Compile(text);

    private static string Body(string tracks) => Samples.Solo(tracks, "tempo 100\ntime 4/4\n");

    [Fact]
    public void A_bar_with_the_wrong_number_of_beats_is_reported_at_its_bar_line()
    {
        string text = Body("    steps { leftFoot.fwd rightFoot.fwd leftFoot.fwd | rightFoot.fwd:4 }");
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
        var written = Compile(Body("    steps { leftFoot.side stand rightFoot.side stand | leftFoot.side stand rightFoot.side stand | leftFoot.side stand rightFoot.side stand }\n    arms { side:2 up:2 | fwd:2 down:2 | side:2 up:2 | fwd:2 down:2 }"));
        var signs = Compile(Body("    steps { leftFoot.side stand rightFoot.side stand | % | % | }\n    arms { side:2 up:2 | fwd:2 down:2 | %2 | }"));

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
        var written = Compile(Body("    rightArm { fwd:1 up:1/2 side:1/2 up:1/2 side:1/2 then:1 up:1 side:1 }"));
        var again = Compile(Body("    rightArm { fwd:1 { up:1/2 side:1/2 } q then:1 q:2 }"));

        Assert.Empty(again.Diagnostics);
        Same(written, again);
    }

    [Fact]
    public void Q_mirrored_steps_to_the_other_side_and_q_without_a_group_is_an_error()
    {
        Same(Compile(Body("    steps { leftFoot.side stand rightFoot.side stand }")), Compile(Body("    steps { { leftFoot.side stand } q@mirror }")));

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
        const string sections = Samples.SoloCast + "score { dancer solo }\n"
            + "section A { solo { arms { side } } }\nsection B { solo { arms { fwd } } }\nsection C { solo { arms { up } } }\n";
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
        var diagnostic = Assert.Single(Compile(Body("    steps { leftFoot.forwd }")).Diagnostics);

        Assert.Equal("SH1012", diagnostic.Code);
        Assert.Contains("'fwd'", diagnostic.Message);
    }

    [Fact]
    public void A_target_opposite_to_where_the_part_is_asks_for_one_in_between()
    {
        // The arms hang, and go up from there in the time of the `then`.
        string text = Body("    arms { then up }");
        var diagnostic = Assert.Single(Compile(text).Diagnostics);

        Assert.Equal("SH2001", diagnostic.Code);
        Assert.Equal("up", text.Substring(diagnostic.Span.Start, diagnostic.Span.Length));

        // As the first word it is the pose the score starts in: there is no way there to ask about.
        Assert.Empty(Compile(Body("    arms { up }")).Diagnostics);
    }

    [Fact]
    public void An_arm_at_down_hangs_as_an_arm_nobody_wrote_does()
    {
        // The left arm is written `down`, the right is not written: the same shape, mirrored.
        var clip = Solve("    leftArm { down:2 side:2 }\n    head { fwd:4 }");
        Vec3 left = At(clip, 0, Rig.LHand) - At(clip, 0, Rig.LShoulder), right = At(clip, 0, Rig.RHand) - At(clip, 0, Rig.RShoulder);
        Assert.True((left - new Vec3(-right.X, right.Y, right.Z)).Length < 1e-6, $"{left} and {right}");
        // In front of the shoulder and bent a little: not the straight line under the joint.
        Assert.True(left.Z > 0.05 && left.Length < Rig.Arm - 0.002, $"{left}");
        // And it stays so until it sets out for the next word: `down` is not a movement from hanging.
        Assert.True((At(clip, 0, Rig.LHand) - At(clip, 1, Rig.LHand)).Length > 1e-3, "on its way to the side through the word");

        // `down@bent` is still the short arm straight down.
        var bent = Solve("    leftArm { down.bent:2 }");
        Vec3 hand = At(bent, 0, Rig.LHand) - At(bent, 0, Rig.LShoulder);
        Assert.True(Math.Abs(hand.Z) < 1e-6 && Math.Abs(hand.X) < 1e-6, $"{hand}");
    }

    [Fact]
    public void A_gesture_with_the_leg_that_carries_the_weight_is_an_error()
    {
        // After left.fwd the weight is on the left foot; the right foot then steps while the
        // left leg is told to kick.
        var compilation = Compile(Body("    steps { leftFoot.fwd rightFoot.fwd:2 then }\n    leftLeg { then fwd.down then:2 }"));

        Assert.Contains(compilation.Diagnostics, d => d.Code == "SH2004");
    }

    [Fact]
    public void The_free_foot_of_a_skip_leaves_the_floor_as_the_hop_bends_and_is_never_too_far()
    {
        // Writing trial 19: the grammar's own skip, step 1 beat and hop half a beat at 120, gave SH2002 on the
        // hops of one foot. The back foot waited on the floor for the take-off, a stride behind the body.
        string text = Samples.Solo(
            "    steps { then:1 leftFoot.fwd:1 hop.fwd:1/2 rightFoot.fwd:1 hop.fwd:1/2 leftFoot.fwd:1 hop.fwd:1/2 rightFoot.fwd:1 hop.fwd:1/2 stand:1 }",
            "tempo 120\ntime 4/4\n");
        var compilation = Compile(text);
        Assert.Empty(compilation.Diagnostics);

        // In the bend before the hop on the right foot (it lands at beat 3.5) the left foot is already off the floor.
        var figure = compilation.Clip.Figures[0];
        Assert.True(figure.Joint(compilation.Clip.FrameAt(3.05), Rig.LAnkle).Y > Rig.AnkleHeight + 0.02);
    }

    [Fact]
    public void A_standing_leg_is_straight()
    {
        // Writing trial 19: every standing knee was bent 11° (a ballet dancer's standing leg is straight). DESIGN.md 78.4.
        var compilation = Compile(Samples.Solo("    turnout { out(80) }\n    steps { together:4 then:4 }\n    rightLeg { then:4 otherKnee:4 }", "tempo 120\ntime 4/4\n"));
        Assert.Empty(compilation.Diagnostics);
        var figure = compilation.Clip.Figures[0];
        double Knee(double beat, int hip)
        {
            var frame = compilation.Clip.FrameAt(beat);
            Vec3 thigh = figure.Joint(frame, hip + 1) - figure.Joint(frame, hip), shin = figure.Joint(frame, hip + 2) - figure.Joint(frame, hip + 1);
            return Math.Acos(Math.Clamp(thigh.Normalized().Dot(shin.Normalized()), -1, 1)) * 180 / Math.PI;
        }
        Assert.InRange(Knee(0, Rig.LHip), 0, 5);
        Assert.InRange(Knee(0, Rig.RHip), 0, 5);
        Assert.InRange(Knee(7, Rig.LHip), 0, 5);   // the leg it stands on in a retiré
    }

    [Fact]
    public void A_leg_put_down_after_a_hop_lands_beside_the_foot_it_stands_on()
    {
        // Before, `down` brought a foot held up after a hop back to where it was held: it never reached the floor
        // without a word of `steps`. DESIGN.md 78.4.
        var compilation = Compile(Samples.Solo("""
                steps    { stand:1 shift.left:1 | hop:2 | then:2 | then:2 | }
                rightLeg { skip:2 | skip:2 | down:2 | then:2 | }
            """, "tempo 120\ntime 2/4\n"));
        Assert.Empty(compilation.Diagnostics);
        var clip = compilation.Clip;
        var figure = clip.Figures[0];
        Vec3 left = figure.Joint(clip.FrameAt(7), Rig.LAnkle), right = figure.Joint(clip.FrameAt(7), Rig.RAnkle);
        Assert.True(figure.Joint(clip.FrameAt(2.2), Rig.RAnkle).Y > Rig.AnkleHeight + 0.05, "held up after the hop");
        Assert.Equal(Rig.AnkleHeight, right.Y, 3);
        Assert.Equal(Rig.Stance, left.X - right.X, 2);   // at standing width, beside it
        Assert.Equal(left.Z, right.Z, 2);
    }

    [Fact]
    public void A_foot_put_down_while_its_leg_row_holds_it_up_is_a_warning_at_the_step()
    {
        // Writing trial 19: the landing leg of a split leap kept its shape and never landed, without a word.
        string leap = Samples.Solo("""
                steps    { stand:1 shift.left:1 | then:2 | leap.fwd(1m):2 | then:2 | }
                leftLeg  { skip:2 | skip:1 then:1/2 back:1/2 | then:2 | then:2 | }
                rightLeg { skip:2 | skip:1 then:1/2 fwd:1/2 | then:2 | then:2 | }
            """, "tempo 120\ntime 2/4\n");
        var warning = Assert.Single(Compile(leap).Diagnostics);
        Assert.Equal(("SH2013", DiagnosticSeverity.Warning), (warning.Code, warning.Severity));
        Assert.StartsWith("leap", leap[warning.Span.Start..]);

        // Put down in its row as it lands: nothing to say.
        Assert.Empty(Compile(leap.Replace("rightLeg { skip:2 | skip:1 then:1/2 fwd:1/2 | then:2 |", "rightLeg { skip:2 | skip:1 then:1/2 fwd:1/2 | down:2 |")).Diagnostics);

        // A `together` under a leg still lifted behind after a hop.
        string close = Samples.Solo("""
                steps    { stand:1 shift.left:1 | hop:2 | together:2 | then:2 | }
                rightLeg { skip:2 | back.down(20):2 | then:2 | then:2 | }
            """, "tempo 120\ntime 2/4\n");
        Assert.Equal("SH2013", Assert.Single(Compile(close).Diagnostics).Code);
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
        foreach (var figure in Samples.Compile(name).Clips.SelectMany(clip => clip.Figures))
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
                Bone(Rig.Pelvis, Rig.Waist, Rig.Spine / 2);
                Bone(Rig.Waist, Rig.Chest, Rig.Spine / 2);
            }
        }
    }

    [Fact]
    public void Three_steps_and_a_quarter_turn_four_times_come_back_to_the_start()
    {
        // Every step is placed from the other foot, so a side of the square is a rigid motion
        // of (right foot, facing): a quarter turn about some point. Four of them are the
        // identity. (The left foot ends one step ahead: it took the last step.)
        var figure = Samples.Compile("square").Clip.Figures[0];
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
        // Each word is there on its beat: the fourth, on beat 3.
        var clip = Solve("    steps { leftFoot.fwd rightFoot.side leftFoot.place rightFoot.back }\n    height { mid high high mid }");

        Assert.True(At(clip, 2, Rig.LAnkle).Y > Rig.AnkleHeight + 0.05, "on the toes on .high");
        Assert.Equal(Rig.AnkleHeight, At(clip, 3, Rig.LAnkle).Y, 2);
        Assert.Equal(Rig.AnkleHeight, At(clip, 4, Rig.LAnkle).Y, 3);
    }

    [Fact]
    public void A_close_takes_a_level()
    {
        var clip = Solve("    steps { leftFoot.fwd stand stand }\n    height { mid high mid }");

        Assert.True(At(clip, 1, Rig.RAnkle).Y > Rig.AnkleHeight + 0.05);
        Assert.Equal(Rig.AnkleHeight, At(clip, 2, Rig.RAnkle).Y, 3);
    }

    [Fact]
    public void Two_steps_forward_and_two_back_end_where_they_began()
    {
        // Standing at the start of each bar (the first word is where the body begins, so a first step would not be taken).
        var clip = Solve("    steps { then leftFoot.fwd rightFoot.fwd stand | then rightFoot.back leftFoot.back stand }");

        Assert.True(At(clip, 3, Rig.Pelvis).Z > At(clip, 0, Rig.Pelvis).Z + 0.9, "two strides ahead after the first bar");
        Assert.True((At(clip, 0, Rig.Pelvis) - At(clip, 7, Rig.Pelvis)).Length < 0.01);
    }

    [Fact]
    public void A_cross_behind_goes_just_behind_the_other_foot_not_a_stride_behind()
    {
        var clip = Solve("    steps { rightFoot.side leftFoot.back.across }");
        Vec3 crossing = At(clip, 2, Rig.LAnkle), standing = At(clip, 2, Rig.RAnkle);

        Assert.Equal(-0.12, crossing.Z - standing.Z, 2);          // behind (the figure faces +z)
        Assert.True(crossing.X < standing.X, "crossed past the right foot (the figure's left is +x)");
    }

    [Fact]
    public void From_both_feet_a_leg_can_lift_without_a_step_first()
    {
        // Up on beat 1 (lifted in the time of the first `then`), kept up for the second, down on beat 3.
        var clip = Solve("    rightLeg { then fwd.down then down then }");

        Assert.True(At(clip, 1.5, Rig.RAnkle).Y > 0.2, "the leg is up");
        Assert.True(Math.Abs(At(clip, 1.5, Rig.Pelvis).X - At(clip, 1.5, Rig.LAnkle).X) < 0.05, "the hips are over the standing foot");
        Assert.True((At(clip, 0, Rig.RAnkle) - At(clip, 3, Rig.RAnkle)).Length < 1e-3, "and it lands where it stood");
    }

    [Fact]
    public void An_arm_leans_with_the_torso_only_when_it_says_body()
    {
        var room = Solve("    arms { fwd up then:2 }\n    torso { then:2 left.up:2 }");
        var body = Solve("    arms { fwd up up.body:2 }\n    torso { then:2 left.up:2 }");

        Vec3 Arm(FuriClip clip) => (At(clip, 4, Rig.LHand) - At(clip, 4, Rig.LShoulder)).Normalized();
        Vec3 Spine(FuriClip clip) => (At(clip, 4, Rig.Chest) - At(clip, 4, Rig.Pelvis)).Normalized();

        Assert.True(Arm(room).Dot(Vec3.Up) > 0.999, "measured against the room, the arm stays vertical");
        Assert.True(Arm(body).Dot(Spine(body)) > 0.999, "with @body it is in line with the spine");
    }

    [Fact]
    public void A_rounded_back_bows_behind_the_line_from_the_pelvis_to_the_chest()
    {
        var straight = Solve("    torso { up.fwd(10):4 }");
        var round = Solve("    torso { up.fwd(10).round:4 }");

        Vec3 Line(FuriClip clip) => (At(clip, 0, Rig.Chest) - At(clip, 0, Rig.Pelvis)).Normalized();
        Vec3 Off(FuriClip clip) => At(clip, 0, Rig.Waist) - (At(clip, 0, Rig.Pelvis) + At(clip, 0, Rig.Chest)) * 0.5;

        Assert.True(Off(straight).Length < 1e-3, "a straight back has the waist on the line");
        Assert.True(Line(round).Dot(Line(straight)) > 0.9999, "the word still says where the chest is from the pelvis");
        double behind = Rig.Spine / 2 * Math.Sin(Rig.RoundAngle * Math.PI / 180);
        Assert.True(Math.Abs(Off(round).Z + behind) < 2e-3 && Math.Abs(Off(round).X) < 1e-3, $"the waist is {behind:0.###} m behind the line: {Off(round)}");

        // The head stands on the chest, which leans further than the line.
        Vec3 Neck(FuriClip clip) => (At(clip, 0, Rig.Head) - At(clip, 0, Rig.Chest)).Normalized();
        Assert.True(Neck(round).Z > Neck(straight).Z + 0.2, "the chest and the head lean further forward");
    }

    [Fact]
    public void Round_is_only_for_the_torso()
    {
        Assert.Equal("SH1010", Assert.Single(Compile(Body("    head { fwd.down(20).round:4 }")).Diagnostics).Code);
        Assert.Equal("SH1010", Assert.Single(Compile(Body("    steps { then:4@round }\n    height { low:4 }")).Diagnostics).Code);
    }

    [Fact]
    public void The_shoulders_turn_on_the_hips_and_not_the_head()
    {
        // There on beat 2; `left` alone is 30 degrees.
        var clip = Solve("    shoulders { center:2 left(30):2 }\n    arms { side.body:2 side.body:2 }");
        var mirrored = Solve("    shoulders { center:2 left(30):2@mirror }");

        Vec3 Across(FuriClip c, double beat) => (At(c, beat, Rig.LShoulder) - At(c, beat, Rig.RShoulder)).Normalized();
        double sin = Math.Sin(30 * Math.PI / 180);
        Assert.True(Math.Abs(Across(clip, 0).Z) < 1e-3, "square at first");
        Assert.True(Math.Abs(Across(clip, 2).Z + sin) < 2e-3, $"the left shoulder goes back, the right comes forward: {Across(clip, 2)}");
        Assert.True(Math.Abs(Across(mirrored, 2).Z - sin) < 2e-3, "mirrored, the other way");

        foreach (int joint in new[] { Rig.Pelvis, Rig.Waist, Rig.Chest, Rig.Head, Rig.Nose, Rig.LHip, Rig.RHip, Rig.LAnkle })
            Assert.True((At(clip, 2, joint) - At(clip, 0, joint)).Length < 1e-3, $"{Rig.Joints[joint]} stays: the spine is the axis, and the face looks where the head says");
        // An arm said against the body goes round with the chest.
        var arm = (At(clip, 2, Rig.LHand) - At(clip, 2, Rig.LShoulder)).Normalized();
        Assert.True(arm.Dot(Across(clip, 2)) > 0.999, "the @body arm is still in line with the shoulders");

        // The bones: the spine takes half of the turn, the chest the rest, the neck turns back under the head.
        var turns = clip.Figure.Rotations[clip.Clip.FrameOfBeat(2)];
        double Yaw(int bone) => 2 * Math.Atan2(turns[bone * 4 + 1], turns[bone * 4 + 3]) * 180 / Math.PI;
        Assert.Equal(15, Yaw(Skeleton.Spine), 0);
        Assert.Equal(15, Yaw(Skeleton.Chest), 0);
        Assert.Equal(-30, Yaw(Skeleton.Neck), 0);
    }

    [Fact]
    public void The_shoulders_say_a_side_and_degrees_from_the_feet_up_to_90_or_center()
    {
        Assert.Empty(Compile(Body("    shoulders { right(20):2 left(60)@sharp center }")).Diagnostics);
        Assert.Equal("SH1013", Assert.Single(Compile(Body("    shoulders { left(100):4 }")).Diagnostics).Code);
        Assert.Equal("SH1012", Assert.Single(Compile(Body("    shoulders { fwd:4 }")).Diagnostics).Code);
        Assert.Equal("SH1013", Assert.Single(Compile(Body("    shoulders { left(high):4 }")).Diagnostics).Code);
        Assert.Equal("SH1013", Assert.Single(Compile(Body("    shoulders { left:4 }")).Diagnostics).Code);   // how far is always written
        Assert.Equal("SH1010", Assert.Single(Compile(Body("    shoulders { left(30):4@large }")).Diagnostics).Code);
        Assert.Equal("SH1012", Assert.Single(Compile(Body("    torso { center:4 }")).Diagnostics).Code);
    }

    [Fact]
    public void A_move_can_turn_the_shoulders_over_any_lean_and_come_back_to_the_pose()
    {
        // The shoulders are a part of their own: the move does not say the lean, and `guard` in its row is the guard's.
        var compilation = Compile(Samples.Solo("    body { guard:2 hook:2 then:2 }", """
            tempo 100
            pose guard { torso { up.left(20) }  shoulders { right(20) } }
            move hook { shoulders { left(30) guard } }

            """));
        Assert.Empty(compilation.Diagnostics);
        var clip = new FuriClip(compilation.Clip, compilation.Clip.Figures[0]);

        // As seen from above: the line from the left shoulder to the right one, on the floor.
        double Turn(double beat) => (At(clip, beat, Rig.RShoulder) - At(clip, beat, Rig.LShoulder)) is var d ? Math.Atan2(d.Z, -d.X) * 180 / Math.PI : 0;
        Assert.Equal(-20, Turn(0), 0);
        Assert.Equal(30, Turn(2), 0);
        Assert.Equal(-20, Turn(3), 0);
        Vec3 Lean(double beat) => (At(clip, beat, Rig.Chest) - At(clip, beat, Rig.Pelvis)).Normalized();
        Assert.True(Lean(2).Dot(Lean(0)) > 0.9999, "the lean is the guard's all through");
    }

    [Fact]
    public void The_hips_turn_over_feet_that_stay_and_the_shoulders_keep_to_their_own_row()
    {
        var clip = Solve("    hips { center:2 left(30):2 left(30):2 right(20):2@mirror }\n    shoulders { center:2 left(30):2 center:2 left(45):2 }");
        double Line(double beat, int left, int right) => Math.Asin((At(clip, beat, right) - At(clip, beat, left)).Normalized().Z) * 180 / Math.PI;

        // 30 degrees to the left: the right hip comes forward, and the right shoulder as far.
        Assert.Equal(0, Line(0, Rig.LHip, Rig.RHip), 0);
        Assert.Equal(30, Line(2, Rig.LHip, Rig.RHip), 0);
        Assert.Equal(30, Line(2, Rig.LShoulder, Rig.RShoulder), 0);
        // Each from the feet: shoulders square over turned hips (a twist dance), and further round than the hips.
        Assert.Equal(30, Line(4, Rig.LHip, Rig.RHip), 0);
        Assert.Equal(0, Line(4, Rig.LShoulder, Rig.RShoulder), 0);
        Assert.Equal(20, Line(6, Rig.LHip, Rig.RHip), 0);
        Assert.Equal(45, Line(6, Rig.LShoulder, Rig.RShoulder), 0);

        // The feet, the pelvis, the head and the nose are where they were.
        foreach (int joint in new[] { Rig.LAnkle, Rig.RAnkle, Rig.LToe, Rig.RToe, Rig.Pelvis, Rig.Chest, Rig.Head, Rig.Nose })
            Assert.True((At(clip, 2, joint) - At(clip, 0, joint)).Length < 0.005, Rig.Joints[joint]);

        // The bones: the hips turn 30, the spine and the chest add nothing, and the neck gives it back under a head that looks ahead.
        var turns = clip.Figure.Rotations[clip.Clip.FrameOfBeat(2)];
        double Yaw(int bone) => 2 * Math.Atan2(turns[bone * 4 + 1], turns[bone * 4 + 3]) * 180 / Math.PI;
        Assert.Equal(30, Yaw(Skeleton.Hips), 0);
        Assert.Equal(0, Yaw(Skeleton.Spine), 0);
        Assert.Equal(0, Yaw(Skeleton.Chest), 0);
        Assert.Equal(-30, Yaw(Skeleton.Neck) + Yaw(Skeleton.Head), 0);

        Assert.Empty(Compile(Body("    hips { right(20):2 left(45)@sharp center }")).Diagnostics);
        Assert.Equal("SH1013", Assert.Single(Compile(Body("    hips { left(60):4 }")).Diagnostics).Code);
        Assert.Equal("SH1012", Assert.Single(Compile(Body("    hips { fwd:4 }")).Diagnostics).Code);
        Assert.Equal("SH1010", Assert.Single(Compile(Body("    hips { left(30):4@round }")).Diagnostics).Code);
    }

    [Fact]
    public void Turnout_turns_the_feet_and_the_knees_out_and_leaves_the_hips_square()
    {
        var clip = Solve("    turnout { parallel:2 out(45):2 out(80):2 then:2 in(20):2@mirror }\n    steps { then:6 then:2 then:2 }\n    height { mid:5 then:1 low:2 then:2 }");
        // Degrees to the figure's left of straight ahead, on the floor.
        double Round(Vec3 v) => Math.Atan2(v.X, v.Z) * 180 / Math.PI;
        double Foot(double beat, int ankle) => Round(At(clip, beat, ankle + 1) - At(clip, beat, ankle));

        Assert.Equal(0, Foot(0, Rig.LAnkle), 0);
        Assert.Equal(45, Foot(2, Rig.LAnkle), 0);
        Assert.Equal(-45, Foot(2, Rig.RAnkle), 0);
        Assert.Equal(80, Foot(4, Rig.LAnkle), 0);
        Assert.Equal(-80, Foot(4, Rig.RAnkle), 0);
        // In, and the same under a mirror: both legs alike.
        Assert.Equal(-20, Foot(10, Rig.LAnkle), 0);
        Assert.Equal(20, Foot(10, Rig.RAnkle), 0);

        // The feet turn about the heels, and the hips stay square.
        foreach (int joint in new[] { Rig.LAnkle, Rig.RAnkle, Rig.Pelvis, Rig.LHip, Rig.RHip })
            Assert.True((At(clip, 4, joint) - At(clip, 0, joint)).Length < 0.005, Rig.Joints[joint]);

        // In a plié the knees go out over the toes (the pole's own bias is a few degrees further out).
        foreach (var (hip, side) in new[] { (Rig.LHip, 1), (Rig.RHip, -1) })
        {
            var knee = At(clip, 6, hip + 1) - (At(clip, 6, hip) + At(clip, 6, hip + 2)) * 0.5;
            Assert.InRange(Round(knee) * side, 80, 92);
        }

        // A straight leg is turned in the thigh too: the knee's hinge goes round with the foot.
        double Hinge(double beat, int bone)
        {
            var hinge = clip.Figure.Rotation(clip.Clip.FrameOfBeat(beat), bone).Rotate(new Vec3(1, 0, 0));
            return Math.Atan2(-hinge.Z, hinge.X) * 180 / Math.PI;
        }
        Assert.Equal(80, Hinge(4, Skeleton.LUpperLeg) - Hinge(0, Skeleton.LUpperLeg), 0);
        Assert.Equal(-80, Hinge(4, Skeleton.RUpperLeg) - Hinge(0, Skeleton.RUpperLeg), 0);
    }

    [Fact]
    public void Turned_out_together_closes_the_heels()
    {
        double Apart(FuriClip clip, double beat) => (At(clip, beat, Rig.LAnkle) - At(clip, beat, Rig.RAnkle)).Length;
        // A row that begins turned out begins with the heels together; a together after a step closes them again.
        var first = Solve("    turnout { out(90) }\n    steps { then:2 leftFoot.side:2 together:2 }");
        Assert.Equal(Rig.HeelsTogether, Apart(first, 0), 3);
        Assert.Equal(Rig.HeelsTogether, Apart(first, 6), 3);
        // Half way out, half way in; parallel or turned in, standing width.
        Assert.Equal(Rig.Stance - (Rig.Stance - Rig.HeelsTogether) * Math.Sin(Math.PI / 4), Apart(Solve("    turnout { out(45) }\n    steps { then:2 }"), 0), 3);
        Assert.Equal(Rig.Stance, Apart(Solve("    turnout { in(20) }\n    steps { then:2 }"), 0), 3);
        Assert.Equal(Rig.Stance, Apart(Solve("    steps { then:2 }"), 0), 3);
    }

    [Fact]
    public void Turnout_says_out_or_in_and_degrees_or_parallel()
    {
        Assert.Empty(Compile(Body("    turnout { out(45):2 out(90)@sharp in(45) parallel }")).Diagnostics);
        Assert.Equal("SH1013", Assert.Single(Compile(Body("    turnout { out(95):4 }")).Diagnostics).Code);
        Assert.Equal("SH1013", Assert.Single(Compile(Body("    turnout { in(50):4 }")).Diagnostics).Code);
        Assert.Equal("SH1013", Assert.Single(Compile(Body("    turnout { out(high):4 }")).Diagnostics).Code);
        Assert.Equal("SH1013", Assert.Single(Compile(Body("    turnout { out:4 }")).Diagnostics).Code);   // how far is always written
        Assert.Equal("SH1012", Assert.Single(Compile(Body("    turnout { left:4 }")).Diagnostics).Code);
        Assert.Equal("SH1010", Assert.Single(Compile(Body("    turnout { out(45):4@bent }")).Diagnostics).Code);
        // A pose's turnout, named in the row.
        Assert.Empty(Compile(Samples.Solo("    body { first:2 }\n    turnout { parallel:2 first:2 }", "tempo 100\npose first { turnout { out(80) }  steps { together } }\n")).Diagnostics);
    }

    [Fact]
    public void Bent_takes_the_degrees_the_elbow_or_the_knee_bends()
    {
        var clip = Solve("    rightArm { down:2 fwd.bent(60):2 fwd.bent:2 }\n    leftLeg { down:2 fwd.down.bent(110):2 down:2 }");
        var at2 = clip.Figure.Angles(clip.Clip.FrameAt(2));
        // The degrees written are the degrees read (check --at says them).
        Assert.InRange(at2.RightElbow, 59, 61);
        Assert.InRange(at2.LeftKnee, 109, 111);
        Assert.InRange(clip.Figure.Angles(clip.Clip.FrameAt(4)).RightElbow, 89, 91);
        // The hand stays in its direction: straight ahead of the shoulder, nearer.
        var hand = At(clip, 2, Rig.RHand) - At(clip, 2, Rig.RShoulder);
        Assert.True(Math.Abs(hand.X) < 0.01 && Math.Abs(hand.Y) < 0.01, hand.ToString());
        Assert.Equal(Rig.BentShare(Rig.UpperArm, Rig.Forearm, 60) * Rig.Arm, hand.Length, 2);
    }

    [Fact]
    public void Elbows_and_knees_point_where_the_text_says()
    {
        // The elbow out from the line of shoulder to hand: down by itself, out to its own side when sent.
        Vec3 Out(FuriClip c, int root) => At(c, 2, root + 1) - (At(c, 2, root) + At(c, 2, root + 2)) * 0.5;
        var plain = Solve("    arms { down:2 fwd.down(15).bent(40):2 }");
        var held = Solve("    arms { down:2 fwd.down(15).bent(40).elbow(side):2 }");
        Assert.True(Out(plain, Rig.LShoulder).Y < -0.03, "hangs");
        Assert.True(Out(held, Rig.LShoulder).X > 0.05, "the left elbow out to the left");
        Assert.True(Out(held, Rig.RShoulder).X < -0.05, "the right elbow out to the right");

        // An attitude: the knee above the foot and out when sent up and out, below it when sent down.
        var hanging = Solve("    rightLeg { down:2 back.up(20).bent.knee(down):2 }");
        var attitude = Solve("    rightLeg { down:2 back.up(20).bent.knee(side.up):2 }");
        Assert.True(At(hanging, 2, Rig.RKnee).Y < At(hanging, 2, Rig.RAnkle).Y, "hangs");
        Assert.True(At(attitude, 2, Rig.RKnee).Y > At(attitude, 2, Rig.RAnkle).Y, "above the foot");
        Assert.True(At(attitude, 2, Rig.RKnee).X < At(attitude, 2, Rig.RHip).X - 0.1, "out to the right");

        // A straight arm sent round turns about its own length: the model's upper arm shows it.
        double Twist(string arm)
        {
            var c = Solve(arm);
            var hinge = c.Figure.Rotation(c.Clip.FrameOfBeat(2), Skeleton.LUpperArm).Rotate(new Vec3(0, 0, 1));
            return Math.Atan2(hinge.Y, -hinge.Z) * 180 / Math.PI;
        }
        Assert.True(Math.Abs(Twist("    leftArm { down:2 side.elbow(back):2 }") - Twist("    leftArm { down:2 side:2 }")) > 40);
    }

    [Fact]
    public void A_toe_goes_to_the_other_legs_knee_or_ankle()
    {
        var fwd = new Vec3(0, 0, 1);
        var clip = Solve("    rightLeg { down:2 otherKnee:2 otherAnkle:2 otherKnee.back:2 otherAnkle.back:2 down:2 }");
        Assert.True((At(clip, 2, Rig.RToe) - (At(clip, 2, Rig.LKnee) + fwd * 0.06)).Length < 0.02, "a retiré");
        Assert.True((At(clip, 4, Rig.RToe) - (At(clip, 4, Rig.LAnkle) + fwd * 0.06 + Vec3.Up * 0.05)).Length < 0.02, "a coupé");
        Assert.True((At(clip, 6, Rig.RToe) - (At(clip, 6, Rig.LKnee) - fwd * 0.06)).Length < 0.02, "behind the knee");
        Assert.True((At(clip, 8, Rig.RToe) - (At(clip, 8, Rig.LAnkle) - fwd * 0.06 + Vec3.Up * 0.05)).Length < 0.02, "behind the ankle");
        Assert.Equal(Rig.AnkleHeight, At(clip, 10, Rig.RAnkle).Y, 2);

        // Parallel, the knee of a retiré points forward; turned out, to the side. In a plié the
        // standing knee comes down and forward, and the toe goes with it.
        var parallel = Solve("    rightLeg { down:2 otherKnee:2 }");
        Assert.True(At(parallel, 2, Rig.RKnee).Z > At(parallel, 2, Rig.RHip).Z + 0.3, "forward");
        var turned = Solve("    turnout { out(80) }\n    rightLeg { down:2 otherKnee:2 }");
        Assert.True(At(turned, 2, Rig.RKnee).X < At(turned, 2, Rig.RHip).X - 0.3, "to the side");
        Assert.True((At(turned, 2, Rig.RToe) - (At(turned, 2, Rig.LKnee) + fwd * 0.06)).Length < 0.02, "turned out");
        var plie = Solve("    turnout { out(80) }\n    steps { then:2 then:2 }\n    height { then:2 low:2 }\n    rightLeg { down:2 otherKnee:2 }");
        Assert.True(At(plie, 2, Rig.LKnee).Y < At(turned, 2, Rig.LKnee).Y - 0.05, "the standing knee bends");
        Assert.True((At(plie, 2, Rig.RToe) - (At(plie, 2, Rig.LKnee) + fwd * 0.06)).Length < 0.02, "in a plié");
    }

    [Fact]
    public void Bent_elbow_knee_and_the_leg_places_say_what_is_wrong()
    {
        string Code(string tracks) => Assert.Single(Compile(Body(tracks)).Diagnostics).Code;
        Assert.Empty(Compile(Body("    arms { fwd.down(15).bent(40).elbow(side):2 fwd.up(70).bent(40).elbow(side.up) }\n    rightLeg { down:2 otherKnee.knee(side):2 back.up(20).bent(90).knee(fwd.up(30)):2 down }")).Diagnostics);
        Assert.Equal("SH1013", Code("    rightArm { fwd.bent(170):4 }"));
        Assert.Equal("SH1013", Code("    rightArm { fwd.bent(left):4 }"));
        Assert.Equal("SH1013", Code("    rightArm { fwd.elbow():4 }"));
        Assert.Equal("SH1012", Code("    rightArm { fwd.elbow(chin):4 }"));
        Assert.Equal("SH1010", Code("    rightArm { fwd.knee(side):4 }"));
        Assert.Equal("SH1010", Code("    rightLeg { fwd.elbow(side):4 }"));
        Assert.Equal("SH1010", Code("    steps { leftFoot.fwd:4@knee(side) }"));
        Assert.Equal("SH1010", Code("    torso { fwd.up(20).elbow(side):4 }"));
        Assert.Equal("SH1010", Code("    rightLeg { down:2 otherKnee.bent:2 }"));
        Assert.Equal("SH1013", Code("    rightArm { knee:4 }"));        // a shape word, after a direction
        Assert.Equal("SH1012", Code("    rightArm { otherKnee:4 }"));
        Assert.Equal("SH1012", Code("    rightLeg { chin:4 }"));
        // Pointed along the limb, the joint is not told anything.
        var along = Assert.Single(Compile(Body("    leftArm { side.elbow(side):4 }")).Diagnostics);
        Assert.Equal(("SH2011", DiagnosticSeverity.Warning), (along.Code, along.Severity));
    }

    [Fact]
    public void Degrees_in_brackets_say_how_far_round_from_the_first_direction_toward_the_second()
    {
        // The face 30 degrees to the left of straight ahead; with a level after the brackets, 10 degrees up as well.
        var clip = Solve("    head { fwd:2 fwd.left(30):2 fwd.left(30).up(10):2 fwd.left(30):2@mirror }\n    rightArm { fwd:2 fwd.across(15):2 fwd.side(90):2 then:2 }");
        Vec3 Face(double beat) => (At(clip, beat, Rig.Nose) - At(clip, beat, Rig.Head)).Normalized();
        double Round(Vec3 v) => Math.Atan2(v.X, v.Z) * 180 / Math.PI;   // to the figure's left of straight ahead
        Assert.Equal(0, Round(Face(0)), 1);
        Assert.Equal(30, Round(Face(2)), 1);
        Assert.Equal(0, Face(2).Y, 2);
        Assert.Equal(30, Round(Face(4)), 1);
        Assert.Equal(Math.Sin(10 * Math.PI / 180), Face(4).Y, 2);
        Assert.Equal(-30, Round(Face(6)), 1);

        // An arm: 15 degrees across is toward the midline (the right arm's across is the figure's left), 90 to the side is `side`.
        Vec3 Arm(double beat) => (At(clip, beat, Rig.RHand) - At(clip, beat, Rig.RShoulder)).Normalized();
        Assert.Equal(15, Round(Arm(2)), 1);
        Assert.Equal(-90, Round(Arm(4)), 1);

        // What has no brackets is as it was, and 45 in brackets is the same thing.
        var plain = Solve("    head { fwd:2 fwd.left:2 }");
        var written = Solve("    head { fwd:2 fwd.left(45):2 }");
        Assert.True((At(plain, 2, Rig.Nose) - At(written, 2, Rig.Nose)).Length < 1e-9);

        // From where is not said by one word, nor by two along one line; a place takes no number.
        foreach (string wrong in new[] { "left(30)", "fwd.back(30)", "fwd(30).left", "fwd.left(120)", "fwd.left(left)", "up(30)" })
            Assert.Equal("SH1013", Assert.Single(Compile(Body($"    head {{ {wrong}:4 }}")).Diagnostics).Code);
        Assert.Equal("SH1012", Assert.Single(Compile(Body("    rightArm { chest(30):4 }")).Diagnostics).Code);
        // The number of a turn goes in its bracket, not after a dot.
        Assert.Equal("SH1012", Assert.Single(Compile(Body("    shoulders { left.30:4 }")).Diagnostics).Code);
    }

    [Fact]
    public void Two_rows_of_an_and_that_turn_the_shoulders_at_the_same_time_are_told_the_first_is_not_seen()
    {
        const string Definitions = """
            tempo 100
            pose guard { arms { fwd.up(25).bent }  shoulders { right(20) } }
            move jab { leftArm { fwd@sharp guard }  shoulders { right(30)@sharp guard } }
            move cross { rightArm { fwd.down.bent fwd@sharp guard }  shoulders { right(35) left(30)@sharp guard } }
            move reach { rightArm { side guard } }

            """;
        ShosaCompilation Of(string body) => Compile(Samples.Solo("    body { guard:1 " + body + " then }", Definitions));

        // The jab's shoulder and the cross's drawn-back shoulder are both wanted in the same half beat.
        string text = Samples.Solo("    body { guard:1 { jab:1 } & { cross:3/2 } then }", Definitions);
        var diagnostic = Assert.Single(Compile(text).Diagnostics);
        Assert.Equal("SH2009", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("jab:1", text.Substring(diagnostic.Span.Start, diagnostic.Span.Length));

        // One after the other they share nothing: the jab is back in the guard's twist (a pose's own
        // word, which loses nothing) while the cross draws back.
        Assert.Empty(Of("{ jab:1 skip:3/2 } & { skip:1/2 cross:3/2 }").Diagnostics);
        // A row that only stands in the pose is not a twist done, and an arm two rows write is as it was.
        Assert.Empty(Of("{ guard:3/2 } & { cross:3/2 }").Diagnostics);
        Assert.Empty(Of("{ reach:3/2 } & { cross:3/2 }").Diagnostics);
        // The hips are one too.
        const string Hips = "tempo 100\nmove a { hips { left(30) center } }\nmove b { hips { right(30) center } }\n";
        Assert.Equal("SH2009", Assert.Single(Compile(Samples.Solo("    body { { a:2 } & { b:2 } then }", Hips)).Diagnostics).Code);
        // Found in the 16th writing trial: a later row that begins by standing in a pose takes the whip of the
        // row before it just the same. What is lost is what counts; one warning names the hips and the chest.
        const string Square = """
            tempo 100
            pose loose { arms { fwd.up(25).bent }  hips { center }  shoulders { center } }
            move hook { hips { loose:1 right(30):1/2@sharp loose:1/2 }  shoulders { loose:1 right(60):1/2@sharp loose:1/2 } }
            move upper { hips { loose:1 left(30):1/2@sharp loose:1/2 }  shoulders { loose:1 left(60):1/2@sharp loose:1/2 } }

            """;
        string flurry = Samples.Solo("    body { loose:1 { hook:2 skip:1 } & { skip:1 upper:2 } then }", Square);
        var hidden = Assert.Single(Compile(flurry).Diagnostics);
        Assert.Equal("SH2009", hidden.Code);
        Assert.Contains("turn the hips and the shoulders", hidden.Message);
        Assert.Equal("hook:2", flurry.Substring(hidden.Span.Start, hidden.Span.Length));
        // The other way round the upper's whip comes after the hook is home: nothing done is lost.
        Assert.Empty(Compile(Samples.Solo("    body { loose:1 { skip:1 upper:2 } & { hook:2 skip:1 } then }", Square)).Diagnostics);
        // Written as rows of the section, a narrower track over a wider one is the way to take a part over.
        Assert.Empty(Compile(Samples.Solo("    body { guard:1 jab:1 then }\n    shoulders { skip:1 left(30):1 then }", Definitions)).Diagnostics);
    }

    [Fact]
    public void A_jump_to_the_left_lands_to_the_left_and_mirrored_to_the_right()
    {
        var left = Solve("    steps { then:2 jump.left then }");
        var right = Solve("    steps { then:2 jump.left@mirror then }");

        Assert.True(Math.Abs(At(left, 4, Rig.Pelvis).X - At(left, 0, Rig.Pelvis).X - 0.4) < 0.01, "0.4 m to the dancer's left");
        Assert.True(Math.Abs(At(left, 4, Rig.Pelvis).Z - At(left, 0, Rig.Pelvis).Z) < 0.01, "and not forward");
        Assert.True(Math.Abs(At(right, 4, Rig.Pelvis).X - At(right, 0, Rig.Pelvis).X + 0.4) < 0.01, "mirrored, to the right");
    }

    [Fact]
    public void Check_says_how_the_body_is_turned_at_a_moment_from_the_way_the_feet_face()
    {
        // How far the elbows and knees are bent comes after: another test reads it.
        string Line(string tracks, params (double, bool)[] moments) =>
            Shosa.Core.Output.CheckReport.At(Compile(Samples.Solo(tracks, "tempo 120\ntime 4/4\n")), moments).Single().Split("; bent:")[0];

        // The numbers written are the numbers read.
        Assert.Equal("  score at beat 2 = 1 s: hips 20° left, shoulders 45° left, face 30° right and 10° up",
            Line("    hips { center left(20):3 }\n    shoulders { center left(45):3 }\n    head { fwd fwd.right(30).up(10):3 }", (2, false)));
        // In seconds, and against each other: the hips to the right under shoulders to the left.
        Assert.Equal("  score at beat 2 = 1 s: hips 20° right, shoulders 10° left, face 0°",
            Line("    hips { center right(20):3 }\n    shoulders { center left(10):3 }", (1, true)));
        // From the feet, not from the room: after a quarter turn nothing is turned on them.
        Assert.Equal("  score at beat 3 = 1.5 s: hips 0°, shoulders 0°, face 0° (the feet face 90° left of the front)",
            Line("    steps { then:2 turn.left(90) then:2 }", (3, false)));
        Assert.Equal("  score at beat 9: not in the score (4 beats, 2 s)", Line("    steps { then:4 }", (9, false)));
        // Over a lean too, the shoulders are where they are seen from above (the chest turns 49° on
        // the hips for 45 on the floor); and a face looking steeply down over them (40° below the chest's own fwd): the line of the
        // ears says the few degrees the head is turned (the nose on the floor said 9).
        Assert.Equal("  score at beat 4 = 2 s: hips 40° right, shoulders 85° right, face 3.5° left and 69.9° down",
            Line("    torso { up.fwd(30):4 }\n    head { fwd.down(40).body:4 }\n    hips { center:2 right(40):2 }\n    shoulders { center:2 right(85):2 }", (4, false)));
    }

    [Fact]
    public void Shoulders_the_chest_cannot_turn_to_on_the_hips_are_told_how_far_they_get()
    {
        // Upright the chest turns 60° on the hips: 90 from the feet over hips at 30 is in reach, over square hips it is not.
        Assert.Empty(Compile(Body("    hips { center:2 right(30):2 }\n    shoulders { center:2 right(90):2 }")).Diagnostics);
        var upright = Assert.Single(Compile(Body("    shoulders { center:2 right(90):2 }")).Diagnostics);
        Assert.Equal("SH2010", upright.Code);
        Assert.Equal(DiagnosticSeverity.Warning, upright.Severity);
        Assert.Contains("get to 60° right of the feet here, not 90° right", upright.Message);
        Assert.DoesNotContain("lean", upright.Message);
        // Bent 60° forward, a turn of the chest about the spine shows half as much from above: 60° of chest is 40.9 on the floor.
        string bent = Body("    torso { fwd.up(30):4 }\n    shoulders { center:2 right(60):2 }");
        var warning = Assert.Single(Compile(bent).Diagnostics);
        Assert.Equal("SH2010", warning.Code);
        Assert.Contains("get to 40.9° right of the feet here, not 60° right", warning.Message);
        Assert.Contains("over a lean of 60°", warning.Message);
        Assert.StartsWith("right(60)", bent.Substring(warning.Span.Start));
        // Leaning far to the side, hips turned 45° under shoulders nobody wrote: the hips' row is the one told.
        string aside = Body("    torso { left.up(30):4 }\n    hips { center:2 left(45):2 }");
        var hips = Assert.Single(Compile(aside).Diagnostics);
        Assert.Equal("SH2010", hips.Code);
        Assert.Contains("not square to the feet", hips.Message);
        Assert.Contains("With no `shoulders` row", hips.Message);
        Assert.StartsWith("left(45)", aside.Substring(hips.Span.Start));
        // Bent level and turned toward the aim with the torso's own direction: every turn about that
        // spine shows the same line from above, so the row is met and nothing is warned (writing trial 18).
        string level = Body("    torso { up:2 fwd.right(70):2 }\n    hips { center:2 right(30):2 }\n    shoulders { center:2 right(70):2 }");
        Assert.Empty(Compile(level).Diagnostics);
        // Bent level straight ahead, the shoulders' row cannot show at all: the warning says to bend toward that side.
        var flat = Assert.Single(Compile(Body("    torso { up:2 fwd:2 }\n    shoulders { center:2 right(60):2 }")).Diagnostics);
        Assert.Equal("SH2010", flat.Code);
        Assert.Contains("stay square to the feet here, not 60° right", flat.Message);
        Assert.Contains("`torso { fwd.right(60) }`", flat.Message);
        Assert.Empty(Compile(Body("    torso { up:2 fwd.right(60):2 }\n    shoulders { center:2 right(60):2 }")).Diagnostics);
    }

    [Fact]
    public void Check_says_where_each_dancer_ends_from_where_it_began()
    {
        string Line(string tracks, string score = "") =>
            Shosa.Core.Output.CheckReport.Scores(Compile(Samples.Solo(tracks, "tempo 100\ntime 4/4\n", score)), "x.shosa")
                .Single(line => line.StartsWith("    solo ") && !line.Contains("already made"));

        Assert.Equal("    solo ends where it began", Line("    steps { then leftFoot.fwd rightFoot.fwd leftFoot.back rightFoot.back stand }"));
        // The first step is already made: it begins with the left foot a stride ahead (the middle of
        // the feet 0.25 m ahead of the right one), and ends with both feet a stride behind the right one.
        Assert.Equal("    solo ends 0.75 m back of where it began", Line("    steps { leftFoot.fwd rightFoot.back leftFoot.back stand }"));
        Assert.Equal("    solo ends 0.4 m to its left of where it began, turned 90° to its right", Line("    steps { then:2 jump.left turn.right(90) then }"));
        Assert.Equal("    solo travels 1 m forward each time round", Line("    steps { leftFoot.fwd rightFoot.fwd }", "loop"));
        Assert.Equal("    solo travels 1 m forward each time round (taken out of the movement: inPlace)", Line("    steps { leftFoot.fwd rightFoot.fwd }", "loop inPlace"));
        Assert.Equal("    solo loops on the spot", Line("    arms { side down }", "loop"));
    }

    private static string Two(double apart, string a, string b = "head { then:2 }") => FormattableString.Invariant(
        $"tempo 100\ntime 4/4\nrole a\nrole b\ndancer ann humanoid on a at {-apart / 2} 0 facing stageLeft\ndancer ben humanoid on b at {apart / 2} 0 facing stageRight\nsection P {{\n  a {{ {a} }}\n  b {{ {b} }}\n}}\nform {{ P }}\nscore {{ dancer ann  dancer ben }}\n");

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
        Assert.Empty(Compile(Two(1.0, "steps { leftFoot.fwd then }\n rightArm { then b.chest }")).Diagnostics);
    }

    [Fact]
    public void A_bar_with_nothing_in_it_is_a_bar_of_saying_nothing()
    {
        // As in Lily#: every line closes a bar, the first one of a row too.
        var clip = Compile(Body("    arms { | | side:1 then:3 | }")).Clip;
        Assert.Empty(Compile(Body("    arms { | | side:1 then:3 | }")).Diagnostics);
        Assert.Equal(12, clip.TotalBeats);

        // `%` does an empty bar again.
        var again = Compile(Body("    arms { | % | side:4 | }"));
        Assert.Empty(again.Diagnostics);
        Assert.Equal(12, again.Clip.TotalBeats);
    }

    private static string Twice(string arms, string form = "form { |: A :| }", string define = "") =>
        $"tempo 100\ntime 4/4\nrole solo\ndancer solo humanoid on solo\n{define}section A {{ solo {{ arms {{ {arms} }} }} }}\n{form}\nscore {{ dancer solo }}\n";

    [Fact]
    public void A_verse_is_done_on_its_play_of_the_section_only()
    {
        // The first time the bar before again, the second time something else; what has no verse is done both times.
        var compiled = Compile(Twice("side:3 fwd:1!every | verse 1 { % | } verse 2 { side.up:4!second | }"));
        Assert.Empty(compiled.Diagnostics);
        Assert.Equal(16, compiled.Clip.TotalBeats);
        // A mark is on the beat its word is written at: the fourth of a bar, and the first of the last bar.
        Assert.Equal(new[] { ("every", 3.0), ("every", 7.0), ("every", 11.0), ("second", 12.0) },
            compiled.Clip.Marks.Select(m => (m.Name, m.Beat)).ToArray());

        // The place is as long as the longest verse on every play, and a list names several plays.
        var uneven = Compile(Twice("verse 1,3 { side:4!odd | } verse 2 { fwd:4 | side.up:4!late | }", "form { |: A :|*3 }"));
        Assert.Empty(uneven.Diagnostics);
        Assert.Equal(24, uneven.Clip.TotalBeats);
        Assert.Equal(new[] { ("odd", 0.0), ("late", 12.0), ("odd", 16.0) },
            uneven.Clip.Marks.Select(m => (m.Name, m.Beat)).ToArray());

        // A range: plays 2 and 3 of three.
        Assert.Equal(new[] { 4.0, 8.0 }, Compile(Twice("verse 2-3 { side:4!late | }", "form { |: A :|*3 }")).Clip.Marks.Select(m => m.Beat).ToArray());
        // What is wrong in a verse is said even where no play of this score does it.
        Assert.Contains(Compile(Twice("side:4 | verse 5 { fwd:3 | }")).Diagnostics, d => d.Code == "SH1002");
        // A move is not played a first and a second time.
        Assert.Contains(Compile(Twice("wave:4 |", define: "move wave { arms { verse 1 { side:4 } } }\n")).Diagnostics, d => d.Code == "SH1034");
        // The pose a score starts in is its first word, on the first play and on the second: a verse is not
        // needed to keep it to the first, and a word of no length is refused in one as anywhere.
        Assert.Contains(Compile(Twice("verse 1 { up:0 } side:4 |")).Diagnostics, d => d.Code == "SH1009");
        Assert.Contains(Compile(Twice("up:0 side:4 |")).Diagnostics, d => d.Code == "SH1009");
        // A part of a bar, the line after it.
        var lastBeat = Compile(Twice("side:3 verse 1 { fwd } verse 2 { side.up!second } |"));
        Assert.Empty(lastBeat.Diagnostics);
        Assert.Equal(new[] { 7.0 }, lastBeat.Clip.Marks.Select(m => m.Beat).ToArray());
        // One bar sixteen times.
        var sixteen = Compile(Body("    arms { side:2 down:2 | % | %2 | %4 | %8 | }"));
        Assert.Empty(sixteen.Diagnostics);
        Assert.Equal(64, sixteen.Clip.TotalBeats);
    }

    [Fact]
    public void A_wait_is_counted_in_bars()
    {
        var waited = Compile(Body("    arms { skip:2bar | side:1 then:3 | then:1bar | }"));
        Assert.Empty(waited.Diagnostics);
        Assert.Equal(16, waited.Clip.TotalBeats);

        // In three to the bar a bar is three beats.
        Assert.Equal(9, Compile(Samples.Solo("    arms { skip:2bar | up:3 | }", "tempo 100\ntime 3/4\n")).Clip.TotalBeats);

        // Only a wait, only whole bars, only from the start of a bar.
        foreach (string row in new[] { "arms { up:2bar | }", "arms { skip:1/2bar up:2 | }", "arms { up:1 skip:1bar | }", "arms { { up side } q:1bar | }" })
            Assert.Contains(Compile(Body("    " + row)).Diagnostics, d => d.Code == "SH1033");
    }

    [Fact]
    public void A_close_with_the_weight_on_both_feet_brings_both_feet_in_alike()
    {
        // After a jump the weight is on both feet: neither is the free one, so each comes half the way.
        // The jump comes down on beat 1, the close is there on beat 3 and is made in the second half of the `then` before it.
        var clip = Solve("    steps { then jump.apart then stand:2 }");
        Vec3 Foot(double beat, int joint) => At(clip, beat, joint) - At(clip, 0, joint);

        Assert.True(Foot(1, Rig.LAnkle).X > 0.1 && Foot(1, Rig.RAnkle).X < -0.1, "apart as the jump comes down");
        Assert.True(Foot(2.5, Rig.LAnkle).X > 0.1 && Foot(2.5, Rig.RAnkle).X < -0.1, "and until the close sets out");
        Assert.True(Foot(3, Rig.LAnkle).Length < 0.01 && Foot(3, Rig.RAnkle).Length < 0.01, "and back where they stood");
        Assert.True(Math.Abs(At(clip, 2.75, Rig.Pelvis).X - At(clip, 0, Rig.Pelvis).X) < 0.01, "the weight stays between them");

        // A foot made free closes alone; feet that already stand together have nothing to do (a knee bend).
        var one = Solve("    steps { then jump.apart shift.left stand:2 }");
        Assert.True(At(one, 3, Rig.RAnkle).X > At(one, 2, Rig.RAnkle).X + 0.3, "the free foot comes in");
        Assert.True((At(one, 3, Rig.LAnkle) - At(one, 2, Rig.LAnkle)).Length < 0.01, "the standing foot stays");
        Assert.Empty(Compile(Body("    steps { stand:2 stand:2 }\n    height { low:2 mid:2 }")).Diagnostics);
    }

    private static string Sections(string first, string second, string form = "form { A B }", string before = "tempo 120\ntime 4/4\n", string score = "") =>
        $"{before}role solo\ndancer solo humanoid on solo\nsection A {{ {first} }}\nsection B {{ {second} }}\n{form}\nscore {{ dancer solo  {score} }}\n";

    [Fact]
    public void A_section_says_the_tempo_and_the_time_from_there_on()
    {
        // The tempo stays until a section that writes another is played: C writes none, and is at 90.
        // The time is each section's own: C and D are in three because they say so.
        string text = $"tempo 120\ntime 4/4\n{Samples.SoloCast}"
            + "section A { solo { arms { side:4 | down:4 | } } }\n"
            + "section B { tempo 90  time 3/4\n solo { arms { side:3!three | % | skip:1bar | } } }\n"
            + "section C { time 3/4\n solo { arms { up:3 | } } }\n"
            + "section D { tempo 120  time 3/4\n solo { arms { side:3 | } } }\n"
            + "form { A B C D }\nscore { dancer solo }\nscore half { dancer solo  tempo 60 }\n";
        var compiled = ShosaCompilation.Compile(text, score: "");
        Assert.Empty(compiled.Diagnostics);
        var clip = compiled.Clip;

        Assert.Equal(23, clip.TotalBeats);
        Assert.Equal(new[] { (0.0, 120.0), (8.0, 90.0), (20.0, 120.0) }, clip.Tempos.Select(t => (t.Beat, t.Bpm)).ToArray());
        Assert.Equal(new[] { (0.0, 4), (8.0, 3) }, clip.Meters.Select(m => (m.Beat, m.BeatsPerBar)).ToArray());
        Assert.Equal(4 + 12 * 60 / 90.0 + 1.5, clip.SecondsAt(23), 6);
        Assert.Equal(new[] { 8.0, 11.0 }, clip.Marks.Select(m => m.Beat).ToArray());   // the first beat of a bar of three, and of the bar `%` does again

        // A score's own tempo is the one the file begins at; the sections' keep their proportion.
        var half = ShosaCompilation.Compile(text, score: "half").Clip;
        Assert.Equal(new[] { 60.0, 45.0, 60.0 }, half.Tempos.Select(t => t.Bpm).ToArray());

        // A section's bars are the same wherever the form plays it: after B, A is in the file's four again,
        // and a section that writes no time is not in three because the one before it was.
        var turned = Compile(Sections("solo { arms { side:4 | } }", "time 3/4\n solo { arms { fwd:3 | } }", "form { B A B }"));
        Assert.Empty(turned.Diagnostics);
        Assert.Equal(new[] { (0.0, 3), (3.0, 4), (7.0, 3) }, turned.Clip.Meters.Select(m => (m.Beat, m.BeatsPerBar)).ToArray());
        var silent = Assert.Single(Compile(Sections("time 3/4\n solo { arms { side:3 | } }", "solo { arms { up:3 | } }")).Diagnostics);
        Assert.Equal("SH1002", silent.Code);
        Assert.Contains("begin it with `time 3/4`", silent.Message);

        // A beat that becomes another note keeps the music's speed: a quaver at 240 is a crotchet at 120, there and back.
        var quavers = Compile(Sections("solo { arms { side:4 | } }", "time 6/8\n solo { arms { up:6 | } }", "form { A B A }")).Clip;
        Assert.Equal(new[] { 120.0, 240.0, 120.0 }, quavers.Tempos.Select(t => t.Bpm).ToArray());
    }

    [Fact]
    public void A_hand_held_out_is_not_fast_because_the_hips_whip_round_under_it()
    {
        // The arm's `fwd` is measured from the feet: the hand stays where it is in the room (under 6 m/s, the
        // shoulder's own way) while hips and shoulders go 90 and 180 degrees in 0.15 s. Judged against the line of the hips
        // it was a hand at 10 m/s, and a warning for an arm that does nothing.
        string Whip(string arms) => $"tempo 120\ntime 4/4\n{Samples.SoloCast}"
            + "section S { solo { steps { jump.apart:8 } arms { " + arms + " }\n"
            + "  hips { center:2 left(45):2 right(45):2@sharp center:2 } shoulders { center:2 left(90):2 right(90):2@sharp center:2 } } }\n"
            + "form { S }\nscore { dancer solo }\n";
        Assert.Empty(ShosaCompilation.Compile(Whip("down:2 down:2 fwd:2 down:2")).Diagnostics);

        // An arm that goes with the chest (`.body`) is carried round by it, and that is still seen.
        var carried = ShosaCompilation.Compile(Whip("side.body:2 side.body:2 side.body:2 side.body:2")).Diagnostics;
        Assert.All(carried, d => Assert.Equal("SH2003", d.Code));
        Assert.NotEmpty(carried);
    }

    [Fact]
    public void A_warning_that_is_not_in_every_score_names_the_scores_it_is_in()
    {
        // Found in the 9th writing trial: the same strike is too fast only in the score that plays it faster.
        // (The arm goes forward in the half beat of the word before it. A `@sharp` strike would not do here: it
        // takes its 0.15 s at any tempo, so it is too fast in every score or in none.)
        string text = $"tempo 120\ntime 4/4\n{Samples.SoloCast}"
            + "section Hit { solo { rightArm { back.side:1/2 fwd:7/2 | } } }\n"
            + "form { Hit }\nscore { dancer solo }\nscore rage { dancer solo  tempo 300 }\nscore slow { dancer solo  tempo 50% }\n";
        var warning = Assert.Single(ShosaCompilation.Compile(text).Diagnostics);
        Assert.Equal("SH2003", warning.Code);
        Assert.EndsWith("(In score rage.)", warning.Message);

        // One score alone, or every score: nothing to tell apart.
        Assert.DoesNotContain("(In", Assert.Single(ShosaCompilation.Compile(text, score: "rage").Diagnostics).Message);
        string both = text.Replace("tempo 120\n", "tempo 300\n").Replace("score rage { dancer solo  tempo 300 }\nscore slow { dancer solo  tempo 50% }\n", "score again { dancer solo }\n");
        Assert.DoesNotContain("(In", Assert.Single(ShosaCompilation.Compile(both).Diagnostics).Message);
    }

    [Fact]
    public void A_scores_own_tempo_is_the_one_it_begins_at()
    {
        // Found in the 7th writing trial: a practice score of a section that writes its own tempo.
        string text = $"tempo 100\ntime 4/4\n{Samples.SoloCast}"
            + "section Warm { solo { arms { side:4 | } } }\n"
            + "section Fast { tempo 160  time 2/4\n solo { arms { up:2!pose | % | } } }\n"   // a mark may be named by any word
            + "section Cool { time 2/4\n solo { arms { fwd:2 | } } }\n"
            + "section AsFast { tempo 160  time 2/4 }\n"
            + "form { Warm Fast Cool }\nform fast { Fast Cool }\nform cool { AsFast Cool }\nform slow { Warm Fast }\n"
            + "score { dancer solo }\nscore fast { dancer solo  form fast  tempo 80 }\n"
            + "score cool { dancer solo  form cool  tempo 80 }\nscore slow { dancer solo  form slow  tempo 50 }\n";
        Assert.Empty(ShosaCompilation.Compile(text, score: "").Diagnostics);

        // Fast is played first and writes 160: the score's 80 is half of THAT, not 80/100 of it.
        var fast = ShosaCompilation.Compile(text, score: "fast").Clip;
        Assert.Equal(80, Assert.Single(fast.Tempos).Bpm);
        Assert.Equal(2, Assert.Single(fast.Meters).BeatsPerBar);
        Assert.Equal(new[] { 0.0, 2.0 }, fast.Marks.Select(m => m.Beat).ToArray());   // `%` gives the mark again

        // A section of no rows has no length, so the score begins in what it writes.
        var cool = ShosaCompilation.Compile(text, score: "cool").Clip;
        Assert.Equal(2, cool.TotalBeats);
        Assert.Equal(80, Assert.Single(cool.Tempos).Bpm);

        // The first section writes none: the file's tempo is the one the score begins at, as before.
        var slow = ShosaCompilation.Compile(text, score: "slow").Clip;
        Assert.Equal(new[] { 50.0, 80.0 }, slow.Tempos.Select(t => t.Bpm).ToArray());
    }

    [Fact]
    public void A_form_says_the_tempo_and_the_time_of_one_play()
    {
        // The 7th writing trial's three: the last march alone faster, the same bars at three tempos, a cut in the whole's tempo.
        string sections = $"tempo 100\ntime 4/4\n{Samples.SoloCast}"
            + "section March { tempo 120  time 4/4\n solo { arms { side:4 | } } }\n"
            + "section Waltz { tempo 90  time 3/4\n solo { arms { up:3 | } } }\n"
            + "section Drill { solo { arms { fwd:2 down:2 | } } }\n"
            + "section Cool { solo { arms { fwd:2 | } } }\n";
        string text = sections
            + "form { March Waltz tempo 132 March }\n"
            + "form drill { tempo 100 Drill tempo 120 Drill\n tempo 140 Drill }\n"
            + "form again { |: tempo 80 Drill March [1. tempo 60 Drill] :| [2. Drill] }\n"
            + "form cool { tempo 160 time 2/4 Cool }\n"
            + "score { dancer solo }\nscore drill { dancer solo  form drill }\nscore again { dancer solo  form again }\n"
            + "score cool { dancer solo  form cool }\nscore half { dancer solo  form cool  tempo 80 }\n";

        // What the form writes before a name comes before what the section writes, that one time.
        var whole = ShosaCompilation.Compile(text, score: "");
        Assert.Empty(whole.Diagnostics);
        Assert.Equal(new[] { (0.0, 120.0), (4.0, 90.0), (7.0, 132.0) }, whole.Clip.Tempos.Select(t => (t.Beat, t.Bpm)).ToArray());
        Assert.Equal(new[] { (0.0, 4), (4.0, 3), (7.0, 4) }, whole.Clip.Meters.Select(m => (m.Beat, m.BeatsPerBar)).ToArray());

        var drill = ShosaCompilation.Compile(text, score: "drill");
        Assert.Empty(drill.Diagnostics);
        Assert.Equal(new[] { (0.0, 100.0), (4.0, 120.0), (8.0, 140.0) }, drill.Clip.Tempos.Select(t => (t.Beat, t.Bpm)).ToArray());

        // It is for that one play: the next name is played as if the form had written nothing (the
        // 8th and 9th writing trials pinned the next section down again, or copied "the usual tempo" to it).
        var once = ShosaCompilation.Compile(sections
            + "form { Drill tempo 110% Drill tempo 120% Drill tempo 110% time 2/4 Cool Drill March tempo 50% Drill Drill }\nscore { dancer solo }\n", score: "");
        Assert.Empty(once.Diagnostics);
        Assert.Equal(new[] { (0.0, 100.0), (4.0, 110.0), (8.0, 120.0), (12.0, 110.0), (14.0, 100.0), (18.0, 120.0), (22.0, 60.0), (26.0, 120.0) },
            once.Clip.Tempos.Select(t => (t.Beat, t.Bpm)).ToArray());
        Assert.Equal(new[] { (0.0, 4), (12.0, 2), (14.0, 4) }, once.Clip.Meters.Select(m => (m.Beat, m.BeatsPerBar)).ToArray());

        // It belongs to the name it stands before, every time a repeat plays that name.
        var again = ShosaCompilation.Compile(text, score: "again");
        Assert.Empty(again.Diagnostics);
        Assert.Equal(new[] { (0.0, 80.0), (4.0, 120.0), (8.0, 60.0), (12.0, 80.0), (16.0, 120.0) }, again.Clip.Tempos.Select(t => (t.Beat, t.Bpm)).ToArray());
        Assert.Equal(24, again.Clip.TotalBeats);

        // A score that plays a section alone says in its form what the whole piece would have played before it.
        var cool = ShosaCompilation.Compile(text, score: "cool").Clip;
        Assert.Equal(160, Assert.Single(cool.Tempos).Bpm);
        Assert.Equal(2, Assert.Single(cool.Meters).BeatsPerBar);
        Assert.Equal(80, Assert.Single(ShosaCompilation.Compile(text, score: "half").Clip.Tempos).Bpm);

        // With no name after it, it is for nothing.
        var last = ShosaCompilation.Compile(sections + "form { March tempo 132 }\nscore { dancer solo }\n", score: "");
        Assert.Equal("SH1037", Assert.Single(last.Diagnostics).Code);

        // The guess of the trial, a verse around the section's tempo, is told where it goes: once.
        var verse = ShosaCompilation.Compile(sections.Replace("section Drill {", "section Drill {\n verse 2 { tempo 132 }\n")
            + "form { Drill Drill }\nscore { dancer solo }\n", score: "");
        Assert.Equal("SH0013", Assert.Single(verse.Diagnostics).Code);
    }

    [Fact]
    public void A_tempo_is_written_in_per_cent_or_by_a_note_wherever_a_tempo_is_written()
    {
        // The 7th and 8th writing trials worked these out by hand: half the speed of an attack
        // (140 looked up, 70 written), a dotted crotchet at 60 in 6/8 (180 written).
        string sections = $"tempo 4 = 120\ntime 4/4\n{Samples.SoloCast}"
            + "section March { tempo 120  time 4/4\n solo { arms { side:4 | } } }\n"
            + "section Jig { tempo 4. = 60\n time 6/8\n solo { arms { fwd:3 down:3 | } } }\n"
            + "section Trio { tempo 80%  time 6/8\n solo { arms { fwd:3 down:3 | } } }\n"
            + "section Plain { solo { arms { fwd:2 down:2 | } } }\n";
        string text = sections
            + "form { March Jig Trio tempo 110% March }\n"
            + "form march { March }\n"
            + "form plain { Plain tempo 50% Plain }\n"
            + "form six { tempo 4 = 120 time 6/4 Trio tempo 4. = 80 time 6/8 Trio }\n"
            + "score { dancer solo }\nscore replay { dancer solo  form march  tempo 50% }\n"
            + "score plain { dancer solo  form plain }\nscore six { dancer solo  form six }\n"
            + "score slow { dancer solo  tempo 2 = 30 }\nscore faster { dancer solo  form plain  tempo 150% }\n";

        // A note's count is turned into the beats of the time in force there; a percentage is of the tempo
        // there would be without it — the tempo before (Trio), or the section's own under a form's (the last March).
        var whole = ShosaCompilation.Compile(text, score: "");
        Assert.Empty(whole.Diagnostics);
        Assert.Equal(new[] { (0.0, 120.0), (4.0, 180.0), (10.0, 144.0), (16.0, 132.0) }, whole.Clip.Tempos.Select(t => (t.Beat, t.Bpm)).ToArray());

        // A score's percentage is of the tempo it would begin at, which its first section may write.
        Assert.Equal(60, Assert.Single(ShosaCompilation.Compile(text, score: "replay").Clip.Tempos).Bpm);
        Assert.Equal(new[] { 60.0, 90.0, 72.0, 66.0 }, ShosaCompilation.Compile(text, score: "slow").Clip.Tempos.Select(t => t.Bpm).ToArray());

        // Before a section that writes no tempo it is of the tempo in force; a score's own is over all of it.
        Assert.Equal(new[] { 120.0, 60.0 }, ShosaCompilation.Compile(text, score: "plain").Clip.Tempos.Select(t => t.Bpm).ToArray());
        Assert.Equal(new[] { 180.0, 90.0 }, ShosaCompilation.Compile(text, score: "faster").Clip.Tempos.Select(t => t.Bpm).ToArray());

        // The same six counts as crotchets and as quavers: a tempo by its note with the time it is counted in.
        var six = ShosaCompilation.Compile(text, score: "six");
        Assert.Empty(six.Diagnostics);
        Assert.Equal(new[] { (0.0, 120.0), (6.0, 240.0) }, six.Clip.Tempos.Select(t => (t.Beat, t.Bpm)).ToArray());

        // At the top of a file without a song there is nothing for a percentage to be of.
        var top = ShosaCompilation.Compile("tempo 50%\n" + sections.Replace("tempo 4 = 120\n", "") + "form { Plain }\nscore { dancer solo }\n", score: "");
        Assert.Equal("SH1038", Assert.Single(top.Diagnostics).Code);

        // What it comes to is still a tempo, and the note is one of Lily#'s.
        var wild = ShosaCompilation.Compile(sections + "form { tempo 1% Plain }\nscore { dancer solo }\n", score: "");
        Assert.Contains("1.2 beats a minute", Assert.Single(wild.Diagnostics).Message);
        var note = ShosaCompilation.Compile(sections + "form { tempo 3 = 60 Plain }\nscore { dancer solo }\n", score: "");
        Assert.Equal("SH1001", Assert.Single(note.Diagnostics).Code);
    }

    [Fact]
    public void A_repeat_after_an_upbeat_does_the_bars_after_it_again()
    {
        string second = "upbeat 1\n solo { arms { up:1 | side:4!wide | % | verse 1 { fwd:4 | } } }";
        var compiled = Compile(Sections("solo { arms { side:4 | fwd:4 | } }", second));
        Assert.Empty(compiled.Diagnostics);
        Assert.Equal(20, compiled.Clip.TotalBeats);
        // B's first bar line is at 8 (the upbeat is the last beat of A): the mark is on the first beat of that bar, and of the one `%` does again.
        Assert.Equal(new[] { 8.0, 12.0 }, compiled.Clip.Marks.Select(m => m.Beat).ToArray());

        // A skip or a `then` of no length is told that it does nothing.
        var none = Compile(Sections("solo { arms { side:4 | } }", "solo { arms { up:2 skip:0 fwd:2 | } }"));
        Assert.Contains(none.Diagnostics, d => d.Code == "SH1009" && d.Message.Contains("does nothing"));
    }

    [Fact]
    public void A_bar_done_again_sets_out_from_the_word_before_the_sign_not_from_the_section_before()
    {
        // A ends on a word of one beat. B's first word had that one beat to come in; done again by `%`,
        // the same word comes after `left:2` and has its two beats (trial 15: it had one).
        string first = "solo { shoulders { left(30):3 right(30):1 | } }";
        var signs = Compile(Sections(first, "solo { shoulders { center:2 left(30):2 | % | } }"));
        var written = Compile(Sections(first, "solo { shoulders { center:2 left(30):2 | center:2 left(30):2 | } }"));

        Assert.Empty(signs.Diagnostics);
        Same(written, signs);
        var again = signs.Ir.Dancers[0].Tracks["shoulders"].Single(s => Math.Abs(s.At - 8) < 1e-9);
        Assert.Equal(6, again.T0, 6);
    }

    [Fact]
    public void An_upbeat_is_done_over_the_end_of_what_is_played_before()
    {
        // The arms use the upbeat (their first bar is as long as it); the steps begin at the bar line.
        string second = "upbeat 1\n solo { arms { up:1!there | side:4 | }\n steps { leftFoot.side stand rightFoot.side stand | } }";
        var compiled = Compile(Sections("solo { arms { side:4 | fwd:4!kept | } }", second));
        Assert.Empty(compiled.Diagnostics);
        var clip = compiled.Clip;

        Assert.Equal(12, clip.TotalBeats);
        Assert.Equal(8, clip.Sections[1].T0);
        // The upbeat's word is written a beat before B's first bar line and the arms are there then, in the last
        // beat of A. A's last word has arrived by then (it is there on its own beat) and has its time up to the upbeat.
        Assert.Equal(new[] { ("kept", 4.0), ("there", 7.0) }, clip.Marks.Select(m => (m.Name, m.Beat)).ToArray());
        var figure = clip.Figures[0];
        Vec3 Arm(double beat) => (figure.Joint(clip.FrameAt(beat), Rig.LHand) - figure.Joint(clip.FrameAt(beat), Rig.LShoulder)).Normalized();
        Assert.True(Arm(4).Z > 0.99, "forward on A's last word");
        Assert.True(Arm(7).Y > 0.99, "the hands are up on the upbeat, a beat before B's first bar");
        Assert.True(Arm(8).X > 0.99, "and out to the sides on its first beat");
        var arm = compiled.Ir.Dancers[0].Tracks[Shosa.Core.Semantics.Channels.LeftArm];
        Assert.Equal(new[] { (0.0, 4.0), (4.0, 7.0), (7.0, 8.0), (8.0, 12.0) }, arm.Select(s => (s.At, s.Until)).ToArray());
        // The way up is made in what is left of A's last word; the first step of B in the half second before its beat.
        Assert.Equal((4.0, 7.0), (arm[2].T0, arm[2].T1));
        var step = compiled.Ir.Dancers[0].Tracks[Shosa.Core.Semantics.Channels.Support][0];
        Assert.Equal((7.0, 8.0, 8.0), (step.T0, step.T1, step.At));

        // With nothing before it an upbeat is before anything is seen: the body starts in the word at the first bar line.
        var first = Compile(Sections("upbeat 1\n solo { arms { up:1 | side:4 | } }", "solo { arms { down:4 | } }"));
        Assert.Empty(first.Diagnostics);
        Assert.Equal(8, first.Clip.TotalBeats);
        Vec3 hand = first.Clip.Figures[0].Joint(0, Rig.LHand), shoulder = first.Clip.Figures[0].Joint(0, Rig.LShoulder);
        Assert.True((hand - shoulder).Normalized().X > 0.99, "out to the side at beat 0");

        // A jump the score starts with has been made: the feet stand where it landed.
        var jumped = Compile(Sections("solo { steps { jump.apart:1 stand:3 | } }", "solo { arms { side:4 | } }")).Clip;
        double Apart(double beat) => jumped.Figures[0].Joint(jumped.FrameAt(beat), Rig.LAnkle).X - jumped.Figures[0].Joint(jumped.FrameAt(beat), Rig.RAnkle).X;
        Assert.True(Apart(0) > 0.5 && Apart(1) < 0.3, $"{Apart(0)} then {Apart(1)}");

        // In a loop what is before the start is the end: the upbeat's word is there in the last beat.
        var loop = Compile(Sections("upbeat 1\n solo { arms { up:1!there | side:4 | } }", "solo { arms { fwd:4 | } }", score: "loop"));
        Assert.Empty(loop.Diagnostics);
        Assert.Equal(7, Assert.Single(loop.Clip.Marks).Beat);
        var last = loop.Clip.Figures[0];
        Assert.True((last.Joint(loop.Clip.FrameAt(7), Rig.LHand) - last.Joint(loop.Clip.FrameAt(7), Rig.LShoulder)).Normalized().Y > 0.99, "up in the last beat of the loop");

        // A bar of another length is still a miscount, and an upbeat is shorter than a bar and stands first.
        Assert.Contains(Compile(Sections("solo { arms { side:4 | } }", "upbeat 1\n solo { arms { up:2 | side:4 | } }")).Diagnostics, d => d.Code == "SH1002");
        Assert.Contains(Compile(Sections("solo { arms { side:4 | } }", "upbeat 4\n solo { arms { side:4 | } }")).Diagnostics, d => d.Code == "SH1036");
        Assert.Contains(Compile(Sections("solo { arms { side:4 | } }", "solo { arms { side:4 | } }\n upbeat 1")).Diagnostics, d => d.Code == "SH1036");
    }

    [Fact]
    public void Two_dancers_in_the_same_place_are_warned_about_once()
    {
        Assert.Empty(Compile(Two(1, "torso { fwd.up:2 }", "torso { then:2 }")).Diagnostics);

        // A metre apart and both bow to horizontal: the heads meet.
        string text = Two(1, "torso { then fwd }", "torso { then fwd }");
        var diagnostic = Assert.Single(Compile(text).Diagnostics);
        Assert.Equal("SH2007", diagnostic.Code);
        Assert.Equal("fwd", text.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
        Assert.Contains("ann and ben", diagnostic.Message);

        // Found in the 10th writing trial: near enough for the hands, and then both bend their knees into each other's.
        Assert.Empty(Compile(Two(0.7, "steps { then:2 }", "steps { then:2 }")).Diagnostics);
        diagnostic = Assert.Single(Compile(Two(0.7, "steps { then:2 }\n    height { low(40cm):2 }", "steps { then:2 }\n    height { low(40cm):2 }")).Diagnostics);
        Assert.Equal("SH2007", diagnostic.Code);
        Assert.Contains("the legs of ann and ben", diagnostic.Message);

        // Standing in one spot from the start: nothing is moving, so the section is pointed at.
        string same = Two(0.1, "head { then:2 }");
        diagnostic = Assert.Single(Compile(same).Diagnostics);
        Assert.Equal("SH2007", diagnostic.Code);
        Assert.Equal("section", same.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
    }

    [Fact]
    public void A_sharp_event_has_not_moved_at_its_first_instant()
    {
        // It used to start at full speed, so a pose taken a hair after the beat showed the arm
        // already a fifth of the way there.
        // It sets out 0.15 s before its beat: a quarter of a beat at tempo 100.
        var clip = Solve("    arms { side:2 fwd:2@sharp }");
        // (Frames are 1/18 of a beat here: 1.72 is the last one before it sets out, 1.78 the first one after.)
        Vec3 before = At(clip, 0, Rig.LHand), at = At(clip, 1.72, Rig.LHand), justAfter = At(clip, 1.78, Rig.LHand);

        Assert.True((before - at).Length < 1e-3, "it waits until then");
        Assert.True((at - justAfter).Length < 0.05, $"moved {(at - justAfter).Length:0.000} m in the first frame of 0.15 s");
        Assert.True((At(clip, 2, Rig.LHand) - At(clip, 4, Rig.LHand)).Length < 1e-3, "there on its beat");
    }
}
