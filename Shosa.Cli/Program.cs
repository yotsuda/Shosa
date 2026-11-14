using System.Text.Json;
using System.Text.Json.Serialization;
using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Music;
using Shosa.Core.Output;
using Shosa.Core.Syntax;

const string Usage = """
    shosa — a movement notation compiler (prototype)

      shosa check   FILE            diagnostics only
      shosa sheet   FILE [-o PNG]   a pose per beat and the floor from above, on one image
                    [--step BEATS] [--from BEAT] [--to BEAT]   closer poses, or part of the clip
                    [--view side]   from stage left: a figure facing front is seen in profile
      shosa preview FILE [-o HTML]  a page with the source and the figures moving, linked
      shosa ir      FILE [-o JSON]  the intermediate representation the solver is given
      shosa clip    FILE [-o JSON]  the solved movement: every joint at every frame
      shosa bvh     FILE [-o BVH]   the movement as bone rotations, for Blender and other tools
                    [--scale N]     metres by default; 100 for centimetres
      shosa gltf    FILE [-o GLB]   the same as glTF, for Godot, Blender, Unity and the web
                                    (-o NAME.gltf writes text with the data embedded)
      shosa vrma    FILE [-o VRMA]  the same as a VRM Animation, to put on any VRM model
      shosa midi    FILE [-o MID]   the song the file is danced to (`music`), in the dance's order and tempo
      shosa wav     FILE [-o WAV] --soundfont FONT.sf2   the same as sound
      shosa grammar [-o MD]        the language in one file, for an AI that will write it

    A file without a `form` has one clip per section. Every command writes them all, each to
    its own file (walk.Idle.png); --clip NAME picks one. glTF holds them all in one file, an
    animation per clip. --score NAME picks one of the file's scores (the output is then named
    FILE-NAME). Marks (`!hit`), loops and `inplace` velocities are in the glTF animations'
    extras; bvh and vrma write them beside the files as FILE.events.json.
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

string? output = Text("-o"), scoreName = Text("--score"), clipName = Text("--clip");

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

foreach (var diagnostic in compilation.Diagnostics)
{
    var (line, column) = ShosaCompilation.LineColumn(text, diagnostic.Span.Start);
    string severity = diagnostic.Severity.ToString().ToLowerInvariant();
    Console.Error.WriteLine($"{path}({line},{column}): {severity} {diagnostic.Code}: {diagnostic.Message}");
}

var clips = compilation.Clips.Where(c => clipName is null || c.Name == clipName).ToList();
if (clipName is not null && clips.Count == 0)
{
    Console.Error.WriteLine($"shosa: no clip named '{clipName}'. This file has: {string.Join(", ", compilation.Clips.Select(c => c.Name))}");
    return 2;
}

var json = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = command == "ir",
};

// walk.shosa -> walk.png; with --score practice -> walk-practice.png.
string Base(string extension)
{
    if (output is not null)
        return output;
    string stem = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path));
    return (string.IsNullOrEmpty(scoreName) ? stem : $"{stem}-{scoreName}") + extension;
}

// One clip goes to the name as it is; several each get their own (walk.Idle.png).
string Target(MotionClip clip, string extension, string? figure = null)
{
    string file = Base(extension);
    string suffix = (clips.Count > 1 ? "." + clip.Name : "") + (figure is null ? "" : "." + figure);
    return suffix == "" ? file : Path.ChangeExtension(file, suffix + Path.GetExtension(file));
}

void WriteEvents()
{
    if (!clips.Any(c => c.Marks.Count > 0 || c.Inplace))
        return;
    var events = clips.Select(c => new
    {
        clip = c.Name,
        seconds = Math.Round(c.SecondsAt(c.TotalBeats), 4),
        loop = c.Loop,
        velocity = c.Inplace ? c.Figures.ToDictionary(f => f.Id, f => f.Velocity) : null,
        marks = c.Marks,
    });
    string file = Path.ChangeExtension(Base(".json"), ".events.json");
    File.WriteAllText(file, JsonSerializer.Serialize(new { clips = events }, new JsonSerializerOptions(json) { WriteIndented = true }));
    Console.WriteLine(file);
}

switch (command)
{
    case "check":
        if (compilation.Diagnostics.Count == 0)
        {
            string list = string.Join(", ", compilation.Clips.Select(c => $"{(c.Name == "" ? "clip" : c.Name)} {Math.Round(c.TotalBeats, 3)} beats"));
            Console.WriteLine($"{path}: no problems ({list}; {compilation.Clip.Figures.Count} dancer(s))");
        }
        // Danced to a song: where its sections fall, which is where the dance's sections were put.
        foreach (var clip in compilation.Clips.Where(c => c.Music is not null && c.Sections.Count > 0))
        {
            // With the bars, since a writer counts in them: "Verse 0-49 (upbeat of 1, then 8 bars from beat 1)".
            string Bars(Shosa.Core.Semantics.SectionIr s)
            {
                int bars = Shosa.Core.Semantics.Bars.Count(clip.Meters, s.T0 + s.Pickup, s.T1);
                string count = FormattableString.Invariant($"{bars} bar{(bars == 1 ? "" : "s")}");
                return s.Pickup > 0 ? FormattableString.Invariant($"upbeat of {Math.Round(s.Pickup, 3)}, then {count} from beat {Math.Round(s.T0 + s.Pickup, 3)}") : count;
            }
            string sections = string.Join(", ", clip.Sections.Select(s => FormattableString.Invariant($"{s.Name} {Math.Round(s.T0, 3)}-{Math.Round(s.T1, 3)} ({Bars(s)})")));
            // A song that changes tempo or time on the way: every change with its beat, since the bars are counted by them.
            string Changes<T>(IEnumerable<T> later, Func<T, FormattableString> say) =>
                later.Any() ? " (" + string.Join(", ", later.Select(c => FormattableString.Invariant(say(c)))) + ")" : "";
            string tempos = Changes(clip.Tempos.Skip(1), t => $"{Math.Round(t.Bpm, 3)} from beat {Math.Round(t.Beat, 3)}");
            string meters = Changes(clip.Meters.Skip(1), m => $"{m.BeatsPerBar} from beat {Math.Round(m.Beat, 3)}");
            Console.WriteLine(FormattableString.Invariant(
                $"  to {clip.Music!.File} at {Math.Round(clip.Tempo, 3)} beats a minute{tempos}, {clip.BeatsPerBar} to the bar{meters}: {sections}"));
        }
        // Marks do nothing to the movement, so this is the only place a writer sees where they fell.
        foreach (var clip in compilation.Clips)
        {
            foreach (var mark in clip.Marks)
            {
                string who = clip.Figures.Count > 1 ? $" ({mark.Figure})" : "";
                Console.WriteLine(FormattableString.Invariant(
                    $"  {(clip.Name == "" ? "clip" : clip.Name)} !{mark.Name} at beat {Math.Round(mark.Beat, 3)} = {Math.Round(mark.Seconds, 3)} s{who}"));
            }
        }
        break;

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
            ContactSheet.Render(clip, Option("--step"), Option("--from") ?? 0, Option("--to"), view).SavePng(Target(clip, ".png"));
            Console.WriteLine(Target(clip, ".png"));
        }
        break;

    case "preview":
        foreach (var clip in clips)
        {
            // Danced to a song: the page plays it with the movement.
            var song = clip.Music is null ? null : songs.Notes(clip.Music);
            if (song?.Error is { } silent)
                Console.Error.WriteLine($"shosa: the page will have no sound. {silent}");
            File.WriteAllText(Target(clip, ".html"), PreviewHtml.Render(clip, text, song is { Error: null } ? song.Notes : null));
            Console.WriteLine(Target(clip, ".html"));
        }
        break;

    case "bvh":
        // BVH holds one skeleton: with several figures, each gets its own file (walk.a.bvh).
        foreach (var clip in clips)
        {
            foreach (var figure in clip.Figures)
            {
                string file = Target(clip, ".bvh", clip.Figures.Count == 1 ? null : figure.Id);
                File.WriteAllText(file, Bvh.Render(clip, figure, Option("--scale") ?? 1));
                Console.WriteLine(file);
            }
        }
        WriteEvents();
        break;

    case "gltf":
    {
        // glTF holds any number of skeletons and animations: every figure and every clip go into
        // the one file, each clip an animation of its name, with its marks in the animation's extras.
        string file = Base(".glb");
        File.WriteAllBytes(file, Gltf.Render(clips, binary: !file.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)));
        Console.WriteLine(file);
        break;
    }

    case "vrma":
        // A VRM Animation is for one humanoid: with several figures, each gets its own file (duet.a.vrma).
        foreach (var clip in clips)
        {
            foreach (var figure in clip.Figures)
            {
                string file = Target(clip, ".vrma", clip.Figures.Count == 1 ? null : figure.Id);
                File.WriteAllBytes(file, Gltf.RenderVrma(clip, figure));
                Console.WriteLine(file);
            }
        }
        WriteEvents();
        break;

    case "midi" or "wav":
    {
        // The song, as this score of the dance plays it: Lily# makes the notes, through shosa-music.
        if (compilation.Ir.Music is not { } music)
        {
            Console.Error.WriteLine($"shosa: {path} is not danced to a song (music \"song.lys\").");
            return 2;
        }
        if (command == "wav" && Text("--soundfont") is null)
        {
            Console.Error.WriteLine("shosa: a wav needs a SoundFont 2 to play the notes with: --soundfont FONT.sf2");
            return 2;
        }
        string file = Base("." + (command == "midi" ? "mid" : command));
        if (songs.Write(music, Path.GetFullPath(file), command == "wav" ? Path.GetFullPath(Text("--soundfont")!) : null) is { } problem)
        {
            Console.Error.WriteLine($"shosa: {problem}");
            return 1;
        }
        Console.WriteLine(file);
        break;
    }

    case "ir":
        Write(clipName is null
            ? JsonSerializer.Serialize(compilation.Score, json)
            : JsonSerializer.Serialize(compilation.Score.Clips.First(c => c.Name == clipName), json));
        break;

    case "clip":
        Write(clips.Count == 1 ? JsonSerializer.Serialize(clips[0], json) : JsonSerializer.Serialize(clips, json));
        break;

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
