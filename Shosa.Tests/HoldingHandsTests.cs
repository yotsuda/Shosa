using System.Text.Json;
using Shosa.Core;
using Shosa.Core.Motion;

namespace Shosa.Tests;

/// <summary>
/// Hand in hand (DESIGN.md 134): another dancer's hand by its side (`b.rightHand`), and whoever stands next to the
/// dancer (`neighbor.hand`), for a role a whole line dances.
/// </summary>
public class HoldingHandsTests
{
    private static string Pair(string a, string b, string ann = "at -0.4 0", string ben = "at 0.4 0", string top = "") => $$"""
        tempo 120
        role a
        role b
        dancer ann humanoid on a {{ann}}
        dancer ben humanoid on b {{ben}}
        {{top}}
        section S {
          a { {{a}} }
          b { {{b}} }
        }
        form { S }
        score { dancer ann  dancer ben }

        """;

    private static string Line(string rows, double apart = 0.8) => FormattableString.Invariant($$"""
        tempo 120
        role line
        dancer alice humanoid on line at {{-apart}} 0
        dancer bob   humanoid on line at 0 0
        dancer carol humanoid on line at {{apart}} 0
        section S {
          line { {{rows}} }
        }
        form { S }
        score { dancer alice  dancer bob  dancer carol }

        """);

    private static Vec3 At(ShosaCompilation compilation, string dancer, double beat, int joint) =>
        compilation.Clip.Figures.Single(f => f.Name == dancer).Joint(compilation.Clip.FrameAt(beat), joint);

    private static double Apart(ShosaCompilation c, string a, int handA, string b, int handB, double beat) =>
        (At(c, a, beat, handA) - At(c, b, beat, handB)).Length;

    [Fact]
    public void Two_side_by_side_hold_their_inner_hands_and_keep_them_while_they_walk()
    {
        const string walk = "steps { then:4 leftFoot.fwd rightFoot.fwd leftFoot.fwd together }";
        var c = ShosaCompilation.Compile(Pair("leftArm { down:2 b.rightHand.low:2 then:4 } " + walk, "rightArm { down:2 a.leftHand.low:2 then:4 } " + walk));
        Assert.Empty(c.Diagnostics);

        Assert.True(Apart(c, "ann", Rig.LHand, "ben", Rig.RHand, 0) > 0.3, "apart to begin with");
        // Met on the word's beat, beside the hips and between the two; and joined at every moment after, walking.
        var met = At(c, "ann", 2, Rig.LHand);
        Assert.InRange(met.X, -0.02, 0.02);
        Assert.InRange(met.Y, At(c, "ann", 2, Rig.Pelvis).Y - 0.05, At(c, "ann", 2, Rig.LShoulder).Y - 0.35);
        for (double beat = 2; beat <= 8; beat += 0.25)
            Assert.True(Apart(c, "ann", Rig.LHand, "ben", Rig.RHand, beat) < 0.01, $"joined at beat {beat}");
        Assert.True(At(c, "ann", 8, Rig.LHand).Z > met.Z + 1, "and gone forward with them");
    }

    [Fact]
    public void Two_who_face_take_both_hands_straight_across()
    {
        var c = ShosaCompilation.Compile(Pair(
            "leftArm { b.rightHand:2 then:2 } rightArm { b.leftHand:2 then:2 }", "leftArm { a.rightHand:2 then:2 } rightArm { a.leftHand:2 then:2 }",
            "at -0.45 0 facing stageLeft", "at 0.45 0 facing stageRight"));
        Assert.Empty(c.Diagnostics);
        Assert.True(Apart(c, "ann", Rig.LHand, "ben", Rig.RHand, 2) < 0.01);
        Assert.True(Apart(c, "ann", Rig.RHand, "ben", Rig.LHand, 2) < 0.01);
        // Not crossed: the two pairs of hands are a body's width apart.
        Assert.True(Apart(c, "ann", Rig.LHand, "ann", Rig.RHand, 2) > 0.25);
    }

    [Fact]
    public void A_line_of_one_role_takes_its_neighbours_hands_and_lifts_them()
    {
        var c = ShosaCompilation.Compile(Line("arms { down:2 neighbor.hand:2 neighbor.hand.high:2 then:2 down:2 }  torso { up:6 up.fwd(40):2 up:2 }"));
        Assert.Empty(c.Diagnostics);

        foreach (double beat in new[] { 2.0, 4, 5, 6 })
        {
            Assert.True(Apart(c, "alice", Rig.LHand, "bob", Rig.RHand, beat) < 0.01, $"alice and bob at {beat}");
            Assert.True(Apart(c, "bob", Rig.LHand, "carol", Rig.RHand, beat) < 0.01, $"bob and carol at {beat}");
        }
        // Beside the hips first, then above the heads; in the bow (beat 6) the hands are still joined (above).
        Assert.True(At(c, "bob", 2, Rig.LHand).Y < At(c, "bob", 2, Rig.Chest).Y - 0.2);
        Assert.True(At(c, "bob", 4, Rig.LHand).Y > At(c, "bob", 4, Rig.Head).Y);
        // The ends of the line: the free hand where a neighbour's would meet it, as high as the joined ones and out to its side.
        foreach (double beat in new[] { 2.0, 4 })
        {
            Vec3 free = At(c, "alice", beat, Rig.RHand), joined = At(c, "alice", beat, Rig.LHand), shoulder = At(c, "alice", beat, Rig.RShoulder);
            Assert.Equal(joined.Y, free.Y, 2);
            Assert.True(free.X < shoulder.X - 0.1, "out to its own side");
        }
        Assert.True(Apart(c, "alice", Rig.LHand, "bob", Rig.RHand, 8) > 0.3, "let go");
    }

    [Fact]
    public void A_neighbours_hand_and_a_hand_named_by_its_role_are_the_same_hand()
    {
        // Neither says a height: beside the hips, as neighbours' hands meet.
        var c = ShosaCompilation.Compile(Pair("leftArm { neighbor.hand:2 then:2 }", "rightArm { a.leftHand:2 then:2 }"));
        Assert.Empty(c.Diagnostics);
        Assert.True(Apart(c, "ann", Rig.LHand, "ben", Rig.RHand, 2) < 0.01);
        Assert.True(At(c, "ann", 2, Rig.LHand).Y < At(c, "ann", 2, Rig.Chest).Y - 0.2);
    }

    [Fact]
    public void A_hand_that_reaches_alone_goes_to_the_other_hand_where_it_is()
    {
        var c = ShosaCompilation.Compile(Pair("leftArm { neighbor.hand:2 then:2 }", "rightArm { side.down(45):2 then:2 }"));
        Assert.Empty(c.Diagnostics);
        Assert.True(Apart(c, "ann", Rig.LHand, "ben", Rig.RHand, 2) < 0.01);
        // ben's hand is where his own word put it.
        var alone = ShosaCompilation.Compile(Pair("leftArm { down:4 }", "rightArm { side.down(45):2 then:2 }"));
        Assert.True((At(c, "ben", 2, Rig.RHand) - At(alone, "ben", 2, Rig.RHand)).Length < 0.01);
    }

    [Fact]
    public void Mirrored_the_hand_named_is_the_other_one_as_the_arm_is()
    {
        // ann stands on the other side of ben: the move written for the left arm, mirrored, is her right arm to his left hand.
        var c = ShosaCompilation.Compile(Pair("body { take:4@mirror }", "leftArm { a.rightHand.low:2 then:2 }", "at 0.4 0", "at -0.4 0",
            "move take { leftArm { b.rightHand.low:2 then:2 } }"));
        Assert.Empty(c.Diagnostics);
        Assert.True(Apart(c, "ann", Rig.RHand, "ben", Rig.LHand, 2) < 0.01);
        var join = Assert.Single(c.Clip.Figures.Single(f => f.Name == "ann").Join!);
        Assert.Equal(("right", "ben", "left"), (join.Hand, join.With, join.Their));
    }

    [Fact]
    public void Neighbours_too_far_apart_are_told_and_an_end_of_the_line_is_not()
    {
        string text = Line("arms { neighbor.hand:2 then:2 }", 1.5);
        var c = ShosaCompilation.Compile(text);
        var diagnostic = Assert.Single(c.Diagnostics);
        Assert.Equal("SH2006", diagnostic.Code);
        Assert.Equal("neighbor.hand:2", text.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
        Assert.Contains("the neighbour's", diagnostic.Message);

        // Somebody before or behind is not a neighbour: nobody on either side, and nothing said.
        var file = ShosaCompilation.Compile(Line("arms { neighbor.hand:2 then:2 }").Replace("at -0.8 0", "at 0 1").Replace("at 0.8 0", "at 0 -1"));
        Assert.Empty(file.Diagnostics);
        Assert.All(file.Clip.Figures, f => Assert.Null(f.Join));
    }

    [Theory]
    [InlineData("head { neighbor.hand:2 }", "'neighbor' is for an arm")]
    [InlineData("leftArm { neighbor.head:2 }", "'head' is not a place on the neighbour")]
    [InlineData("leftArm { neighbor:2 }", "A place on the neighbour is")]
    [InlineData("leftArm { neighbor.shoulder.high:2 }", "Only an arm reaching for a hand takes a height")]
    [InlineData("leftArm { b.leftHand.up:2 }", "Only an arm reaching for a hand takes a height")]
    [InlineData("leftArm { b.leftFoot:2 }", "'leftFoot' is not a place on a dancer")]
    public void What_is_not_a_place_on_a_neighbour_is_said(string row, string said)
    {
        var diagnostic = Assert.Single(ShosaCompilation.Compile(Pair(row, "rightArm { down:2 }")).Diagnostics);
        Assert.Equal("SH1012", diagnostic.Code);
        Assert.Contains(said, diagnostic.Message);
    }

    [Fact]
    public void A_hand_on_the_neighbours_shoulder()
    {
        var c = ShosaCompilation.Compile(Pair("leftArm { neighbor.shoulder:2 then:2 }", "rightArm { down:4 }", "at -0.3 0", "at 0.3 0"));
        Assert.Empty(c.Diagnostics);
        Assert.True(Apart(c, "ann", Rig.LHand, "ben", Rig.RShoulder, 2) < 0.02, "on the near shoulder");
    }

    [Fact]
    public void The_clip_says_whose_hand_each_hand_is_in_for_the_preview()
    {
        var clip = ShosaCompilation.Compile(Line("arms { down:2 neighbor.hand:2 then:2 down:2 }")).Clip;
        var bob = clip.Figures.Single(f => f.Name == "bob");
        Assert.Equal([("left", "carol", "right"), ("right", "alice", "left")], bob.Join!.Select(j => (j.Hand, j.With, j.Their)).Order());
        var join = bob.Join!.Single(j => j.Hand == "left");
        Assert.Equal(bob.Frames.Count, join.Weight.Count);
        Assert.Equal(0, join.Weight[clip.FrameOfBeat(0)], 3);
        Assert.InRange(join.Weight[clip.FrameOfBeat(1)], 0.2, 0.8);
        Assert.Equal(1, join.Weight[clip.FrameOfBeat(2)], 3);
        Assert.Equal(1, join.Weight[clip.FrameOfBeat(4)], 3);
        Assert.Equal(0, join.Weight[clip.FrameOfBeat(6)], 3);
        // The ends of the line join one hand each.
        Assert.Single(clip.Figures.Single(f => f.Name == "alice").Join!);

        var json = JsonDocument.Parse(JsonSerializer.Serialize(clip, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })).RootElement;
        var first = json.GetProperty("figures")[0].GetProperty("join")[0];
        Assert.Equal(("left", "bob", "right"), (first.GetProperty("hand").GetString(), first.GetProperty("with").GetString(), first.GetProperty("their").GetString()));
        // A handshake is a join too; a hand sent nowhere near anybody is none.
        Assert.NotNull(ShosaCompilation.Compile(Pair("rightArm { b.hand:2 }", "rightArm { a.hand:2 }", "at -0.5 0 facing stageLeft", "at 0.5 0 facing stageRight")).Clip.Figures[0].Join);
        Assert.Null(ShosaCompilation.Compile(Pair("rightArm { b.chest:2 }", "rightArm { down:2 }", "at -0.25 0 facing stageLeft", "at 0.25 0 facing stageRight")).Clip.Figures[0].Join);
    }
}
