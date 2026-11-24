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
//   dotnet run -c Release --project tools\perf -- FILE.shosa [--find TEXT] [--times N]
//
// --find: the text whose last character is typed over (the first place it is found); without it,
// the first digit after the middle of the file.

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
int frames = 0, figures = 0;
for (int i = 0; i < times; i++)
{
    SyntaxTree tree = null!;
    parse.Add(Ms(() => tree = SyntaxTree.Parse(text)));
    ScoreIr score = null!;
    bind.Add(Ms(() => score = Binder.Bind(tree, null, songs).Item1));
    var clips = new List<MotionClip>();
    solve.Add(Ms(() =>
    {
        foreach (var ir in score.Clips)
            clips.Add(Solver.Solve(ir, []));
    }));
    write.Add(Ms(() => bytes = JsonSerializer.SerializeToUtf8Bytes(new { clips }, json).LongLength));
    whole.Add(Ms(() => ShosaCompilation.Compile(text, null, songs, usings)));
    frames = clips.Sum(c => c.Figures.Sum(f => f.Frames.Count));
    figures = clips.Sum(c => c.Figures.Count);
}
Console.WriteLine($"  parse          {Stats(parse)}");
Console.WriteLine($"  bind           {Stats(bind)}");
Console.WriteLine($"  solve          {Stats(solve)}   ({figures} figures, {frames} frames of a figure)");
Console.WriteLine($"  write as JSON  {Stats(write)}   ({bytes / 1024.0 / 1024.0:0.00} MB)");
Console.WriteLine($"  compile, whole {Stats(whole)}");

// ---- The server, as the editor drives it.
string uri = new Uri(file).AbsoluteUri;
long sent = 0;
int messages = 0;
var server = new LanguageServer(message =>
{
    sent += System.Text.Encoding.UTF8.GetByteCount(message);
    messages++;
});
void Send(object message) => server.Handle(JsonSerializer.Serialize(message));
Send(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { capabilities = new { } } });
double open = Ms(() => Send(new { jsonrpc = "2.0", method = "textDocument/didOpen", @params = new { textDocument = new { uri, languageId = "shosa", version = 1, text } } }));
Console.WriteLine(FormattableString.Invariant($"  server: open   {open,15:0.0} ms   ({sent / 1024.0 / 1024.0:0.00} MB sent)"));

var edits = new List<double>();
int version = 1;
char original = text[at];
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
}
Console.WriteLine($"  server: edit   {Stats(edits)}   ({sent / 1024.0 / 1024.0:0.00} MB sent in {messages} messages)");
return 0;
