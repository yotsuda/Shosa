using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

/// <summary>Formations: `formation NAME { alice at X Z … }` and the word `go.NAME` of the steps (DESIGN.md 132).</summary>
public class FormationTests
{
    private const string Cast = """
        tempo 120
        role lead
        role wing
        dancer alice humanoid on lead at 0 0
        dancer bob   humanoid on wing at -1.2 0
        dancer carol humanoid on wing at 1.2 0

        """;

    private static ShosaCompilation Trio(string formations, string lead, string wing) => ShosaCompilation.Compile(
        Cast + formations + "\nsection P {\n  lead { steps { " + lead + " } }\n  wing { steps { " + wing + " } }\n}\nform { P }\nscore { dancer alice  dancer bob  dancer carol }\n");

    private static Vec3 At(ShosaCompilation compilation, string dancer, double beat, int joint)
    {
        var figure = compilation.Clip.Figures.Single(f => f.Name == dancer);
        return figure.Joint(compilation.Clip.FrameAt(beat), joint);
    }

    /// <summary>The middle of the ankles on the floor.</summary>
    private static (double X, double Z) Stands(ShosaCompilation compilation, string dancer, double beat)
    {
        Vec3 left = At(compilation, dancer, beat, Rig.LAnkle), right = At(compilation, dancer, beat, Rig.RAnkle);
        return ((left.X + right.X) / 2, (left.Z + right.Z) / 2);
    }

    private static void Near((double X, double Z) expected, (double X, double Z) actual)
    {
        Assert.InRange(actual.X, expected.X - 0.02, expected.X + 0.02);
        Assert.InRange(actual.Z, expected.Z - 0.02, expected.Z + 0.02);
    }

    private static double Nearest(ShosaCompilation compilation, string a, string b)
    {
        double nearest = double.MaxValue;
        for (double beat = 0; beat <= compilation.Clip.TotalBeats; beat += 0.125)
        {
            Vec3 p = At(compilation, a, beat, Rig.Pelvis), q = At(compilation, b, beat, Rig.Pelvis);
            nearest = Math.Min(nearest, Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Z - q.Z) * (p.Z - q.Z)));
        }
        return nearest;
    }

    [Fact]
    public void Each_dancer_of_a_role_walks_to_its_own_place_and_stands_there_on_the_beat()
    {
        var compilation = Trio("formation vee { alice at 0 0.8  bob at -1.2 -0.4  carol at 1.2 -0.4 }",
            "stand:4 | go.vee:4 | then:4 | go.home:4 |", "stand:4 | go.vee:4 | then:4 | go.home:4 |");
        Assert.Empty(compilation.Diagnostics);
        // Standing until the walk, which is the time of the word before: there on the word's own beat, and staying.
        Near((0, 0), Stands(compilation, "alice", 0));
        Near((0, 0.8), Stands(compilation, "alice", 4));
        Near((-1.2, -0.4), Stands(compilation, "bob", 4));
        Near((1.2, -0.4), Stands(compilation, "carol", 4));
        Near((1.2, -0.4), Stands(compilation, "carol", 8));
        // The feet closed at standing width, facing as they faced.
        var apart = At(compilation, "bob", 4, Rig.LAnkle) - At(compilation, "bob", 4, Rig.RAnkle);
        Assert.Equal(Rig.Stance, apart.X, 2);
        Assert.Equal(0, apart.Z, 2);
        // `home` is each dancer's own `at`.
        Near((0, 0), Stands(compilation, "alice", 12));
        Near((-1.2, 0), Stands(compilation, "bob", 12));
        // On the way it is on the way.
        Assert.InRange(Stands(compilation, "alice", 2).Z, 0.2, 0.6);
    }

    [Fact]
    public void A_first_word_is_already_made_the_score_begins_in_the_formation()
    {
        var compilation = Trio("formation vee { alice at 0 0.8  bob at -1.2 -0.4  carol at 1.2 -0.4 }", "go.vee:4 |", "go.vee:4 |");
        Assert.Empty(compilation.Diagnostics);
        Near((0, 0.8), Stands(compilation, "alice", 0));
        Near((-1.2, -0.4), Stands(compilation, "bob", 0));
        Near((1.2, -0.4), Stands(compilation, "carol", 3));
    }

    [Fact]
    public void Dancers_who_change_places_go_round_each_other()
    {
        // Bob and carol change places along one line, and alice walks back across it: nobody wrote how they pass.
        var compilation = Trio("formation swap { alice at 0 -0.8  bob at 1.2 0  carol at -1.2 0 }",
            "stand:4~ | :4 | go.swap:4 |", "stand:4~ | :4 | go.swap:4 |");
        Assert.Empty(compilation.Diagnostics);
        Near((1.2, 0), Stands(compilation, "bob", 8));
        Near((-1.2, 0), Stands(compilation, "carol", 8));
        Near((0, -0.8), Stands(compilation, "alice", 8));
        // Too far to walk sideways: they turn the way they go, and stand facing the front again.
        var apart = At(compilation, "bob", 8, Rig.LAnkle) - At(compilation, "bob", 8, Rig.RAnkle);
        Assert.Equal(Rig.Stance, apart.X, 2);
        Assert.Equal(0, apart.Z, 2);
        var shoulders = At(compilation, "bob", 4, Rig.LShoulder) - At(compilation, "bob", 4, Rig.RShoulder);
        Assert.True(Math.Abs(shoulders.Z) > Math.Abs(shoulders.X), "half-way, bob is turned the way he walks");
        Assert.True(Nearest(compilation, "bob", "carol") > 0.45, "bob and carol pass");
        Assert.True(Nearest(compilation, "alice", "bob") > 0.45, "alice and bob pass");
        Assert.True(Nearest(compilation, "alice", "carol") > 0.45, "alice and carol pass");
    }

    [Fact]
    public void A_way_bowed_by_the_writer_goes_where_it_is_written()
    {
        string formations = "formation over { alice at 0 0  bob at 1.2 0  carol at 1.2 0.01 }";
        var front = Trio(formations, "stand:4 | then:4 |", "stand:4 | go.over.front(0.6m).steps(8):4 |");
        var back = Trio(formations, "stand:4 | then:4 |", "stand:4 | go.over.back(0.6m).steps(8):4 |");
        // Bob walks from the left of alice to her right: in front of her (toward the audience), or behind her.
        Assert.InRange(Stands(front, "bob", 2).Z, 0.4, 0.75);
        Assert.InRange(Stands(back, "bob", 2).Z, -0.75, -0.4);
        Near((1.2, 0), Stands(front, "bob", 4));
        Near((1.2, 0), Stands(back, "bob", 4));
    }

    [Fact]
    public void Ways_bowed_to_the_walkers_own_side_take_everybody_round_one_ring()
    {
        // Three on a ring, each to the next one's place by the same word: straight ways are a small triangle across the
        // middle; bowed to each walker's own left they are one circle (DESIGN.md 140).
        const string ring = "formation ring { alice at 0 1.3  bob at -1.13 -0.65  carol at 1.13 -0.65 }  formation next { alice at -1.13 -0.65  bob at 1.13 -0.65  carol at 0 1.3 }";
        const string row = "go.ring:4 | then:4 | then:4 | go.next.left(0.65m):4 |";
        var compilation = Trio(ring, row, row);
        Assert.Empty(compilation.Diagnostics);
        foreach (string dancer in new[] { "alice", "bob", "carol" })
        {
            for (double beat = 4; beat <= 12; beat += 0.5)
            {
                var (x, z) = Stands(compilation, dancer, beat);
                Assert.InRange(Math.Sqrt(x * x + z * z), 0.95, 1.5);   // straight, the middle of the way is 0.65 m from the centre
            }
        }
        Near((-1.13, -0.65), Stands(compilation, "alice", 12));
        Assert.True(Nearest(compilation, "alice", "bob") > 1.5 && Nearest(compilation, "bob", "carol") > 1.5 && Nearest(compilation, "alice", "carol") > 1.5);

        // The walker's own side, not the stage's: whichever way bob walks, his left and his right are opposite sides of it.
        string over = "formation over { alice at 0 0  bob at 1.2 0  carol at 1.2 0.01 }";
        var left = Trio(over, "stand:4 | then:4 |", "stand:4 | go.over.left(0.6m).steps(8):4 |");
        var right = Trio(over, "stand:4 | then:4 |", "stand:4 | go.over.right(0.6m).steps(8):4 |");
        Assert.InRange(Stands(left, "bob", 2).Z * Stands(right, "bob", 2).Z, -0.75 * 0.75, -0.4 * 0.4);
        // One bow to a way.
        Assert.Contains(Trio(over, "stand:4 | then:4 |", "stand:4 | go.over.left.front:4 |").Diagnostics, d => d.Code == "SH1012");
    }

    [Fact]
    public void One_who_walks_goes_round_one_who_stands()
    {
        // Alice stands on the line bob walks: he goes round her, and still ends on his place.
        var compilation = ShosaCompilation.Compile(Cast + """
            formation over { bob at 1.2 0.6 }
            section P {
              lead { steps { stand:4 | then:4 | } }
              wing { steps { stand:4 | go.over.steps(8):4 | } }
            }
            form { P }
            score { dancer alice  dancer bob }
            """);
        Assert.Empty(compilation.Diagnostics);
        Near((1.2, 0.6), Stands(compilation, "bob", 4));
        Assert.True(Nearest(compilation, "alice", "bob") > 0.55, FormattableString.Invariant($"bob passes alice at {Nearest(compilation, "alice", "bob"):0.00} m"));
    }

    [Fact]
    public void The_steps_are_one_a_beat_or_as_many_as_written_and_too_long_a_stride_is_said()
    {
        static ShosaCompilation Walk(string steps, double x) => ShosaCompilation.Compile(
            FormattableString.Invariant($"tempo 120\nrole solo\ndancer solo humanoid on solo\nformation there {{ solo at 0 {x} }}\nsection P {{\n  solo {{ steps {{ {steps} }} }}\n}}\nform {{ P }}\nscore {{ dancer solo }}\n"));

        // 1.5 m forward in four beats: three steps of half a metre and a close.
        var walk = Walk("stand:4 | go.there:4 |", 1.5);
        Assert.Empty(walk.Diagnostics);
        var support = walk.Score.Clips[0].Dancers[0].Tracks[Channels.Support];
        Assert.Equal(SegKinds.Go, support[^1].Kind);
        Assert.Equal((0.0, 1.5), (support[^1].ToX, support[^1].ToZ));
        // 6 m in two beats is more than steps reach, even two a beat.
        var far = Walk("stand:2 go.there:2 |", 6);
        Assert.Contains(far.Diagnostics, d => d.Code == "SH2026" && d.Severity == DiagnosticSeverity.Warning);
        // Written, the steps are as many as written: quicker ones are shorter ones, and too quick for their length is said too.
        Assert.Empty(Walk("stand:4 | go.there.steps(6):4 |", 1.5).Diagnostics);
        Assert.Contains(Walk("stand:2 go.there.steps(12):2 |", 6).Diagnostics, d => d.Code == "SH2026");
    }

    [Theory]
    [InlineData("formation vee { dave at 0 0 }", "stand:4 |", "SH1045")]                              // nobody of that name
    [InlineData("formation vee { alice 0 0 }", "stand:4 |", "SH1045")]                                // no `at`
    [InlineData("formation vee { alice at 0 }", "stand:4 |", "SH1045")]                               // one number
    [InlineData("formation vee { alice at 0 0  alice at 1 1 }", "stand:4 |", "SH1045")]               // twice
    [InlineData("formation side { alice at 0 0 }", "stand:4 |", "SH1045")]                            // a word of the language
    [InlineData("formation home { alice at 0 0 }", "stand:4 |", "SH1045")]                            // the formation every file has
    [InlineData("formation vee { alice at 0 1 }\nformation vee { alice at 0 2 }", "stand:4 |", "SH1045")]
    [InlineData("formation vee { alice at 0 1 }", "stand:4 | go.vea:4 |", "SH1045")]                  // no such formation
    [InlineData("formation vee { bob at 0 1 }", "stand:4 | go.vee:4 |", "SH1045")]                    // no place for alice, who dances the row
    [InlineData("formation vee { alice at 0 1 }", "stand:4 | go:4 |", "SH1012")]
    [InlineData("formation vee { alice at 0 1 }", "stand:4 | go.vee.up:4 |", "SH1012")]
    [InlineData("formation vee { alice at 0 1 }", "stand:4 | go.vee.steps:4 |", "SH1013")]
    [InlineData("formation vee { alice at 0 1 }", "stand:4 | go.vee.steps(1):4 |", "SH1013")]
    [InlineData("formation vee { alice at 0 1 }", "stand:4 | go.vee.front(30):4 |", "SH1013")]
    [InlineData("formation vee { alice at 0 1 }", "stand:4 | go.vee:4@turn(left(90)) |", "SH1010")]
    public void What_is_wrong_with_a_formation_or_a_go_is_said(string formations, string lead, string code)
    {
        var compilation = Trio(formations, lead, "stand:4 |");
        Assert.Contains(compilation.Diagnostics, d => d.Code == code && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void A_misspelt_formation_is_offered_the_one_meant()
    {
        var compilation = Trio("formation vee { alice at 0 1 }", "stand:4 | go.vea:4 |", "stand:4 |");
        Assert.Contains("vee", Assert.Single(compilation.Diagnostics).Message);
    }

    [Fact]
    public void Off_the_feet_a_go_is_said_to_need_them()
    {
        var compilation = Trio("formation vee { alice at 0 1 }", "stand:2 seat:2 | go.vee:4 |", "stand:4 | then:4 |");
        Assert.Contains(compilation.Diagnostics, d => d.Code == "SH2025");
    }
}
