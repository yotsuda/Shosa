using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

/// <summary>What the redesigned grammar added: definitions, overlapping tracks, clips, dancers (DESIGN.md 12).</summary>
public class DefinitionTests
{
    private const string Fighter = """
        tempo 120
        pose guard { steps { left.fwd.low@small }  arms { fwd.high@bent } }
        move jab  { leftArm { fwd:1@sharp!hit guard:1 } }
        move kick { rightLeg { fwd.low:1@bent fwd:1@sharp fwd.low:1@bent down:1 } }

        """;

    private static ShosaCompilation Clean(string text)
    {
        var compilation = ShosaCompilation.Compile(text);
        Assert.Empty(compilation.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info));
        return compilation;
    }

    private static Vec3 At(MotionClip clip, double beat, int joint, int figure = 0) => clip.Figures[figure].Joint(clip.FrameAt(beat), joint);

    private static List<Seg> Track(ShosaCompilation compilation, string channel, int clip = 0) => compilation.Score.Clips[clip].Dancers[0].Tracks[channel];

    [Fact]
    public void Every_score_is_a_clip_of_its_own_in_the_order_written()
    {
        var compilation = Clean(Fighter + Samples.SoloCast
            + Samples.Movement("Idle", "body { guard:8 }", "loop") + Samples.Movement("Punch", "body { guard:0 jab:2 }"));

        Assert.Equal(new[] { "Idle", "Punch" }, compilation.Clips.Select(c => c.Name).ToArray());
        Assert.Equal(new[] { "Idle", "Punch" }, compilation.Score.Scores.ToArray());
        Assert.Equal(new[] { 8.0, 2.0 }, compilation.Clips.Select(c => c.TotalBeats).ToArray());
        Assert.True(compilation.Clips[0].Loop);
        Assert.False(compilation.Clips[1].Loop);
        Assert.Same(compilation.Clips[0], compilation.Clip);
    }

    [Fact]
    public void A_gltf_file_holds_every_clip_as_an_animation_with_its_marks()
    {
        var compilation = Clean(Fighter + Samples.SoloCast
            + Samples.Movement("Idle", "body { guard:8 }", "loop") + Samples.Movement("Punch", "body { guard:0 jab:2 }")
            + Samples.Movement("Walk", "steps { left.fwd right.fwd }", "loop inplace"));
        using var gltf = System.Text.Json.JsonDocument.Parse(Shosa.Core.Output.Gltf.Render(compilation.Clips, binary: false));
        var animations = gltf.RootElement.GetProperty("animations");

        Assert.Equal(new[] { "Idle", "Punch", "Walk" }, animations.EnumerateArray().Select(a => a.GetProperty("name").GetString()).ToArray());
        Assert.Single(gltf.RootElement.GetProperty("skins").EnumerateArray());   // one skeleton for the three
        Assert.True(animations[0].GetProperty("extras").GetProperty("loop").GetBoolean());
        var mark = Assert.Single(animations[1].GetProperty("extras").GetProperty("marks").EnumerateArray());
        Assert.Equal(("hit", 0.25), (mark.GetProperty("name").GetString(), mark.GetProperty("time").GetDouble()));
        // Two strides in one second (tempo 120), along z.
        Assert.Equal(1.0, animations[2].GetProperty("extras").GetProperty("velocity").GetProperty("solo")[2].GetDouble());
        // Every animation drives the same bones.
        int Channels(int i) => animations[i].GetProperty("channels").GetArrayLength();
        Assert.Equal(Channels(0), Channels(2));
    }

    [Fact]
    public void Scores_with_different_dancers_share_one_gltf_file_by_the_dancers_names()
    {
        // The second score has only ben, who is the SECOND skeleton of the file: his animation
        // must drive his own bones, not the first skeleton's.
        var compilation = Clean("""
            figure a
            figure b
            dancer ann humanoid on a at -1 0
            dancer ben humanoid on b at 1 0
            section P { a { arms { side:2 } }  b { arms { fwd:2 } } }
            form { P }
            score { dancer ann  dancer ben }
            score solo { dancer ben }
            """);
        using var gltf = System.Text.Json.JsonDocument.Parse(Shosa.Core.Output.Gltf.Render(compilation.Clips, binary: false));
        var root = gltf.RootElement;

        var skins = root.GetProperty("skins").EnumerateArray().ToList();
        Assert.Equal(new[] { "ann", "ben" }, skins.Select(s => s.GetProperty("name").GetString()).ToArray());
        var bens = skins[1].GetProperty("joints").EnumerateArray().Select(j => j.GetInt32()).ToHashSet();
        var solo = root.GetProperty("animations")[1];
        Assert.Equal("solo", solo.GetProperty("name").GetString());
        Assert.All(solo.GetProperty("channels").EnumerateArray(), c => Assert.Contains(c.GetProperty("target").GetProperty("node").GetInt32(), bens));
    }

    [Fact]
    public void A_move_written_with_a_length_is_stretched_to_it_and_without_one_keeps_its_own()
    {
        var compilation = Clean(Samples.Solo("body { jab jab:1/2 }", Fighter));
        var arm = Track(compilation, Channels.LeftArm);

        Assert.Equal(new[] { 1.0, 2.0, 2.25, 2.5 }, arm.Select(s => s.T1).ToArray());
        Assert.Equal(2.5, compilation.Clip.TotalBeats);
    }

    [Fact]
    public void A_definition_does_only_the_part_its_track_covers()
    {
        // `guard` inside the jab's leftArm track is the guard's left arm: nothing for the right arm or the feet.
        var compilation = Clean(Samples.Solo("leftArm { guard }", Fighter));
        var tracks = compilation.Ir.Dancers[0].Tracks;

        Assert.Equal(new[] { Channels.LeftArm }, tracks.Keys.ToArray());

        var none = ShosaCompilation.Compile(Samples.Solo("head { guard }", Fighter));
        Assert.Equal("SH1015", Assert.Single(none.Diagnostics).Code);
    }

    [Fact]
    public void Mirror_swaps_the_limb_a_move_uses()
    {
        var compilation = Clean(Samples.Solo("steps { right.fwd hold:3 | left.fwd hold:3 } body { kick | kick@mirror }", Fighter));
        var clip = compilation.Clip;

        Assert.True(At(clip, 2, Rig.RAnkle).Y > 0.5 && At(clip, 2, Rig.LAnkle).Y < 0.1, "the right leg kicks first");
        Assert.True(At(clip, 6, Rig.LAnkle).Y > 0.5 && At(clip, 6, Rig.RAnkle).Y < 0.1, "then the left");
    }

    [Fact]
    public void A_narrower_track_wins_and_the_wider_one_has_the_part_back_afterwards()
    {
        // The arms go up over four beats; the left arm is taken sideways for the middle two.
        var compilation = Clean(Samples.Solo(" arms { fwd:4 }\n leftArm { skip:1 side:2 }"));
        var left = Track(compilation, Channels.LeftArm);

        Assert.Equal(new[] { (0.0, 4.0), (1.0, 3.0), (3.0, 4.0) }, left.Select(s => (s.T0, s.T1)).ToArray());
        var clip = compilation.Clip;
        Vec3 Arm(double beat, int hand, int shoulder) => (At(clip, beat, hand) - At(clip, beat, shoulder)).Normalized();
        Assert.True(Arm(3, Rig.LHand, Rig.LShoulder).X > 0.99, "sideways at the end of its own event");
        Assert.True(Arm(4, Rig.LHand, Rig.LShoulder).Z > 0.99, "and forward with the other arm when the wide event ends");
        Assert.True(Arm(4, Rig.RHand, Rig.RShoulder).Z > 0.99);
    }

    [Fact]
    public void In_an_overlay_the_rows_start_together_and_the_later_row_wins()
    {
        var compilation = Clean(Samples.Solo("body { { wave } & { jab:2 } hold:2 }",
            Fighter + "move wave { rightArm { side.high:2 down:2 } leftArm { side:4 } }\n"));
        var left = Track(compilation, Channels.LeftArm);

        Assert.Equal(6, compilation.Clip.TotalBeats);
        // The jab has the left arm for its two beats; wave's `side:4` has what is left of them.
        Assert.Equal(new[] { (0.0, 1.0), (1.0, 2.0), (2.0, 4.0) }, left.Where(s => s.Kind != SegKinds.Hold).Select(s => (s.T0, s.T1)).ToArray());
    }

    [Fact]
    public void A_zero_length_pose_is_where_the_clip_starts_and_is_refused_anywhere_else()
    {
        var compilation = Clean(Samples.Solo("body { guard:0 hold:2 }", Fighter));
        var clip = compilation.Clip;

        Assert.True((At(clip, 0, Rig.LHand) - At(clip, 2, Rig.LHand)).Length < 1e-3, "in guard from the first frame");
        Assert.True(At(clip, 0, Rig.LHand).Y > At(clip, 0, Rig.LShoulder).Y, "the fists are up");
        Assert.True(At(clip, 0, Rig.LAnkle).Z > At(clip, 0, Rig.RAnkle).Z + 0.25, "the left foot is in front");

        var late = ShosaCompilation.Compile(Samples.Solo("body { hold guard:0 }", Fighter));
        Assert.Equal("SH1009", Assert.Single(late.Diagnostics).Code);
    }

    [Fact]
    public void A_pose_is_a_stance_being_in_it_again_moves_no_foot()
    {
        var clip = Clean(Samples.Solo("body { guard:0 hold guard:2 }", Fighter)).Clip;

        for (int f = 1; f < clip.Figures[0].Frames.Count; f++)
        {
            Assert.Equal(clip.Figures[0].Joint(0, Rig.LAnkle), clip.Figures[0].Joint(f, Rig.LAnkle));
            Assert.Equal(clip.Figures[0].Joint(0, Rig.RAnkle), clip.Figures[0].Joint(f, Rig.RAnkle));
        }
    }

    [Fact]
    public void A_loop_ends_in_the_pose_it_starts_in()
    {
        var compilation = Clean(Samples.Solo(" steps { left.fwd!step right.fwd!step }\n leftArm { back.-70 fwd.-70 }\n rightArm { fwd.-70 back.-70 }",
            "tempo 120\n", "loop inplace"));
        var clip = compilation.Clip;
        var figure = clip.Figures[0];
        int last = figure.Frames.Count - 1;

        for (int joint = 0; joint < Rig.Joints.Length; joint++)
            Assert.True((figure.Joint(0, joint) - figure.Joint(last, joint)).Length < 2e-3, $"joint {Rig.Joints[joint]} does not join");
        // In mid-stride at the join, not standing: the loop follows itself.
        Assert.True(Math.Abs(figure.Joint(0, Rig.LAnkle).Z - figure.Joint(0, Rig.RAnkle).Z) > 0.3);
        // Two strides of 0.5 m in one second were taken out of the movement and are reported instead.
        Assert.Equal(new[] { 0.0, 1.0 }, figure.Velocity);
        Assert.Equal(new[] { 1.0, 2.0 }, clip.Marks.Select(m => m.Beat).ToArray());
    }

    [Fact]
    public void A_row_of_knee_bends_beside_a_pose_takes_the_height_and_leaves_the_stance()
    {
        // The height is a part of its own: `both` does not take the feet from the pose (DESIGN.md 15).
        var compilation = Clean(Samples.Solo(" body { guard:8 }\n steps { { both.low@small both.low } q q q }", Fighter, "loop"));
        var clip = compilation.Clip;

        Assert.Equal(SegKinds.Step, Assert.Single(Track(compilation, Channels.Support)).Kind);
        Assert.All(new[] { 0.0, 1, 4.5, 8 }, beat =>
            Assert.Equal(0.3, At(clip, beat, Rig.LAnkle).Z - At(clip, beat, Rig.RAnkle).Z, 2));   // the guard: left foot a small step ahead
        Assert.True(At(clip, 1, Rig.Pelvis).Y > At(clip, 2, Rig.Pelvis).Y + 0.05, "it bounces");
        Assert.Equal(At(clip, 0, Rig.Pelvis).Y, At(clip, 8, Rig.Pelvis).Y, 3);
    }

    [Fact]
    public void A_height_written_beside_a_walk_keeps_the_steps()
    {
        var compilation = Clean(Samples.Solo(" body { walk:2 }\n steps { both.low:2 }", "move walk { steps { left.fwd right.fwd } }\n", "loop inplace"));
        var plain = Clean(Samples.Solo("steps { left.fwd right.fwd }", score: "loop inplace"));

        Assert.Equal(2, Track(compilation, Channels.Support).Count);
        Assert.True(At(compilation.Clip, 1, Rig.Pelvis).Y < At(plain.Clip, 1, Rig.Pelvis).Y - 0.1, "the same walk, low");
        Assert.Equal(At(plain.Clip, 1, Rig.LAnkle).Z, At(compilation.Clip, 1, Rig.LAnkle).Z, 3);
    }

    [Fact]
    public void A_height_that_loses_part_of_its_time_still_arrives_when_it_said()
    {
        var compilation = Clean(Samples.Solo(" body { guard:8 }\n steps { skip:4 both.mid:2 skip:2 }", Fighter, "loop"));
        var clip = compilation.Clip;

        Assert.True(At(clip, 6, Rig.Pelvis).Y > At(clip, 4, Rig.Pelvis).Y + 0.1, "up for the two beats written");
        Assert.Equal(At(clip, 4, Rig.Pelvis).Y, At(clip, 8, Rig.Pelvis).Y, 3);
    }

    [Fact]
    public void A_loop_that_ends_facing_another_way_is_reported()
    {
        var compilation = ShosaCompilation.Compile(Samples.Solo("steps { left.fwd turn.left.quarter }", score: "loop"));

        Assert.Equal("SH2005", Assert.Single(compilation.Diagnostics).Code);
    }

    [Fact]
    public void A_mark_is_at_the_moment_of_arrival_and_lengths_can_be_seconds_or_frames()
    {
        // tempo 120: 0.5 s is one beat, 15 frames at 30 fps is one beat.
        var compilation = Clean(Samples.Solo("rightArm { fwd:0.5s@sharp!hit down:15f!back }", "tempo 120\n"));

        Assert.Equal(2, compilation.Clip.TotalBeats, 6);
        Assert.Equal(new[] { ("hit", 0.5), ("back", 2.0) }, compilation.Clip.Marks.Select(m => (m.Name, Math.Round(m.Beat, 6))).ToArray());
        Assert.Equal(0.25, compilation.Clip.Marks[0].Seconds, 6);
    }

    [Fact]
    public void One_bar_of_a_repeat_is_changed_by_a_narrower_track_and_the_mark_it_covers_is_gone()
    {
        // The second clap is held back by the narrower row: it does not happen, so it is not marked.
        var clip = Clean(Samples.Solo(" body { vine:4 | % | }\n arms { skip:4 | skip:3 hold:1 | }\n steps { skip:4 | skip:3 jump.together:1!jump | }",
            "move vine { steps { right.side left.back.across right.side together }  arms { down:3 clap:1@sharp!clap } }\n")).Clip;

        Assert.Equal(new[] { ("clap", 3.5), ("jump", 7.85) }, clip.Marks.Select(m => (m.Name, Math.Round(m.Beat, 6))).ToArray());
        Assert.True((At(clip, 8, Rig.LHand) - At(clip, 8, Rig.RHand)).Length > 0.2, "the hands are apart");
    }

    [Fact]
    public void A_number_in_the_place_of_the_level_is_degrees_above_the_horizontal()
    {
        var clip = Clean(Samples.Solo("torso { fwd.60 } rightArm { fwd.-70 }")).Clip;
        Vec3 spine = (At(clip, 1, Rig.Chest) - At(clip, 1, Rig.Pelvis)).Normalized();
        Vec3 arm = (At(clip, 1, Rig.RHand) - At(clip, 1, Rig.RShoulder)).Normalized();

        Assert.Equal(Math.Sin(60 * Math.PI / 180), spine.Y, 2);
        Assert.Equal(Math.Sin(-70 * Math.PI / 180), arm.Y, 2);
    }

    [Fact]
    public void A_step_can_turn_and_lands_where_it_would_after_the_turn()
    {
        var clip = Clean(Samples.Solo("steps { left.fwd@turn(left.quarter) }")).Clip;
        Vec3 left = At(clip, 1, Rig.LAnkle), right = At(clip, 1, Rig.RAnkle), start = At(clip, 0, Rig.RAnkle);

        Assert.True((right - start).Length < 1e-3, "the standing foot swivels where it is");
        // Facing +x now (the figure's old left): the left foot is a stride ahead in x, and to the new left (-z).
        Assert.Equal(Rig.Stride, left.X - right.X, 2);
        Assert.Equal(-Rig.Stance, left.Z - right.Z, 2);
    }

    [Fact]
    public void Only_a_free_body_can_be_lifted_and_it_hangs_at_the_height_written()
    {
        var bound = ShosaCompilation.Compile(Samples.Solo("steps { lift(0.4) }"));
        Assert.Equal("SH1022", Assert.Single(bound.Diagnostics).Code);

        var clip = Clean("figure mage\ndancer mage humanoid on mage free\nsection A { mage { steps { lift(0.4):2 hold land:2 } } }\n"
            + "form { A }\nscore { dancer mage }\n").Clip;
        Assert.Equal(Rig.AnkleHeight + 0.4, At(clip, 2.5, Rig.LAnkle).Y, 3);
        Assert.Equal(Rig.AnkleHeight, At(clip, 5, Rig.LAnkle).Y, 3);
    }

    [Fact]
    public void Shifting_the_weight_moves_the_hips_and_no_foot()
    {
        var clip = Clean(Samples.Solo("steps { shift.left:2 shift.right:2 }")).Clip;

        Assert.True(At(clip, 2, Rig.Pelvis).X > 0.05 && At(clip, 4, Rig.Pelvis).X < -0.05);
        Assert.Equal(At(clip, 0, Rig.LAnkle), At(clip, 4, Rig.LAnkle));
    }

    private const string Pair = """
        figure a
        figure b
        dancer ann humanoid on a at -0.5 0 facing stageLeft
        dancer ben humanoid on b at 0.5 0 facing stageRight
        score { dancer ann  dancer ben }

        """;

    [Fact]
    public void Two_hands_that_reach_for_each_other_meet()
    {
        var clip = Clean(Pair + "section Greet {\n a { rightArm { b.hand:2 } head { b.head } }\n b { rightArm { a.hand:2 } }\n}\nform { Greet }\n").Clip;

        Assert.Equal(new[] { "ann", "ben" }, clip.Figures.Select(f => f.Id).ToArray());
        Assert.True((At(clip, 2, Rig.RHand, 0) - At(clip, 2, Rig.RHand, 1)).Length < 0.01, "the right hands are in the same place");
    }

    [Fact]
    public void Two_hands_meet_above_the_heads_for_a_high_five_and_stay_there_until_they_leave()
    {
        var clip = Clean(Pair.Replace("0.5", "0.35")
            + "section Five {\n a { rightArm { fwd.high@bent b.hand.high:1 down:2 } }\n b { rightArm { fwd.high@bent a.hand.high:1 down:2 } }\n}\nform { Five }\n").Clip;

        var hand = At(clip, 2, Rig.RHand, 0);
        Assert.True((hand - At(clip, 2, Rig.RHand, 1)).Length < 0.01, "the right hands are in the same place");
        Assert.True(hand.Y > At(clip, 2, Rig.Head, 0).Y, "above the head");
    }

    [Fact]
    public void A_small_knee_bend_is_less_than_a_knee_bend_and_a_large_one_more()
    {
        var clip = Clean(Samples.Solo("steps { both.low@small both.low both.low@large }")).Clip;

        double Drop(double beat) => At(clip, 0, Rig.Pelvis).Y - At(clip, beat, Rig.Pelvis).Y;
        Assert.True(Drop(1) > 0.1 && Drop(1) < Drop(2) - 0.05 && Drop(2) < Drop(3) - 0.05, $"{Drop(1)} {Drop(2)} {Drop(3)}");
    }

    [Fact]
    public void Arms_held_out_through_a_turning_jump_are_not_too_fast()
    {
        Clean(Samples.Solo("steps { hold jump@turn(left.half) } arms { side.high hold }", "tempo 120\n"));
    }

    [Fact]
    public void A_mirrored_dancer_does_the_same_with_the_other_side_and_a_delayed_one_does_it_later()
    {
        var clip = Clean("""
            figure f
            dancer one humanoid on f at -1 0
            dancer two humanoid on f at 1 0 mirror
            dancer three humanoid on f at 3 0 delay 2
            section A { f { leftArm { side:2 } steps { left.side hold } } }
            form { A }
            score { dancer one  dancer two  dancer three }

            """).Clip;

        Assert.Equal(4, clip.TotalBeats);
        Assert.True(At(clip, 2, Rig.LHand, 0).Y > 1.2 && At(clip, 2, Rig.RHand, 0).Y < 1.0, "one: the left arm");
        Assert.True(At(clip, 2, Rig.RHand, 1).Y > 1.2 && At(clip, 2, Rig.LHand, 1).Y < 1.0, "two: the right arm");
        Assert.True(At(clip, 1, Rig.Pelvis, 1).X < 1 && At(clip, 1, Rig.Pelvis, 0).X > -1, "and they step apart");
        Assert.True(At(clip, 2, Rig.LHand, 2).Y < 1.0 && At(clip, 4, Rig.LHand, 2).Y > 1.2, "three: two beats later");
    }

    [Fact]
    public void A_score_picks_a_form_a_tempo_and_a_cast()
    {
        const string text = """
            tempo 100
            figure f
            dancer one humanoid on f
            dancer two humanoid on f at 2 0
            section A { f { arms { side:4 } } }
            section B { f { arms { down:2 } } }
            form { A B }
            form short { B }
            score { dancer one  dancer two }
            score practice { form short  tempo 50  dancer two }

            """;
        var whole = Clean(text);
        var practice = ShosaCompilation.Compile(text, "practice");

        Assert.Equal(new[] { "", "practice" }, whole.Score.Scores.ToArray());
        Assert.Equal(new[] { "", "practice" }, whole.Clips.Select(c => c.Name).ToArray());
        Assert.Equal((6.0, 100.0, 2), (whole.Clip.TotalBeats, whole.Clip.Tempo, whole.Clip.Figures.Count));
        Assert.Equal((2.0, 50.0, 1), (practice.Clip.TotalBeats, practice.Clip.Tempo, practice.Clip.Figures.Count));
        Assert.Equal("practice", Assert.Single(practice.Clips).Name);
        // A dancer the score does not list is not there.
        Assert.Equal("two", Assert.Single(practice.Clip.Figures).Id);
        // Asked for by name, the unnamed score is the one written without a name.
        Assert.Equal(6.0, Assert.Single(ShosaCompilation.Compile(text, "").Clips).TotalBeats);
    }

    [Fact]
    public void A_figure_nobody_dances_in_a_score_is_not_there_to_reach_for()
    {
        // No body stands in for it: ben is in the file and not in the score.
        string text = Pair.Replace("  dancer ben }", " }") + "section Greet {\n a { rightArm { b.hand:2 } }\n b { rightArm { a.hand:2 } }\n}\nform { Greet }\n";
        var compilation = ShosaCompilation.Compile(text);

        Assert.Equal("ann", Assert.Single(compilation.Clip.Figures).Id);
        var diagnostic = Assert.Single(compilation.Diagnostics);
        Assert.Equal("SH1021", diagnostic.Code);
        Assert.Equal("b", text.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
    }

    [Fact]
    public void A_file_without_a_score_is_told_at_its_end_what_to_add()
    {
        string text = Samples.Walk.Replace("score { dancer solo }", "");
        var diagnostic = Assert.Single(ShosaCompilation.Compile(text).Diagnostics);

        Assert.Equal(("SH1030", text.Length, 0), (diagnostic.Code, diagnostic.Span.Start, diagnostic.Span.Length));
        Assert.Contains("score { dancer solo }", diagnostic.Message);
    }

    [Theory]
    [InlineData("score { dancer solo }", "score { }", "SH1031", "score")]                       // a score lists who dances
    [InlineData("form { Walk }", "form Walk { Walk }", "SH1032", "score")]                       // the unnamed score plays the unnamed form
    [InlineData("score { dancer solo }", "score { dancer sola }", "SH1005", "sola")]
    [InlineData("score { dancer solo }", "score { dancer solo  form Wlak }", "SH1005", "form Wlak")]
    [InlineData("humanoid on solo", "humanoid", "SH1003", "solo")]                               // a dancer says which figure it dances
    [InlineData("section Walk {", "section Walk loop {", "SH1024", "loop")]                      // loop is said in the score
    public void A_file_that_leaves_a_part_of_its_structure_out_is_told_which(string whole, string broken, string code, string at)
    {
        string text = Samples.Walk.Replace(whole, broken);
        var diagnostic = Assert.Single(ShosaCompilation.Compile(text).Diagnostics);

        Assert.Equal(code, diagnostic.Code);
        Assert.Equal(at, text.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
    }

    [Theory]
    [InlineData("section A { solo { body { fwd } } }", "SH1019")]                       // body takes definitions only
    [InlineData("pose p { arms { fwd up } }\nsection A { solo { arms { p } } }", "SH1023")]   // a pose is one target per track
    [InlineData("move m { arms { m } }\nsection A { solo { arms { m } } }", "SH1017")]   // a move inside itself
    [InlineData("pose fwd { arms { up } }\nsection A { solo { arms { hold } } }", "SH1005")]   // a word of the language
    [InlineData("section A { steps { hold } }", "SH1003")]                              // tracks go in a figure's block
    [InlineData("section A wobble { solo { steps { hold } } }", "SH1024")]
    [InlineData("section A { solo { arms { fwd.95 } } }", "SH1013")]
    public void What_does_not_make_sense_is_said_with_its_code(string text, string code)
    {
        // The rest of the file is whole, so the one thing wrong is the one thing said.
        var compilation = ShosaCompilation.Compile(Samples.SoloCast + text + "\nform { A }\nscore { dancer solo }\n");

        Assert.Equal(code, Assert.Single(compilation.Diagnostics).Code);
    }
}
