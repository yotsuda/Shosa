// shosa-music — reads a Lily# song for Shosa. GPL-3.0-or-later (see SongFile.cs).

using System.Globalization;
using System.Text.Json;
using LilySharp.Core.Audio;
using Shosa.Music;

const string Usage = """
    shosa-music — what Shosa asks of a Lily# song (.lys)

      shosa-music timeline SONG.lys             tempo, time, sections and their lengths, as JSON
      shosa-music notes    SONG.lys             every note as [beat, beats, pitch, velocity, timbre], as JSON
      shosa-music midi     SONG.lys -o OUT.mid  the song as the dance plays it
      shosa-music wav      SONG.lys -o OUT.wav --soundfont FONT.sf2

      --score NAME     one of the song's scores (its default without)
      --order "A B A"  play these sections instead of the score's own form
      --tempo N        start at N beats per minute instead of the song's own tempo
    """;

if (args.Length < 2 || args[0] is "-h" or "--help")
{
    Console.WriteLine(Usage);
    return args.Length == 0 ? 1 : 0;
}

string? Text(string name)
{
    int at = Array.IndexOf(args, name, 2);
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}

string command = args[0], path = args[1];
var order = Text("--order")?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
double? tempo = double.TryParse(Text("--tempo"), NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm) ? bpm : null;
var song = SongFile.Load(path, Text("--score"), order, tempo);

switch (command)
{
    case "timeline":
        // Always JSON, an unreadable song too: the error is for the author of the dance.
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine(JsonSerializer.Serialize(song.Timeline, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        return 0;

    case "notes":
        // For a preview that plays the song itself, on the dance's own clock.
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine(JsonSerializer.Serialize(new { error = song.Timeline.Error, notes = song.Notes() }));
        return 0;

    case "midi" or "wav":
        if (song.Midi is null || Text("-o") is not { } output)
        {
            Console.Error.WriteLine($"shosa-music: {song.Timeline.Error ?? "say where to write it: -o FILE"}");
            return 1;
        }
        if (Path.GetDirectoryName(Path.GetFullPath(output)) is { Length: > 0 } folder)
            Directory.CreateDirectory(folder);
        if (command == "midi")
        {
            song.Midi.Save(output);
        }
        else
        {
            if (Text("--soundfont") is not { } font || !File.Exists(font))
            {
                Console.Error.WriteLine("shosa-music: a wav needs a SoundFont 2 to play the notes with: --soundfont FONT.sf2");
                return 1;
            }
            try
            {
                using var stream = File.Create(output);
                WavRenderer.Render(song.Midi, font, stream);
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine($"shosa-music: {ex.Message}");
                return 1;
            }
        }
        Console.WriteLine(output);
        return 0;

    default:
        Console.Error.WriteLine($"shosa-music: unknown command '{command}'");
        Console.WriteLine(Usage);
        return 2;
}
