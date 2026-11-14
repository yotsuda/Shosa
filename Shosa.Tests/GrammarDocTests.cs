using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Shosa.Core;
using Shosa.Core.Output;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

public class GrammarDocTests
{
    private static string DocPath([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "..", "docs", "GRAMMAR_FOR_LLM.md");

    [Fact]
    public void The_file_in_docs_is_what_the_compiler_prints()
    {
        // Regenerate with: shosa grammar -o docs/GRAMMAR_FOR_LLM.md
        Assert.Equal(GrammarDoc.Render(), File.ReadAllText(DocPath()).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void The_complete_file_it_shows_compiles_without_a_diagnostic()
    {
        // What an AI copies first must itself be right.
        var example = Regex.Match(GrammarDoc.Render(), "```shosa\n(.*?)```", RegexOptions.Singleline).Groups[1].Value;

        Assert.Contains("phrase", example);
        Assert.Empty(ShosaCompilation.Compile(example).Diagnostics);
    }
}
