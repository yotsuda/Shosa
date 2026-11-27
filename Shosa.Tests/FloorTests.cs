using Shosa.Core;
using Shosa.Core.Motion;

namespace Shosa.Tests;

/// <summary>Off the feet (DESIGN.md 128): `steps { hands }`, `{ head }`, `{ back }`, `{ seat }`, `{ knees }`, `{ feet }`, and the torso tipped as its row says.</summary>
public class FloorTests
{
    private const string Head = "tempo 120\ntime 4/4\n";
    private static ShosaCompilation Compile(string tracks) => ShosaCompilation.Compile(Samples.Solo(tracks, Head));

    private static Vec3 At(ShosaCompilation compilation, double beat, int joint) =>
        compilation.Clip.Figures[0].Joint(compilation.Clip.FrameAt(beat), joint);

    [Fact]
    public void A_handstand_is_the_body_upside_down_over_hands_put_ahead_of_the_feet()
    {
        var compilation = Compile("    steps { stand:2 then:2 hands.fwd(0.6m):4 then:2 feet.back(0.6m):4 }\n    torso { up:2 fwd.down(30):2 down:4 then:2 fwd.down(30):1 up:3 }");
        Assert.Empty(compilation.Diagnostics);
        Vec3 left = At(compilation, 6, Rig.LHand), right = At(compilation, 6, Rig.RHand), feet = (At(compilation, 0, Rig.LAnkle) + At(compilation, 0, Rig.RAnkle)) * 0.5;
        // The hands on the floor, a shoulder's width apart, 0.6 m ahead of where the feet stood.
        Assert.Equal(0.02, left.Y, 2);
        Assert.Equal(2 * Rig.ShoulderHalf, left.X - right.X, 2);
        Assert.Equal(0.6, (left.Z + right.Z) / 2 - feet.Z, 2);
        // The shoulders over them, the hips over the shoulders, the feet over the hips: head down.
        Assert.True(At(compilation, 6, Rig.LAnkle).Y > At(compilation, 6, Rig.Pelvis).Y + 0.8);
        Assert.True(At(compilation, 6, Rig.Pelvis).Y > At(compilation, 6, Rig.Chest).Y + 0.45);
        Assert.True(At(compilation, 6, Rig.Head).Y < At(compilation, 6, Rig.Chest).Y);
        Assert.InRange(Math.Abs(At(compilation, 6, Rig.Pelvis).Z - left.Z), 0, 0.02);
        // And back on the feet where they stood, standing as before.
        Assert.Equal(0, (At(compilation, 13, Rig.LAnkle) - At(compilation, 0, Rig.LAnkle)).Length, 2);
        Assert.Equal(At(compilation, 0, Rig.Pelvis).Y, At(compilation, 13, Rig.Pelvis).Y, 2);
        // The hands are on the floor before the body is over them, and the feet until it has set off.
        Assert.Equal(0.02, At(compilation, 3.4, Rig.LHand).Y, 2);
        Assert.Equal(Rig.AnkleHeight, At(compilation, 2.4, Rig.LAnkle).Y, 2);
    }

    [Fact]
    public void Kneeling_and_sitting_stand_on_level_hips_where_the_knees_and_the_seat_are()
    {
        // (A word is there on its beat: knees at 2, sat back at 6, on the right knee at 10.)
        var kneel = Compile("    steps { stand:2 knees:2 then:2 then:2 then:2 rightKnee:2 then:2 feet:2 }\n    height { mid:4 then:2 low(25cm):2 mid:8 }");
        Assert.Empty(kneel.Diagnostics);
        Assert.Equal(0.05, At(kneel, 3, Rig.LKnee).Y, 2);
        Assert.Equal(0.05 + Rig.Thigh, At(kneel, 3, Rig.Pelvis).Y, 1);
        Assert.True(At(kneel, 3, Rig.LAnkle).Z < At(kneel, 3, Rig.LKnee).Z - 0.4, "the shins on the floor behind");
        Assert.True(At(kneel, 6, Rig.Pelvis).Y < At(kneel, 3, Rig.Pelvis).Y - 0.15, "sat back toward the heels");
        // One knee: the other foot flat in front.
        Assert.Equal(0.05, At(kneel, 11, Rig.RKnee).Y, 2);
        Assert.Equal(Rig.AnkleHeight, At(kneel, 11, Rig.LAnkle).Y, 2);
        Assert.True(At(kneel, 11, Rig.LAnkle).Z > At(kneel, 11, Rig.RKnee).Z + 0.25);

        var sit = Compile("    steps { stand:2 seat:4 then:4 }\n    torso { up:4 then:2 then:3 back:1 }\n    legs { down:4 then:2 then:3 fwd.floor.bent(90):1 }");
        Assert.Empty(sit.Diagnostics);
        Assert.Equal(0.10, At(sit, 4, Rig.Pelvis).Y, 2);
        Assert.True(At(sit, 4, Rig.Chest).Y > 0.55 && At(sit, 4, Rig.LAnkle).Y < 0.07 && At(sit, 4, Rig.LAnkle).Z > At(sit, 4, Rig.Pelvis).Z + 0.8, "upright, the legs out in front");
        Assert.True(At(sit, 9.9, Rig.Chest).Y < 0.15 && At(sit, 9.9, Rig.LKnee).Y > 0.3, "lying back, the knees up");
    }

    [Fact]
    public void On_the_head_the_body_turns_about_it_and_nothing_is_under_the_floor()
    {
        var compilation = Compile("    steps { stand:2 head.fwd(0.4m):2 then:2 turn.left(360):2 then:2 }\n    torso { up:1 fwd.down(40):1 down:8 }\n    arms { down:2 side.floor:8 }\n    legs { down:4 side.up(40):6 }");
        Assert.DoesNotContain(compilation.Diagnostics, d => d.Severity == Shosa.Core.Syntax.DiagnosticSeverity.Error);
        var head = At(compilation, 5, Rig.Head);
        Assert.Equal(0.11, head.Y, 2);
        Assert.Equal(0.02, At(compilation, 5, Rig.LHand).Y, 2);
        // Half-way round, the feet have changed sides and the head has stayed.
        Assert.Equal(0, (At(compilation, 6.5, Rig.Head) - head).Length, 2);
        Assert.True(At(compilation, 5, Rig.LAnkle).X * At(compilation, 6.5, Rig.LAnkle).X < -0.09, "half-way round at beat 5, round at 6");
        var figure = compilation.Clip.Figures[0];
        for (int f = 0; f < figure.Frames.Count; f++)
            for (int j = 0; j < Rig.Joints.Length; j++)
                Assert.True(figure.Joint(f, j).Y > -0.03, $"{Rig.Joints[j]} is {figure.Joint(f, j).Y:0.00} at frame {f}");
    }

    [Fact]
    public void A_hand_goes_to_the_floor_and_the_chest_faces_where_facing_says()
    {
        var crouch = Compile("    steps { seat:4 }\n    arms { side.floor:4 }");
        Assert.Empty(crouch.Diagnostics);
        Assert.Equal(0.02, At(crouch, 2, Rig.LHand).Y, 2);
        Assert.True(At(crouch, 2, Rig.LHand).X > At(crouch, 2, Rig.LShoulder).X + 0.1);
        // Lying on the back, and on the front: the nose up, the nose down.
        var back = Compile("    steps { seat:1 back:4 }\n    torso { up:1 back:4 }");
        Assert.True(At(back, 3, Rig.Nose).Y > At(back, 3, Rig.Head).Y + 0.08 && At(back, 3, Rig.Chest).Y < 0.13);
        var turned = Compile("    steps { seat:1 back:4 }\n    torso { up:1 back.facing(down):4 }");
        Assert.True(At(turned, 4.9, Rig.LShoulder).X < At(turned, 4.9, Rig.RShoulder).X, "turned over: the left shoulder on the right");
    }

    [Theory]
    [InlineData("steps { stand:1 hands:2 leftFoot.fwd:1 }", "SH2025")]
    [InlineData("steps { stand:1 seat:2 jump:1 }", "SH2025")]
    [InlineData("steps { stand:1 hands.up(1m):2 }", "SH1012")]
    [InlineData("steps { stand:4 }\n    torso { up.facing:4 }", "SH1013")]
    [InlineData("steps { stand:4 }\n    head { fwd.facing(up):4 }", "SH1010")]
    public void What_the_floor_does_not_take_is_said(string rows, string code)
    {
        Assert.Contains(Compile("    " + rows).Diagnostics, d => d.Code == code);
    }
}
