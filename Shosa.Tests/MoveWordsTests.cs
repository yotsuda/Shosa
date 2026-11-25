using Shosa.Core;

namespace Shosa.Tests;

/// <summary>What is wrong with a word of a move is said at that word, once, with the beats it comes at (DESIGN.md 123.5).</summary>
public class MoveWordsTests
{
    private const string Dance = """
        tempo 100
        time 4/4
        role solo
        dancer solo humanoid on solo
        move flip { arms { side:1 up:1 down:2 } }
        move far { steps { leftFoot.side(1.5m):2 stand:2 } }
        section P {
          solo {
            arms { flip:4 | flip:4 | flip:4 }
            steps { stand:4 | far:4 | stand:4 }
          }
        }
        form { P }
        score { dancer solo }
        """;

    [Fact]
    public void A_word_of_a_move_used_three_times_is_told_of_once_at_itself()
    {
        var compilation = ShosaCompilation.Compile(Dance);
        // The binder's: `down` straight from `up`.
        var opposite = Assert.Single(compilation.Diagnostics, d => d.Code == "SH2001");
        Assert.Equal("down:2", Dance.Substring(opposite.Span.Start, opposite.Span.Length));
        Assert.EndsWith("This word is in `move flip`; it is so where it comes, at beats 2, 6 and 10.", opposite.Message);
        // The solver's: a step longer than the leg.
        var far = Assert.Single(compilation.Diagnostics, d => d.Code == "SH2002");
        Assert.True(far.Span.Start > Dance.IndexOf("move far") && far.Span.Start < Dance.IndexOf("section"), "said in the move");
        Assert.Contains("This word is in `move far`", far.Message);
    }

    [Fact]
    public void A_word_written_where_it_is_danced_is_told_of_as_before()
    {
        string text = Dance.Replace("flip:4 | flip:4 | flip:4", "side:1 up:1 down:2 | side:4 | side:4");
        var opposite = Assert.Single(ShosaCompilation.Compile(text).Diagnostics, d => d.Code == "SH2001");
        Assert.Equal("down:2", text.Substring(opposite.Span.Start, opposite.Span.Length));
        Assert.DoesNotContain("This word is in", opposite.Message);
    }

    [Fact]
    public void A_line_above_the_move_moves_what_is_said_of_its_word_and_solves_nothing_again()
    {
        string after = Dance.Replace("move far", "// far apart\nmove far");
        var first = ShosaCompilation.Compile(Dance);
        var second = ShosaCompilation.Compile(after, previous: first);
        Assert.Same(first.Clip.Figures[0], second.Clip.Figures[0]);
        Assert.Equal(ShosaCompilation.Compile(after).Diagnostics, second.Diagnostics);
        var far = Assert.Single(second.Diagnostics, d => d.Code == "SH2002");
        Assert.True(far.Span.Start > after.IndexOf("move far") && far.Span.Start < after.IndexOf("section"));
    }
}
