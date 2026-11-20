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
        pose guard { steps { leftFoot.fwd(0.3m) }  height { low(8cm) }  arms { fwd.up.bent } }
        move jab  { leftArm { fwd:1@sharp!hit guard:1 } }
        move kick { rightLeg { fwd.down.bent:1 fwd:1@sharp fwd.down.bent:1 down:1 } }

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
            + Samples.Movement("Idle", "body { guard:8 }", "loop") + Samples.Movement("Punch", "body { guard jab:2 }"));

        Assert.Equal(new[] { "Idle", "Punch" }, compilation.Clips.Select(c => c.Name).ToArray());
        Assert.Equal(new[] { "Idle", "Punch" }, compilation.Score.Scores.ToArray());
        // The guard the punch starts in is a word like any other: a beat of standing in it, then the jab's two.
        Assert.Equal(new[] { 8.0, 3.0 }, compilation.Clips.Select(c => c.TotalBeats).ToArray());
        Assert.True(compilation.Clips[0].Loop);
        Assert.False(compilation.Clips[1].Loop);
        Assert.Same(compilation.Clips[0], compilation.Clip);
    }

    [Fact]
    public void A_gltf_file_holds_every_clip_as_an_animation_with_its_marks()
    {
        var compilation = Clean(Fighter + Samples.SoloCast
            + Samples.Movement("Idle", "body { guard:8 }", "loop") + Samples.Movement("Punch", "body { guard jab:2 }")
            + Samples.Movement("Walk", "steps { leftFoot.fwd rightFoot.fwd }", "loop inPlace"));
        using var gltf = System.Text.Json.JsonDocument.Parse(Shosa.Core.Output.Gltf.Render(compilation.Clips, binary: false));
        var animations = gltf.RootElement.GetProperty("animations");

        Assert.Equal(new[] { "Idle", "Punch", "Walk" }, animations.EnumerateArray().Select(a => a.GetProperty("name").GetString()).ToArray());
        Assert.Single(gltf.RootElement.GetProperty("skins").EnumerateArray());   // one skeleton for the three
        Assert.True(animations[0].GetProperty("extras").GetProperty("loop").GetBoolean());
        var mark = Assert.Single(animations[1].GetProperty("extras").GetProperty("marks").EnumerateArray());
        // The fist is there on the beat the jab is written at: beat 1, half a second in at tempo 120.
        Assert.Equal(("hit", 0.5), (mark.GetProperty("name").GetString(), mark.GetProperty("time").GetDouble()));
        // The beat too, the section that wrote it and which play of it this is. FILE.events.json
        // is made of the same object (Gltf.Extras), so the two cannot differ.
        Assert.Equal(("Punch", 1), (mark.GetProperty("section").GetString(), mark.GetProperty("play").GetInt32()));
        Assert.Equal(1.0, mark.GetProperty("beat").GetDouble());
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
            role a
            role b
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

        // The jab is two words of a beat each. As it is written they are at beats 0 and 1; in half a beat, a quarter apart.
        Assert.Equal(new[] { (0.0, 1.0), (1.0, 2.0), (2.0, 2.25), (2.25, 2.5) }, arm.Select(s => (s.At, s.Until)).ToArray());
        Assert.Equal(new[] { 0.0, 1.0, 2.0, 2.25 }, arm.Select(s => s.T1).ToArray());   // each is there on its beat
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
        // The leg is out on the second word of the kick. Each kick has a `then` before it to lift the leg in.
        var compilation = Clean(Samples.Solo("body { then kick then kick@mirror }", Fighter));
        var clip = compilation.Clip;

        Assert.True(At(clip, 2, Rig.RAnkle).Y > 0.5 && At(clip, 2, Rig.LAnkle).Y < 0.1, "the right leg kicks first");
        Assert.True(At(clip, 7, Rig.LAnkle).Y > 0.5 && At(clip, 7, Rig.RAnkle).Y < 0.1, "then the left");
    }

    [Fact]
    public void A_narrower_track_wins_and_the_wider_one_has_the_part_back_afterwards()
    {
        // The arms are forward for four beats; the left arm is taken sideways for the middle two.
        var compilation = Clean(Samples.Solo(" arms { fwd:4 }\n leftArm { skip:1 side:2 }"));
        var left = Track(compilation, Channels.LeftArm);

        // Who has the arm when: the wide word, the narrow one, the wide one again.
        Assert.Equal(new[] { (0.0, 1.0), (1.0, 3.0), (3.0, 4.0) }, left.Select(s => (s.At, s.Until)).ToArray());
        // The narrow word is there on its beat, and getting the arm back is an arrival too: the way is made in the time of what had it.
        Assert.Equal(new[] { (0.0, 0.0), (0.0, 1.0), (1.0, 3.0) }, left.Select(s => (s.T0, s.T1)).ToArray());
        Assert.Equal((0.0, 4.0), (Assert.Single(Track(compilation, Channels.RightArm)).At, Track(compilation, Channels.RightArm)[0].Until));
        var clip = compilation.Clip;
        Vec3 Arm(double beat, int hand, int shoulder) => (At(clip, beat, hand) - At(clip, beat, shoulder)).Normalized();
        Assert.True(Arm(0, Rig.LHand, Rig.LShoulder).Z > 0.99, "forward with the other arm to begin with");
        Assert.True(Arm(1, Rig.LHand, Rig.LShoulder).X > 0.99, "sideways on the beat of the narrow word");
        Assert.True(Arm(3, Rig.LHand, Rig.LShoulder).Z > 0.99, "and forward with the other arm again when the narrow word ends");
        Assert.All(new[] { 0.0, 1, 3, 4 }, beat => Assert.True(Arm(beat, Rig.RHand, Rig.RShoulder).Z > 0.99));
    }

    [Fact]
    public void In_an_overlay_the_rows_start_together_and_the_later_row_wins()
    {
        var compilation = Clean(Samples.Solo("body { { wave } & { jab:2 } then:2 }",
            Fighter + "move wave { rightArm { side.up:2 down:2 } leftArm { side:4 } }\n"));
        var left = Track(compilation, Channels.LeftArm);

        Assert.Equal(6, compilation.Clip.TotalBeats);
        // The jab has the left arm for its two beats; wave's `side:4` has what is left of them.
        Assert.Equal(new[] { (0.0, 1.0), (1.0, 2.0), (2.0, 4.0) }, left.Where(s => s.Kind != SegKinds.Hold).Select(s => (s.At, s.Until)).ToArray());
        // It gets there in the jab's last beat, and is there when the jab lets go.
        Assert.Equal((1.0, 2.0), (left[2].T0, left[2].T1));
    }

    [Fact]
    public void The_first_word_is_the_pose_the_clip_starts_in_and_a_length_of_zero_is_refused_everywhere()
    {
        var compilation = Clean(Samples.Solo("body { guard then:2 }", Fighter));
        var clip = compilation.Clip;

        Assert.Equal(3, clip.TotalBeats);
        Assert.True((At(clip, 0, Rig.LHand) - At(clip, 3, Rig.LHand)).Length < 1e-3, "in guard from the first frame");
        Assert.True(At(clip, 0, Rig.LHand).Y > At(clip, 0, Rig.LShoulder).Y, "the fists are up");
        Assert.True(At(clip, 0, Rig.LAnkle).Z > At(clip, 0, Rig.RAnkle).Z + 0.25, "the left foot is in front");

        // A word takes time: `:0` was the way to write the starting pose, and is nothing now — first, last or in a definition.
        foreach (string row in new[] { "body { guard:0 then:2 }", "body { then guard:0 }", "arms { side:0 fwd }", "steps { then leftFoot.fwd:0 }" })
        {
            var refused = ShosaCompilation.Compile(Samples.Solo(row, Fighter));
            Assert.Equal("SH1009", Assert.Single(refused.Diagnostics).Code);
        }
        var defined = ShosaCompilation.Compile(Samples.Solo("body { ready }", "move ready { arms { side:0 fwd } }\n"));
        Assert.Contains(defined.Diagnostics, d => d.Code == "SH1009");
    }

    [Fact]
    public void A_pose_is_a_stance_being_in_it_again_moves_no_foot()
    {
        var clip = Clean(Samples.Solo("body { guard then guard:2 }", Fighter)).Clip;

        for (int f = 1; f < clip.Figures[0].Frames.Count; f++)
        {
            Assert.Equal(clip.Figures[0].Joint(0, Rig.LAnkle), clip.Figures[0].Joint(f, Rig.LAnkle));
            Assert.Equal(clip.Figures[0].Joint(0, Rig.RAnkle), clip.Figures[0].Joint(f, Rig.RAnkle));
        }
    }

    [Fact]
    public void A_loop_ends_in_the_pose_it_starts_in()
    {
        var compilation = Clean(Samples.Solo(" steps { leftFoot.fwd!step rightFoot.fwd!step }\n leftArm { back.down(70) fwd.down(70) }\n rightArm { fwd.down(70) back.down(70) }",
            "tempo 120\n", "loop inPlace"));
        var clip = compilation.Clip;
        var figure = clip.Figures[0];
        int last = figure.Frames.Count - 1;

        for (int joint = 0; joint < Rig.Joints.Length; joint++)
            Assert.True((figure.Joint(0, joint) - figure.Joint(last, joint)).Length < 2e-3, $"joint {Rig.Joints[joint]} does not join");
        // In mid-stride at the join, not standing: the loop follows itself.
        Assert.True(Math.Abs(figure.Joint(0, Rig.LAnkle).Z - figure.Joint(0, Rig.RAnkle).Z) > 0.3);
        // Two strides of 0.5 m in one second were taken out of the movement and are reported instead.
        Assert.Equal(new[] { 0.0, 1.0 }, figure.Velocity);
        // A foot lands on the beat its step is written at.
        Assert.Equal(new[] { 0.0, 1.0 }, clip.Marks.Select(m => m.Beat).ToArray());
    }

    [Fact]
    public void A_row_of_knee_bends_beside_a_pose_takes_the_height_and_leaves_the_stance()
    {
        // The height is a row of its own: it does not take the feet from the pose (DESIGN.md 15, 77).
        var compilation = Clean(Samples.Solo(" body { guard:8 }\n height { { low(8cm) low } q q q }", Fighter, "loop"));
        var clip = compilation.Clip;

        Assert.Equal(SegKinds.Step, Assert.Single(Track(compilation, Channels.Support)).Kind);
        Assert.All(new[] { 0.0, 1, 4.5, 8 }, beat =>
            Assert.Equal(0.3, At(clip, beat, Rig.LAnkle).Z - At(clip, beat, Rig.RAnkle).Z, 2));   // the guard: left foot a small step ahead
        // A small bend on the even beats and a deep one on the odd ones.
        Assert.True(At(clip, 0, Rig.Pelvis).Y > At(clip, 1, Rig.Pelvis).Y + 0.05 && At(clip, 2, Rig.Pelvis).Y > At(clip, 1, Rig.Pelvis).Y + 0.05, "it bounces");
        Assert.Equal(At(clip, 0, Rig.Pelvis).Y, At(clip, 8, Rig.Pelvis).Y, 3);
    }

    [Fact]
    public void A_height_written_beside_a_walk_keeps_the_steps()
    {
        var compilation = Clean(Samples.Solo(" body { walk:2 }\n height { low:2 }", "move walk { steps { leftFoot.fwd rightFoot.fwd } }\n", "loop inPlace"));
        var plain = Clean(Samples.Solo("steps { leftFoot.fwd rightFoot.fwd }", score: "loop inPlace"));

        Assert.Equal(2, Track(compilation, Channels.Support).Count);
        Assert.True(At(compilation.Clip, 1, Rig.Pelvis).Y < At(plain.Clip, 1, Rig.Pelvis).Y - 0.09, "the same walk, low (16 cm under standing; mid-stride a plain walk is lower too)");
        Assert.Equal(At(plain.Clip, 1, Rig.LAnkle).Z, At(compilation.Clip, 1, Rig.LAnkle).Z, 3);
    }

    [Fact]
    public void A_height_written_inside_a_poses_time_is_there_on_its_beat_and_the_poses_height_is_back_when_it_ends()
    {
        var compilation = Clean(Samples.Solo(" body { guard:8 }\n height { skip:4 high:2 skip:2 }", Fighter, "loop"));
        var clip = compilation.Clip;
        var height = Track(compilation, Channels.Height);

        // Up on beat 4, rising through all of the four beats before it, as a gesture does (a height is not a step:
        // DESIGN.md 78.4); the guard has the height back at 6, and comes down to it through the two beats written.
        Assert.Equal(new[] { (0.0, 4.0, 4.0, 6.0), (4.0, 6.0, 6.0, 8.0) }, height.Select(s => (s.T0, s.T1, s.At, s.Until)).ToArray());
        double y0 = At(clip, 0, Rig.Pelvis).Y, y2 = At(clip, 2, Rig.Pelvis).Y, y4 = At(clip, 4, Rig.Pelvis).Y, y5 = At(clip, 5, Rig.Pelvis).Y;
        Assert.True(y4 > y0 + 0.1, "up on the beat it is written at");
        Assert.True(y2 > y0 + 0.03 && y2 < y4 - 0.03, "on its way up half-way through");
        Assert.True(y5 > y0 + 0.03 && y5 < y4 - 0.03, "on its way down");
        Assert.Equal(y0, At(clip, 6, Rig.Pelvis).Y, 2);
        Assert.Equal(At(clip, 0, Rig.Pelvis).Y, At(clip, 8, Rig.Pelvis).Y, 3);
        // The feet are the guard's all along: it is one word of the feet, and in a loop the way to it is at the end.
        Assert.Equal((0.0, 8.0), (Assert.Single(Track(compilation, Channels.Support)).At, Track(compilation, Channels.Support)[0].Until));
    }

    // ---- A word is written at the beat the body is there (DESIGN.md 50). T0..T1 of a segment is when the part moves.

    private static (double T0, double T1)[] Moves(ShosaCompilation compilation, string channel) =>
        Track(compilation, channel).Select(s => (Math.Round(s.T0, 6), Math.Round(s.T1, 6))).ToArray();

    [Fact]
    public void A_word_arrives_on_its_beat_and_the_way_there_is_in_the_time_of_the_word_before()
    {
        // An arm has all of the word before. A step after a step walks: its foot swings through 80% of the word
        // before, and no more than 0.6 s (1.2 beats at tempo 120; DESIGN.md 90).
        var compilation = Clean(Samples.Solo(" arms { side fwd:4 up }\n steps { leftFoot.fwd rightFoot.fwd:4 leftFoot.fwd }", "tempo 120\n"));
        var clip = compilation.Clip;

        Assert.Equal(new[] { (0.0, 0.0), (0.0, 1.0), (1.0, 5.0) }, Moves(compilation, Channels.LeftArm));
        Assert.Equal(new[] { (0.0, 0.0), (0.2, 1.0), (3.8, 5.0) }, Moves(compilation, Channels.Support));
        Assert.Equal(new[] { (0.0, 1.0), (1.0, 5.0), (5.0, 6.0) }, Track(compilation, Channels.Support).Select(s => (s.At, s.Until)).ToArray());

        Vec3 Arm(double beat) => (At(clip, beat, Rig.LHand) - At(clip, beat, Rig.LShoulder)).Normalized();
        Assert.True(Arm(0).X > 0.99 && Arm(1).Z > 0.99 && Arm(5).Y > 0.99, "each where its word says, on its beat");
        Assert.True(Arm(3).Z > 0.2 && Arm(3).Y > 0.2, "and on the way up through the four beats of the word before");
        // The right foot stands for the first fifth of the beat and is a stride ahead on the next one.
        Assert.Equal(At(clip, 0, Rig.RAnkle), At(clip, 0.15, Rig.RAnkle));
        Assert.Equal(Rig.Stride, At(clip, 1, Rig.RAnkle).Z - At(clip, 1, Rig.LAnkle).Z, 2);
        // The left foot stands through most of the four beats it has to wait, not two.
        Assert.Equal(At(clip, 2, Rig.LAnkle), At(clip, 3.75, Rig.LAnkle));
        Assert.True((At(clip, 4.5, Rig.LAnkle) - At(clip, 4, Rig.LAnkle)).Length > 0.1, "on its way in the last beat");
        Assert.Equal(Rig.Stride, At(clip, 5, Rig.LAnkle).Z - At(clip, 5, Rig.RAnkle).Z, 2);
    }

    [Fact]
    public void A_sharp_word_waits_and_sets_out_at_most_015_seconds_before_its_beat()
    {
        // tempo 120: 0.15 s is 0.3 of a beat. Where the word before is shorter than that, all of it — half of it for a foot.
        var compilation = Clean(Samples.Solo(" arms { side:2 fwd:2@sharp }\n steps { leftFoot.fwd:2 rightFoot.fwd:2@sharp }\n torso { fwd.up:1/4 up@sharp then:11/4 }", "tempo 120\n"));
        var clip = compilation.Clip;

        Assert.Equal(new[] { (0.0, 0.0), (1.7, 2.0) }, Moves(compilation, Channels.LeftArm));
        Assert.Equal(new[] { (0.0, 0.0), (1.7, 2.0) }, Moves(compilation, Channels.Support));
        Assert.Equal(new[] { (0.0, 0.0), (0.0, 0.25) }, Moves(compilation, Channels.Torso).Take(2).ToArray());
        Vec3 Arm(double beat) => (At(clip, beat, Rig.LHand) - At(clip, beat, Rig.LShoulder)).Normalized();
        Assert.True(Arm(1.6).X > 0.999, "it has not moved until then");
        Assert.True(Arm(2).Z > 0.999, "and is there on its beat");
        Assert.Equal(At(clip, 0, Rig.RAnkle), At(clip, 1.6, Rig.RAnkle));
        Assert.Equal(Rig.Stride, At(clip, 2, Rig.RAnkle).Z - At(clip, 2, Rig.LAnkle).Z, 2);

        // The same strike at half the tempo takes the same seconds: fewer beats.
        var slow = Clean(Samples.Solo(" arms { side:2 fwd:2@sharp }", "tempo 60\n"));
        Assert.Equal(new[] { (0.0, 0.0), (1.85, 2.0) }, Moves(slow, Channels.LeftArm));
    }

    [Fact]
    public void A_hold_moves_nothing_on_its_beat_and_the_word_after_it_travels_in_the_holds_time()
    {
        var compilation = Clean(Samples.Solo(" arms { side:2 then:2 fwd }\n steps { leftFoot.side:2 then:2 stand }", "tempo 120\n"));
        var clip = compilation.Clip;

        Assert.Equal(5, clip.TotalBeats);
        Assert.Equal(new[] { (0.0, 0.0), (2.0, 2.0), (2.0, 4.0) }, Moves(compilation, Channels.LeftArm));
        Assert.Equal(new[] { (0.0, 0.0), (2.0, 2.0), (3.0, 4.0) }, Moves(compilation, Channels.Support));
        Assert.Equal(SegKinds.Hold, Track(compilation, Channels.LeftArm)[1].Kind);
        Assert.All(new[] { Rig.LHand, Rig.RHand, Rig.LAnkle, Rig.Pelvis }, joint =>
            Assert.True((At(clip, 0, joint) - At(clip, 2, joint)).Length < 1e-3, $"{Rig.Joints[joint]} moved before the `then`"));
        Vec3 Arm(double beat) => (At(clip, beat, Rig.LHand) - At(clip, beat, Rig.LShoulder)).Normalized();
        Assert.True(Arm(3).X > 0.5 && Arm(3).Z > 0.5, "half-way in the middle of the `then`");
        Assert.True(Arm(4).Z > 0.99, "and there on its beat");
        Assert.Equal(At(clip, 0, Rig.LAnkle), At(clip, 3, Rig.LAnkle));
    }

    [Fact]
    public void The_first_word_of_a_score_is_where_it_starts_and_in_a_loop_the_way_to_it_is_at_the_end()
    {
        const string rows = " arms { side:2 fwd:2 }\n steps { leftFoot.fwd:2 rightFoot.fwd:2 }";
        var once = Clean(Samples.Solo(rows, "tempo 120\n"));
        var loop = Clean(Samples.Solo(rows, "tempo 120\n", "loop inPlace"));

        // Nothing moves to get to the first word: it is a segment of no time at beat 0.
        Assert.Equal(new[] { (0.0, 0.0), (0.0, 2.0) }, Moves(once, Channels.LeftArm));
        Assert.Equal(new[] { (0.0, 0.0), (0.8, 2.0) }, Moves(once, Channels.Support));
        Vec3 Arm(MotionClip clip, double beat) => (At(clip, beat, Rig.LHand) - At(clip, beat, Rig.LShoulder)).Normalized();
        Assert.True(Arm(once.Clip, 0).X > 0.99 && At(once.Clip, 0, Rig.LAnkle).Z > At(once.Clip, 0, Rig.RAnkle).Z + 0.4, "in it at the first frame");

        // In a loop the word is still written at beat 0, and the part arrives there as the loop ends: in the time of the last word.
        Assert.Equal(4, loop.Clip.TotalBeats);
        Assert.Equal(new[] { (0.0, 2.0), (2.0, 4.0) }, Moves(loop, Channels.LeftArm));
        Assert.Equal(new[] { (0.8, 2.0), (2.8, 4.0) }, Moves(loop, Channels.Support));
        var first = Track(loop, Channels.LeftArm).Single(s => s.At == 0);
        Assert.Equal((2.0, 4.0, 2.0), (first.T0, first.T1, first.Until));
        Assert.True(Arm(loop.Clip, 0).X > 0.99 && Arm(loop.Clip, 2).Z > 0.99 && Arm(loop.Clip, 4).X > 0.99, "the end is the start");
    }

    [Fact]
    public void A_mark_is_on_the_beat_of_its_word_and_a_jump_comes_down_on_it()
    {
        var compilation = Clean(Samples.Solo(" steps { then jump!land then }\n arms { down clap!clap then }", "tempo 120\n"));
        var clip = compilation.Clip;

        Assert.Equal(new[] { ("land", 1.0, 0.5), ("clap", 1.0, 0.5) }, clip.Marks.OrderByDescending(m => m.Name).Select(m => (m.Name, m.Beat, m.Seconds)).ToArray());
        Assert.True((At(clip, 1, Rig.LHand) - At(clip, 1, Rig.RHand)).Length < 0.06, "the hands are together on the beat");   // the wrists 5 cm apart, palm to palm (DESIGN.md 95)

        // The jump has four fifths of the beat before to bend and fly in, and a fifth of its own to give in.
        var jump = Track(compilation, Channels.Support).Single(s => s.Kind == SegKinds.Jump);
        Assert.Equal((0.2, 1.2, 1.0, 0.8), (Math.Round(jump.T0, 6), Math.Round(jump.T1, 6), jump.At, Math.Round(jump.Land ?? -1, 6)));
        Assert.True(At(clip, 0.8, Rig.LAnkle).Y > Rig.AnkleHeight + 0.03 && At(clip, 0.8, Rig.RAnkle).Y > Rig.AnkleHeight + 0.03, "in the air shortly before");
        Assert.All(new[] { 1.0, 1.2, 2 }, beat =>
        {
            Assert.Equal(Rig.AnkleHeight, At(clip, beat, Rig.LAnkle).Y, 2);
            Assert.Equal(Rig.AnkleHeight, At(clip, beat, Rig.RAnkle).Y, 2);
        });

        // A long word before it or a long one of its own gives it no more than a second: 0.8 s before, 0.2 s after.
        var slow = Track(Clean(Samples.Solo(" steps { then:8 jump:8 }", "tempo 120\n")), Channels.Support).Single(s => s.Kind == SegKinds.Jump);
        Assert.Equal((6.4, 8.4, 8.0, 0.8), (Math.Round(slow.T0, 6), Math.Round(slow.T1, 6), slow.At, Math.Round(slow.Land ?? -1, 6)));
    }

    [Fact]
    public void A_rows_first_word_after_a_skip_travels_in_the_skip_and_after_a_hold_only_in_the_hold()
    {
        // The skip is what is before the word in its row: an arm has all of it, a foot the end of it.
        var skipped = Clean(Samples.Solo(" rightArm { skip:4 fwd }\n steps { skip:4 leftFoot.fwd }", "tempo 120\n"));
        Assert.Equal(new[] { (0.0, 4.0) }, Moves(skipped, Channels.RightArm));
        Assert.Equal(new[] { (3.0, 4.0) }, Moves(skipped, Channels.Support));
        Vec3 Arm(MotionClip clip, double beat) => (At(clip, beat, Rig.RHand) - At(clip, beat, Rig.RShoulder)).Normalized();
        Assert.True(Arm(skipped.Clip, 0).Y < -0.95 && Arm(skipped.Clip, 2).Z > 0.3 && Arm(skipped.Clip, 4).Z > 0.99, "on the way all through the skip");

        // A `then` after the skip is the word before: nothing sets out before it begins.
        var held = Clean(Samples.Solo(" rightArm { skip:3 then:1 fwd }\n steps { skip:3 then:1 leftFoot.fwd }", "tempo 120\n"));
        Assert.Equal(new[] { (3.0, 3.0), (3.0, 4.0) }, Moves(held, Channels.RightArm));
        Assert.Equal(new[] { (3.0, 3.0), (3.5, 4.0) }, Moves(held, Channels.Support));
        Assert.True((At(held.Clip, 0, Rig.RHand) - At(held.Clip, 3, Rig.RHand)).Length < 1e-3, "still until the `then`");
        Assert.True(Arm(held.Clip, 4).Z > 0.99);
    }

    [Fact]
    public void After_a_move_the_next_word_sets_out_in_the_moves_last_word_not_through_all_of_it()
    {
        // The second kick is the other leg, which the first never wrote: it lifts while the first
        // leg is put down (the move's last word), not while that leg is still kicking.
        var compilation = Clean(Samples.Solo(" body { then kick kick@mirror }",
            "tempo 120\nmove kick { rightLeg { fwd.down.bent fwd@sharp fwd.down.bent down } }\n"));
        Assert.Equal((4.0, 5.0), Moves(compilation, Channels.LeftLeg)[1]);   // [0] is the `then` the row begins with
    }

    [Fact]
    public void A_loop_that_ends_facing_another_way_is_reported()
    {
        var compilation = ShosaCompilation.Compile(Samples.Solo("steps { leftFoot.fwd turn.left(90) }", score: "loop"));

        Assert.Equal("SH2005", Assert.Single(compilation.Diagnostics).Code);
    }

    [Fact]
    public void A_mark_is_at_the_beat_its_word_is_written_at_and_lengths_can_be_seconds_or_frames()
    {
        // tempo 120: 0.5 s is one beat, 15 frames at 30 fps is one beat.
        var compilation = Clean(Samples.Solo("rightArm { down:15f fwd:0.5s@sharp!hit down:15f!back }", "tempo 120\n"));
        var clip = compilation.Clip;

        Assert.Equal(3, clip.TotalBeats, 6);
        Assert.Equal(new[] { ("hit", 1.0), ("back", 2.0) }, clip.Marks.Select(m => (m.Name, Math.Round(m.Beat, 6))).ToArray());
        Assert.Equal(0.5, clip.Marks[0].Seconds, 6);
        // And the hand is there then: straight out in front on the first mark, hanging on the second.
        Vec3 Arm(double beat) => (At(clip, beat, Rig.RHand) - At(clip, beat, Rig.RShoulder)).Normalized();
        Assert.True(Arm(1).Z > 0.99 && Arm(2).Y < -0.95, $"{Arm(1).Z} {Arm(2).Y}");
    }

    [Fact]
    public void One_bar_of_a_repeat_is_changed_by_a_narrower_track_and_the_mark_it_covers_is_gone()
    {
        // The second clap is held back by the narrower row: it does not happen, so it is not marked.
        var clip = Clean(Samples.Solo(" body { vine:4 | % | }\n arms { skip:4 | skip:3 then:1 | }\n steps { skip:4 | skip:3 jump.stand:1!jump | }",
            "move vine { steps { rightFoot.side leftFoot.back.across rightFoot.side stand }  arms { down:3 clap:1@sharp!clap } }\n")).Clip;

        // Each on the beat it is written at: the fourth of the first bar, the fourth of the second.
        Assert.Equal(new[] { ("clap", 3.0), ("jump", 7.0) }, clip.Marks.Select(m => (m.Name, Math.Round(m.Beat, 6))).ToArray());
        Assert.True((At(clip, 3, Rig.LHand) - At(clip, 3, Rig.RHand)).Length < 0.06, "the hands are together on the clap");
        Assert.True((At(clip, 7, Rig.LHand) - At(clip, 7, Rig.RHand)).Length > 0.2, "and apart where the second one would be");
        Assert.True((At(clip, 8, Rig.LHand) - At(clip, 8, Rig.RHand)).Length > 0.2, "the hands are apart");
    }

    [Fact]
    public void A_number_in_the_place_of_the_level_is_degrees_above_the_horizontal()
    {
        var clip = Clean(Samples.Solo("torso { up.fwd(30) } rightArm { fwd.down(70) }")).Clip;
        Vec3 spine = (At(clip, 1, Rig.Chest) - At(clip, 1, Rig.Pelvis)).Normalized();
        Vec3 arm = (At(clip, 1, Rig.RHand) - At(clip, 1, Rig.RShoulder)).Normalized();

        Assert.Equal(Math.Sin(60 * Math.PI / 180), spine.Y, 2);
        Assert.Equal(Math.Sin(-70 * Math.PI / 180), arm.Y, 2);
    }

    [Fact]
    public void A_step_can_turn_and_lands_where_it_would_after_the_turn()
    {
        var clip = Clean(Samples.Solo("steps { leftFoot.fwd@turn(left(90)) }")).Clip;
        Vec3 left = At(clip, 1, Rig.LAnkle), right = At(clip, 1, Rig.RAnkle), start = At(clip, 0, Rig.RAnkle);

        Assert.True((right - start).Length < 1e-3, "the standing foot swivels where it is");
        // Facing +x now (the figure's old left): the left foot is a stride ahead in x, and to the new left (-z).
        Assert.Equal(Rig.Stride, left.X - right.X, 2);
        Assert.Equal(-Rig.Stance, left.Z - right.Z, 2);
    }

    [Fact]
    public void Only_a_free_body_can_be_lifted_and_it_hangs_at_the_height_written()
    {
        var bound = ShosaCompilation.Compile(Samples.Solo("steps { lift(0.4m) }"));
        Assert.Equal("SH1022", Assert.Single(bound.Diagnostics).Code);

        var clip = Clean("role mage\ndancer mage humanoid on mage free\nsection A { mage { steps { lift(0.4m):2 then land:2 } } }\n"
            + "form { A }\nscore { dancer mage }\n").Clip;
        Assert.Equal(Rig.AnkleHeight + 0.4, At(clip, 2.5, Rig.LAnkle).Y, 3);
        Assert.Equal(Rig.AnkleHeight, At(clip, 5, Rig.LAnkle).Y, 3);
    }

    [Fact]
    public void Shifting_the_weight_moves_the_hips_and_no_foot()
    {
        var clip = Clean(Samples.Solo("steps { then shift.left:2 shift.right:2 }")).Clip;

        Assert.True(Math.Abs(At(clip, 0, Rig.Pelvis).X) < 0.01, "between the feet to begin with");
        Assert.True(At(clip, 1, Rig.Pelvis).X > 0.05 && At(clip, 3, Rig.Pelvis).X < -0.05);
        Assert.All(new[] { 1.0, 3, 5 }, beat => Assert.Equal(At(clip, 0, Rig.LAnkle), At(clip, beat, Rig.LAnkle)));
    }

    private const string Pair = """
        role a
        role b
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
            + "section Five {\n a { rightArm { fwd.up.bent b.hand.high then down:2 } }\n b { rightArm { fwd.up.bent a.hand.high then down:2 } }\n}\nform { Five }\n").Clip;

        // They meet on the beat the reach is written at, and the `then` keeps them there for its beat: `down` sets out in it.
        Assert.True((At(clip, 0, Rig.RHand, 0) - At(clip, 0, Rig.RHand, 1)).Length > 0.05, "apart to begin with");
        Assert.All(new[] { 1.0, 2 }, beat =>
        {
            var hand = At(clip, beat, Rig.RHand, 0);
            Assert.True((hand - At(clip, beat, Rig.RHand, 1)).Length < 0.01, "the right hands are in the same place");
            Assert.True(hand.Y > At(clip, beat, Rig.Head, 0).Y, "above the head");
        });
        Assert.True(At(clip, 3, Rig.RHand, 0).Y < 1.0 && At(clip, 3, Rig.RHand, 1).Y < 1.0, "and down on the beat `down` is written at");
    }

    [Fact]
    public void A_small_knee_bend_is_less_than_a_knee_bend_and_a_large_one_more()
    {
        // From standing (the `then` the row starts with), each bend on its own beat.
        var clip = Clean(Samples.Solo("steps { then then then then }\n    height { then low(8cm) low low(40cm) }")).Clip;

        double Drop(double beat) => At(clip, 0, Rig.Pelvis).Y - At(clip, beat, Rig.Pelvis).Y;
        Assert.True(Drop(1) > 0.05 && Drop(1) < Drop(2) - 0.05 && Drop(2) < Drop(3) - 0.15 && Drop(2) < 0.2, $"{Drop(1)} {Drop(2)} {Drop(3)}");
    }

    [Fact]
    public void Arms_held_out_through_a_turning_jump_are_not_too_fast()
    {
        Clean(Samples.Solo("steps { then jump@turn(left(180)) } arms { side.up then }", "tempo 120\n"));
    }

    [Fact]
    public void A_mirrored_dancer_does_the_same_with_the_other_side_and_a_delayed_one_does_it_later()
    {
        var clip = Clean("""
            role f
            dancer one humanoid on f at -1 0
            dancer two humanoid on f at 1 0 mirror
            dancer three humanoid on f at 3 0 delay 2
            section A { f { leftArm { down:2 side:2 } steps { then leftFoot.side then:2 } } }
            form { A }
            score { dancer one  dancer two  dancer three }

            """).Clip;

        Assert.Equal(6, clip.TotalBeats);
        Assert.True(At(clip, 2, Rig.LHand, 0).Y > 1.2 && At(clip, 2, Rig.RHand, 0).Y < 1.0, "one: the left arm");
        Assert.True(At(clip, 2, Rig.RHand, 1).Y > 1.2 && At(clip, 2, Rig.LHand, 1).Y < 1.0, "two: the right arm");
        Assert.True(At(clip, 1, Rig.Pelvis, 1).X < 1 && At(clip, 1, Rig.Pelvis, 0).X > -1, "and they step apart");
        Assert.True(At(clip, 2, Rig.LHand, 2).Y < 1.0 && At(clip, 4, Rig.LHand, 2).Y > 1.2, "three: two beats later");
        Assert.True(Math.Abs(At(clip, 1, Rig.Pelvis, 2).X - 3) < 1e-3 && At(clip, 3, Rig.Pelvis, 2).X > 3.2, "the step too");
    }

    [Fact]
    public void A_score_picks_a_form_a_tempo_and_a_cast()
    {
        const string text = """
            tempo 100
            role f
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
    [InlineData("pose fwd { arms { up } }\nsection A { solo { arms { then } } }", "SH1005")]   // a word of the language
    [InlineData("section A { steps { then } }", "SH1003")]                              // tracks go in a figure's block
    [InlineData("section A wobble { solo { steps { then } } }", "SH1024")]
    [InlineData("section A { solo { arms { fwd.up(95) } } }", "SH1013")]
    public void What_does_not_make_sense_is_said_with_its_code(string text, string code)
    {
        // The rest of the file is whole, so the one thing wrong is the one thing said.
        var compilation = ShosaCompilation.Compile(Samples.SoloCast + text + "\nform { A }\nscore { dancer solo }\n");

        Assert.Equal(code, Assert.Single(compilation.Diagnostics).Code);
    }
}
