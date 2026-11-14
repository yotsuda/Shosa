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

        public string[] Complete(string after)
        {
            int offset = Text.IndexOf(after, StringComparison.Ordinal) + after.Length;
            var items = Request("textDocument/completion", new { textDocument = new { uri = Uri }, position = PositionOf(offset) });
            return items.EnumerateArray().Select(i => i.GetProperty("label").GetString()!).ToArray();
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
        // tempo and time before the section, the form after it.
        Assert.Equal(3, session.Last("shosa/clip").GetProperty("reusedItems").GetInt32());

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
        Assert.Contains("leftLeg", session.Complete("section Walk {\n"));
        Assert.Equal(new[] { "Walk" }, session.Complete("form { "));

        session.Replace("down:2", "down:2@");
        var annotations = session.Complete("down:2@");
        Assert.Contains("body", annotations);
        Assert.DoesNotContain("small", annotations);
    }

    [Fact]
    public void Completion_knows_the_figures_dancers_and_definitions_of_the_file()
    {
        var session = new Session("""
            figure a "Lead"
            figure b
            dancer ann humanoid on a facing front
            pose guard { arms { fwd.high@bent } }
            move jab { leftArm { fwd guard } }
            section Greet loop {
              a { body { jab } rightArm { b. } }
            }

            """);

        Assert.Equal(new[] { "humanoid" }, session.Complete("dancer ann "));
        Assert.Equal(new[] { "a", "b" }, session.Complete("humanoid on "));
        Assert.Contains("stageLeft", session.Complete("facing "));
        Assert.Contains("inplace", session.Complete("section Greet "));
        Assert.Equal(new[] { "a", "b" }, session.Complete("section Greet loop {\n"));
        Assert.Equal(new[] { "guard", "jab", "hold", "skip", "q" }, session.Complete("a { body { "));
        Assert.Contains("hand", session.Complete("rightArm { b."));
        Assert.Contains("pose", session.Complete("\n\n"));
        Assert.Contains("Move of this file", session.Hover("jab }"));
    }

    [Fact]
    public void Hover_on_a_move_says_how_long_it_is()
    {
        var session = new Session("""
            pose guard { arms { fwd.high@bent } }
            move combo { leftArm { fwd:1/2 guard:1/2 fwd guard } rightArm { skip:2 fwd } }
            section Fight { body { combo combo:6 } }

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
