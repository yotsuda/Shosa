using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

/// <summary>Sitting: `height { sit(0.45m) }` (DESIGN.md 121).</summary>
public class SitTests
{
    private static ShosaCompilation Compile(string tracks) => ShosaCompilation.Compile(Samples.Solo(tracks, "tempo 60\ntime 4/4\n"));

    private static Vec3 At(ShosaCompilation compilation, double beat, int joint) =>
        compilation.Clip.Figures[0].Joint(compilation.Clip.FrameAt(beat), joint);

    [Theory]
    [InlineData(0.45)]
    [InlineData(0.30)]
    [InlineData(0.70)]
    public void The_hips_are_on_the_seat_behind_the_feet_which_stay_on_the_floor(double seat)
    {
        var compilation = Compile(FormattableString.Invariant($"    steps {{ stand:8 }}\n    height {{ mid:2 then:2 sit({seat}m):4 }}"));
        Assert.Empty(compilation.Diagnostics);
        Vec3 pelvis = At(compilation, 6, Rig.Pelvis), ankle = At(compilation, 6, Rig.LAnkle), knee = At(compilation, 6, Rig.LKnee), stood = At(compilation, 0, Rig.LAnkle);
        Assert.Equal(seat + Rig.SeatHip, pelvis.Y, 3);
        // Behind the ankles by what the grammar says (Rig.SeatBack), the shins upright, the feet where they stood.
        Assert.Equal(Rig.SeatBack(seat), ankle.Z - pelvis.Z, 3);
        Assert.InRange(knee.Z - ankle.Z, -0.01, 0.01);
        Assert.InRange(knee.Y - (Rig.AnkleHeight + Rig.Shin), -0.01, 0.01);
        Assert.Equal(0, (ankle - stood).Length, 4);
        Assert.Equal(Rig.AnkleHeight, ankle.Y, 4);
        // Standing until the `then` that is the way down, and all the way down on the word's beat.
        Assert.Equal(At(compilation, 0, Rig.Pelvis).Y, At(compilation, 2, Rig.Pelvis).Y, 2);
        Assert.Equal(pelvis.Y, At(compilation, 4, Rig.Pelvis).Y, 4);
    }

    [Fact]
    public void A_chair_is_what_sit_alone_means_and_a_seat_has_its_limits()
    {
        var plain = Compile("    steps { stand:4 }\n    height { sit:4 }");
        Assert.Empty(plain.Diagnostics);
        Assert.Equal(Rig.Chair + Rig.SeatHip, At(plain, 0, Rig.Pelvis).Y, 3);   // a first word is where the body is: seated from the start
        Assert.Equal("SH1013", Assert.Single(Compile("    steps { stand:4 }\n    height { sit(1.2m):4 }").Diagnostics).Code);
        Assert.Equal("SH1013", Assert.Single(Compile("    steps { stand:4 }\n    height { sit(45):4 }").Diagnostics).Code);
    }

    [Fact]
    public void Down_and_up_the_torso_leans_over_the_feet_and_sitting_it_is_as_its_row_says()
    {
        var compilation = Compile("    steps { stand:12 }\n    height { mid:2 then:2 sit(0.45m):4 then:2 mid:2 }\n    torso { up:12 }");
        Assert.Empty(compilation.Diagnostics);
        double Lean(double beat)
        {
            var spine = At(compilation, beat, Rig.Chest) - At(compilation, beat, Rig.Pelvis);
            return Math.Atan2(spine.Z, spine.Y) * 180 / Math.PI;
        }
        Assert.Equal(0, Lean(1), 1);
        Assert.InRange(Lean(3), 20, 30);     // half-way down
        Assert.Equal(0, Lean(6), 1);         // sitting: upright, as written
        Assert.InRange(Lean(9), 20, 30);     // half-way up
        Assert.Equal(0, Lean(11), 1);
        // Up again where it stood.
        Assert.Equal(0, (At(compilation, 11, Rig.Pelvis) - At(compilation, 1, Rig.Pelvis)).Length, 2);
    }

    [Fact]
    public void Sitting_the_hands_rest_on_the_thighs_and_the_arms_do_what_their_rows_say()
    {
        using var life = Solver.Life(true);
        var compilation = Compile("    steps { stand:8 }\n    height { sit(0.45m):8 }\n    rightArm { down:2 fwd:2 down:4 }");
        Assert.Empty(compilation.Diagnostics);
        // The unwritten left hand: over the left thigh, above the seat, and still (the weight does not drift on a seat).
        Vec3 left = At(compilation, 2, Rig.LHand), hip = At(compilation, 2, Rig.LHip), knee = At(compilation, 2, Rig.LKnee);
        Assert.InRange(left.Y, hip.Y + 0.05, hip.Y + 0.12);
        Assert.InRange(left.Z, hip.Z + 0.1, knee.Z - 0.1);
        Assert.Equal(0, (At(compilation, 6, Rig.Pelvis) - At(compilation, 2, Rig.Pelvis)).Length, 3);
        // The right arm: `down` rests as the unwritten one does, `fwd` points level from the shoulder.
        Assert.Equal(left.Y, At(compilation, 0, Rig.RHand).Y, 2);
        Assert.Equal(At(compilation, 2, Rig.RShoulder).Y, At(compilation, 2, Rig.RHand).Y, 2);
        Assert.Equal(left.Y, At(compilation, 4, Rig.RHand).Y, 2);
    }

    [Fact]
    public void A_step_taken_on_the_seat_is_a_warning()
    {
        // `mid:2 sit` goes down through the two beats of `mid`: the step at beat 1 is taken on the way.
        var compilation = Compile("    steps { then:1 leftFoot.fwd:1 rightFoot.fwd:1 stand:5 }\n    height { mid:2 sit(0.45m):6 }");
        var warning = Assert.Single(compilation.Diagnostics, d => d.Code == "SH2024");
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Empty(Compile("    steps { then:1 leftFoot.fwd:1 rightFoot.fwd:1 stand:1 then:4 }\n    height { mid:4 then:2 sit(0.45m):2 }").Diagnostics);
    }

    [Fact]
    public void The_sample_sits_and_gets_up()
    {
        var compilation = Samples.Compile("sit");
        Assert.Empty(compilation.Diagnostics);
        var figure = compilation.Clip.Figures[0];
        Assert.Equal(Rig.Chair + Rig.SeatHip, figure.Joint(compilation.Clip.FrameAt(8), Rig.Pelvis).Y, 3);
        Assert.True(figure.Joint(compilation.Clip.FrameAt(15), Rig.Pelvis).Y > 0.9);
    }
}
