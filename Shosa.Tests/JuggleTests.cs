using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Semantics;

namespace Shosa.Tests;

/// <summary>Juggling in siteswap: `dancer alice humanoid juggles balls` and a `juggle` row (DESIGN.md 118).</summary>
public class JuggleTests
{
    /// <summary>Tempo 120 in 4/4: a beat is half a second, and `every 1/2` throws every quarter of a second.</summary>
    private static string Juggle(string row, string more = "", string dancer = "juggles balls") =>
        $"tempo 120\nrole solo\ndancer alice humanoid {dancer} on solo\nsection A {{ solo {{ juggle {{ {row} }} {more} }} }}\nform F {{ A }}\nscore {{ form F  dancer alice }}\n";

    private static ShosaCompilation Compile(string text) => ShosaCompilation.Compile(text, null, null);

    private static IEnumerable<string> Codes(ShosaCompilation c) => c.Diagnostics.Select(d => d.Code);

    [Theory]
    [InlineData("3", 3)]
    [InlineData("531", 3)]
    [InlineData("441", 3)]
    [InlineData("51", 3)]
    [InlineData("4", 4)]
    [InlineData("b97", 9)]
    [InlineData("(4,4)", 4)]
    [InlineData("(4,2x)*", 3)]
    [InlineData("(6x,4)*", 5)]
    [InlineData("[32]1", 3)]
    [InlineData("[54]24", 5)]
    public void A_pattern_is_read_and_its_balls_counted(string text, int balls)
    {
        var (pattern, error) = Siteswap.Parse(text);
        Assert.Null(error);
        Assert.Equal(balls, pattern!.Balls);
    }

    [Theory]
    [InlineData("54", "come down")]        // 4.5 balls: two land together
    [InlineData("543", "come down")]       // 5 and 4 land on the same beat
    [InlineData("(3,4)", "even")]
    [InlineData("3x", "x crosses")]
    [InlineData("[3", "not closed")]
    [InlineData("(4,4", "not closed")]
    [InlineData("3*", "pairs")]
    [InlineData("3?", "not a throw")]
    public void A_pattern_that_cannot_be_juggled_says_why(string text, string why)
    {
        var (pattern, error) = Siteswap.Parse(text);
        Assert.Null(pattern);
        Assert.Contains(why, error);
    }

    [Fact]
    public void The_throws_between_two_patterns_are_found()
    {
        // In the air in a cascade: a ball on each of the next three beats. 51 expects them on 0, 1 and 3: a 4 does it.
        var three = Siteswap.Expects(Siteswap.Parse("3").Pattern!, false).Select(e => e.Beat).ToList();
        var shower = Siteswap.Expects(Siteswap.Parse("51").Pattern!, false).Select(e => e.Beat).ToList();
        Assert.Equal([0, 1, 2], three);
        Assert.Equal([0, 1, 3], shower);
        Assert.Equal("4", Siteswap.Bridge(three, shower, 5));
        Assert.Equal(three, Siteswap.Expects(Siteswap.Parse("531").Pattern!, false).Select(e => e.Beat).ToList());
    }

    [Fact]
    public void A_cascade_is_juggled_and_the_balls_are_in_the_clip()
    {
        var c = Compile(Juggle("every 1/2  3:4 | 531:3 3:1 | 441:3 3:1 |"));
        Assert.Empty(c.Diagnostics);
        var juggle = c.Clip.Figures[0].Juggle!;
        Assert.Equal("balls", juggle.Prop);
        Assert.Equal(3, juggle.Count);
        Assert.Equal(c.Clip.Figures[0].Frames.Count, juggle.Frames.Count);
        Assert.Equal(c.Clip.Figures[0].Frames.Count, juggle.Weight.Count);

        // A 3 at a throw every 0.25 s flies 0.425 s and rises g t² / 8 = 0.22 m over the hands.
        int start = c.Clip.FrameOfBeat(0.5), end = c.Clip.FrameOfBeat(3.5);
        double handY = c.Clip.Figures[0].Joint(start, Rig.RHand).Y;
        double top = Enumerable.Range(start, end - start).SelectMany(f => Enumerable.Range(0, 3).Select(b => juggle.Frames[f][b * 7 + 1])).Max();
        Assert.InRange(top - handY, 0.2, 0.35);
        // Some ball is in the air at every frame of the cascade.
        Assert.All(Enumerable.Range(start, end - start), f => Assert.Contains(Enumerable.Range(0, 3), b => juggle.Frames[f][b * 7 + 3] != juggle.Frames[f][b * 7 + 4]));
        // The 5 of 531 goes higher: a 5 flies 0.925 s, about 1 m.
        int high0 = c.Clip.FrameOfBeat(4), high1 = c.Clip.FrameOfBeat(7);
        double five = Enumerable.Range(high0, high1 - high0).SelectMany(f => Enumerable.Range(0, 3).Select(b => juggle.Frames[f][b * 7 + 1])).Max();
        Assert.InRange(five - handY, 0.9, 1.25);
    }

    [Fact]
    public void While_it_juggles_the_hands_go_round_before_the_belly()
    {
        var c = Compile(Juggle("every 1/2  3:8 |"));
        var figure = c.Clip.Figures[0];
        for (double beat = 1; beat < 7; beat += 0.3)
        {
            int f = c.Clip.FrameOfBeat(beat);
            var chest = figure.Joint(f, Rig.Chest);
            var pelvis = figure.Joint(f, Rig.Pelvis);
            foreach (var (hand, side) in new[] { (Rig.RHand, -1), (Rig.LHand, 1) })
            {
                var at = figure.Joint(f, hand);
                Assert.InRange(at.Z - chest.Z, 0.2, 0.45);
                Assert.InRange(at.Y, pelvis.Y, chest.Y - 0.2);
                Assert.True((at.X - pelvis.X) * side > 0.08, $"beat {beat}: the hand is {(at.X - pelvis.X) * side:0.00} m off the middle, on its own side");
            }
        }
    }

    [Fact]
    public void The_first_throw_is_the_right_hands_and_mirror_gives_it_to_the_left()
    {
        int FirstFrom(string row)
        {
            var juggle = Compile(Juggle(row)).Clip.Figures[0].Juggle!;
            foreach (var frame in juggle.Frames)
                for (int b = 0; b < juggle.Count; b++)
                    if (frame[b * 7 + 3] != frame[b * 7 + 4])
                        return (int)frame[b * 7 + 3];
            return -1;
        }
        Assert.Equal(0, FirstFrom("every 1/2  3:4 |"));
        Assert.Equal(1, FirstFrom("every 1/2  3:4@mirror |"));
    }

    [Fact]
    public void A_change_that_cannot_be_made_says_the_throws_that_make_it()
    {
        var c = Compile(Juggle("every 1/2  3:2 51:2 |"));
        var error = Assert.Single(c.Diagnostics);
        Assert.Equal("SH2019", error.Code);
        Assert.Contains("'51' cannot follow '3'", error.Message);
        Assert.Contains("Throw 4 between them", error.Message);

        // With the 4 between them it can: thrown once, it is a passage, not a pattern of four balls.
        Assert.Empty(Compile(Juggle("every 1/2  3:3/2 4:1/2 51:2 |")).Diagnostics);
        // Thrown twice it is the fountain of four, and the row has three.
        Assert.Contains("4 balls", Assert.Single(Compile(Juggle("every 1/2  3:1 4:1 51:2 |")).Diagnostics).Message);
        // A passage of no pattern (52 is three and a half balls) leads from something: at the start it is an error.
        Assert.Contains("not a pattern on its own", Assert.Single(Compile(Juggle("every 1/2  52:1 3:3 |")).Diagnostics).Message);
        Assert.Contains("SH1041", Codes(Compile(Juggle("every 1/2  3:1 52:2 3:1 |"))));   // thrown twice, 52 is not a pattern
    }

    [Fact]
    public void A_pattern_of_other_balls_is_an_error_and_a_skip_starts_again_from_the_hands()
    {
        var c = Compile(Juggle("every 1/2  3:2 4:2 |"));
        var error = Assert.Single(c.Diagnostics);
        Assert.Equal("SH2019", error.Code);
        Assert.Contains("4 balls", error.Message);

        // A plain pattern and pairs do not follow each other directly; after a skip, from the hands, they do.
        Assert.Contains("SH2019", Codes(Compile(Juggle("every 1/2  3:2 (4,2x)*:2 |"))));
        Assert.Empty(Compile(Juggle("every 1/2  3:1 skip:1 (4,2x)*:2 |")).Diagnostics);
    }

    [Fact]
    public void What_a_juggle_row_needs_is_said()
    {
        Assert.Contains("SH1042", Codes(Compile(Juggle("3:4 |"))));                                     // no every
        Assert.Contains("SH1041", Codes(Compile(Juggle("every 1/2  54:4 |"))));                         // cannot be juggled
        Assert.Contains("SH1043", Codes(Compile(Juggle("every 1/2  3:4 |", dancer: ""))));              // nothing to throw
        Assert.Contains("SH1043", Codes(Compile(Juggle("every 1/2  3:4 |", dancer: "juggles balls mic right"))));
        Assert.Contains("SH1006", Codes(Compile(Juggle("every 1/2  3:4 |", dancer: "juggles knives"))));
        var inMove = "tempo 120\nrole solo\ndancer alice humanoid juggles balls on solo\nmove m { juggle { every 1/2 3:2 } }\nsection A { solo { body { m:2 } } }\nform F { A }\nscore { form F  dancer alice }\n";
        Assert.Contains("SH1043", Codes(Compile(inMove)));
    }

    [Fact]
    public void Siteswap_is_read_as_written_with_its_brackets()
    {
        var c = Compile(Juggle("every 1/2  (4,2x)*:2 [32]1:2 |", dancer: "juggles balls"));
        Assert.DoesNotContain(c.Diagnostics, d => d.Code.StartsWith("SH00") || d.Code == "SH1041");
        var segs = c.Ir.Dancers[0].Tracks[Channels.Juggle];
        Assert.Equal(["(4,2x)*", "[32]1"], segs.Select(s => s.Pattern));
        Assert.All(segs, s => Assert.Equal(0.5, s.Every));
    }

    [Fact]
    public void An_arm_word_while_it_juggles_is_a_warning_and_after_a_skip_it_is_danced()
    {
        var c = Compile(Juggle("every 1/2  3:4 | skip:4 |", "rightArm { skip:2 fwd:2 | skip:2 side:2 | }"));
        var hidden = Assert.Single(c.Diagnostics);
        Assert.Equal("SH2021", hidden.Code);
        // After the skip (and the last balls caught) the arm is its row's: side at beat 6 is danced.
        var figure = c.Clip.Figures[0];
        int f = c.Clip.FrameOfBeat(7.5);
        Assert.True(figure.Joint(f, Rig.RHand).Y > figure.Joint(f, Rig.RShoulder).Y - 0.1);
        Assert.Equal(0, c.Clip.Figures[0].Juggle!.Weight[f], 3);
    }

    [Fact]
    public void The_hand_swings_up_as_fast_as_the_ring_it_lets_go()
    {
        // Rings every 1 at tempo 120: a 3 leaves at about 4 m/s. The hand is going up nearly as fast as it lets go.
        var c = Compile(Juggle("every 1  3:4 | 3:4 |", dancer: "juggles rings"));
        Assert.Empty(c.Diagnostics);
        var figure = c.Clip.Figures[0];
        var juggle = figure.Juggle!;
        int fps = c.Clip.Fps, seen = 0;
        for (int f = 2; f + 1 < juggle.Frames.Count; f++)
        {
            for (int b = 0; b < juggle.Count; b++)
            {
                // Let go between f−1 and f: held at f−1, in the air at f.
                var before = juggle.Frames[f - 1];
                var now = juggle.Frames[f];
                if (before[b * 7 + 3] != before[b * 7 + 4] || now[b * 7 + 3] == now[b * 7 + 4])
                    continue;
                int wrist = now[b * 7 + 3] == 0 ? Rig.RHand : Rig.LHand;
                double hand = (figure.Joint(f, wrist).Y - figure.Joint(f - 1, wrist).Y) * fps;
                double ring = (juggle.Frames[f + 1][b * 7 + 1] - now[b * 7 + 1]) * fps;
                Assert.True(ring > 3, $"the ring leaves at {ring:0.0} m/s");
                Assert.True(hand > 0.6 * ring, $"frame {f}: the hand goes up at {hand:0.0} m/s, the ring at {ring:0.0}");
                seen++;
            }
        }
        Assert.True(seen >= 6);
    }

    [Fact]
    public void Too_high_and_too_fast_are_warnings()
    {
        // tempo 120, every 1: a 9 flies 9 × 0.5 − 0.65 s and rises 18 m.
        Assert.Contains("SH2020", Codes(Compile(Juggle("every 1  9:9 |"))));
        // A throw every 1/16 of a beat: 0.03 s.
        Assert.Contains("SH2020", Codes(Compile(Juggle("every 1/16  3:4 |"))));
        Assert.DoesNotContain("SH2020", Codes(Compile(Juggle("every 1/2  3:4 |"))));
        // Rings 0.22 m high meet in the air; every 1 sends them 0.9 m up.
        Assert.Contains("every 1 rather than every 1/2", Assert.Single(Compile(Juggle("every 1/2  3:4 |", dancer: "juggles rings")).Diagnostics).Message);
        Assert.Empty(Compile(Juggle("every 1  3:4 |", dancer: "juggles rings")).Diagnostics);
    }

    [Fact]
    public void Tricks_change_the_hands_and_not_the_throws()
    {
        // The same cascade, with and without a trick: the same throws at the same times, other places for the hands.
        var plain = Compile(Juggle("every 1/2  3:4 |"));
        Assert.Empty(plain.Diagnostics);
        foreach (string trick in new[] { "mills", "penguin", "claw", "reverse" })
        {
            var c = Compile(Juggle($"every 1/2  3:4@{trick} |"));
            Assert.Empty(c.Diagnostics);
            var was = plain.Clip.Figures[0].Juggle!.Flights;
            var now = c.Clip.Figures[0].Juggle!.Flights;
            Assert.Equal(was.Select(f => (f[0], f[1], f[2])), now.Select(f => (f[0], f[1], f[2])));
            if (trick != "mills")
            {
                Assert.Equal(was.Select(f => f[3]), now.Select(f => f[3]));
                continue;
            }
            // Mills' first and second throws of three go higher, thrown a little before their beats (DESIGN.md 118.12); the
            // third on it.
            var early = was.Zip(now).Where(p => p.First[3] != p.Second[3]).ToList();
            Assert.NotEmpty(early);
            Assert.All(early, p => Assert.InRange(p.First[3] - p.Second[3], 0.01, 0.2));
            Assert.True(early.Count <= 2 * (was.Count() + 2) / 3, $"{early.Count} of {was.Count()} throws moved");
        }

        // Mills' Mess: a hand throws from the other side of the body, the arms crossed.
        var mills = Compile(Juggle("every 1/2  3:4@mills |")).Clip;
        var right = mills.Figures[0].Juggle!.Flights.First(f => f[1] == 0 && f[3] > 0.4f);
        int at = (int)Math.Round(right[3] * mills.Fps);
        Assert.True(mills.Figures[0].Joint(at, Rig.RHand).X > mills.Figures[0].Joint(at, Rig.Pelvis).X, "the right hand throws from the left of the body");

        // Penguin: the hands down by the hips.
        var penguin = Compile(Juggle("every 1/2  3:4@penguin |")).Clip;
        int mid = penguin.FrameOfBeat(2);
        Assert.True(penguin.Figures[0].Joint(mid, Rig.RHand).Y < penguin.Figures[0].Joint(mid, Rig.Pelvis).Y + 0.05);
        Assert.Contains(penguin.Figures[0].Juggle!.Styles, s => s[0] == (int)Juggling.Style.Penguin);
        // Claw: the palm down.
        Assert.Contains(Compile(Juggle("every 1/2  3:4@claw |")).Clip.Figures[0].Juggle!.Styles, s => s[1] == (int)Juggling.Style.Claw);
    }

    [Fact]
    public void Tricks_that_need_the_body_say_so()
    {
        // Under the leg with the leg down, and lifted.
        Assert.Contains("SH2022", Codes(Compile(Juggle("every 1/2  3:2 3:1/2@under 3:3/2 |"))));
        var lifted = Compile(Juggle("every 1/2  3:2 3:1/2@under 3:3/2 |", "steps { shift.left:4 | } rightLeg { skip:1 fwd.bent:1 then:1 down:1 | }"));
        Assert.DoesNotContain("SH2022", Codes(lifted));
        // Behind the back, too low to clear the shoulder; at every 1 high enough.
        Assert.Contains("SH2022", Codes(Compile(Juggle("every 1/2  3:2 3:1/2@back 3:3/2 |"))));
        Assert.DoesNotContain("SH2022", Codes(Compile(Juggle("every 1  3:2 3:1@back 3:1 |"))));
        // Two places for one hand, and a trick that is not one.
        Assert.Contains("SH1010", Codes(Compile(Juggle("every 1/2  3:4@penguin@claw |"))));
        Assert.Contains("mills", Assert.Single(Compile(Juggle("every 1/2  3:4@mils |")).Diagnostics).Message);
    }
}
