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
        foreach (string trick in new[] { "mills", "penguinCatch", "clawCatch", "reverse" })
        {
            var c = Compile(Juggle($"every 1/2  3:4@{trick} |"));
            // A penguin's balls at this pace stay below the shoulders (DESIGN.md 118.17): said, and nothing else.
            Assert.Equal(trick == "penguinCatch" ? ["SH2020"] : Array.Empty<string>(), Codes(c).Distinct());
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

        // Penguin: caught down by the hip and a little behind it, the arm straight (DESIGN.md 118.17).
        var penguin = Compile(Juggle("every 1/2  3:4@penguinCatch |")).Clip;
        var caughtRight = penguin.Figures[0].Juggle!.Flights.First(f => f[2] == 0 && f[4] > 0.6f);
        int mid = (int)Math.Round(caughtRight[4] * penguin.Fps);
        var fig = penguin.Figures[0];
        Assert.True(fig.Joint(mid, Rig.RHand).Y < fig.Joint(mid, Rig.Pelvis).Y + 0.05, "caught down by the hip");
        // By the side of the hip, not in front of it, the catching shoulder forward (DESIGN.md 118.20).
        Assert.True(fig.Joint(mid, Rig.RHand).Z < fig.Joint(mid, Rig.Pelvis).Z + 0.05, "caught by the side of the hip");
        Assert.True(fig.Joint(mid, Rig.RShoulder).Z > fig.Joint(mid, Rig.LShoulder).Z + 0.1, "the right shoulder forward");
        Assert.True((fig.Joint(mid, Rig.RHand) - fig.Joint(mid, Rig.RShoulder)).Length > 0.97 * (Rig.UpperArm + Rig.Forearm), "the arm straight");
        Assert.Contains(penguin.Figures[0].Juggle!.Styles, s => s[0] == (int)Juggling.Style.Penguin);
        // Thrown less often, its balls go above the shoulders, thrown in front of the body with the palm up (DESIGN.md 118.17).
        var high = Compile(Juggle("every 1  3:4@penguinCatch |"));
        Assert.DoesNotContain("SH2020", Codes(high));
        var thrown = high.Clip.Figures[0].Juggle!.Flights.First(f => f[1] == 0 && f[3] > 0.4f);
        int throwAt = (int)Math.Round(thrown[3] * high.Clip.Fps);
        Assert.True(high.Clip.Figures[0].Joint(throwAt, Rig.RHand).Z > high.Clip.Figures[0].Joint(throwAt, Rig.Pelvis).Z + 0.15, "thrown in front of the body");
        Assert.NotEqual((int)Juggling.Style.Penguin, high.Clip.Figures[0].Juggle!.Styles[throwAt][0]);
        // Claw: the palm down.
        Assert.Contains(Compile(Juggle("every 1/2  3:4@clawCatch |")).Clip.Figures[0].Juggle!.Styles, s => s[1] == (int)Juggling.Style.Claw);
    }

    [Fact]
    public void A_pattern_with_no_length_is_its_throws_as_written()
    {
        // DESIGN.md 118.47: one throwing beat a throw. `531 531` and `531531` are the same six throws, 3 beats at every 1/2,
        // and the same as `531:3`; bar lines count them.
        double[] Thrown(string row)
        {
            var c = Compile(Juggle(row));
            Assert.Empty(c.Diagnostics);
            return c.Clip.Figures[0].Juggle!.Flights.Select(f => Math.Round((double)f[3], 3)).ToArray();
        }
        var written = Thrown("every 1/2  3:1/2 531 531 3:1/2 |");
        Assert.Equal(written, Thrown("every 1/2  3:1/2 531531 3:1/2 |"));
        Assert.Equal(written, Thrown("every 1/2  3:1/2 531:3 3:1/2 |").Take(written.Length));
        Assert.Empty(Compile(Juggle("every 1/2  33333333 | 531531 33 | 3 3 3 3 441 3 |")).Diagnostics);
        var c = Compile(Juggle("every 1/2  3333333 |"));
        Assert.Contains("SH1002", Codes(c));
        // A pair is two throwing beats, as siteswap counts it: (4,4) is 1 beat at every 1/2, (4,2x)* 2.
        Assert.Empty(Compile(Juggle("every 1/2  (4,4)(4,4)(4,4)(4,4) |")).Diagnostics);
        Assert.Empty(Compile(Juggle("every 1/2  (4,2x)* (4,2x)* |")).Diagnostics);
    }

    [Fact]
    public void The_throws_suggested_between_two_patterns_are_written_as_they_are()
    {
        // 3 into 51 needs a throw between them: the error says it in siteswap, and written in so, the row is right.
        var c = Compile(Juggle("every 1/2  3:2 51:2 |"));
        var error = Assert.Single(c.Diagnostics, d => d.Code == "SH2019");
        var m = System.Text.RegularExpressions.Regex.Match(error.Message, @"… 3 (\S+) 51 …");
        Assert.True(m.Success, error.Message);
        string bridge = m.Groups[1].Value;
        var fixedRow = Compile(Juggle($"every 1/2  3:2 {bridge} 51:2 |"));
        Assert.DoesNotContain("SH2019", Codes(fixedRow));
    }

    [Fact]
    public void The_throws_go_on_evenly_from_one_word_into_the_next()
    {
        // every 3/4 in words of 4 beats: the throws go on every 3/4 over the bar line, not starting again at each word's
        // start (DESIGN.md 118.11, 118.47).
        var c = Compile(Juggle("every 3/4  3:4 | 3:4 | 3:4 |"));
        Assert.DoesNotContain("SH2019", Codes(c));
        var thrown = c.Clip.Figures[0].Juggle!.Flights.Select(f => (double)f[3]).OrderBy(t => t).ToList();
        var gaps = thrown.Zip(thrown.Skip(1), (a, b) => b - a).ToList();
        Assert.All(gaps, g => Assert.InRange(g, 0.375 - 0.01, 0.375 + 0.01));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_half_shower_throws_over_from_one_hand_and_under_from_the_other(bool mirror)
    {
        // The right hand (@mirror the left) always from well outside, longer in the air; the other always from inside,
        // shorter (DESIGN.md 118.48).
        string m = mirror ? "@mirror" : "";
        var plain = Compile(Juggle($"every 1/2  3:4{m} | 3:4{m} |"));
        var c = Compile(Juggle($"every 1/2  3:4@halfShower{m} | 3:4@halfShower{m} |"));
        Assert.Empty(c.Diagnostics);
        var was = plain.Clip.Figures[0].Juggle!.Flights;
        var now = c.Clip.Figures[0].Juggle!.Flights;
        Assert.Equal(was.Select(f => (f[0], f[1], f[2])), now.Select(f => (f[0], f[1], f[2])));
        var clip = c.Clip;
        var fig = clip.Figures[0];
        int over = mirror ? 1 : 0;
        double Out(float[] f)
        {
            int at = (int)Math.Round(f[3] * clip.Fps);
            var hand = fig.Joint(at, f[1] == 0 ? Rig.RHand : Rig.LHand);
            return (f[1] == 0 ? -1 : 1) * (hand.X - fig.Joint(at, Rig.Pelvis).X);
        }
        foreach (var (p, q) in was.Zip(now).Where(z => z.First[3] > 0.3 && z.First[3] < 3.6))
        {
            if ((int)q[1] == over)
            {
                Assert.True(Out(q) > 0.25, $"thrown over {Out(q):0.00} m out at {q[3]:0.00} s");
                Assert.True(q[4] - q[3] > p[4] - p[3] + 0.08, $"thrown over at {q[3]:0.00} s flies longer");
            }
            else
            {
                Assert.True(Out(q) < 0.2, $"thrown under {Out(q):0.00} m out at {q[3]:0.00} s");
                Assert.True(q[4] - q[3] < p[4] - p[3] - 0.03, $"thrown under at {q[3]:0.00} s flies shorter");
            }
        }
        // One whole pattern at a time.
        Assert.Contains("SH1010", Codes(Compile(Juggle("every 1/2  3:4@halfShower@tennis |"))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Takeouts_swipe_the_own_4_across_as_it_falls_and_carry_it_round_past_the_forehead_into_the_next_throw(bool mirror)
    {
        // 423 (DESIGN.md 118.49, the user): a hand throws its 3 from its own side as it sets off on its swipe (the other hand
        // catches it), takes its own 4 from above near the middle as it falls and carries it straight across to the far side,
        // stops once there and carries it round a big circle, the ball past the forehead, out past its own side and below
        // (the 2), up into its next 4 from inside, while the other hand throws its own 4 under it.
        string m = mirror ? "@mirror" : "";
        var c = Compile(Juggle($"every 1/2  3:4{m} | 423:4~@takeouts{m} | :2 3:2{m} |"));
        Assert.Empty(c.Diagnostics);
        var clip = c.Clip;
        var fig = clip.Figures[0];
        var flights = clip.Figures[0].Juggle!.Flights;
        var styles = clip.Figures[0].Juggle!.Styles;
        Vec3 HandAt(int hand, double sec) => fig.Joint((int)Math.Round(sec * clip.Fps), hand == 0 ? Rig.RHand : Rig.LHand) - fig.Joint((int)Math.Round(sec * clip.Fps), Rig.Pelvis);
        double Out(int hand, double sec) => (hand == 0 ? -1 : 1) * HandAt(hand, sec).X;
        // 4s thrown in the takeouts (2 to 5 s) and held there.
        var fours = flights.Where(f => f[1] == f[2] && f[3] > 1.95 && f[4] < 4.7).ToList();
        Assert.True(fours.Count >= 3);
        foreach (var f in fours)
        {
            int hand = (int)f[1];
            // Each hand always swipes its own ball (the user): the right ball 0, the left ball 2 (red and yellow in the preview).
            Assert.Equal(hand == 0 ? 0 : 2, (int)f[0]);
            // Taken on the catching arm's own side, out near it (the user), swiping down from above just after the hand's own 3
            // from further out, and carried on to the far side.
            Assert.InRange(Out(hand, f[4]), 0.1, 0.3);
            // The palm turned out as it swipes and catches, and kept so after, till the hand is past the face (the user: then
            // the palm up and the elbow opened at once).
            Assert.Equal((int)Juggling.Style.Outward, styles[(int)Math.Round(f[4] * clip.Fps)][hand]);
            Assert.Equal((int)Juggling.Style.Outward, styles[(int)Math.Round((f[4] + 0.15) * clip.Fps)][hand]);
            Assert.NotEqual((int)Juggling.Style.Outward, styles[(int)Math.Round((f[4] + 0.5) * clip.Fps)][hand]);
            // (Only to the middle: the forearm turns round the elbow kept out on its own side, and the swipe rises into the
            // round there, DESIGN.md 118.49.)
            Assert.True(Out(hand, f[4] + 0.15) < 0.05, $"carried across to {Out(hand, f[4] + 0.15):0.00} m by {f[4] + 0.15:0.00} s");
            var three = flights.Where(g => (int)g[1] == hand && g[3] < f[4]).MaxBy(g => g[3])!;
            Assert.Equal(1, (int)three[0]);   // the one thrown across from outside, never swiped: ball 1 (blue)
            Assert.True(three[2] != hand && f[4] - three[3] < 0.25, $"its 3 just before, at {three[3]:0.00} s");
            // From well outside to well outside, high, as tennis's ball (the user).
            Assert.True(Out(hand, three[3]) > 0.35, $"3 thrown {Out(hand, three[3]):0.00} m out at {three[3]:0.00} s");
            Assert.True(Out(1 - hand, three[4]) > 0.35, $"3 caught {Out(1 - hand, three[4]):0.00} m out at {three[4]:0.00} s");
            Assert.True(three[4] - three[3] > 0.5, $"3 in the air {three[4] - three[3]:0.00} s");
            // The round's top about when the 3 thrown just before is at its top (the user: the two at their tops at one time).
            if (three[4] < 5.0)
            {
                var round = Enumerable.Range(0, 24).Select(k => f[4] + 0.6 * k / 24).ToList();
                double topAt = round.MaxBy(s => HandAt(hand, s).Y);
                // (The 3's top: it is caught higher than it is thrown, so a little after the middle of its flight.)
                double tf = three[4] - three[3], rise = HandAt(1 - hand, three[4]).Y - HandAt(hand, three[3]).Y;
                Assert.InRange(topAt - (three[3] + tf / 2 + rise / (9.81 * tf)), -0.25, 0.25);
            }
            // Not thrown on: held, stopped once, then back round an arc up over the top and out past its own side, the palm
            // to the front, while the other hand throws its 4.
            double next = flights.Where(g => (int)g[1] == hand && g[3] > f[4]).Min(g => (double)g[3]);
            Assert.True(next - f[4] > 0.5, $"held {next - f[4]:0.00} s");
            var speeds = Enumerable.Range(1, 9).Select(k => (HandAt(hand, f[4] + k / clip.Fps) - HandAt(hand, f[4] + (k - 1) / clip.Fps)).Length * clip.Fps).ToList();
            Assert.True(speeds.Min() < 0.3, $"stopped after {f[4]:0.00} s (slowest {speeds.Min():0.00} m/s)");
            var back = Enumerable.Range(1, 12).Select(k => f[4] + 0.6 * k / 12).ToList();
            Assert.True(back.Max(s => HandAt(hand, s).Y) > HandAt(hand, f[4]).Y + 0.1, $"up round an arc after {f[4]:0.00} s");
            Assert.True(back.Max(s => Out(hand, s)) > 0.3, $"out past its own side after {f[4]:0.00} s");
            // Round past the forehead (the user: the ball just before it), and the next 4 thrown up from inside.
            int nose = Array.IndexOf(clip.Joints, "nose");
            double top = back.Max(s => fig.Joint((int)Math.Round(s * clip.Fps), hand == 0 ? Rig.RHand : Rig.LHand).Y);
            Assert.True(top > fig.Frames[(int)Math.Round(f[4] * clip.Fps)][nose * 3 + 1] - 0.12, $"up to {top:0.00} m, the nose at {fig.Frames[(int)Math.Round(f[4] * clip.Fps)][nose * 3 + 1]:0.00}");
            if (next < 4.95)
                Assert.True(Out(hand, next) < 0.2, $"next 4 thrown {Out(hand, next):0.00} m out at {next:0.00} s");
            Assert.Contains(flights, g => (int)g[1] == 1 - hand && g[1] == g[2] && g[3] > f[4] && g[3] < f[4] + 0.4);
            // As the other hand throws its 3 up from outside, this one is out on its own side at that height (the user: the
            // two at one height at one time).
            var across = flights.FirstOrDefault(g => (int)g[1] == 1 - hand && g[1] != g[2] && g[3] > f[4] && g[3] < next);
            if (across is not null && across[4] < 5.0)
            {
                Assert.True(Out(hand, across[3]) > 0.15, $"out {Out(hand, across[3]):0.00} m as the other throws at {across[3]:0.00} s");
                // (Not at one height any more: past the face the elbow opens and the hand goes on round a slow circle, not
                // timed to be level with the other's throw; the user, 2026-11-24. The round's top is still about the 3's, above.)
            }
        }
        Assert.Contains("SH1010", Codes(Compile(Juggle("every 1/2  423:4@takeouts@halfShower |"))));
        Assert.Contains("SH1010", Codes(Compile(Juggle("every 1/2  423:4@takeouts@clawCatch |"))));
    }

    [Fact]
    public void Tennis_throws_one_ball_from_outside_and_the_others_from_inside()
    {
        // The same throws as the cascade; one ball (the word's first across) leaves from outside the hand's own catch, every
        // throw of it in the word, and the other two from inside (DESIGN.md 118.45). The tennis ball goes higher, longer in
        // the air, and the others lower (118.46).
        var plain = Compile(Juggle("every 1/2  3:4 | 3:4 |"));
        var c = Compile(Juggle("every 1/2  3:4@tennis | 3:4@tennis |"));
        Assert.Empty(c.Diagnostics);
        var was = plain.Clip.Figures[0].Juggle!.Flights;
        var now = c.Clip.Figures[0].Juggle!.Flights;
        Assert.Equal(was.Select(f => (f[0], f[1], f[2])), now.Select(f => (f[0], f[1], f[2])));
        var clip = c.Clip;
        var fig = clip.Figures[0];
        int tennis = (int)now[0][0];
        foreach (var (p, q) in was.Zip(now).Where(z => z.First[3] > 0.3 && z.First[3] < 3.6))
        {
            Assert.InRange(q[3] - p[3], -0.1, 0.1);
            if ((int)q[0] == tennis)
                Assert.True(q[4] - q[3] > p[4] - p[3] + 0.08, $"the tennis ball thrown at {q[3]:0.00} s flies longer");
            else
                Assert.True(q[4] - q[3] < p[4] - p[3] - 0.03, $"the ball thrown at {q[3]:0.00} s flies shorter");
        }
        // Off the pelvis, to the throwing hand's own side: + outward.
        double Out(float[] f)
        {
            int at = (int)Math.Round(f[3] * clip.Fps);
            var hand = fig.Joint(at, f[1] == 0 ? Rig.RHand : Rig.LHand);
            return (f[1] == 0 ? -1 : 1) * (hand.X - fig.Joint(at, Rig.Pelvis).X);
        }
        var inWord = now.Where(f => f[3] > 0.3 && f[3] < 3.6).ToList();
        Assert.True(inWord.Count(f => (int)f[0] == tennis) >= 3);
        Assert.All(inWord.Where(f => (int)f[0] == tennis), f => Assert.True(Out(f) > 0.25, $"tennis ball thrown {Out(f):0.00} m out at {f[3]:0.00} s"));
        Assert.All(inWord.Where(f => (int)f[0] != tennis), f => Assert.True(Out(f) < 0.2, $"cascade ball thrown {Out(f):0.00} m out at {f[3]:0.00} s"));
    }

    [Fact]
    public void A_claw_is_thrown_up_to_the_face_and_caught_there_near_its_top()
    {
        // At tempo 120 every 1/2: caught at the face, no more than 5 cm down from its top, from above (the palm down).
        var c = Compile(Juggle("every 1/2  3:4 | 3:4@clawCatch | 3:4@clawCatch |"));
        Assert.DoesNotContain("SH2023", Codes(c));
        var clip = c.Clip;
        var juggle = clip.Figures[0].Juggle!;
        int nose = Array.IndexOf(clip.Joints, "nose");
        foreach (var f in juggle.Flights.Where(f => f[4] > 2.6 && f[4] < 5.5))
        {
            int caught = (int)Math.Floor(f[4] * clip.Fps), b = (int)f[0];
            var ys = Enumerable.Range((int)Math.Ceiling(f[3] * clip.Fps), caught - (int)Math.Ceiling(f[3] * clip.Fps) + 1).Select(k => juggle.Frames[k][b * 7 + 1]).ToList();
            double face = clip.Figures[0].Frames[caught][nose * 3 + 1];
            Assert.InRange(ys[^1], face - 0.12, face + 0.06);
            Assert.InRange(ys.Max() - ys[^1], 0, 0.06);
            Assert.Equal((int)Juggling.Style.Claw, juggle.Styles[caught][(int)f[2]]);
        }
        // Too slow a tempo, the ball falls well past its top first; too quick, the hand is hurried. Both say the tempo it suits.
        string Slower(string text, int tempo) => text.Replace("tempo 120", $"tempo {tempo}");
        var slow = Compile(Slower(Juggle("every 1/2  3:4 | 3:4@clawCatch |"), 90));
        Assert.Contains("past its top", Assert.Single(slow.Diagnostics, d => d.Code == "SH2023").Message);
        var quick = Compile(Slower(Juggle("every 1/2  3:4 | 3:4@clawCatch |"), 225));
        Assert.Contains("hurried", Assert.Single(quick.Diagnostics, d => d.Code == "SH2023").Message);
    }

    [Theory]
    [InlineData("every 1/2  4:4 | 534:3 4:1 | 633:3 4:1 | 7441:4 |", 0.10)]
    [InlineData("every 1/3  5:4 | 645:4 | 744:4 | 97531:10/3 5:2/3 | 75751:10/3 5:2/3 |", 0.10)]
    // Changes of pattern and of pace (DESIGN.md 118.43): a ball thrown in one comes down as it was thrown.
    [InlineData("every 1/2  3:4 | 3:4@mills | 3:4@reverse | 3:4 | every 1  3:2 3:1@backThrow 3:1 | every 1/2  3:4@windmill | 3:4 |", 0.08)]
    // Tennis: one ball over the top of the others, to and fro (DESIGN.md 118.45).
    [InlineData("every 1/2  3:4 | 3:4@tennis | 3:4@tennis | 3:4 |", 0.10)]
    [InlineData("every 1/2  3:4 | 3:4@halfShower | 3:4@halfShower | 3:4 |", 0.10)]
    [InlineData("every 1/2  3:4 | 423:4~@takeouts | :2 3:2 | 3:4 |", 0.10)]
    public void Balls_in_the_air_keep_well_apart_in_four_and_five_ball_patterns(string row, double apart)
    {
        // The fountain thrown inside and caught outside, the one going up in front; higher throws across wider (DESIGN.md
        // 118.42). On the figure 10 cm or more: a character's shorter arms draw it some 0.6 as wide, and a ball is 6.8 cm.
        var c = Compile(Juggle(row));
        Assert.Empty(c.Diagnostics);
        var jg = c.Clip.Figures[0].Juggle!;
        int balls = jg.Frames[0].Length / 7;
        for (int frame = 0; frame < jg.Frames.Count; frame++)
        {
            double sec = frame / c.Clip.Fps;
            var air = jg.Flights.Where(f => f[3] + 0.04 <= sec && sec <= f[4] - 0.04).Select(f => (int)f[0]).ToHashSet();
            foreach (int a in air)
                foreach (int b in air)
                    if (a < b)
                    {
                        var p = new Vec3(jg.Frames[frame][a * 7], jg.Frames[frame][a * 7 + 1], jg.Frames[frame][a * 7 + 2]);
                        var q = new Vec3(jg.Frames[frame][b * 7], jg.Frames[frame][b * 7 + 1], jg.Frames[frame][b * 7 + 2]);
                        Assert.True((p - q).Length >= apart, $"balls {a} and {b} {(p - q).Length:0.000} m apart at {sec:0.00} s");
                    }
        }
        Assert.True(balls >= 3);
    }

    [Fact]
    public void Mills_written_on_in_a_second_word_goes_on_round_as_one_word_does()
    {
        // Eight throws a word, the six throws round go on over the bar line: the hands move as in one word of sixteen in a
        // bar of 8/4 (DESIGN.md 118.40; counted again from the first of six at the second word, they jumped there).
        var two = Compile(Juggle("every 1/2  3:4@mills | 3:4@mills |"));
        var one = Compile(Juggle("every 1/2  3:8@mills |").Replace("tempo 120\n", "tempo 120\ntime 8/4\n"));
        Assert.Empty(two.Diagnostics);
        Assert.Empty(one.Diagnostics);
        for (int frame = 0; frame < Math.Min(two.Clip.Figures[0].Frames.Count, one.Clip.Figures[0].Frames.Count); frame++)
            foreach (var joint in new[] { Rig.RHand, Rig.LHand, Rig.Chest })
                Assert.True((two.Clip.Figures[0].Joint(frame, joint) - one.Clip.Figures[0].Joint(frame, joint)).Length < 1e-3, $"frame {frame}");
    }

    [Fact]
    public void A_ball_caught_behind_the_back_is_met_behind_the_waist_the_shoulders_turned()
    {
        // The ball caught in the @backCatch word (the left hand's, thrown again on beat 5 of every 1): at its catch the left
        // hand is behind the chest, the shoulders turned well round, the left one back.
        var c = Compile(Juggle("every 1  3:4 | 3:1 3:1@backCatch 3:2 |"));
        Assert.Empty(c.Diagnostics);
        var clip = c.Clip;
        var fig = clip.Figures[0];
        var caught = clip.Figures[0].Juggle!.Flights.Single(f => f[2] == 1 && Math.Abs(f[4] - 1.85) < 0.02);
        int frame = (int)Math.Round(caught[4] * clip.Fps);
        var (l, r) = (fig.Joint(frame, Rig.LShoulder), fig.Joint(frame, Rig.RShoulder));
        var chestFwd = new Vec3(-(l - r).Z, 0, (l - r).X).Normalized();
        Assert.True((fig.Joint(frame, Rig.LHand) - fig.Joint(frame, Rig.Chest)).Dot(chestFwd) < -0.15, "the left hand behind the chest");
        var feetLeft = fig.Joint(frame, Rig.LAnkle) - fig.Joint(frame, Rig.RAnkle);
        var feetFwd = new Vec3(-feetLeft.Z, 0, feetLeft.X).Normalized();
        Assert.True((l - r).Dot(feetFwd) < -0.2, "the left shoulder back");
        // Too low to come down over the shoulder; and not with a penguin's or a claw's catch.
        Assert.Contains("SH2022", Codes(Compile(Juggle("every 1/2  3:4 | 3:1/2 3:1/2@backCatch 3:3 |"))));
        Assert.Contains("SH1010", Codes(Compile(Juggle("every 1  3:4@backCatch@penguinCatch |"))));
        Assert.Contains("SH1010", Codes(Compile(Juggle("every 1  3:4@clawCatch@backCatch |"))));
    }

    [Fact]
    public void Behind_the_back_the_shoulders_turn_well_round_the_throwing_one_back()
    {
        // The right hand's throw from behind the back on beat 3 (every 1): at it the shoulders' line is turned some 70
        // degrees from the feet's, the right shoulder back, and turned back square a beat either side.
        var c = Compile(Juggle("every 1  3:2 3:1@backThrow 3:1 |"));
        Assert.Empty(c.Diagnostics);
        var clip = c.Clip;
        double Turn(int frame)
        {
            var fig = clip.Figures[0];
            var (l, r) = (fig.Joint(frame, Rig.LShoulder), fig.Joint(frame, Rig.RShoulder));
            var (la, ra) = (fig.Joint(frame, Rig.LAnkle), fig.Joint(frame, Rig.RAnkle));
            double a = Math.Atan2(l.Z - r.Z, l.X - r.X) - Math.Atan2(la.Z - ra.Z, la.X - ra.X);
            return Math.Atan2(Math.Sin(a), Math.Cos(a)) * 180 / Math.PI;
        }
        var thrown = clip.Figures[0].Juggle!.Flights.Single(f => f[1] == 0 && Math.Abs(f[3] - 1.0) < 1e-3);
        double at = Turn((int)Math.Round(thrown[3] * clip.Fps));
        Assert.InRange(Math.Abs(at), 55, 80);
        // The right shoulder back: the right one is behind the left along the feet's forward.
        int frame0 = (int)Math.Round(thrown[3] * clip.Fps);
        var fig0 = clip.Figures[0];
        var feetLeft = fig0.Joint(frame0, Rig.LAnkle) - fig0.Joint(frame0, Rig.RAnkle);
        var fwd = new Vec3(-feetLeft.Z, 0, feetLeft.X).Normalized();
        Assert.True((fig0.Joint(frame0, Rig.RShoulder) - fig0.Joint(frame0, Rig.LShoulder)).Dot(fwd) < -0.2, "the right shoulder back");
        Assert.InRange(Math.Abs(Turn(clip.FrameOfBeat(0.5))), 0, 5);
        Assert.InRange(Math.Abs(Turn(clip.FrameOfBeat(3.5))), 0, 5);
    }

    [Fact]
    public void A_ball_thrown_far_above_the_head_tips_the_head_back_a_little()
    {
        // A 7 (some 2 m above the head) tips it back up to about 20 degrees; a cascade's balls, below the head, do not.
        var c = Compile(Juggle("every 1/2  3:7/2 5:1/2 | 711:3 22:1 | 3:4 |"));
        Assert.Empty(c.Diagnostics);
        var clip = c.Clip;
        double Up(int frame)
        {
            var d = clip.Figures[0].Joint(frame, Rig.Nose) - clip.Figures[0].Joint(frame, Rig.Head);
            return Math.Asin(d.Y / d.Length) * 180 / Math.PI;
        }
        Assert.InRange(Up(clip.FrameOfBeat(2)), -5, 5);
        Assert.InRange(Enumerable.Range(clip.FrameOfBeat(4), clip.FrameOfBeat(8) - clip.FrameOfBeat(4)).Max(Up), 14, 25);
        Assert.InRange(Up(clip.FrameOfBeat(11)), -5, 5);
    }

    [Fact]
    public void The_windmill_throws_every_ball_on_one_side_with_the_first_arm_on_top()
    {
        // The same throws as the cascade, on the beat; every ball thrown on the right and caught on the left (@mirror: the
        // other way round), the right hand always above the left once they are going.
        var plain = Compile(Juggle("every 1/2  3:4 |"));
        var c = Compile(Juggle("every 1/2  3:4@windmill | 3:4@windmill |"));
        Assert.Empty(c.Diagnostics);
        var clip = c.Clip;
        var flights = clip.Figures[0].Juggle!.Flights;
        Assert.Equal(plain.Clip.Figures[0].Juggle!.Flights.Select(f => f[3]), flights.Take(plain.Clip.Figures[0].Juggle!.Flights.Count).Select(f => f[3]));
        // Paired as Mills' first and second for the preview's heights: the top hand's throw, then the one from under the arm.
        Assert.All(flights.Where(f => f[5] == 1), f => Assert.Equal(f[1] == 0 ? 0f : 1f, f[6]));
        // The back leans the top arm's shoulder up all through (DESIGN.md 118.28).
        for (int frame = clip.FrameOfBeat(2); frame < clip.FrameOfBeat(6); frame += 3)
            Assert.True(clip.Figures[0].Joint(frame, Rig.RShoulder).Y > clip.Figures[0].Joint(frame, Rig.LShoulder).Y + 0.01, $"the right shoulder up at frame {frame}");
        var balls = clip.Figures[0].Juggle!.Frames;
        double Side(int frame, int ball) => balls[frame][ball * 7] - clip.Figures[0].Joint(frame, Rig.Pelvis).X;
        foreach (var f in flights.Where(f => f[5] == 1 && f[3] > 1))
        {
            int thrown = (int)Math.Ceiling(f[3] * clip.Fps), caught = (int)Math.Floor(f[4] * clip.Fps);
            Assert.True(Side(thrown, (int)f[0]) < 0, $"thrown on the right at {f[3]}");
            Assert.True(Side(caught, (int)f[0]) > 0, $"caught on the left at {f[4]}");
        }
        // Where the hands pass each other side to side, the right one over the left (it swings down to its throw on its own
        // side, the left hand over on the other: DESIGN.md 118.35).
        for (int frame = clip.FrameOfBeat(2); frame < clip.FrameOfBeat(6); frame++)
        {
            var (r, l) = (clip.Figures[0].Joint(frame, Rig.RHand), clip.Figures[0].Joint(frame, Rig.LHand));
            if (Math.Abs(r.X - l.X) < 0.15)
                Assert.True(r.Y > l.Y, $"the right hand on top at frame {frame}");
        }
        var mirrored = Compile(Juggle("every 1/2  3:4@windmill@mirror | 3:4@windmill@mirror |")).Clip;
        int passing = 0;
        for (int frame = mirrored.FrameOfBeat(2); frame < mirrored.FrameOfBeat(6); frame++)
        {
            var (r, l) = (mirrored.Figures[0].Joint(frame, Rig.RHand), mirrored.Figures[0].Joint(frame, Rig.LHand));
            if (Math.Abs(r.X - l.X) < 0.15)
            {
                passing++;
                Assert.True(l.Y > r.Y, $"mirrored, the left hand on top at frame {frame}");
            }
        }
        Assert.True(passing > 0, "mirrored, the hands pass each other");
        // Not with Mills' Mess, nor with what puts a hand elsewhere.
        Assert.Contains("SH1010", Codes(Compile(Juggle("every 1/2  3:4@windmill@mills |"))));
        Assert.Contains("SH1010", Codes(Compile(Juggle("every 1/2  3:4@penguinCatch@windmill |"))));
    }

    [Fact]
    public void Tricks_that_need_the_body_say_so()
    {
        // Under the leg with the leg down, and lifted.
        Assert.Contains("SH2022", Codes(Compile(Juggle("every 1/2  3:2 3:1/2@underThrow 3:3/2 |"))));
        var lifted = Compile(Juggle("every 1/2  3:2 3:1/2@underThrow 3:3/2 |", "steps { shift.left:4 | } rightLeg { skip:1 fwd.bent:1 then:1 down:1 | }"));
        Assert.DoesNotContain("SH2022", Codes(lifted));
        // Behind the back, too low to clear the shoulder; at every 1 high enough.
        Assert.Contains("SH2022", Codes(Compile(Juggle("every 1/2  3:2 3:1/2@backThrow 3:3/2 |"))));
        Assert.DoesNotContain("SH2022", Codes(Compile(Juggle("every 1  3:2 3:1@backThrow 3:1 |"))));
        // Two places for one hand, and a trick that is not one.
        Assert.Contains("SH1010", Codes(Compile(Juggle("every 1/2  3:4@penguinCatch@clawCatch |"))));
        Assert.Contains("mills", Assert.Single(Compile(Juggle("every 1/2  3:4@mils |")).Diagnostics).Message);
    }
}
