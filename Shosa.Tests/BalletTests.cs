using Shosa.Core;
using Shosa.Core.Motion;

namespace Shosa.Tests;

/// <summary>
/// What ballet could not say before DESIGN.md 127: a jump that lands in a stance, beats, a pointed and a flexed foot,
/// pointe, a toe at the shin and the thigh, a toe resting on the floor with the knee bent, a hand on the leg, and a
/// pose's stance that stays on its spot.
/// </summary>
public class BalletTests
{
    private const string Head = "tempo 120\ntime 4/4\n";
    private static ShosaCompilation Compile(string tracks) => ShosaCompilation.Compile(Samples.Solo(tracks, Head));

    private static Vec3 At(ShosaCompilation compilation, double beat, int joint) =>
        compilation.Clip.Figures[0].Joint(compilation.Clip.FrameAt(beat), joint);

    [Fact]
    public void A_poses_stance_and_its_mirror_in_turn_stay_on_one_spot()
    {
        string source = Head + """
            role solo
            dancer solo humanoid on solo
            pose fifth { turnout { out(80) }  steps { rightFoot.fwd(0.08m).across(0.13m) }  height { straight } }
            section Main { solo { body { fifth:2 fifth:2@mirror fifth:2 fifth:2@mirror fifth:2 fifth:2@mirror fifth:4 } } }
            form { Main }
            score { dancer solo }
            """;
        var compilation = ShosaCompilation.Compile(source);
        Assert.Empty(compilation.Diagnostics);
        double Middle(double beat) => (At(compilation, beat, Rig.LAnkle).Z + At(compilation, beat, Rig.RAnkle).Z) / 2;
        // (Each named foot stepping ahead of the other, the dancer walked 0.16 m forward every pair.)
        foreach (double beat in new[] { 2.0, 4, 6, 8, 10, 12, 15 })
            Assert.InRange(Middle(beat) - Middle(0), -0.001, 0.081);
        Assert.Equal(Middle(0), Middle(15), 3);
        // And each is the stance it says: the right foot in front, then the left.
        Assert.True(At(compilation, 15, Rig.RAnkle).Z > At(compilation, 15, Rig.LAnkle).Z + 0.07);
        Assert.True(At(compilation, 10, Rig.LAnkle).Z > At(compilation, 10, Rig.RAnkle).Z + 0.07);
    }

    [Fact]
    public void A_jump_lands_in_the_stance_a_foot_after_it_says()
    {
        var compilation = Compile("    turnout { out(80):8 }\n    height { straight:8 }\n    steps { together:1 jump.apart(0.5m).up(8cm):1 then:1 jump.rightFoot.fwd(0.08m).across(0.13m).up(8cm):1 then:4 }");
        Assert.Empty(compilation.Diagnostics);
        Vec3 left = At(compilation, 6, Rig.LAnkle), right = At(compilation, 6, Rig.RAnkle), middle = (At(compilation, 2, Rig.LAnkle) + At(compilation, 2, Rig.RAnkle)) * 0.5;
        Assert.Equal(0.5, At(compilation, 2, Rig.LAnkle).X - At(compilation, 2, Rig.RAnkle).X, 2);   // the second it closes from
        Assert.Equal(0.08, right.Z - left.Z, 3);          // the right foot in front
        Assert.Equal(0.13, right.X - left.X, 3);          // crossed: on the left of the left foot
        Assert.Equal(0, ((left + right) * 0.5 - middle).Length, 2);   // where it took off
        // As a step's words: how high may be written after them, and the travel before the foot.
        Assert.Empty(Compile("    steps { together:1 jump.fwd(0.3m).leftFoot.fwd(0.08m).across(0.13m).up(8cm):1 then:2 }").Diagnostics);
        Assert.Equal("SH1012", Assert.Single(Compile("    steps { together:1 jump.change.rightFoot.fwd(0.08m):1 then:2 }").Diagnostics).Code);
        Assert.Equal("SH1012", Assert.Single(Compile("    steps { together:1 shift.left:1 hop.rightFoot.fwd(0.08m):1 then:1 }").Diagnostics).Code);
    }

    [Theory]
    [InlineData("jump.beat.up(12cm)", 2, false)]          // an entrechat quatre: right front, left, right
    [InlineData("jump.change.beat.up(12cm)", 1, true)]     // a royale: right front, right again (the beat), left
    [InlineData("jump.change.beat(2).up(18cm)", 3, true)]  // an entrechat six: right front, left, right, left
    public void A_beat_closes_the_legs_the_other_way_round_in_the_air(string jump, int passes, bool changes)
    {
        var compilation = Compile($"    turnout {{ out(80):8 }}\n    height {{ straight:8 }}\n    steps {{ rightFoot.fwd(0.08m).across(0.13m):2 {jump}:2 then:4 }}");
        Assert.Empty(compilation.Diagnostics);
        var figure = compilation.Clip.Figures[0];
        double Ahead(int frame) => figure.Joint(frame, Rig.RAnkle).Z - figure.Joint(frame, Rig.LAnkle).Z;   // how far the right foot is in front
        // The frames in the air, and how often the foot in front changes through them and the landing.
        var air = Enumerable.Range(0, figure.Frames.Count).Where(f => figure.Joint(f, Rig.LAnkle).Y > Rig.AnkleHeight + 0.03 && figure.Joint(f, Rig.RAnkle).Y > Rig.AnkleHeight + 0.03).ToList();
        Assert.NotEmpty(air);
        int crossings = 0;
        double before = Ahead(air[0] - 1);
        for (int f = air[0]; f <= air[^1] + 2; f++)
        {
            double now = Ahead(f);
            if (Math.Abs(now) > 0.02 && Math.Sign(now) != Math.Sign(before))
            {
                crossings++;
                before = now;
            }
        }
        // It lands as it says (as it stood, or changed), the feet having passed each other so many times on the way.
        Assert.Equal(changes ? -0.08 : 0.08, Ahead(compilation.Clip.FrameOfBeat(3)), 3);
        Assert.Equal(passes, crossings);
        // At a beat the feet are as far apart front to back as they stand, the other way round from the landing (the last one).
        Assert.Contains(air, f => Math.Abs(Ahead(f) + (changes ? -0.08 : 0.08)) < 0.012);
    }

    [Fact]
    public void A_foot_in_the_air_is_pointed_or_flexed_as_its_word_says()
    {
        var compilation = Compile("    steps { stand:8 }\n    rightLeg { down:1 fwd.down(45):1 fwd.down(45).pointed:1 fwd.down(45).flexed:1 down:4 }");
        Assert.Empty(compilation.Diagnostics);
        // The angle between the shin, on from the ankle, and the foot: 0 pointed along it.
        double Ankle(double beat)
        {
            var shin = (At(compilation, beat, Rig.RAnkle) - At(compilation, beat, Rig.RKnee)).Normalized();
            var foot = (At(compilation, beat, Rig.RToe) - At(compilation, beat, Rig.RAnkle)).Normalized();
            return Math.Acos(Math.Clamp(shin.Dot(foot), -1, 1)) * 180 / Math.PI;
        }
        Assert.InRange(Ankle(1), 15, 80);     // as it stood: level, the leg sloping down to it
        Assert.InRange(Ankle(2), 0, 3);       // pointed
        Assert.InRange(Ankle(3), 100, 110);   // flexed: 15 degrees past square
        Assert.Equal("SH1010", Assert.Single(Compile("    arms { fwd.pointed:4 }").Diagnostics).Code);
        Assert.Equal("SH1012", Assert.Single(Compile("    rightLeg { down:1 fwd.pointed.flexed:3 }").Diagnostics).Code);
        Assert.Equal("SH1010", Assert.Single(Compile("    rightLeg { down:1 otherKnee.pointed:3 }").Diagnostics).Code);
    }

    [Fact]
    public void On_pointe_the_feet_are_upright_and_the_hips_higher_than_on_the_toes()
    {
        var compilation = Compile("    steps { together:8 }\n    height { straight:2 high:2 pointe:2 straight:2 }");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(Rig.AnkleHeight + Rig.HighRise, At(compilation, 2, Rig.LAnkle).Y, 3);
        Assert.Equal(Rig.AnkleHeight + Rig.PointeRise, At(compilation, 4, Rig.LAnkle).Y, 3);
        Assert.Equal(Rig.PointeRise - Rig.HighRise, At(compilation, 4, Rig.Pelvis).Y - At(compilation, 2, Rig.Pelvis).Y, 3);
        // The toe under the ankle, where on demi-pointe it is a foot's length ahead of it.
        var foot = At(compilation, 4, Rig.LToe) - At(compilation, 4, Rig.LAnkle);
        Assert.InRange(Math.Sqrt(foot.X * foot.X + foot.Z * foot.Z), 0, 0.005);
        var demi = At(compilation, 2, Rig.LToe) - At(compilation, 2, Rig.LAnkle);
        Assert.InRange(Math.Sqrt(demi.X * demi.X + demi.Z * demi.Z), 0.15, 0.18);
        Assert.Equal("SH1012", Assert.Single(Compile("    steps { together:4 }\n    height { pointe(5cm):4 }").Diagnostics).Code);
    }

    [Theory]
    [InlineData("otherShin", 0.5, false)]
    [InlineData("otherShin.back", 0.5, false)]
    [InlineData("otherThigh", 0.5, true)]
    public void A_toe_goes_to_the_shin_and_the_thigh_of_the_other_leg(string place, double share, bool thigh)
    {
        var compilation = Compile($"    steps {{ stand:8 }}\n    rightLeg {{ down:1 {place}:1 then:2 down:4 }}");
        Assert.Empty(compilation.Diagnostics);
        var toe = At(compilation, 2, Rig.RToe);
        var at = thigh ? Vec3.Lerp(At(compilation, 2, Rig.LHip), At(compilation, 2, Rig.LKnee), share) : Vec3.Lerp(At(compilation, 2, Rig.LKnee), At(compilation, 2, Rig.LAnkle), share);
        Assert.InRange((toe - at).Length, 0.03, 0.12);   // at the leg's surface, not in its bone
        Assert.Equal(place.EndsWith(".back"), toe.Z < at.Z);
    }

    [Fact]
    public void A_toe_rests_on_the_floor_with_the_knee_bent()
    {
        // (From a plié: standing straight, a leg bent 60 degrees does not reach the floor, and bends as far as does — the last lines.)
        var compilation = Compile("    steps { stand:8 }\n    height { low(12cm):8 }\n    rightLeg { down:1 back.floor.bent(60):1 then:2 down:4 }");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(0.02, At(compilation, 2, Rig.RToe).Y, 2);
        Assert.InRange(compilation.Clip.Figures[0].Angles(compilation.Clip.FrameAt(2)).RightKnee, 55, 65);
        Assert.True(At(compilation, 2, Rig.RToe).Z < At(compilation, 2, Rig.LAnkle).Z - 0.1, "behind the standing foot");
        Assert.True(At(compilation, 2, Rig.RAnkle).Y > 0.12, "the heel up");
        // Standing on a straight leg the floor is too far for a knee bent 60 degrees: the toe is on it all the same, the knee bent less.
        var straight = Compile("    steps { stand:8 }\n    height { straight:8 }\n    rightLeg { down:1 back.floor.bent(60):1 then:2 down:4 }");
        Assert.Equal(0.02, At(straight, 2, Rig.RToe).Y, 2);
        Assert.InRange(straight.Clip.Figures[0].Angles(straight.Clip.FrameAt(2)).RightKnee, 20, 55);
    }

    [Fact]
    public void A_hand_goes_to_the_thigh_and_the_knee_of_its_own_side()
    {
        var compilation = Compile("    steps { stand:8 }\n    height { low(20cm):8 }\n    torso { up.fwd(40):8 }\n    arms { thigh:2 then:2 knee:4 }");
        Assert.Empty(compilation.Diagnostics);
        foreach (int side in new[] { 1, -1 })
        {
            int hand = side == 1 ? Rig.LHand : Rig.RHand, hip = side == 1 ? Rig.LHip : Rig.RHip;
            Assert.InRange((At(compilation, 1, hand) - Vec3.Lerp(At(compilation, 1, hip), At(compilation, 1, hip + 1), 0.5)).Length, 0.05, 0.12);
            Assert.InRange((At(compilation, 5, hand) - At(compilation, 5, hip + 1)).Length, 0.05, 0.12);
        }
    }
}
