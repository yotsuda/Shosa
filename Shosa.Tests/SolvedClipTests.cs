using System.Text.Json;
using Shosa.Core;
using Shosa.Core.Semantics;

namespace Shosa.Tests;

/// <summary>
/// A compilation made after another does not solve again the clips the edit left as they were but for where
/// their words are (DESIGN.md 120.8): the figures are the same objects, and everything else — the spans, what
/// the solver said and where — is what compiling the text alone gives.
/// </summary>
public class SolvedClipTests
{
    private const string Two = """
        tempo 120
        time 4/4
        role solo
        dancer solo humanoid on solo
        section A {
          solo {
            steps    { leftFoot.fwd rightFoot.fwd leftFoot.fwd stand | then:4 | }
            rightArm { down:4 | side.up(30):2 down:2 | }
          }
        }
        // between
        section B {
          solo { rightArm { fwd.up(20):2 down:2 | } }
        }
        form F { A B }
        form G { B }
        score Long { form F  dancer solo }
        score Short { form G  dancer solo }

        """;

    /// <summary>Hands too fast to perform: the solver says so (SH2003) at the words.</summary>
    private static readonly string Fast = Samples.Solo("    arms { up:1/4 down:1/4 up:1/4 down:1/4 up:1/4 down:1/4 up:1/4 down:1/4 | }", "tempo 200\ntime 4/4\n");

    /// <summary>The second compilation is, in everything that can be seen, the text compiled alone.</summary>
    private static void AssertAsAlone(ShosaCompilation alone, ShosaCompilation after)
    {
        Assert.Equal(alone.Diagnostics, after.Diagnostics);
        Assert.Equal(JsonSerializer.Serialize(alone.Score), JsonSerializer.Serialize(after.Score));
        Assert.Equal(alone.Clips.Count, after.Clips.Count);
        for (int c = 0; c < alone.Clips.Count; c++)
        {
            Assert.Equal(JsonSerializer.Serialize(alone.Clips[c]), JsonSerializer.Serialize(after.Clips[c]));
            for (int f = 0; f < alone.Clips[c].Figures.Count; f++)
            {
                var (x, y) = (alone.Clips[c].Figures[f], after.Clips[c].Figures[f]);
                Assert.Equal(x.Yaws, y.Yaws);
                Assert.Equal(x.Facing, y.Facing);
                Assert.Equal(x.Rotations.Count, y.Rotations.Count);
                for (int i = 0; i < x.Rotations.Count; i++)
                    Assert.True(x.Rotations[i].AsSpan().SequenceEqual(y.Rotations[i]));
            }
        }
    }

    /// <summary>How many clips of the second compilation have the figures of the first, the same objects.</summary>
    private static int Kept(ShosaCompilation before, ShosaCompilation after) =>
        after.Clips.Count(clip => clip.Figures.Count > 0 && before.Clips.Any(was => was.Figures.SequenceEqual(clip.Figures, ReferenceEqualityComparer.Instance)));

    private static ShosaCompilation Edited(string before, string after, out ShosaCompilation first)
    {
        first = ShosaCompilation.Compile(before);
        var second = ShosaCompilation.Compile(after, previous: first);
        AssertAsAlone(ShosaCompilation.Compile(after), second);
        return second;
    }

    [Fact]
    public void A_comment_above_the_dance_solves_nothing_again_and_moves_the_spans()
    {
        var second = Edited(Two, "// a line above\n" + Two, out var first);
        Assert.Equal(2, Kept(first, second));
        int moved = "// a line above\n".Length;
        Assert.Equal(first.Clips[0].Spans.Select(s => s.Start + moved), second.Clips[0].Spans.Select(s => s.Start));
        Assert.NotSame(first.Clips[0], second.Clips[0]);
    }

    [Fact]
    public void An_edit_between_the_words_moves_only_the_words_after_it()
    {
        var second = Edited(Two, Two.Replace("// between", "// between the two sections"), out var first);
        Assert.Equal(2, Kept(first, second));
    }

    [Fact]
    public void A_word_written_another_way_that_means_the_same_solves_nothing_again()
    {
        // Longer by a character, and the same movement: the span has the new length.
        var second = Edited(Two, Two.Replace("side.up(30):2", "side.up(30.0):2"), out var first);
        Assert.Equal(2, Kept(first, second));
    }

    [Fact]
    public void An_edit_of_a_section_solves_again_only_the_scores_that_dance_it()
    {
        var second = Edited(Two, Two.Replace("side.up(30)", "side.up(60)"), out var first);
        Assert.Equal(1, Kept(first, second));
        Assert.True(first.Clips[1].Figures.SequenceEqual(second.Clips[1].Figures, ReferenceEqualityComparer.Instance));   // Short dances B alone
        Assert.NotSame(first.Clips[0].Figures[0], second.Clips[0].Figures[0]);
    }

    [Fact]
    public void A_change_of_what_is_danced_is_solved()
    {
        Assert.Equal(0, Kept(ShosaCompilation.Compile(Two), Edited(Two, Two.Replace("tempo 120", "tempo 96"), out _)));
        Assert.Equal(0, Kept(ShosaCompilation.Compile(Two), Edited(Two, Two.Replace("dancer solo humanoid", "dancer solo humanoid \"a.vrm\""), out _)));
        var second = Edited(Two, Two.Replace("score Short { form G", "score Short { form F"), out var first);
        Assert.Equal(1, Kept(first, second));
    }

    [Fact]
    public void What_the_solver_said_is_said_again_where_the_words_are_now()
    {
        var second = Edited(Fast, "// faster than hands go\n\n" + Fast, out var first);
        Assert.Equal(1, Kept(first, second));
        Assert.Contains(first.Diagnostics, d => d.Code == "SH2003");
        Assert.Equal(first.Diagnostics.Select(d => (d.Code, d.Span.Start + 25, d.Span.Length, d.Message)),
            second.Diagnostics.Select(d => (d.Code, d.Span.Start, d.Span.Length, d.Message)));
    }

    [Fact]
    public void An_error_typed_and_taken_out_again_comes_back_to_what_it_was()
    {
        var first = ShosaCompilation.Compile(Two);
        string broken = Two.Replace("rightArm { down:4 |", "rightArm { down:4 oops |");
        var second = ShosaCompilation.Compile(broken, previous: first);
        AssertAsAlone(ShosaCompilation.Compile(broken), second);
        var third = ShosaCompilation.Compile(Two, previous: second);
        AssertAsAlone(first, third);
    }

    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_sample_with_a_line_put_in_is_as_compiled_alone(string name)
    {
        string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(SolvedClipTests).Assembly.Location)!, "..", "..", "..", "..", "samples", name + ".shosa"));
        string text = File.ReadAllText(path);
        ShosaCompilation Compile(string of, ShosaCompilation? previous = null) => ShosaCompilation.Compile(of, null, null, UsingSource.For(path), previous);
        var first = Compile(text);
        int solvable = first.Clips.Count(clip => clip.Figures.Count > 0);

        // Above everything: every word moves, no movement changes.
        string above = "// a line above\n" + text;
        var second = Compile(above, first);
        AssertAsAlone(Compile(above), second);
        Assert.Equal(solvable, Kept(first, second));

        // In the middle, at the end of a line: the words after it move.
        int middle = text.IndexOf('\n', text.Length / 2) + 1;
        string between = text[..middle] + "\n" + text[middle..];
        var third = Compile(between, second);
        AssertAsAlone(Compile(between), third);
        Assert.Equal(solvable, Kept(first, third));
    }
}
