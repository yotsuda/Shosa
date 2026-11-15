using System.Text;
using Shosa.Core.Syntax;

namespace Shosa.Core.Semantics;

/// <summary>
/// Where the files a `using` line names are read from: relative to the folder of the file that
/// writes the line. <see cref="MainPath"/> is the file being compiled, when it is one (a file
/// that is named again by a file it uses is not read twice). <c>read</c> is asked first (an
/// editor's unsaved text, a test's files); otherwise the file is read from disk.
/// </summary>
public sealed class UsingSource(string? mainPath, Func<string, string?>? read = null)
{
    public string? MainPath { get; } = mainPath is null ? null : Path.GetFullPath(mainPath);

    /// <summary>The source for a .shosa file on disk.</summary>
    public static UsingSource For(string shosaFile, Func<string, string?>? read = null) => new(shosaFile, read);

    /// <summary>The text of a file, by its full path; null when it cannot be read.</summary>
    public string? Read(string path)
    {
        if (read?.Invoke(path) is { } text)
            return text;
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>A file a `using` brought in: where its text is in the combined source, and the `using` line of the main file it came by.</summary>
public sealed record IncludedFile(string Path, int Start, int End, TextSpan Using);

/// <summary>
/// `using "parts.shosa"`, as Lily# has it (its UsingExpander): the files a file uses join it,
/// as one source. The main text is the PREFIX of the combined source, so every position in the
/// file being edited stays where it is (the preview lights its words, a click finds them).
/// Each file is appended once, depth first, by full path; a file met again — a cycle — is not
/// appended again. A file that cannot be read is said (SH1039) and left out.
///
/// What is said of the text of a used file is said on the `using` line of the main file that
/// brought it in, with the file and the line it is about.
/// </summary>
public static class Usings
{
    public sealed record Combined(SyntaxTree Tree, IReadOnlyList<IncludedFile> Files, IReadOnlyList<Diagnostic> Diagnostics);

    /// <summary>True when a tree has a `using` line. They are top-level items only.</summary>
    public static bool Any(SyntaxTree tree) => tree.Root.ChildNodes(SyntaxKind.Header).Any(IsUsing);

    public static bool IsUsing(SyntaxNode header) => header.ChildTokens().FirstOrDefault()?.Text == "using";

    /// <summary>The file a `using` line names, as written; null when the line names none.</summary>
    public static SyntaxToken? FileOf(SyntaxNode header) =>
        header.ChildTokens().FirstOrDefault(t => t.Kind == SyntaxKind.String && !t.IsMissing);

    public static string Unquote(string text) => text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text;

    public static Combined Expand(SyntaxTree main, UsingSource source)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (source.MainPath is { } self)
            visited.Add(self);
        var text = new StringBuilder(main.Text);
        if (main.Text.Length > 0 && main.Text[^1] != '\n')
            text.Append('\n');
        var files = new List<IncludedFile>();
        var diagnostics = new List<Diagnostic>();
        string folder = source.MainPath is { } path ? Path.GetDirectoryName(path) ?? "." : Directory.GetCurrentDirectory();

        void Walk(SyntaxTree tree, string from, TextSpan? via, string? viaFile, int offset)
        {
            foreach (var header in tree.Root.ChildNodes(SyntaxKind.Header).Where(IsUsing))
            {
                if (FileOf(header) is not { } token)
                    continue;   // the parser has said what is missing
                string written = Unquote(token.Text);
                var at = via ?? token.Span;
                string Where(string message) => viaFile is null ? message
                    : $"In {viaFile}, line {ShosaCompilation.LineColumn(tree.Text, token.Span.Start).Line}: {message}";
                if (written.Length == 0)
                {
                    diagnostics.Add(new Diagnostic("SH1039", DiagnosticSeverity.Warning, at, Where("A `using` with no file name brings in nothing: name the file, using \"parts.shosa\".")));
                    continue;
                }
                string resolved = Path.GetFullPath(Path.Combine(from, written));
                if (visited.Contains(resolved))
                    continue;   // already in: a file is brought in once, and a cycle ends here
                if (!resolved.EndsWith(".shosa", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(new Diagnostic("SH1039", DiagnosticSeverity.Warning, at, Where($"A `using` brings in a .shosa file: '{written}' is not one, and nothing of it is used.")));
                    continue;
                }
                if (source.Read(resolved) is not { } included)
                {
                    diagnostics.Add(new Diagnostic("SH1039", DiagnosticSeverity.Warning, at, Where($"There is no file '{written}' beside this one: nothing it would bring in is here.")));
                    continue;
                }
                visited.Add(resolved);
                int start = text.Length;
                text.Append(included);
                if (included.Length > 0 && included[^1] != '\n')
                    text.Append('\n');
                var line = via ?? header.Span;
                files.Add(new IncludedFile(resolved, start, text.Length, line));
                Walk(SyntaxTree.Parse(included), Path.GetDirectoryName(resolved) ?? from, line, viaFile ?? written, start);
            }
        }

        Walk(main, folder, null, null, 0);
        if (files.Count == 0)
            return new Combined(main, files, diagnostics);
        return new Combined(SyntaxTree.Parse(text.ToString()), files, diagnostics);
    }

    /// <summary>
    /// A diagnostic as the author of the main file is to read it: one about the text of a used
    /// file is put on the `using` line it came by, and says the file and the line.
    /// </summary>
    public static Diagnostic Attribute(Diagnostic diagnostic, string combinedText, int mainLength, IReadOnlyList<IncludedFile> files, string? mainFolder)
    {
        if (diagnostic.Span.Start < mainLength)
            return diagnostic;
        // Between the two is only the line end put after the main text: that is the main file's end.
        if (files.LastOrDefault(f => f.Start <= diagnostic.Span.Start) is not { } file)
            return diagnostic with { Span = new TextSpan(mainLength, 0) };
        int line = ShosaCompilation.LineColumn(combinedText, diagnostic.Span.Start).Line - ShosaCompilation.LineColumn(combinedText, file.Start).Line + 1;
        string name = mainFolder is null ? Path.GetFileName(file.Path) : Path.GetRelativePath(mainFolder, file.Path).Replace('\\', '/');
        return diagnostic with { Span = file.Using, Message = $"In {name}, line {line}: {diagnostic.Message}" };
    }
}
