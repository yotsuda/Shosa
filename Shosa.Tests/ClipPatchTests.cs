using System.Text.Json;
using System.Text.Json.Nodes;
using Shosa.Lsp;

namespace Shosa.Tests;

/// <summary>
/// An editor that keeps the clips is sent what each edit changed (DESIGN.md 120): the patches, put into the
/// clips it had, must give what the server has; and edits with more waiting behind them are solved once.
/// </summary>
public class ClipPatchTests
{
    private const string Uri = "file:///test.shosa";

    private const string Duet = """
        tempo 120
        time 4/4
        role lead
        role follow
        dancer a humanoid "a.vrm" on lead at -0.5 0
        dancer b humanoid on follow at 0.5 0
        section A {
          lead {
            steps    { leftFoot.fwd rightFoot.fwd leftFoot.fwd stand | then:4 | rightFoot.side together then:2 | then:4 | }
            rightArm { down:4 | side.up(30):2 down:2 | then:4 | fwd:2 down:2 | }
          }
          follow {
            steps   { then:4 | rightFoot.back leftFoot.back stand:2 | then:4 | then:4 | }
            leftArm { down:4 | then:4 | side:2 down:2 | then:4 | }
          }
        }
        section B {
          lead { rightArm { fwd.up(20):2 down:2 | } }
          follow { leftArm { side:2 down:2 | } }
        }
        form F { A B }
        form G { B }
        score Long { form F  dancer a  dancer b }
        score Short { form G  dancer a  dancer b }

        """;

    /// <summary>An editor that takes patches: the server, and the clips as the patches leave them.</summary>
    private sealed class Editor
    {
        private readonly List<JsonNode> _sent = [];
        private readonly LanguageServer _server;
        private int _nextId = 1, _version = 1;

        public Editor(string text, bool patches = true)
        {
            _server = new LanguageServer(message => _sent.Add(JsonNode.Parse(message)!));
            Request("initialize", new { capabilities = new { }, initializationOptions = new { clipPatches = patches } });
            Text = text;
            Notify("textDocument/didOpen", new { textDocument = new { uri = Uri, languageId = "shosa", version = _version, text } });
            Take();
        }

        public string Text { get; private set; }
        /// <summary>The clips as this editor has them.</summary>
        public JsonArray Clips { get; private set; } = [];
        public int Seq { get; private set; }
        /// <summary>The `shosa/clip` messages taken so far: "whole" or "patch", and their size.</summary>
        public List<(string Kind, int Bytes)> Taken { get; } = [];
        public JsonNode? LastPatch { get; private set; }

        public void Notify(string method, object parameters, bool more = false) =>
            _server.Handle(JsonSerializer.Serialize(new { jsonrpc = "2.0", method, @params = parameters }), more);

        public JsonNode? Request(string method, object parameters)
        {
            int id = _nextId++;
            _server.Handle(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters }));
            return _sent.Last(m => m["id"]?.GetValue<int>() == id)["result"];
        }

        public void Flush()
        {
            _server.Flush();
            Take();
        }

        /// <summary>Types over a stretch of the text; with `more`, as if other messages were waiting behind it.</summary>
        public void Replace(string oldText, string newText, bool more = false)
        {
            int at = Text.IndexOf(oldText, StringComparison.Ordinal);
            Assert.True(at >= 0, $"'{oldText}' is not in the text");
            object Position(int offset) => new { line = Text[..offset].Count(c => c == '\n'), character = offset - (Text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1) };
            var change = new { range = new { start = Position(at), end = Position(at + oldText.Length) }, text = newText };
            Text = Text.Remove(at, oldText.Length).Insert(at, newText);
            Notify("textDocument/didChange", new { textDocument = new { uri = Uri, version = ++_version }, contentChanges = new[] { change } }, more);
            Take();
        }

        /// <summary>Every `shosa/clip` the server has sent since the last time, taken as the extension takes them.</summary>
        private void Take()
        {
            foreach (var message in _sent.Where(m => m["method"]?.GetValue<string>() == "shosa/clip").ToList())
            {
                _sent.Remove(message);
                var said = message["params"]!;
                int bytes = said.ToJsonString().Length;
                if (said["patch"] is { } patch)
                {
                    Assert.Equal(Seq, said["base"]!.GetValue<int>());
                    Clips = Apply(Clips, patch);
                    LastPatch = patch;
                    Taken.Add(("patch", bytes));
                }
                else
                {
                    Clips = (JsonArray)said["clips"]!.DeepClone();
                    Taken.Add(("whole", bytes));
                }
                Seq = said["seq"]!.GetValue<int>();
            }
        }

        /// <summary>What the server has: every clip, asked for whole.</summary>
        public JsonArray Whole() => (JsonArray)Request("shosa/clip", new { uri = Uri })!["clips"]!;

        // editors\vscode\src\preview\patch.js, in C#.
        private static JsonArray Apply(JsonArray clips, JsonNode patch)
        {
            var next = new JsonArray();
            foreach (var entry in patch["clips"]!.AsArray())
            {
                if (entry!["whole"] is { } whole)
                {
                    next.Add(whole.DeepClone());
                    continue;
                }
                var was = clips[entry["from"]!.GetValue<int>()]!;
                var clip = (entry["head"] ?? was).DeepClone().AsObject();
                var figures = was["figures"]!.DeepClone().AsArray();
                if (entry["figures"] is JsonArray changes)
                {
                    for (int f = 0; f < changes.Count; f++)
                    {
                        if (changes[f] is not JsonObject change)
                            continue;
                        var before = figures[f]!.AsObject();
                        var figure = (change["head"] ?? before).DeepClone().AsObject();
                        foreach (string key in new[] { "frames", "turns", "mouth", "mic", "juggle" })
                        {
                            if (before[key] is { } kept)
                                figure[key] = kept.DeepClone();
                        }
                        foreach (var (key, count) in change["counts"]?.AsObject() ?? [])
                        {
                            var rows = figure[key]!.AsArray();
                            while (rows.Count > count!.GetValue<int>())
                                rows.RemoveAt(rows.Count - 1);
                        }
                        foreach (var (key, runs) in change["rows"]?.AsObject() ?? [])
                        {
                            var rows = figure[key]!.AsArray();
                            foreach (var run in runs!.AsArray())
                            {
                                int first = run![0]!.GetValue<int>();
                                for (int i = 1; i < run.AsArray().Count; i++)
                                {
                                    if (first + i - 1 < rows.Count)
                                        rows[first + i - 1] = run[i]!.DeepClone();
                                    else
                                        rows.Add(run[i]!.DeepClone());
                                }
                            }
                        }
                        foreach (string key in new[] { "mic", "juggle" })
                        {
                            if (!change.ContainsKey(key))
                                continue;
                            if (change[key] is { } value)
                                figure[key] = value.DeepClone();
                            else
                                figure.Remove(key);
                        }
                        figures[f] = figure;
                    }
                }
                clip["figures"] = figures;
                next.Add(clip);
            }
            return next;
        }
    }

    private static void AssertSame(JsonArray expected, JsonArray actual) =>
        Assert.True(JsonNode.DeepEquals(expected, actual), "The clips the patches left are not the clips the server has.");

    [Fact]
    public void An_editor_that_takes_patches_is_sent_the_clips_whole_once_and_then_what_each_edit_changed()
    {
        var editor = new Editor(Duet);
        Assert.Equal(["whole"], editor.Taken.Select(t => t.Kind));
        Assert.Equal(2, editor.Clips.Count);
        Assert.Null(editor.Request("shosa/clip", new { uri = Uri })!["clip"]);   // not twice in one message

        editor.Replace("side.up(30)", "side.up(60)");
        Assert.Equal(["whole", "patch"], editor.Taken.Select(t => t.Kind));
        AssertSame(editor.Whole(), editor.Clips);
        // The bars of one figure's rows that moved (two of this short dance's five), not all of it.
        Assert.True(editor.Taken[1].Bytes * 3 < editor.Taken[0].Bytes, $"The patch is {editor.Taken[1].Bytes} bytes of {editor.Taken[0].Bytes}.");
    }

    [Fact]
    public void A_patch_leaves_alone_the_figure_and_the_clip_an_edit_did_not_move()
    {
        var editor = new Editor(Duet);
        // In section A alone, in the lead's arm: the other dancer of Long, and all of Short (it dances B), are as they were.
        editor.Replace("side.up(30)", "side.up(60)");
        var clips = editor.LastPatch!["clips"]!.AsArray();
        var longer = clips[0]!.AsObject();
        Assert.NotNull(longer["figures"]![0]);
        Assert.Null(longer["figures"]![1]);
        Assert.Null(clips[1]!["figures"]);
        // Of the lead's rows, only those about the bar the word is in.
        var runs = longer["figures"]![0]!["rows"]!["frames"]!.AsArray();
        int changed = runs.Sum(run => run!.AsArray().Count - 1), all = editor.Clips[0]!["figures"]![0]!["frames"]!.AsArray().Count;
        Assert.True(changed * 2 < all, $"{changed} of {all} frames were sent.");
        Assert.NotNull(longer["figures"]![0]!["rows"]!["turns"]);   // the figure with a character has its bones' turns patched too
    }

    [Fact]
    public void Patches_follow_edits_that_change_what_a_clip_is_made_of()
    {
        var editor = new Editor(Duet + "// end\n");
        void Edit(string oldText, string newText)
        {
            editor.Replace(oldText, newText);
            AssertSame(editor.Whole(), editor.Clips);
        }

        Edit("// end", "// the end");                                    // a comment: nothing moves
        Edit("tempo 120", "tempo 96");                                   // every frame, and how many there are
        Edit("score Short", "score Brief");                              // a clip renamed
        Edit("score Brief { form G  dancer a  dancer b }", "");          // a clip gone
        Edit("score Long { form F  dancer a  dancer b }", "score Long { form F  dancer a }");   // a figure gone
        Edit("dancer a humanoid \"a.vrm\"", "dancer a humanoid");        // a character taken off: no turns any more
        Edit("dancer a humanoid", "dancer a humanoid \"a.vrm\" mic right");   // and put on, with a mic
        Edit("mic right", "mic left");
        Edit(" mic left", "");
        Edit("rightArm { down:4 |", "rightArm { down:4 oops |");         // an error, and out of it
        Edit("down:4 oops |", "down:4 |");
        Edit("form F { A B }", "form F { A B A }");                      // longer
    }

    [Fact]
    public void A_dance_made_longer_or_shorter_is_patched_too_and_keeps_the_rows_before_its_end()
    {
        var editor = new Editor(Duet);
        int Frames() => editor.Clips[0]!["figures"]![0]!["frames"]!.AsArray().Count;
        int was = Frames();

        // A section more at the end: the rows of the sections before it are not sent again.
        editor.Replace("form F { A B }", "form F { A B B }");
        Assert.Equal("patch", editor.Taken[^1].Kind);
        AssertSame(editor.Whole(), editor.Clips);
        int longer = Frames();
        Assert.True(longer > was);
        var figure = editor.LastPatch!["clips"]![0]!["figures"]![0]!;
        Assert.Equal(longer, figure["counts"]!["frames"]!.GetValue<int>());
        Assert.Equal(longer, figure["counts"]!["turns"]!.GetValue<int>());
        int sent = figure["rows"]!["frames"]!.AsArray().Sum(run => run!.AsArray().Count - 1);
        Assert.True(sent < longer - was / 2, $"{sent} of {longer} frames were sent for {longer - was} more.");

        // And taken away again.
        editor.Replace("form F { A B B }", "form F { A B }");
        Assert.Equal("patch", editor.Taken[^1].Kind);
        AssertSame(editor.Whole(), editor.Clips);
        Assert.Equal(was, Frames());
        Assert.Equal(was, editor.LastPatch!["clips"]![0]!["figures"]![0]!["counts"]!["frames"]!.GetValue<int>());
    }

    [Fact]
    public void Edits_with_more_waiting_behind_them_are_solved_once()
    {
        var editor = new Editor(Duet);
        editor.Replace("side.up(30)", "side.up(4", more: true);
        editor.Replace("side.up(4", "side.up(45", more: true);
        editor.Replace("side.up(45", "side.up(45)", more: true);
        Assert.Equal(["whole"], editor.Taken.Select(t => t.Kind));       // nothing said yet

        editor.Flush();
        Assert.Equal(["whole", "patch"], editor.Taken.Select(t => t.Kind));
        AssertSame(editor.Whole(), editor.Clips);
        Assert.Contains("side.up(45)", editor.Text);

        editor.Flush();                                                  // nothing left to say
        Assert.Equal(2, editor.Taken.Count);
    }

    [Fact]
    public void A_request_between_waiting_edits_is_answered_from_the_text_as_it_is()
    {
        var editor = new Editor(Duet);
        editor.Replace("side.up(30)", "side.up(60)", more: true);
        // Asked for every clip before the edit was told: it is told first, so the patches after are made on what was answered.
        var whole = editor.Whole();
        editor.Flush();
        AssertSame(whole, editor.Clips);
        editor.Replace("side.up(60)", "side.up(20)");
        AssertSame(editor.Whole(), editor.Clips);
    }

    [Fact]
    public void An_editor_that_does_not_ask_for_patches_is_sent_every_clip_every_time()
    {
        var editor = new Editor(Duet, patches: false);
        editor.Replace("side.up(30)", "side.up(60)");
        Assert.Equal(["whole", "whole"], editor.Taken.Select(t => t.Kind));
    }
}
