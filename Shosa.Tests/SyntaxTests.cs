using Shosa.Core;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

public class SyntaxTests
{
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void A_sample_compiles_without_errors_and_the_tree_reproduces_its_text(string name)
    {
        string text = Samples.Read(name);
        var compilation = ShosaCompilation.Compile(text);

        Assert.Equal(text, compilation.Tree.ToFullString());
        Assert.DoesNotContain(compilation.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.True(compilation.Ir.TotalBeats > 0);
    }

    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void A_file_cut_off_anywhere_still_parses_and_loses_no_character(string name)
    {
        // What the editor sees while a file is being typed: every prefix of a real one.
        string text = Samples.Read(name);
        for (int length = 0; length <= text.Length; length += 3)
        {
            string prefix = text[..length];
            var compilation = ShosaCompilation.Compile(prefix);
            Assert.Equal(prefix, compilation.Tree.ToFullString());
        }
    }

    [Fact]
    public void The_new_shapes_parse_into_their_own_nodes()
    {
        const string text = """
            figure mage
            dancer mage humanoid on mage at 0 1 free
            pose guard { arms { fwd.high@bent } }
            move jab { leftArm { fwd.-10:0.3s@sharp!hit guard:12f } }
            section Idle {
              mage {
                body  { guard:0 | { jab:1/2 jab:1/2 } & { skip:1 } hold:3 }
                steps { left.fwd@turn(left.quarter) lift.fwd(0.4)@free land@free hold }
              }
            }
            form short { Idle }
            score practice { dancer mage  form short  tempo 80  loop inplace }

            """;
        var tree = SyntaxTree.Parse(text);

        Assert.Empty(tree.Diagnostics);
        Assert.Equal(text, tree.ToFullString());
        int Count(SyntaxKind kind) => tree.Root.DescendantNodes().Count(n => n.Kind == kind);
        Assert.Equal(3, Count(SyntaxKind.Clause));          // on mage, at 0 1, free; and nothing taken from the next line
        Assert.Equal(1, Count(SyntaxKind.FigureBlock));
        Assert.Equal(1, Count(SyntaxKind.Overlay));
        Assert.Equal(2, Count(SyntaxKind.Sequence));
        Assert.Equal(1, Count(SyntaxKind.Mark));
        Assert.Equal(2, Count(SyntaxKind.Arguments));       // @turn(left.quarter), lift.fwd(0.4)
        Assert.Equal(5, Count(SyntaxKind.ScoreItem));       // dancer, form, tempo, loop, inplace
        // The unit of a length is part of the length; a number can stand where a level does.
        var durations = tree.Root.DescendantNodes().Where(n => n.Kind == SyntaxKind.Duration).Select(n => n.ToString().Trim()).ToList();
        Assert.Contains(":0.3s", durations);
        Assert.Contains(":12f", durations);
    }

    [Fact]
    public void Characters_removed_from_the_middle_do_not_break_the_parser()
    {
        string text = Samples.Read("taiso");
        var random = new Random(7);
        for (int i = 0; i < 300; i++)
        {
            int start = random.Next(text.Length), length = Math.Min(random.Next(1, 12), text.Length - start);
            string damaged = text.Remove(start, length);
            Assert.Equal(damaged, ShosaCompilation.Compile(damaged).Tree.ToFullString());
        }
    }

    [Fact]
    public void An_unclosed_track_does_not_swallow_the_track_after_it()
    {
        const string text = """
            figure a
            section P {
              a {
                steps { left.fwd right.fwd
                arms  { side down }
              }
            }
            """;
        var tree = SyntaxTree.Parse(text);

        Assert.Single(tree.Diagnostics);
        var tracks = tree.Root.DescendantNodes().Where(n => n.Kind == SyntaxKind.Track).ToList();
        Assert.Equal(new[] { "steps", "arms" }, tracks.Select(t => t.Token(SyntaxKind.Identifier)!.Text).ToArray());
    }

    [Fact]
    public void A_dot_at_the_end_of_a_line_does_not_take_the_next_line_s_word()
    {
        const string text = "section P { solo { arms { side.\n down } } }\n";
        var tree = SyntaxTree.Parse(text);

        var events = tree.Root.DescendantNodes().Where(n => n.Kind == SyntaxKind.Event).ToList();
        Assert.Equal(2, events.Count);
        Assert.Contains(tree.Diagnostics, d => d.Code == "SH0010");
    }

    [Fact]
    public void An_edit_inside_one_section_reuses_every_other_item()
    {
        string text = Samples.Read("taiso");
        var tree = SyntaxTree.Parse(text);
        int at = text.IndexOf("left.high", StringComparison.Ordinal);

        var edited = tree.WithChange(new TextChange(new TextSpan(at, 4), "right"));
        var full = SyntaxTree.Parse(edited.Text);

        Assert.Equal(Samples.Dump(full.Root), Samples.Dump(edited.Root));
        // title, tempo, time, the figure, the dancer and section Stretch before the edit; the form and the score after it.
        Assert.Equal(8, edited.ReusedItems);
    }

    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Incremental_and_full_parses_agree_through_a_run_of_random_edits(string name)
    {
        string[] inserts = ["x", " ", "{", "}", "|", ".", ":2", "@", "\n", "\"", "left.fwd ", "section Q {", "// c\n", "form",
            "&", "!hit", "(", ")", "dancer x humanoid ", "pose g {", "move m {", "score ", "music \"a\" ", "-3", "s"];
        var random = new Random(name.Length * 31 + 5);
        var tree = SyntaxTree.Parse(Samples.Read(name));

        for (int i = 0; i < 400; i++)
        {
            string text = tree.Text;
            int start = random.Next(text.Length + 1);
            int length = random.Next(3) == 0 ? 0 : Math.Min(random.Next(1, 6), text.Length - start);
            string insert = random.Next(3) == 0 ? "" : inserts[random.Next(inserts.Length)];

            tree = tree.WithChange(new TextChange(new TextSpan(start, length), insert));
            var full = SyntaxTree.Parse(tree.Text);

            Assert.Equal(tree.Text, tree.ToFullString());
            Assert.Equal(Samples.Dump(full.Root), Samples.Dump(tree.Root));
            Assert.Equal(full.Diagnostics, tree.Diagnostics);
        }
    }
}
