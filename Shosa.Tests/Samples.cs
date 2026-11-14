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
        section Walk {
          steps { left.fwd right.fwd left.fwd right.fwd | left.fwd right.fwd left.fwd together }
          arms  { side:2 down:2 | hold:4 }
        }
        form { Walk }
        """;

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
