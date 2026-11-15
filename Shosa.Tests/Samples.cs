using System.Runtime.CompilerServices;
using System.Text;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

internal static class Samples
{
    private static string Directory([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "..", "samples");

    public static IEnumerable<object[]> Names() =>
        System.IO.Directory.GetFiles(Directory(), "*.shosa").Select(f => new object[] { Path.GetFileNameWithoutExtension(f) });

    public static string Read(string name) => File.ReadAllText(Path.Combine(Directory(), name + ".shosa"));

    public const string Walk = """
        tempo 100
        time 4/4
        figure solo
        dancer solo humanoid on solo
        section Walk {
          solo {
            steps { left.fwd right.fwd left.fwd right.fwd | left.fwd right.fwd left.fwd together }
            arms  { side:2 down:2 | then:4 }
          }
        }
        form { Walk }
        score { dancer solo }
        """;

    /// <summary>The one body most tests are about: a figure and the dancer who dances it.</summary>
    public const string SoloCast = "figure solo\ndancer solo humanoid on solo\n";

    /// <summary>
    /// A whole file around the tracks of one body: a section P that `solo` dances, the form
    /// that plays it and the score that writes it out. <paramref name="before"/> is what stands
    /// above (tempo, definitions), <paramref name="score"/> what the score says besides its dancer.
    /// </summary>
    public static string Solo(string tracks, string before = "", string score = "") =>
        before + SoloCast + "section P {\n  solo {\n" + tracks + "\n  }\n}\nform { P }\nscore { dancer solo  " + score + " }\n";

    /// <summary>
    /// One movement of a file that has several, as a game character's does: a section, the
    /// form of the same name that plays it and the score that writes it out. Goes after <see cref="SoloCast"/>.
    /// </summary>
    public static string Movement(string name, string tracks, string score = "") =>
        $"section {name} {{\n  solo {{\n{tracks}\n  }}\n}}\nform {name} {{ {name} }}\nscore {name} {{ dancer solo  form {name}  {score} }}\n";

    /// <summary>The tree as text — kinds and token texts, no positions — to compare two parses.</summary>
    public static string Dump(SyntaxNode node)
    {
        var sb = new StringBuilder();
        Write(node, 0);
        return sb.ToString();

        void Write(SyntaxNode n, int depth)
        {
            sb.Append(' ', depth * 2).Append(n.Kind).Append(' ').Append(n.FullSpan.Start).Append('+').Append(n.FullSpan.Length).AppendLine();
            foreach (var child in n.Children())
            {
                if (child is SyntaxToken token)
                    sb.Append(' ', depth * 2 + 2).Append(token.Kind).Append(token.IsMissing ? " <missing>" : " '" + token.Text + "'")
                        .Append(" @").Append(token.Span.Start).AppendLine();
                else
                    Write((SyntaxNode)child, depth + 1);
            }
        }
    }
}
