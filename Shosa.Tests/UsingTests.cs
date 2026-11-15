using System.Text.Json;
using Shosa.Core;
using Shosa.Core.Output;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;
using Shosa.Lsp;

namespace Shosa.Tests;

/// <summary>`using "parts.shosa"`: another file joins this one, as in Lily# (DESIGN.md 72 節).</summary>
public class UsingTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "shosa-using-tests");
    private static string At(string file) => Path.GetFullPath(Path.Combine(Root, file));

    private const string Parts = """
        // The parts of one character.
        pose guard { steps { left.fwd.low@small }  arms { fwd.25@bent } }
        move jab   { leftArm { fwd.25@sharp!hit guard } }

        """;

    private const string Combo = """
        using "hero.parts.shosa"

        figure hero
        dancer hero humanoid on hero
        section Combo { hero { body { guard jab:1 jab:2 | } } }
        form { Combo }
        score { dancer hero }

        """;

    /// <summary>Compiles the main file with the files of a little folder that is not on disk.</summary>
    private static ShosaCompilation Compile(string main, Dictionary<string, string> files, string name = "combo.shosa") =>
        ShosaCompilation.Compile(main, null, null, new UsingSource(At(name), path =>
            files.FirstOrDefault(f => string.Equals(At(f.Key), path, StringComparison.OrdinalIgnoreCase)).Value));

    [Fact]
    public void The_poses_and_moves_of_a_file_of_parts_are_danced_as_if_written_here()
    {
        var used = Compile(Combo, new() { ["hero.parts.shosa"] = Parts });
        var inline = ShosaCompilation.Compile(Combo.Replace("using \"hero.parts.shosa\"", Parts));

        Assert.Empty(used.Diagnostics);
        Assert.Equal(inline.Clip.Figures[0].Frames.Count, used.Clip.Figures[0].Frames.Count);
        for (int f = 0; f < inline.Clip.Figures[0].Frames.Count; f++)
            Assert.Equal(inline.Clip.Figures[0].Frames[f], used.Clip.Figures[0].Frames[f]);
        Assert.Equal(2, used.Clip.Marks.Count);
        Assert.Equal(At("hero.parts.shosa"), Assert.Single(used.Included).Path, ignoreCase: true);
    }

    [Fact]
    public void Nothing_of_a_used_file_is_lit_in_the_text_being_edited()
    {
        var used = Compile(Combo, new() { ["hero.parts.shosa"] = Parts });

        Assert.All(used.Clip.Spans, s => Assert.True(s.Start < Combo.Length));
        Assert.Contains(used.Clip.Spans, s => Combo.Substring(s.Start, s.Length) == "jab:1");
    }

    [Fact]
    public void A_file_of_parts_needs_no_score_and_check_says_what_it_offers()
    {
        var parts = Compile(Parts, [], "hero.parts.shosa");

        Assert.Empty(parts.Diagnostics);
        Assert.Empty(parts.Clips);
        Assert.Contains("  a file of parts, for using: pose guard, move jab (2 beats)", CheckReport.Scores(parts, At("hero.parts.shosa")));
        // A file with a section is still a dance, and a dance needs a score.
        Assert.Contains(Compile(Parts + "figure hero\n", [], "x.shosa").Diagnostics, d => d.Code == "SH1030");
    }

    [Fact]
    public void Check_lists_the_files_brought_in()
    {
        var used = Compile(Combo, new() { ["hero.parts.shosa"] = Parts });
        Assert.Contains("  using hero.parts.shosa", CheckReport.Scores(used, At("combo.shosa")));
    }

    [Fact]
    public void A_file_that_is_not_there_is_said_on_its_line()
    {
        var used = Compile(Combo, []);

        var missing = Assert.Single(used.Diagnostics, d => d.Code == "SH1039");
        Assert.Equal(DiagnosticSeverity.Warning, missing.Severity);
        Assert.Equal("\"hero.parts.shosa\"", Combo.Substring(missing.Span.Start, missing.Span.Length));
        Assert.Contains(used.Diagnostics, d => d.Code == "SH1019");   // and so `guard` is not known
    }

    [Fact]
    public void What_is_wrong_in_a_used_file_is_said_on_the_using_line_with_its_file_and_line()
    {
        var used = Compile(Combo, new() { ["hero.parts.shosa"] = Parts.Replace("fwd.25@sharp!hit", "fwd.25@shrap!hit") });

        var wrong = Assert.Single(used.Diagnostics, d => d.Code == "SH1010");
        Assert.StartsWith("In hero.parts.shosa, line 3: ", wrong.Message);
        Assert.Equal("using \"hero.parts.shosa\"", Combo.Substring(wrong.Span.Start, wrong.Span.Length));
    }

    [Fact]
    public void A_name_is_defined_once_in_all_the_files()
    {
        var used = Compile("pose guard { arms { up } }\n" + Combo, new() { ["hero.parts.shosa"] = Parts });

        var twice = Assert.Single(used.Diagnostics, d => d.Code == "SH1005");
        Assert.StartsWith("In hero.parts.shosa, line 2: 'guard'", twice.Message);
    }

    [Fact]
    public void Files_brought_in_bring_in_theirs_from_their_own_folder_and_each_comes_once()
    {
        var files = new Dictionary<string, string>
        {
            ["parts/hero.shosa"] = "using \"basics.shosa\"\nusing \"../combo.shosa\"\n" + Parts,
            ["parts/basics.shosa"] = "using \"hero.shosa\"\nmove nod { head { fwd.-25 fwd } }\n",
        };
        var main = Combo.Replace("hero.parts.shosa", "parts/hero.shosa").Replace("jab:2 |", "nod:2 |") + "using \"parts/basics.shosa\"\n";

        var used = Compile(main, files);

        Assert.Empty(used.Diagnostics);
        Assert.Equal(["parts/hero.shosa", "parts/basics.shosa"], used.Included.Select(f => Path.GetRelativePath(Root, f.Path).Replace('\\', '/')));
    }

    [Fact]
    public void Without_a_source_a_using_line_brings_in_nothing_and_is_no_error_of_its_own()
    {
        var alone = ShosaCompilation.Compile(Combo);
        Assert.DoesNotContain(alone.Diagnostics, d => d.Code is "SH0011" or "SH1039");
        Assert.Contains(alone.Diagnostics, d => d.Code == "SH1019");
    }

    /// <summary>An editor with two files open, each with a path: a file of parts and a dance that brings it in.</summary>
    private sealed class Editor
    {
        private readonly List<JsonDocument> _sent = [];
        private readonly LanguageServer _server;
        private int _id = 1;

        public Editor()
        {
            _server = new LanguageServer(message => _sent.Add(JsonDocument.Parse(message)));
            Send("initialize", new { capabilities = new { } }, request: true);
        }

        public static string UriOf(string file) => new Uri(At(file)).AbsoluteUri;

        public void Open(string file, string text) =>
            Send("textDocument/didOpen", new { textDocument = new { uri = UriOf(file), languageId = "shosa", version = 1, text } });

        public void Change(string file, string text, int version) =>
            Send("textDocument/didChange", new { textDocument = new { uri = UriOf(file), version }, contentChanges = new[] { new { text } } });

        public JsonElement Send(string method, object parameters, bool request = false)
        {
            int id = _id++;
            _server.Handle(request
                ? JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters })
                : JsonSerializer.Serialize(new { jsonrpc = "2.0", method, @params = parameters }));
            return request ? _sent.Last(m => m.RootElement.TryGetProperty("id", out var i) && i.GetInt32() == id).RootElement.GetProperty("result") : default;
        }

        public string[] Codes(string file) => _sent
            .Where(m => m.RootElement.TryGetProperty("method", out var name) && name.GetString() == "textDocument/publishDiagnostics")
            .Last(m => m.RootElement.GetProperty("params").GetProperty("uri").GetString() == UriOf(file))
            .RootElement.GetProperty("params").GetProperty("diagnostics").EnumerateArray().Select(d => d.GetProperty("code").GetString()!).ToArray();
    }

    [Fact]
    public void In_the_editor_a_dance_follows_the_unsaved_text_of_its_file_of_parts()
    {
        var editor = new Editor();
        editor.Open("hero.parts.shosa", Parts);
        editor.Open("combo.shosa", Combo);
        Assert.Empty(editor.Codes("combo.shosa"));

        editor.Change("hero.parts.shosa", Parts.Replace("move jab", "move punch"), 2);
        Assert.Contains("SH1019", editor.Codes("combo.shosa"));

        editor.Change("hero.parts.shosa", Parts, 3);
        Assert.Empty(editor.Codes("combo.shosa"));
    }

    [Fact]
    public void In_the_editor_the_words_of_a_file_of_parts_are_completed_and_described()
    {
        var editor = new Editor();
        editor.Open("hero.parts.shosa", Parts);
        string text = Combo.Replace("jab:2 |", "j|");
        editor.Open("combo.shosa", text);
        int at = text.IndexOf("j|", StringComparison.Ordinal) + 1;
        object Position(int offset) => new { line = text[..offset].Count(c => c == '\n'), character = offset - (text.LastIndexOf('\n', offset - 1) + 1) };

        var items = editor.Send("textDocument/completion", new { textDocument = new { uri = Editor.UriOf("combo.shosa") }, position = Position(at) }, request: true);
        var jab = items.EnumerateArray().Single(i => i.GetProperty("label").GetString() == "jab");
        Assert.Contains("Move of hero.parts.shosa", jab.GetProperty("detail").GetString());

        var hover = editor.Send("textDocument/hover", new { textDocument = new { uri = Editor.UriOf("combo.shosa") }, position = Position(text.IndexOf("guard", StringComparison.Ordinal) + 1) }, request: true);
        Assert.Contains("Pose of hero.parts.shosa", hover.GetProperty("contents").GetProperty("value").GetString());
    }
}
