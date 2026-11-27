using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Shosa.Core;
using Shosa.Core.Output;
using Shosa.Core.Semantics;

namespace Shosa.Tests;

/// <summary>
/// Shosa's own files of parts, `using "shosa:jazz"` (DESIGN.md 125), and the choreography guide that ends with
/// them as its catalogue (`shosa guide`, the tool shosa_guide).
/// </summary>
public class LibraryTests
{
    private static readonly string[] Styles = ["ballet", "basics", "break", "idol", "jazz", "pop"];

    private static string DocPath([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "..", "docs", "CHOREOGRAPHY_FOR_LLM.md");

    private static IEnumerable<string> Definitions(string style) =>
        Regex.Matches(Library.Read(style)!, @"^(?:pose|move) (\w+)", RegexOptions.Multiline).Select(m => m.Groups[1].Value);

    [Fact]
    public void There_are_six_styles()
    {
        Assert.Equal(Styles, Library.Names);
    }

    /// <summary>
    /// Each style's every pose and move, one after another and then all of them mirrored, in one dance that
    /// brings in all four: no name is in two of them, and each goes after any other (they start and end standing).
    /// </summary>
    [Fact]
    public void Every_pose_and_move_of_every_style_is_danced_after_the_one_before_and_mirrored_without_a_diagnostic()
    {
        var dance = new StringBuilder();
        foreach (string style in Styles)
            dance.Append("using \"shosa:").Append(style).Append("\"\n");
        dance.Append("tempo 120\ntime 4/4\nrole solo\ndancer solo humanoid on solo\n");
        var sections = new List<string>();
        foreach (string style in Styles)
            foreach (string mirror in new[] { "", "@mirror" })
            {
                string name = style + (mirror == "" ? "" : "Mirrored");
                sections.Add(name);
                dance.Append($"section {name} {{ solo {{ body {{ {string.Join(' ', Definitions(style).Select(d => d + mirror))} }} }} }}\n");
            }
        dance.Append($"form {{ {string.Join(' ', sections)} }}\nscore {{ dancer solo }}\n");

        var compiled = ShosaCompilation.Compile(dance.ToString(), null, null, new UsingSource(Path.Combine(Path.GetTempPath(), "library-test.shosa")));
        Assert.Empty(compiled.Diagnostics);
        Assert.Equal(Styles.Select(s => Library.Prefix + s), compiled.Included.Select(f => f.Name(null)));
        Assert.True(Styles.Sum(s => Definitions(s).Count()) >= 100);
    }

    [Fact]
    public void Every_pose_and_move_has_its_line_in_the_catalogue_above_it()
    {
        // The assistant chooses by these lines: `// NAME — …` right above each definition.
        foreach (string style in Styles)
        {
            string[] lines = Library.Read(style)!.ReplaceLineEndings("\n").Split('\n');
            Assert.StartsWith($"// shosa:{style} — ", lines[0]);
            for (int i = 0; i < lines.Length; i++)
            {
                if (Regex.Match(lines[i], @"^(?:pose|move) (\w+)") is not { Success: true } definition)
                    continue;
                int at = i - 1;
                while (at > 0 && lines[at].StartsWith("// ", StringComparison.Ordinal) && !lines[at].StartsWith($"// {definition.Groups[1].Value} — ", StringComparison.Ordinal))
                    at--;
                Assert.True(at >= 0 && lines[at].StartsWith($"// {definition.Groups[1].Value} — ", StringComparison.Ordinal), $"shosa:{style}: {definition.Groups[1].Value} has no line above it");
            }
        }
    }

    [Fact]
    public void A_style_Shosa_has_not_is_told_with_the_ones_it_has()
    {
        var compiled = ShosaCompilation.Compile("using \"shosa:salsa\"\n", null, null, new UsingSource(Path.Combine(Path.GetTempPath(), "x.shosa")));
        var said = Assert.Single(compiled.Diagnostics, d => d.Code == "SH1039");
        Assert.Contains("no file of parts 'shosa:salsa': there are shosa:ballet, shosa:basics, shosa:break, shosa:idol, shosa:jazz, shosa:pop", said.Message);
    }

    [Fact]
    public void Check_lists_a_style_brought_in_by_its_name()
    {
        string path = Path.Combine(Path.GetTempPath(), "check-library.shosa");
        string text = "using \"shosa:basics\"\nrole solo\ndancer solo humanoid on solo\nsection A { solo { body { stepTouch } } }\nform { A }\nscore { dancer solo }\n";
        var compiled = ShosaCompilation.Compile(text, null, null, new UsingSource(path));
        var report = CheckReport.Scores(compiled, path).ToList();
        Assert.Contains($"{path}: no problems", report);
        Assert.Contains("  using shosa:basics", report);
    }

    [Fact]
    public void The_guide_in_docs_is_what_the_compiler_prints_and_ends_with_every_style()
    {
        // Regenerate with: shosa guide -o docs/CHOREOGRAPHY_FOR_LLM.md
        string guide = ChoreographyDoc.Render();
        Assert.Equal(guide, File.ReadAllText(DocPath()).ReplaceLineEndings("\n"));
        foreach (string style in Styles)
            Assert.Contains($"### `using \"shosa:{style}\"`\n\n```shosa\n// shosa:{style} — ", guide);
    }
}
