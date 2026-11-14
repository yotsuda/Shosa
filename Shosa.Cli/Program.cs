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

foreach (var diagnostic in compilation.Diagnostics)
{
    var (line, column) = ShosaCompilation.LineColumn(text, diagnostic.Span.Start);
    string severity = diagnostic.Severity.ToString().ToLowerInvariant();
    Console.Error.WriteLine($"{path}({line},{column}): {severity} {diagnostic.Code}: {diagnostic.Message}");
}

// One solved movement per score.
var clips = compilation.Clips.ToList();
string Label(string name) => name == "" ? "score" : name;

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
    if (c.Marks.Count == 0 && !c.Inplace)
        return;
    var events = new
    {
        score = c.Name,
        seconds = Math.Round(c.SecondsAt(c.TotalBeats), 4),
        loop = c.Loop,
        velocity = c.Inplace ? c.Figures.ToDictionary(f => f.Id, f => f.Velocity) : null,
        marks = c.Marks,
    };
    string file = Path.ChangeExtension(Target(c.Name, ".json"), ".events.json");
    File.WriteAllText(file, JsonSerializer.Serialize(events, new JsonSerializerOptions(json) { WriteIndented = true }));
    Console.WriteLine(file);
}

switch (command)
{
    case "check":
        if (compilation.Diagnostics.Count == 0)
            Console.WriteLine($"{path}: no problems");
        // Every score: how long it is and how many dance in it; then a line for each section it
        // plays — where it falls, how long it takes, the tempo and the time it is played in.
        // A cut of the piece may begin in another tempo and time than the file, and a writer
        // counts in bars and checks a speed in seconds: all of it is said, for every score.
        foreach (var clip in clips)
        {
            string N(double value) => Math.Round(value, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string Seconds(double from, double to) => (clip.SecondsAt(to) - clip.SecondsAt(from)).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " s";
            string head = clip.Music is { } song ? (clip.Name == "" ? "score" : clip.Name) + " to " + song.File : Label(clip.Name);
            Console.WriteLine($"  {head}: {N(clip.TotalBeats)} beats, {Seconds(0, clip.TotalBeats)}, {clip.Figures.Count} dancer{(clip.Figures.Count == 1 ? "" : "s")}");

            var tempos = Shosa.Core.Semantics.TempoMap.Of(clip.Tempos, clip.Tempo);
            var meters = Shosa.Core.Semantics.Bars.Of(clip.Meters, clip.BeatsPerBar);
            var rows = new List<string[]>();
            foreach (var s in clip.Sections)
            {
                // With the bars, since a writer counts in them: "upbeat of 1, then 8 bars from beat 1".
                int bars = Shosa.Core.Semantics.Bars.Count(meters, s.T0 + s.Pickup, s.T1);
                string count = $"{bars} bar{(bars == 1 ? "" : "s")}";
                if (s.Pickup > 0)
                    count = $"upbeat of {N(s.Pickup)}, then {count} from beat {N(s.T0 + s.Pickup)}";
                // A song may change tempo or time inside a section: each change with its beat, since the bars are counted by them.
                bool Inside(double beat) => beat > s.T0 + 1e-9 && beat < s.T1 - 1e-9;
                string tempo = string.Concat(tempos.Where(t => Inside(t.Beat)).Select(t => $", {N(t.Bpm)} from beat {N(t.Beat)}"));
                var first = Shosa.Core.Semantics.Bars.MeterAt(meters, s.T0);
                string time = string.Concat(meters.Where(m => Inside(m.Beat)).Select(m => $", {m.BeatsPerBar}/{m.BeatUnit} from beat {N(m.Beat)}"));
                // In a compound time (6/8, 9/8, 12/8) the beat is the quaver and the pulse a dancer
                // feels is the dotted crotchet: the tempo is said by both, "at 288 (4. = 96)".
                double bpm = Shosa.Core.Semantics.TempoMap.BpmAt(tempos, s.T0);
                string felt = first is { BeatUnit: 8, BeatsPerBar: 6 or 9 or 12 } ? $" (4. = {N(bpm / 3)})" : "";
                rows.Add([s.Name, $"{N(s.T0)}-{N(s.T1)}", count, Seconds(s.T0, s.T1),
                    $"at {N(bpm)}{felt}{tempo}", $"{first.BeatsPerBar}/{first.BeatUnit}{time}"]);
            }
            foreach (var row in rows)
                Console.WriteLine("    " + string.Join("   ", row.Select((cell, i) => cell.PadRight(rows.Max(r => r[i].Length)))).TrimEnd());
        }
        // Marks do nothing to the movement, so this is the only place a writer sees where they fell.
        foreach (var clip in compilation.Clips)
        {
            foreach (var mark in clip.Marks)
            {
                string who = clip.Figures.Count > 1 ? $" ({mark.Figure})" : "";
                Console.WriteLine(FormattableString.Invariant(
                    $"  {Label(clip.Name)} !{mark.Name} at beat {Math.Round(mark.Beat, 3)} = {Math.Round(mark.Seconds, 3)} s{who}"));
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
            ContactSheet.Render(clip, Option("--step"), Option("--from") ?? 0, Option("--to"), view).SavePng(Target(clip.Name, ".png"));
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
