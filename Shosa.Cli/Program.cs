using System.Text.Json;
using System.Text.Json.Serialization;
using Shosa.Core;
using Shosa.Core.Output;
using Shosa.Core.Syntax;

const string Usage = """
    shosa — a movement notation compiler (prototype)

      shosa check   FILE            diagnostics only
      shosa sheet   FILE [-o PNG]   a pose per beat and the floor from above, on one image
                    [--step BEATS] [--from BEAT] [--to BEAT]   closer poses, or part of the piece
      shosa preview FILE [-o HTML]  a page with the source and the figures moving, linked
      shosa ir      FILE [-o JSON]  the intermediate representation the solver is given
      shosa clip    FILE [-o JSON]  the solved movement: every joint at every frame
      shosa bvh     FILE [-o BVH]   the movement as bone rotations, for Blender and other tools
                    [--scale N]     metres by default; 100 for centimetres
      shosa gltf    FILE [-o GLB]   the same as glTF, for Godot, Blender, Unity and the web
                                    (-o NAME.gltf writes text with the data embedded)
      shosa vrma    FILE [-o VRMA]  the same as a VRM Animation, to put on any VRM model
      shosa grammar [-o MD]        the language in one file, for an AI that will write it
    """;

if (args.Length >= 1 && args[0] == "grammar")
{
    string grammar = GrammarDoc.Render();
    int at = Array.IndexOf(args, "-o");
    if (at >= 0 && at + 1 < args.Length)
        File.WriteAllText(args[at + 1], grammar);
    else
        Console.Write(grammar);
    return 0;
}

if (args.Length < 2 || args[0] is "-h" or "--help")
{
    Console.WriteLine(Usage);
    return args.Length == 0 ? 1 : 0;
}

string command = args[0], path = args[1];
string? output = null;
for (int i = 2; i < args.Length - 1; i++)
{
    if (args[i] == "-o")
        output = args[i + 1];
}

double? Option(string name)
{
    int at = Array.IndexOf(args, name);
    return at >= 0 && at + 1 < args.Length
        && double.TryParse(args[at + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value)
        ? value
        : null;
}

if (!File.Exists(path))
{
    Console.Error.WriteLine($"shosa: no such file: {path}");
    return 2;
}

string text = File.ReadAllText(path);
var compilation = ShosaCompilation.Compile(text);

foreach (var diagnostic in compilation.Diagnostics)
{
    var (line, column) = ShosaCompilation.LineColumn(text, diagnostic.Span.Start);
    string severity = diagnostic.Severity.ToString().ToLowerInvariant();
    Console.Error.WriteLine($"{path}({line},{column}): {severity} {diagnostic.Code}: {diagnostic.Message}");
}

var json = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = command == "ir",
};

string Target(string extension) => output ?? Path.ChangeExtension(path, extension);

switch (command)
{
    case "check":
        if (compilation.Diagnostics.Count == 0)
            Console.WriteLine($"{path}: no problems ({compilation.Ir.TotalBeats} beats, {compilation.Ir.Figures.Count} figure(s))");
        break;

    case "sheet":
        ContactSheet.Render(compilation.Clip, Option("--step"), Option("--from") ?? 0, Option("--to")).SavePng(Target(".png"));
        Console.WriteLine(Target(".png"));
        break;

    case "preview":
        File.WriteAllText(Target(".html"), PreviewHtml.Render(compilation.Clip, text));
        Console.WriteLine(Target(".html"));
        break;

    case "bvh":
        // BVH holds one skeleton: with several figures, each gets its own file (walk.a.bvh).
        foreach (var figure in compilation.Clip.Figures)
        {
            string file = compilation.Clip.Figures.Count == 1 ? Target(".bvh") : Path.ChangeExtension(Target(".bvh"), $".{figure.Id}.bvh");
            File.WriteAllText(file, Bvh.Render(compilation.Clip, figure, Option("--scale") ?? 1));
            Console.WriteLine(file);
        }
        break;

    case "gltf":
        // glTF holds any number of skeletons: every figure goes into the one file.
        File.WriteAllBytes(Target(".glb"), Gltf.Render(compilation.Clip, binary: !Target(".glb").EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)));
        Console.WriteLine(Target(".glb"));
        break;

    case "vrma":
        // A VRM Animation is for one humanoid: with several figures, each gets its own file (duet.a.vrma).
        foreach (var figure in compilation.Clip.Figures)
        {
            string file = compilation.Clip.Figures.Count == 1 ? Target(".vrma") : Path.ChangeExtension(Target(".vrma"), $".{figure.Id}.vrma");
            File.WriteAllBytes(file, Gltf.RenderVrma(compilation.Clip, figure));
            Console.WriteLine(file);
        }
        break;

    case "ir":
        Write(JsonSerializer.Serialize(compilation.Ir, json), ".ir.json");
        break;

    case "clip":
        Write(JsonSerializer.Serialize(compilation.Clip, json), ".clip.json");
        break;

    default:
        Console.Error.WriteLine($"shosa: unknown command '{command}'");
        Console.WriteLine(Usage);
        return 2;
}

return compilation.HasErrors ? 1 : 0;

void Write(string content, string extension)
{
    if (output is null)
    {
        Console.WriteLine(content);
    }
    else
    {
        File.WriteAllText(output, content);
        Console.WriteLine(output);
    }
}
