using System.Globalization;
using System.Text.Json;
using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Music;
using Shosa.Core.Output;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

namespace Shosa.Lsp;

/// <summary>
/// The language server, without the transport: a message in, messages out through the
/// callback. That makes it a plain object a test can talk to.
///
/// Each open document keeps its last <see cref="SyntaxTree"/>. An edit arrives as a range and
/// goes through <see cref="SyntaxTree.WithChange"/>, so the phrases it did not touch are not
/// parsed again. After every change the server publishes the diagnostics and sends the solved
/// movement (`shosa/clip`) — that notification is what the preview draws.
/// </summary>
public sealed class LanguageServer(Action<string> send)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private sealed class Document(SyntaxTree tree, int version, string? folder)
    {
        public SyntaxTree Tree { get; set; } = tree;
        public int Version { get; set; } = version;
        /// <summary>Where the document's file is, and so its songs; null for a document that is not a file.</summary>
        public string? Folder { get; } = folder;
        /// <summary>Reads the songs the document names, beside its file; null for a document that is not a file.</summary>
        public SongSource? Songs { get; } = folder is null ? null : new SongReader(folder).Read;
        public ShosaCompilation Compilation { get; set; } = ShosaCompilation.Compile(tree, null, folder is null ? null : new SongReader(folder).Read);
    }

    private static string? FolderOf(string uri) => PathOf(uri) is { } path ? Path.GetDirectoryName(path) : null;

    /// <summary>
    /// The file a document's URI names, or null when it is not a file. VS Code writes the colon
    /// of a Windows drive as %3A (file:///c%3A/dir/a.shosa); .NET then gives "/c:/dir/a.shosa",
    /// which is not a path on that drive.
    /// </summary>
    private static string? PathOf(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || !parsed.IsFile)
            return null;
        string path = parsed.LocalPath;
        if (path.Length >= 3 && path[0] == '/' && char.IsAsciiLetter(path[1]) && path[2] == ':')
            path = path[1..];
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// What a `music` line is completed from: the .lys files beside the document and one folder
    /// down (as written after `music`, with '/'), and the named scores of one of them.
    /// </summary>
    private static Words.Songs? SongsFor(Document document)
    {
        if (document.Folder is not { } folder)
            return null;
        return new Words.Songs(
            () =>
            {
                try
                {
                    var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 1, IgnoreInaccessible = true };
                    return Directory.EnumerateFiles(folder, "*.lys", options).Take(200)
                        .Select(path => Path.GetRelativePath(folder, path).Replace('\\', '/'))
                        .OrderBy(name => name.Count(c => c == '/')).ThenBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return [];
                }
            },
            file => document.Songs?.Invoke(file, null, null)?.Scores.Where(name => name.Length > 0).ToList() ?? []);
    }

    private readonly Dictionary<string, Document> _documents = [];

    /// <summary>
    /// The text a request is about, compiled (one score of it, or all): the `text` it carries,
    /// or else the open document of its `uri`. Songs are read beside the file the uri names.
    /// Null when there is neither. The name is the file's, for the lines of a report.
    /// </summary>
    private (ShosaCompilation Compilation, string Text, string Name)? Compiled(JsonElement parameters, string? score)
    {
        string uri = parameters.TryGetProperty("uri", out var u) ? u.GetString() ?? "" : "";
        string? text = parameters.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()
            : _documents.TryGetValue(uri, out var document) ? document.Tree.Text : null;
        if (text is null)
            return null;
        string name = PathOf(uri) is { } path ? Path.GetFileName(path) : "file.shosa";
        return (ShosaCompilation.Compile(text, score, FolderOf(uri) is { } folder ? new SongReader(folder).Read : null), text, name);
    }

    public bool ExitRequested { get; private set; }

    public void Handle(string json)
    {
        using var message = JsonDocument.Parse(json);
        var root = message.RootElement;
        if (!root.TryGetProperty("method", out var methodElement))
            return;   // a response to something we sent; we send nothing that needs one

        string method = methodElement.GetString() ?? "";
        JsonElement? id = root.TryGetProperty("id", out var idElement) ? idElement.Clone() : null;
        var parameters = root.TryGetProperty("params", out var p) ? p : default;

        switch (method)
        {
            case "initialize":
                Reply(id, new
                {
                    capabilities = new
                    {
                        textDocumentSync = new { openClose = true, change = 2 },   // 2: incremental
                        completionProvider = new { triggerCharacters = new[] { ".", "@", "\"" } },
                        hoverProvider = true,
                    },
                    serverInfo = new { name = "shosa-lsp", version = "0.1.0" },
                });
                break;

            case "shutdown":
                Reply(id, null);
                break;

            case "exit":
                ExitRequested = true;
                break;

            case "textDocument/didOpen":
            {
                var item = parameters.GetProperty("textDocument");
                string uri = item.GetProperty("uri").GetString()!;
                _documents[uri] = new Document(SyntaxTree.Parse(item.GetProperty("text").GetString() ?? ""), item.GetProperty("version").GetInt32(), FolderOf(uri));
                Publish(uri);
                break;
            }

            case "textDocument/didChange":
            {
                var item = parameters.GetProperty("textDocument");
                string uri = item.GetProperty("uri").GetString()!;
                if (!_documents.TryGetValue(uri, out var document))
                    break;

                var tree = document.Tree;
                foreach (var change in parameters.GetProperty("contentChanges").EnumerateArray())
                {
                    string text = change.GetProperty("text").GetString() ?? "";
                    if (change.TryGetProperty("range", out var range))
                    {
                        int start = Offset(tree.Text, range.GetProperty("start"));
                        int end = Offset(tree.Text, range.GetProperty("end"));
                        tree = tree.WithChange(new TextChange(new TextSpan(start, Math.Max(0, end - start)), text));
                    }
                    else
                    {
                        tree = SyntaxTree.Parse(text);
                    }
                }

                document.Tree = tree;
                document.Version = item.GetProperty("version").GetInt32();
                document.Compilation = ShosaCompilation.Compile(tree, null, document.Songs);
                Publish(uri);
                break;
            }

            case "textDocument/didClose":
            {
                string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString()!;
                _documents.Remove(uri);
                Notify("textDocument/publishDiagnostics", new { uri, diagnostics = Array.Empty<object>() });
                break;
            }

            case "workspace/didChangeWatchedFiles":
            {
                // A song was saved (the editor watches .lys files). The dances that name a song
                // are compiled again: the reader notices by the file's time which songs to read
                // anew, so a song nobody changed costs nothing.
                foreach (var (uri, document) in _documents)
                {
                    if (document.Songs is null || !document.Tree.Text.Contains("music", StringComparison.Ordinal))
                        continue;
                    document.Compilation = ShosaCompilation.Compile(document.Tree, null, document.Songs);
                    Publish(uri);
                }
                break;
            }

            case "textDocument/completion":
                Reply(id, Complete(parameters));
                break;

            case "textDocument/hover":
                Reply(id, Hover(parameters));
                break;

            case "shosa/clip":
            {
                // The preview asks for the current movement when it opens.
                string uri = parameters.GetProperty("uri").GetString()!;
                Reply(id, _documents.TryGetValue(uri, out var document) ? ClipMessage(uri, document) : null);
                break;
            }

            case "shosa/song":
            {
                // The preview asks for the notes of the song a clip is danced to (the clip's
                // `music`: file, score, order), to sound them with the movement.
                string uri = parameters.GetProperty("uri").GetString()!;
                string? Said(string name) => parameters.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                var order = parameters.TryGetProperty("order", out var names) && names.ValueKind == JsonValueKind.Array
                    ? names.EnumerateArray().Select(n => n.GetString() ?? "").ToList()
                    : null;
                Reply(id, FolderOf(uri) is { } folder && Said("file") is not null
                    ? new SongReader(folder).Notes(new MusicIr(Said("file")!, Said("score"), order, null))
                    : new SongNotes { Error = "Only a dance that is a file has a song beside it." });
                break;
            }

            // ---- For an AI assistant in the editor (the extension's language model tools). Each
            // takes the text with the request (`text`; without it, the open document's), so it
            // reads what the editor shows, saved or not, and a file nobody has opened.

            case "shosa/grammar":
                Reply(id, new { text = Shosa.Core.Output.GrammarDoc.Render() });
                break;

            case "shosa/check":
            {
                // What `shosa check FILE [--score NAME] [--at 3,0.75s]` prints: the diagnostics, then every
                // score (or the one named) with its sections and marks, then how the body is turned at
                // the moments asked for (`at`, the same text as the command line's).
                string? Said(string key) => parameters.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                IReadOnlyList<(double, bool)> moments = [];
                if (Said("at") is { } at)
                {
                    if (CheckReport.Moments(at, out string? wrong) is not { } asked)
                    {
                        Reply(id, new { text = $"'at' takes beats, or seconds with an s, with commas between (\"3,4.5\" or \"0.75s\"), not '{wrong}'." });
                        break;
                    }
                    moments = asked;
                }
                Reply(id, Compiled(parameters, Said("score")) is var (compilation, text, name)
                    ? new { text = string.Join("\n", CheckReport.Problems(compilation, text, name).Concat(CheckReport.Scores(compilation, name)).Concat(CheckReport.At(compilation, moments))) }
                    : null);
                break;
            }

            case "shosa/sheet":
            {
                // What `shosa sheet FILE` draws: for each score (or the one named) a PNG, as base64.
                string? Said(string key) => parameters.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                double? Number(string key) => parameters.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
                if (Compiled(parameters, Said("score")) is not var (compilation, text, name))
                {
                    Reply(id, null);
                    break;
                }
                double view = Said("view") switch { "side" => 90, "back" => 180, _ => Number("view") ?? ContactSheet.FrontView };
                // `from` and `to`: a number of beats, or the command line's text ("2", "0.8s").
                double? Moment(string key, MotionClip clip) =>
                    Number(key) ?? (Said(key) is { } said && CheckReport.Moments(said, out _) is [var (value, seconds)]
                        ? seconds ? TempoMap.Beat(TempoMap.Of(clip.Tempos, clip.Tempo), value) : value
                        : null);
                Reply(id, new
                {
                    problems = string.Join("\n", CheckReport.Problems(compilation, text, name)),
                    sheets = compilation.Clips.Where(clip => clip.Figures.Count > 0 && clip.TotalBeats > 0).Select(clip => new
                    {
                        score = clip.Name,
                        beats = clip.TotalBeats,
                        png = Convert.ToBase64String(ContactSheet.Render(clip, Number("step"), Moment("from", clip) ?? 0, Moment("to", clip), view).ToPng()),
                    }),
                });
                break;
            }

            // ---- For the preview's Export buttons.

            case "shosa/export":
            {
                // What `shosa gltf|vrma|bvh FILE --score NAME` writes, as base64: one score in the
                // format asked for (`glb`, `vrma`, `bvh`). A file is named by its `figure` (null: the
                // one file; else one per figure, as the CLI's walk.a.bvh) and its `kind` (`model`, or
                // `events`: the FILE.events.json a BVH or a VRM Animation has beside it).
                string? Said(string key) => parameters.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (Compiled(parameters, Said("score") ?? "") is not var (compilation, text, name))
                {
                    Reply(id, null);
                    break;
                }
                string format = Said("format") ?? "glb";
                var files = new List<object>();
                void Add(string? figure, string kind, byte[] data) => files.Add(new { figure, kind, data = Convert.ToBase64String(data) });
                foreach (var clip in compilation.Clips.Where(clip => clip.Figures.Count > 0 && clip.TotalBeats > 0).Take(1))
                {
                    if (format == "glb")
                    {
                        Add(null, "model", Gltf.Render([clip]));
                        continue;
                    }
                    foreach (var figure in clip.Figures)
                    {
                        Add(clip.Figures.Count == 1 ? null : figure.Id, "model",
                            format == "vrma" ? Gltf.RenderVrma(clip, figure) : System.Text.Encoding.UTF8.GetBytes(Bvh.Render(clip, figure)));
                    }
                    if (Gltf.EventsJson(clip) is { } events)
                        Add(null, "events", System.Text.Encoding.UTF8.GetBytes(events));
                }
                Reply(id, new { problems = string.Join("\n", CheckReport.Problems(compilation, text, name)), files });
                break;
            }

            default:
                if (id is not null)
                {
                    Send(new { jsonrpc = "2.0", id, error = new { code = -32601, message = $"Method not found: {method}" } });
                }
                break;
        }
    }

    // ---- Out.

    private void Send(object message) => send(JsonSerializer.Serialize(message, Json));

    private void Reply(JsonElement? id, object? result)
    {
        if (id is not null)
            Send(new { jsonrpc = "2.0", id, result });
    }

    private void Notify(string method, object parameters) => Send(new { jsonrpc = "2.0", method, @params = parameters });

    private void Publish(string uri)
    {
        var document = _documents[uri];
        string text = document.Tree.Text;
        var diagnostics = document.Compilation.Diagnostics.Select(d => new
        {
            // A zero-width span (something missing) would not show; give it one character.
            range = Range(text, d.Span.Length > 0 ? d.Span : new TextSpan(Math.Max(0, Math.Min(d.Span.Start, text.Length) - 1), 1)),
            severity = d.Severity switch { DiagnosticSeverity.Error => 1, DiagnosticSeverity.Warning => 2, _ => 3 },
            code = d.Code,
            source = "shosa",
            message = d.Message,
        });
        Notify("textDocument/publishDiagnostics", new { uri, version = document.Version, diagnostics });
        Notify("shosa/clip", ClipMessage(uri, document));
    }

    private static object ClipMessage(string uri, Document document) => new
    {
        uri,
        version = document.Version,
        // Every clip of the file; `clip` is the first, for a view that shows one.
        clip = document.Compilation.Clip,
        clips = document.Compilation.Clips,
        reusedItems = document.Tree.ReusedItems,
    };

    // ---- Positions. LSP counts UTF-16 code units, which is what a C# string index is.

    private static int Offset(string text, JsonElement position)
    {
        int line = position.GetProperty("line").GetInt32(), character = position.GetProperty("character").GetInt32();
        int offset = 0;
        for (int i = 0; i < line; i++)
        {
            int next = text.IndexOf('\n', offset);
            if (next < 0)
                return text.Length;
            offset = next + 1;
        }
        int lineEnd = text.IndexOf('\n', offset);
        if (lineEnd < 0)
            lineEnd = text.Length;
        return Math.Min(offset + character, lineEnd);
    }

    private static object Position(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        int line = 0, lineStart = 0;
        for (int i = 0; i < offset; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }
        return new { line, character = offset - lineStart };
    }

    private static object Range(string text, TextSpan span) => new { start = Position(text, span.Start), end = Position(text, span.End) };

    // ---- Features.

    private object? Complete(JsonElement parameters)
    {
        string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString()!;
        if (!_documents.TryGetValue(uri, out var document))
            return null;

        int offset = Offset(document.Tree.Text, parameters.GetProperty("position"));
        string text = document.Tree.Text;
        IEnumerable<Vocabulary.Word> words;
        string kind;
        int? from = null;
        if (Words.AtMusic(document.Tree, offset, SongsFor(document)) is { } music)
            (words, kind, from) = music;
        else
            (words, kind) = Words.At(document.Tree, offset);
        int itemKind = kind switch
        {
            "keyword" => 14,                 // Keyword
            "file" => 17,                    // File
            "track" or "property" => 10,     // Property
            "figure" or "phrase" => 6,       // Variable
            "annotation" => 20,              // EnumMember
            _ => 12,                         // Value
        };
        int order = 0;
        return words.Select(w =>
        {
            // Keep the table's order: it goes from the common word to the rare one.
            string sortText = (order++).ToString("000", CultureInfo.InvariantCulture);
            // A top-level word brings the least its item needs: nothing of a file can be left out.
            if (kind == "keyword" && Words.Snippet(document.Tree, w.Name) is { } snippet)
                return new { label = w.Name, kind = itemKind, detail = w.Doc, sortText, insertText = snippet, insertTextFormat = 2 };
            if (kind == "word" && w.Name == "verse")
                return new { label = w.Name, kind = 14, detail = w.Doc, sortText, insertText = Words.VerseSnippet, insertTextFormat = 2 };
            if (from is not { } start)
                return (object)new { label = w.Name, kind = itemKind, detail = w.Doc, sortText };
            // A file's name replaces everything typed since the quote, dots and all.
            return new
            {
                label = w.Name, kind = itemKind, detail = w.Doc, sortText,
                filterText = w.Name,
                textEdit = new { range = Range(text, new TextSpan(start, offset - start)), newText = w.Name },
            };
        }).ToArray();
    }

    private object? Hover(JsonElement parameters)
    {
        string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString()!;
        if (!_documents.TryGetValue(uri, out var document))
            return null;

        var tree = document.Tree;
        int offset = Offset(tree.Text, parameters.GetProperty("position"));
        if (Words.Describe(tree, offset, document.Compilation.Score.MoveLengths) is not (var word, var span))
            return null;

        string value = $"**`{word.Name}`** — {word.Doc}";

        // When is it performed? The IR remembers which event each segment came from.
        var ir = document.Compilation.Ir;
        var times = document.Compilation.Score.Clips.SelectMany(c => c.Dancers).SelectMany(f => f.Tracks.Values).SelectMany(s => s)
            .Where(s => offset >= s.Src && offset <= s.Src + s.Len)
            .Select(s => (T0: s.At, T1: s.Until)).Distinct().OrderBy(t => t.T0).ToList();
        if (times.Count > 0)
        {
            string When((double T0, double T1) t)
            {
                // The bars of the clip the event is in: a song's upbeat moves them off the multiples of the bar.
                var clip = document.Compilation.Score.Clips.FirstOrDefault(c => c.Dancers.SelectMany(f => f.Tracks.Values).SelectMany(s => s)
                    .Any(s => s.At == t.T0 && offset >= s.Src && offset <= s.Src + s.Len)) ?? ir;
                var (bar, beat) = Bars.At(clip.Sections, Bars.Of(clip.Meters, clip.BeatsPerBar, clip.BeatUnit), t.T0);
                return $"bar {bar}, beat {(beat + 1).ToString("0.##", CultureInfo.InvariantCulture)}";
            }

            double length = times[0].T1 - times[0].T0;
            value += $"\n\nThere at {string.Join("; ", times.Take(4).Select(When))}{(times.Count > 4 ? "; …" : "")}"
                + $", for {length.ToString("0.##", CultureInfo.InvariantCulture)} beat{(length == 1 ? "" : "s")}.";
        }

        return new { contents = new { kind = "markdown", value }, range = Range(tree.Text, span) };
    }
}
