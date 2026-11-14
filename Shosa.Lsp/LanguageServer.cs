using System.Globalization;
using System.Text.Json;
using Shosa.Core;
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

    private sealed class Document(SyntaxTree tree, int version)
    {
        public SyntaxTree Tree { get; set; } = tree;
        public int Version { get; set; } = version;
        public ShosaCompilation Compilation { get; set; } = ShosaCompilation.Compile(tree);
    }

    private readonly Dictionary<string, Document> _documents = [];

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
                        completionProvider = new { triggerCharacters = new[] { ".", "@" } },
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
                _documents[uri] = new Document(SyntaxTree.Parse(item.GetProperty("text").GetString() ?? ""), item.GetProperty("version").GetInt32());
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
                document.Compilation = ShosaCompilation.Compile(tree);
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
        var (words, kind) = Words.At(document.Tree, offset);
        int itemKind = kind switch
        {
            "keyword" => 14,                 // Keyword
            "track" or "property" => 10,     // Property
            "figure" or "phrase" => 6,       // Variable
            "annotation" => 20,              // EnumMember
            _ => 12,                         // Value
        };
        int order = 0;
        return words.Select(w => new
        {
            label = w.Name,
            kind = itemKind,
            detail = w.Doc,
            // Keep the table's order: it goes from the common word to the rare one.
            sortText = (order++).ToString("000", CultureInfo.InvariantCulture),
        }).ToArray();
    }

    private object? Hover(JsonElement parameters)
    {
        string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString()!;
        if (!_documents.TryGetValue(uri, out var document))
            return null;

        var tree = document.Tree;
        int offset = Offset(tree.Text, parameters.GetProperty("position"));
        if (Words.Describe(tree, offset) is not (var word, var span))
            return null;

        string value = $"**`{word.Name}`** — {word.Doc}";

        // When is it performed? The IR remembers which event each segment came from.
        var ir = document.Compilation.Ir;
        var times = document.Compilation.Score.Clips.SelectMany(c => c.Dancers).SelectMany(f => f.Tracks.Values).SelectMany(s => s)
            .Where(s => offset >= s.Src && offset <= s.Src + s.Len)
            .Select(s => (s.T0, s.T1)).Distinct().OrderBy(t => t.T0).ToList();
        if (times.Count > 0)
        {
            string When((double T0, double T1) t)
            {
                int bar = (int)Math.Floor(t.T0 / ir.BeatsPerBar) + 1;
                double beat = t.T0 - (bar - 1) * ir.BeatsPerBar + 1;
                return $"bar {bar}, beat {beat.ToString("0.##", CultureInfo.InvariantCulture)}";
            }

            double length = times[0].T1 - times[0].T0;
            value += $"\n\nStarts at {string.Join("; ", times.Take(4).Select(When))}{(times.Count > 4 ? "; …" : "")}"
                + $" and takes {length.ToString("0.##", CultureInfo.InvariantCulture)} beat{(length == 1 ? "" : "s")}.";
        }

        return new { contents = new { kind = "markdown", value }, range = Range(tree.Text, span) };
    }
}
