using System.Diagnostics;
using System.Text.Json;
using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Music;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;
using Shosa.Lsp;

// What an edit costs, stage by stage: the stages one at a time (parse, bind, solve, write as JSON),
// then the language server as the editor drives it — a file opened, and one character of it typed
// over again and again (DESIGN.md 120).
//
//   dotnet run -c Release --project tools\perf -- FILE.shosa [--find TEXT] [--times N] [--messages FOLDER]
//
// --find: the text whose last character is typed over (the first place it is found); without it,
// the first digit after the middle of the file. --messages: the shosa/clip messages of the file opened and of the
// first two edits, as an editor that takes patches is sent them, for out\pv\harness-perf.html to give the preview's page.

// SHOSA_FRAMES_AT_ONCE=N: the solver finds N frames of a figure on each processor (DESIGN.md 120.10); 0 for all on one.
// Whatever it is, --compare must find every number the same.
if (int.TryParse(Environment.GetEnvironmentVariable("SHOSA_FRAMES_AT_ONCE"), out int atOnce))
    Solver.FramesAtOnce = atOnce <= 0 ? int.MaxValue : atOnce;

// The binder alone, again and again, for a profiler:  Shosa.Perf.exe --bind FILE.shosa [--times N]
if (args.Length >= 2 && args[0] == "--bind")
{
    string bound = Path.GetFullPath(args[1]);
    var boundTree = SyntaxTree.Parse(File.ReadAllText(bound));
    SongSource boundSongs = SongReader.For(bound);
    int count = Array.IndexOf(args, "--times") is >= 0 and var at0 && at0 + 1 < args.Length ? int.Parse(args[at0 + 1]) : 200;
    Binder.Bind(boundTree, null, boundSongs);
    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var watch = Stopwatch.StartNew();
    for (int n = 0; n < count; n++)
        Binder.Bind(boundTree, null, boundSongs);
    Console.WriteLine(FormattableString.Invariant($"bind: {watch.Elapsed.TotalMilliseconds / count:0.00} ms, {(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore) / 1024.0 / 1024.0 / count:0.00} MB each ({count} times)"));
    return 0;
}

// What the solver allocates, by type (DESIGN.md 120.12): the runtime tells of about every 100 KB allocated, and of what.
//   Shosa.Perf.exe --allocations FILE.shosa [--times N]
if (args.Length >= 2 && args[0] == "--allocations")
    return Allocations.Run(args);

// Every number the solver gives, kept and compared (Corpus.cs).
if (args.Length >= 3 && args[0] is "--dump" or "--compare")
    return Corpus.Run(args);

string file = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--")) ?? @"samples\dance.shosa");
string? Option(string name) => Array.IndexOf(args, name) is >= 0 and var i && i + 1 < args.Length ? args[i + 1] : null;
int times = int.Parse(Option("--times") ?? "10");
string text = File.ReadAllText(file);

int at;
if (Option("--find") is { } find)
{
    at = text.IndexOf(find, StringComparison.Ordinal);
    if (at < 0)
    {
        Console.Error.WriteLine($"'{find}' is not in {file}.");
        return 1;
    }
    at += find.Length - 1;
}
else
{
    at = text.Length / 2;
    while (at < text.Length && !char.IsAsciiDigit(text[at]))
        at++;
}
int line = text[..at].Count(c => c == '\n');
int character = at - (text.LastIndexOf('\n', Math.Max(0, at - 1)) + 1);
Console.WriteLine($"{Path.GetFileName(file)}: {text.Length} characters; the edit is at line {line + 1}, column {character + 1} ('{text[at]}').");

static double Ms(Action action)
{
    var watch = Stopwatch.StartNew();
    action();
    return watch.Elapsed.TotalMilliseconds;
}

static string Stats(List<double> ms)
{
    ms.Sort();
    return FormattableString.Invariant($"median {ms[ms.Count / 2],8:0.0} ms   min {ms[0],8:0.0}   max {ms[^1],8:0.0}");
}

// ---- The stages, each alone.
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
SongSource songs = SongReader.For(file);
var usings = UsingSource.For(file);
ShosaCompilation.Compile(text, null, songs, usings);   // warm: the JIT, and the song is read once

var parse = new List<double>();
var bind = new List<double>();
var solve = new List<double>();
var write = new List<double>();
var whole = new List<double>();
long bytes = 0;
int frames = 0, figures = 0, collected = 0;
long allocated = 0;
for (int i = 0; i < times; i++)
{
    SyntaxTree tree = null!;
    parse.Add(Ms(() => tree = SyntaxTree.Parse(text)));
    ScoreIr score = null!;
    bind.Add(Ms(() => score = Binder.Bind(tree, null, songs).Item1));
    var clips = new List<MotionClip>();
    long before = GC.GetAllocatedBytesForCurrentThread();
    int collections = GC.CollectionCount(0);
    solve.Add(Ms(() =>
    {
        foreach (var ir in score.Clips)
            clips.Add(Solver.Solve(ir, []));
    }));
    allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    collected = GC.CollectionCount(0) - collections;
    write.Add(Ms(() => bytes = JsonSerializer.SerializeToUtf8Bytes(new { clips }, json).LongLength));
    whole.Add(Ms(() => ShosaCompilation.Compile(text, null, songs, usings)));
    frames = clips.Sum(c => c.Figures.Sum(f => f.Frames.Count));
    figures = clips.Sum(c => c.Figures.Count);
}
Console.WriteLine($"  parse          {Stats(parse)}");
Console.WriteLine($"  bind           {Stats(bind)}");
Console.WriteLine($"  solve          {Stats(solve)}   ({figures} figures, {frames} frames of a figure; {allocated / 1024.0 / 1024.0:0.0} MB allocated, {collected} collections)");
Console.WriteLine($"  write as JSON  {Stats(write)}   ({bytes / 1024.0 / 1024.0:0.00} MB)");
Console.WriteLine($"  compile, whole {Stats(whole)}");

// ---- The server, as the editor drives it: an editor that is sent every clip after every edit (as before
// DESIGN.md 120, and as the tests still ask), then one that keeps the clips and takes patches (the extension).
string uri = new Uri(file).AbsoluteUri;
char original = text[at];
string? messagesFolder = Option("--messages") is { } folder ? Path.GetFullPath(folder) : null;
foreach (bool patches in new[] { false, true })
{
    long sent = 0;
    int messages = 0;
    string? lastClip = null;
    var server = new LanguageServer(message =>
    {
        sent += System.Text.Encoding.UTF8.GetByteCount(message);
        messages++;
        if (message.Contains("\"method\":\"shosa/clip\""))
            lastClip = message;
    });
    void Send(object message) => server.Handle(JsonSerializer.Serialize(message));
    void Keep(string name)
    {
        if (!patches || messagesFolder is null || lastClip is null)
            return;
        Directory.CreateDirectory(messagesFolder);
        using var said = JsonDocument.Parse(lastClip);
        File.WriteAllText(Path.Combine(messagesFolder, name + ".json"), said.RootElement.GetProperty("params").GetRawText());
    }

    Console.WriteLine(patches ? "  an editor that takes patches:" : "  an editor that is sent every clip:");
    Send(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { capabilities = new { }, initializationOptions = new { clipPatches = patches } } });
    double open = Ms(() => Send(new { jsonrpc = "2.0", method = "textDocument/didOpen", @params = new { textDocument = new { uri, languageId = "shosa", version = 1, text } } }));
    Console.WriteLine(FormattableString.Invariant($"    open   {open,21:0.0} ms   ({sent / 1024.0 / 1024.0:0.00} MB sent)"));
    Keep("open");

    var edits = new List<double>();
    int version = 1;
    for (int i = 0; i < times; i++)
    {
        // The character typed over: another digit, and back.
        string typed = (i % 2 == 0 ? (char.IsAsciiDigit(original) ? (char)('0' + (original - '0' + 1) % 10) : original) : original).ToString();
        sent = 0;
        messages = 0;
        edits.Add(Ms(() => Send(new
        {
            jsonrpc = "2.0",
            method = "textDocument/didChange",
            @params = new
            {
                textDocument = new { uri, version = ++version },
                contentChanges = new[] { new { range = new { start = new { line, character }, end = new { line, character = character + 1 } }, text = typed } },
            },
        })));
        if (i < 2)
            Keep("edit" + (i + 1));
        if (i == 0 && patches && messagesFolder is not null)
        {
            // The same edit with every clip whole, to compare what the page makes of the patch with.
            string? answer = null;
            var asked = new LanguageServer(message => { if (message.Contains("\"id\":2")) answer = message; });
            asked.Handle(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { capabilities = new { }, initializationOptions = new { clipPatches = true } } }));
            string edited = text.Remove(at, 1).Insert(at, typed);
            asked.Handle(JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "textDocument/didOpen", @params = new { textDocument = new { uri, languageId = "shosa", version = 1, text = edited } } }));
            asked.Handle(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 2, method = "shosa/clip", @params = new { uri } }));
            using var said = JsonDocument.Parse(answer!);
            File.WriteAllText(Path.Combine(messagesFolder, "edit1-whole.json"), said.RootElement.GetProperty("result").GetRawText());
        }
    }
    Console.WriteLine($"    edit   {Stats(edits)}   ({sent / 1024.0:0.0} KB sent in {messages} messages)");

    // An edit that changes no movement, only where the words are: a blank line put in at the top, and taken out
    // (as a comment written above the dance is). No clip is solved again (DESIGN.md 120.8).
    var moves = new List<double>();
    for (int i = 0; i < times; i++)
    {
        sent = 0;
        messages = 0;
        var range = new { start = new { line = 0, character = 0 }, end = new { line = i % 2, character = 0 } };
        moves.Add(Ms(() => Send(new
        {
            jsonrpc = "2.0",
            method = "textDocument/didChange",
            @params = new { textDocument = new { uri, version = ++version }, contentChanges = new[] { new { range, text = i % 2 == 0 ? "\n" : "" } } },
        })));
    }
    Console.WriteLine($"    a line above {Stats(moves)}   ({sent / 1024.0:0.0} KB sent in {messages} messages)");
}
return 0;
