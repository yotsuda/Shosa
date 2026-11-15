using System.Text.Json;
using System.Text.Json.Serialization;
using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Music;
using Shosa.Core.Output;
using Shosa.Core.Syntax;

const string Usage = """
    shosa — a movement notation compiler (prototype)

      shosa check   FILE            diagnostics, and what each score came to
                    [--at 3,0.75s]  how the body is turned at those beats (seconds with an s):
                                    hips, shoulders and face, in degrees from the way the feet face
      shosa sheet   FILE [-o PNG]   a pose per beat and the floor from above, on one image
                    [--step BEATS] [--from BEAT] [--to BEAT]   closer poses, or part of the clip
                                    (--from and --to take seconds with an s: --from 0.5s --to 1s)
                    [--view side]   from stage left: a figure facing front is seen in profile
      shosa preview FILE [-o HTML]  a page with the source and the figures moving, linked
      shosa ir      FILE [-o JSON]  the intermediate representation the solver is given
      shosa clip    FILE [-o JSON]  the solved movement: every joint at every frame, and the same angles
      shosa bvh     FILE [-o BVH]   the movement as bone rotations, for Blender and other tools
                    [--scale N]     metres by default; 100 for centimetres
      shosa gltf    FILE [-o GLB]   the same as glTF, for Godot, Blender, Unity and the web
                                    (-o NAME.gltf writes text with the data embedded)
                    [--combined]    every score in ONE file, an animation each (for a game engine)
      shosa vrma    FILE [-o VRMA]  the same as a VRM Animation, to put on any VRM model
      shosa midi    FILE [-o MID]   the song the file is danced to (`music`), in the dance's order and tempo
      shosa wav     FILE [-o WAV] --soundfont FONT.sf2   the same as sound
      shosa grammar [-o MD]        the language in one file, for an AI that will write it

    One `score` of the file is one file written. Every command writes every score: the
    unnamed one to the input's name (walk.png), `score Idle` to walk-Idle.png. --score NAME
    writes that one only (--score "" the unnamed one); gltf --combined puts them all into one. Marks (`!hit`), loops and `inplace`
    velocities are in the glTF animation's extras; bvh and vrma write them beside the files
    as FILE.events.json.
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

string? Text(string name)
{
    int at = Array.IndexOf(args, name, 2);
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}

double? Option(string name) =>
    double.TryParse(Text(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : null;

string? output = Text("-o"), scoreName = Text("--score");

if (!File.Exists(path))
{
    Console.Error.WriteLine($"shosa: no such file: {path}");
    return 2;
}

// -o out\walk.png into a folder that is not there yet: make it.
if (output is not null && Path.GetDirectoryName(Path.GetFullPath(output)) is { Length: > 0 } folder)
    Directory.CreateDirectory(folder);

string text = File.ReadAllText(path);
var songs = new SongReader(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
var compilation = ShosaCompilation.Compile(text, scoreName, songs.Read);

foreach (string problem in CheckReport.Problems(compilation, text, path))
    Console.Error.WriteLine(problem);

// One solved movement per score.
var clips = compilation.Clips.ToList();

var json = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = command == "ir",
};

// As Lily# names what it writes: the unnamed score goes to the input's name (walk.png), `score
// practice` to walk-practice.png. With -o the same is done to that name; -o with --score is the
// name as it is.
string Target(string score, string extension, string? figure = null)
{
    string file = output ?? Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + extension);
    string suffix = (score == "" || (output is not null && scoreName is not null) ? "" : "-" + score) + (figure is null ? "" : "." + figure);
    return Path.Combine(Path.GetDirectoryName(file) ?? "", Path.GetFileNameWithoutExtension(file) + suffix + Path.GetExtension(file));
}

void WriteEvents(MotionClip c)
{
    // The score and its length, then what the extras of a glTF animation hold, field for field.
    if (Gltf.EventsJson(c) is not { } events)
        return;
    string file = Path.ChangeExtension(Target(c.Name, ".json"), ".events.json");
    File.WriteAllText(file, events);
    Console.WriteLine(file);
}

switch (command)
{
    case "check":
        // The report is Core's (CheckReport): the language server gives the same one to an AI assistant in the editor.
    {
        // --at 3,4.5,0.75s: beats, or seconds with an s.
        IReadOnlyList<(double, bool)> moments = [];
        if (Text("--at") is { } wanted)
        {
            if (CheckReport.Moments(wanted, out string? wrong) is not { } said)
            {
                Console.Error.WriteLine($"shosa: --at takes beats, or seconds with an s, with commas between (--at 3,4.5 or --at 0.75s), not '{wrong}'.");
                return 2;
            }
            moments = said;
        }
        foreach (string line in CheckReport.Scores(compilation, path).Concat(CheckReport.At(compilation, moments)))
            Console.WriteLine(line);
        break;
    }

    case "sheet":
        if (Text("--view") is { } asked and not ("side" or "back" or "front") && Option("--view") is null)
        {
            Console.Error.WriteLine($"shosa: --view takes side, back, front or a number of degrees, not '{asked}'.");
            return 2;
        }
        foreach (var clip in clips)
        {
            // --view side looks from stage left (a front-facing figure in profile); a number is degrees round from the front.
            double view = Text("--view") switch { "side" => 90, "back" => 180, "front" or null => ContactSheet.FrontView, _ => Option("--view") ?? ContactSheet.FrontView };
            // --from and --to are beats, or seconds with an s, as --at: a writer checking "0.8 s" should not divide by the tempo.
            double? Moment(string name)
            {
                if (Text(name) is not { } said)
                    return null;
                if (CheckReport.Moments(said, out _) is not [var (value, seconds)])
                    return Option(name);
                return seconds ? Shosa.Core.Semantics.TempoMap.Beat(Shosa.Core.Semantics.TempoMap.Of(clip.Tempos, clip.Tempo), value) : value;
            }
            ContactSheet.Render(clip, Option("--step"), Moment("--from") ?? 0, Moment("--to"), view).SavePng(Target(clip.Name, ".png"));
            Console.WriteLine(Target(clip.Name, ".png"));
        }
        break;

    case "preview":
        foreach (var clip in clips)
        {
            // Danced to a song: the page plays it with the movement.
            var song = clip.Music is null ? null : songs.Notes(clip.Music);
            if (song?.Error is { } silent)
                Console.Error.WriteLine($"shosa: the page will have no sound. {silent}");
            File.WriteAllText(Target(clip.Name, ".html"), PreviewHtml.Render(clip, text, song is { Error: null } ? song.Notes : null));
            Console.WriteLine(Target(clip.Name, ".html"));
        }
        break;

    case "bvh":
        // BVH holds one skeleton: with several figures, each gets its own file (walk.a.bvh).
        foreach (var clip in clips)
        {
            foreach (var figure in clip.Figures)
            {
                string file = Target(clip.Name, ".bvh", clip.Figures.Count == 1 ? null : figure.Id);
                File.WriteAllText(file, Bvh.Render(clip, figure, Option("--scale") ?? 1));
                Console.WriteLine(file);
            }
            WriteEvents(clip);
        }
        break;

    case "gltf":
        // glTF holds any number of skeletons and animations. A score is a file with its one
        // animation; --combined puts every score into one file instead, an animation each, named
        // after its score — what a game engine wants of a character with several movements.
        if (args.Contains("--combined"))
        {
            if (scoreName is not null)
            {
                Console.Error.WriteLine("shosa: --combined writes every score into one file, --score writes one: take one of the two out.");
                return 2;
            }
            string file = output ?? Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + ".glb");
            File.WriteAllBytes(file, Gltf.Render(clips, binary: !file.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)));
            Console.WriteLine(file);
            break;
        }
        foreach (var clip in clips)
        {
            string file = Target(clip.Name, ".glb");
            File.WriteAllBytes(file, Gltf.Render([clip], binary: !file.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)));
            Console.WriteLine(file);
        }
        break;

    case "vrma":
        // A VRM Animation is for one humanoid: with several figures, each gets its own file (duet.a.vrma).
        foreach (var clip in clips)
        {
            foreach (var figure in clip.Figures)
            {
                string file = Target(clip.Name, ".vrma", clip.Figures.Count == 1 ? null : figure.Id);
                File.WriteAllBytes(file, Gltf.RenderVrma(clip, figure));
                Console.WriteLine(file);
            }
            WriteEvents(clip);
        }
        break;

    case "midi" or "wav":
    {
        // The song, as each score of the dance plays it: Lily# makes the notes, through shosa-music.
        var danced = compilation.Score.Clips.Where(c => c.Music is not null).ToList();
        if (danced.Count == 0)
        {
            Console.Error.WriteLine($"shosa: {path} is not danced to a song (music \"song.lys\").");
            return 2;
        }
        if (command == "wav" && Text("--soundfont") is null)
        {
            Console.Error.WriteLine("shosa: a wav needs a SoundFont 2 to play the notes with: --soundfont FONT.sf2");
            return 2;
        }
        foreach (var clip in danced)
        {
            string file = Target(clip.Name, "." + (command == "midi" ? "mid" : command));
            if (songs.Write(clip.Music!, Path.GetFullPath(file), command == "wav" ? Path.GetFullPath(Text("--soundfont")!) : null) is { } problem)
            {
                Console.Error.WriteLine($"shosa: {problem}");
                return 1;
            }
            Console.WriteLine(file);
        }
        break;
    }

    case "ir":
        Write(JsonSerializer.Serialize(compilation.Score, json));
        break;

    case "clip":
    {
        // With the joints, how the body is turned at every frame (what `check --at` says of one
        // moment): a writer checking a twist should not have to take an arctangent of two hips.
        System.Text.Json.Nodes.JsonNode Clip(MotionClip clip)
        {
            var node = JsonSerializer.SerializeToNode(clip, json)!;
            for (int i = 0; i < clip.Figures.Count; i++)
            {
                var rows = new System.Text.Json.Nodes.JsonArray();
                for (int f = 0; f < clip.Figures[i].Frames.Count; f++)
                {
                    var a = clip.Figures[i].Angles(f);
                    rows.Add(new System.Text.Json.Nodes.JsonArray(
                        Math.Round(a.Feet, 1), Math.Round(a.Hips, 1), Math.Round(a.Shoulders, 1), Math.Round(a.Face, 1), Math.Round(a.FaceUp, 1)));
                }
                node["figures"]![i]!["angles"] = rows;
            }
            node["angleNames"] = new System.Text.Json.Nodes.JsonArray("feet", "hips", "shoulders", "face", "faceUp");
            return node;
        }
        Write(clips.Count == 1 ? Clip(clips[0]).ToJsonString(json)
            : new System.Text.Json.Nodes.JsonArray(clips.Select(Clip).ToArray()).ToJsonString(json));
        break;
    }

    default:
        Console.Error.WriteLine($"shosa: unknown command '{command}'");
        Console.WriteLine(Usage);
        return 2;
}

return compilation.HasErrors ? 1 : 0;

void Write(string content)
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
