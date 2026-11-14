using System.Text.Json;
using Shosa.Lsp;

namespace Shosa.Tests;

public class LanguageServerTests
{
    private const string Uri = "file:///test.shosa";

    /// <summary>A server and everything it has sent, as an editor would see it.</summary>
    private sealed class Session
    {
        private readonly List<JsonDocument> _sent = [];
        private readonly LanguageServer _server;
        private int _nextId = 1, _version = 1;

        public Session(string text)
        {
            _server = new LanguageServer(message => _sent.Add(JsonDocument.Parse(message)));
            Request("initialize", new { capabilities = new { } });
            Text = text;
            Notify("textDocument/didOpen", new { textDocument = new { uri = Uri, languageId = "shosa", version = _version, text } });
        }

        public string Text { get; private set; }

        public void Notify(string method, object parameters) =>
            _server.Handle(JsonSerializer.Serialize(new { jsonrpc = "2.0", method, @params = parameters }));

        public JsonElement Request(string method, object parameters)
        {
            int id = _nextId++;
            _server.Handle(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters }));
            return _sent.Last(m => m.RootElement.TryGetProperty("id", out var i) && i.GetInt32() == id).RootElement.GetProperty("result");
        }

        public JsonElement Last(string method) =>
            _sent.Last(m => m.RootElement.TryGetProperty("method", out var name) && name.GetString() == method).RootElement.GetProperty("params");

        public object PositionOf(int offset)
        {
            int line = Text[..offset].Count(c => c == '\n');
            return new { line, character = offset - (Text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1) };
        }

        /// <summary>Types over a stretch of the text, the way an editor reports it: as a range.</summary>
        public void Replace(string oldText, string newText)
        {
            int at = Text.IndexOf(oldText, StringComparison.Ordinal);
            Assert.True(at >= 0, $"'{oldText}' is not in the text");
            var change = new { range = new { start = PositionOf(at), end = PositionOf(at + oldText.Length) }, text = newText };
            Text = Text.Remove(at, oldText.Length).Insert(at, newText);
            Notify("textDocument/didChange", new { textDocument = new { uri = Uri, version = ++_version }, contentChanges = new[] { change } });
        }

        public string[] Complete(string after) => Items(after).Select(i => i.GetProperty("label").GetString()!).ToArray();

        public JsonElement[] Items(string after)
        {
            int at = Text.IndexOf(after, StringComparison.Ordinal);
            Assert.True(at >= 0, $"'{after}' is not in the text");
            var items = Request("textDocument/completion", new { textDocument = new { uri = Uri }, position = PositionOf(at + after.Length) });
            return items.EnumerateArray().ToArray();
        }

        public string? Hover(string on)
        {
            int offset = Text.IndexOf(on, StringComparison.Ordinal) + 1;
            var hover = Request("textDocument/hover", new { textDocument = new { uri = Uri }, position = PositionOf(offset) });
            return hover.ValueKind == JsonValueKind.Null ? null : hover.GetProperty("contents").GetProperty("value").GetString();
        }
    }

    [Fact]
    public void Opening_a_file_publishes_its_diagnostics_and_its_movement()
    {
        var session = new Session(Samples.Walk);

        Assert.Empty(session.Last("textDocument/publishDiagnostics").GetProperty("diagnostics").EnumerateArray());
        var clip = session.Last("shosa/clip").GetProperty("clip");
        Assert.Equal(8, clip.GetProperty("totalBeats").GetDouble());
        Assert.True(clip.GetProperty("figures")[0].GetProperty("frames").GetArrayLength() > 100);
    }

    [Fact]
    public void An_edit_arrives_as_a_range_and_the_rest_of_the_file_is_not_parsed_again()
    {
        var session = new Session(Samples.Walk);

        session.Replace("side:2", "sid:2");

        var diagnostic = Assert.Single(session.Last("textDocument/publishDiagnostics").GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("SH1012", diagnostic.GetProperty("code").GetString());
        Assert.Contains("'side'", diagnostic.GetProperty("message").GetString());
        // The range the editor will underline is the misspelt word.
        int line = Samples.Walk[..Samples.Walk.IndexOf("side:2", StringComparison.Ordinal)].Count(c => c == '\n');
        Assert.Equal(line, diagnostic.GetProperty("range").GetProperty("start").GetProperty("line").GetInt32());
        // tempo, time, the figure and the dancer before the section; the form and the score after it.
        Assert.Equal(6, session.Last("shosa/clip").GetProperty("reusedItems").GetInt32());

        session.Replace("sid:2", "fwd:2");
        Assert.Empty(session.Last("textDocument/publishDiagnostics").GetProperty("diagnostics").EnumerateArray());
    }

    [Fact]
    public void After_a_dot_completion_offers_what_can_follow_in_that_track()
    {
        var session = new Session(Samples.Walk);

        session.Replace("together }", "left. }");
        var afterFoot = session.Complete("left. }"[..5]);
        Assert.Contains("fwd", afterFoot);
        Assert.Contains("across", afterFoot);
        Assert.DoesNotContain("left", afterFoot);
        Assert.DoesNotContain("up", afterFoot);

        session.Replace("side:2", "side.");
        var afterSide = session.Complete("arms  { side.");
        Assert.Equal(new[] { "fwd", "back", "low", "mid", "high" }, afterSide);   // not `across`, its opposite; not left / right
    }

    [Fact]
    public void Completion_knows_where_it_is()
    {
        var session = new Session(Samples.Walk);

        Assert.Contains("turn", session.Complete("steps { "));
        Assert.DoesNotContain("hip", session.Complete("steps { "));
        Assert.Contains("hip", session.Complete("arms  { "));
        // In a section the file's figures, never a track; the tracks inside a figure's block.
        Assert.Equal(new[] { "solo", "upbeat", "tempo", "time" }, session.Complete("section Walk {\n"));   // what a section may begin with
        Assert.Contains("leftLeg", session.Complete("solo {\n"));
        Assert.Equal(new[] { "Walk", "tempo", "time" }, session.Complete("form { "));   // a tempo or a time for the section named next
        // After a whole number in the length of a hold or a skip: the unit `bar`. Of nothing else.
        Assert.Equal(new[] { "bar" }, session.Complete("| hold:4"));
        Assert.Empty(session.Complete("side:2"));
        Assert.Contains("verse", session.Complete("steps { "));

        session.Replace("down:2", "down:2@");
        var annotations = session.Complete("down:2@");
        Assert.Contains("body", annotations);
        Assert.DoesNotContain("small", annotations);

        // In a form, after `tempo` comes a number; the word says whose tempo it is.
        session.Replace("form { ", "form { tempo 97 ");
        Assert.Empty(session.Complete("form { tempo "));
        Assert.Contains("Walk", session.Complete("form { tempo 97 "));
        Assert.Contains("the section named next", session.Hover("tempo 97 "));
    }

    [Fact]
    public void Completion_knows_the_figures_dancers_and_definitions_of_the_file()
    {
        var session = new Session("""
            figure a
            figure b
            dancer ann humanoid on a facing front
            dancer ben humanoid on b

            pose guard { arms { fwd.high@bent } }
            move jab { leftArm { fwd guard } }
            section Greet {
              a { body { jab } rightArm { b. } }
            }
            form { Greet }
            form short { Greet }
            score { dancer ann  dancer ben }
            score practice { dancer ann  form short  loop }

            """);

        Assert.Equal(new[] { "humanoid" }, session.Complete("dancer ann "));
        Assert.Equal(new[] { "a", "b" }, session.Complete("humanoid on "));
        Assert.Contains("stageLeft", session.Complete("facing "));
        Assert.Empty(session.Complete("section Greet "));   // loop and inplace are said in the score
        Assert.Equal(new[] { "a", "b", "upbeat", "tempo", "time" }, session.Complete("section Greet {\n"));
        Assert.Equal(new[] { "guard", "jab", "hold", "skip", "q", "verse" }, session.Complete("a { body { "));
        Assert.DoesNotContain("verse", session.Complete("move jab { leftArm { "));   // a move is not played a second time
        Assert.DoesNotContain("verse", session.Complete("rightArm { b."));
        Assert.Contains("hand", session.Complete("rightArm { b."));
        Assert.Contains("pose", session.Complete("\n\n"));
        Assert.Contains("Move of this file", session.Hover("jab }"));

        // In a score: what it can say, the file's dancers after `dancer`, its named forms after `form`.
        Assert.Equal(new[] { "dancer", "form", "loop", "inplace", "tempo", "title", "music" }, session.Complete("score practice { "));
        Assert.Equal(new[] { "dancer", "form", "loop", "inplace", "tempo", "title", "music" }, session.Complete("form short  "));
        Assert.Equal(new[] { "ann", "ben" }, session.Complete("score practice { dancer "));
        Assert.Equal(new[] { "short" }, session.Complete("dancer ann  form "));
    }

    [Fact]
    public void A_top_level_word_is_completed_to_the_least_its_item_needs_with_the_names_of_the_file()
    {
        var session = new Session("""
            figure lead
            dancer alice humanoid on lead

            section Verse { lead { arms { side } } }
            section Chorus { lead { arms { fwd } } }

            """);
        string Snippet(string word) => session.Items("\n\n").Single(i => i.GetProperty("label").GetString() == word).GetProperty("insertText").GetString()!;

        Assert.Equal("form { ${1:Verse Chorus} }", Snippet("form"));
        Assert.Equal("score { dancer ${1:alice} }", Snippet("score"));
        Assert.Equal("dancer ${1:alice} humanoid on ${2:lead}", Snippet("dancer"));
        Assert.StartsWith("section ${1:Main} {\n  ${2:lead} {", Snippet("section"));
        // Nothing is left to add after them: the file they complete has no diagnostics.
        session.Replace("\n\n", "\nform { Verse Chorus }\nscore { dancer alice }\n");
        Assert.Empty(session.Last("textDocument/publishDiagnostics").GetProperty("diagnostics").EnumerateArray());
    }

    [Fact]
    public void Hover_on_a_move_says_how_long_it_is()
    {
        var session = new Session("""
            pose guard { arms { fwd.high@bent } }
            move combo { leftArm { fwd:1/2 guard:1/2 fwd guard } rightArm { skip:2 fwd } }
            figure solo
            dancer solo humanoid on solo
            section Fight { solo { body { combo combo:6 } } }
            form { Fight }
            score { dancer solo }

            """);

        // Its longest track: 1/2 + 1/2 + 1 + 1. Said where it is used and where it is defined.
        Assert.Contains("it takes 3 beats", session.Hover("combo combo:6"));
        Assert.Contains("it takes 3 beats", session.Hover("combo {"));
        Assert.Contains("reached in 1 beat", session.Hover("guard {"));
    }

    [Fact]
    public void Hover_says_what_a_word_means_and_when_it_is_performed()
    {
        var session = new Session(Samples.Walk);

        string? hover = session.Hover("together");
        Assert.Contains("closes to the supporting foot", hover);
        Assert.Contains("bar 2, beat 4", hover);

        Assert.Contains("Both arms at once", session.Hover("arms"));
        Assert.Null(session.Hover("100"));
    }
}
