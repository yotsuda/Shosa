using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Shosa.Core.Semantics;

namespace Shosa.Core.Music;

/// <summary>
/// Reads Lily# songs for the files of one folder, by asking `shosa-music`: a program of its
/// own (Shosa.Music), because Lily# is under the GPL and brings an engraver along. Nothing of
/// Lily# is linked here; what crosses is a command line and JSON.
///
/// An answer is kept for as long as the song's file is unchanged, so an editor that compiles
/// on every keystroke starts the program once per song, not once per key.
/// </summary>
public sealed class SongReader(string folder)
{
    private static readonly ConcurrentDictionary<string, SongInfo> Cache = new();
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly Lazy<string?> Found = new(Find);

    /// <summary>The reader for the songs a file names: they are looked for beside it.</summary>
    public static SongSource For(string shosaFile) =>
        new SongReader(Path.GetDirectoryName(Path.GetFullPath(shosaFile)) ?? ".").Read;

    /// <summary>Where `shosa-music` is, or null when it is not installed.</summary>
    public static string? Program => Found.Value;

    public string PathOf(string file) => Path.GetFullPath(Path.Combine(folder, file));

    public SongInfo? Read(string file, string? score, IReadOnlyList<string>? order)
    {
        string path = PathOf(file);
        if (!File.Exists(path))
            return new SongInfo { Error = $"There is no file '{file}' beside this one." };
        if (Program is not { } program)
            return new SongInfo { Error = "The song cannot be read: shosa-music, the program that reads Lily# files, is not installed beside shosa (or named by SHOSA_MUSIC)." };

        string key = string.Join('\n', path, File.GetLastWriteTimeUtc(path).Ticks.ToString(CultureInfo.InvariantCulture), score, order is null ? "" : "order " + string.Join(' ', order));
        return Cache.GetOrAdd(key, _ =>
        {
            var arguments = new List<string> { "timeline", path };
            if (!string.IsNullOrEmpty(score))
                arguments.AddRange(["--score", score]);
            if (order is not null)
                arguments.AddRange(["--order", string.Join(' ', order)]);
            var (exit, output, error) = Run(program, arguments);
            try
            {
                if (exit == 0 && JsonSerializer.Deserialize<SongInfo>(output, Json) is { } song)
                    return song;
            }
            catch (JsonException)
            {
            }
            return new SongInfo { Error = $"'{file}' could not be read: {(error.Trim() is { Length: > 0 } said ? said : "shosa-music gave no answer")}" };
        });
    }

    /// <summary>
    /// The notes of the song in the clip's order, in the clip's beats (its tempo does not matter
    /// to them). Kept until the song's file changes, one answer per song, score and order.
    /// </summary>
    public SongNotes Notes(MusicIr music)
    {
        string path = PathOf(music.File);
        if (!File.Exists(path))
            return new SongNotes { Error = $"There is no file '{music.File}' beside this one." };
        if (Program is not { } program)
            return new SongNotes { Error = "shosa-music, the program that reads Lily# files, is not installed." };

        long written = File.GetLastWriteTimeUtc(path).Ticks;
        string key = string.Join('\n', path, music.Score, music.Order is null ? "" : "order " + string.Join(' ', music.Order));
        if (NotesCache.TryGetValue(key, out var kept) && kept.Written == written)
            return kept.Notes;

        var arguments = new List<string> { "notes", path };
        if (!string.IsNullOrEmpty(music.Score))
            arguments.AddRange(["--score", music.Score]);
        if (music.Order is not null)
            arguments.AddRange(["--order", string.Join(' ', music.Order)]);
        var (exit, output, error) = Run(program, arguments);
        SongNotes? notes = null;
        try
        {
            if (exit == 0)
                notes = JsonSerializer.Deserialize<SongNotes>(output, Json);
        }
        catch (JsonException)
        {
        }
        notes ??= new SongNotes { Error = $"'{music.File}' could not be read: {(error.Trim() is { Length: > 0 } said ? said : "shosa-music gave no answer")}" };
        NotesCache[key] = (written, notes);
        return notes;
    }

    private static readonly ConcurrentDictionary<string, (long Written, SongNotes Notes)> NotesCache = new();

    /// <summary>
    /// Writes the song as the clip plays it — its order, its tempo — to a .mid, or to a .wav
    /// when a SoundFont is given. Returns what went wrong, or null.
    /// </summary>
    public string? Write(MusicIr music, string output, string? soundFont = null)
    {
        if (Program is not { } program)
            return "shosa-music, the program that reads Lily# files, is not installed.";
        var arguments = new List<string> { soundFont is null ? "midi" : "wav", PathOf(music.File), "-o", output };
        if (soundFont is not null)
            arguments.AddRange(["--soundfont", soundFont]);
        if (!string.IsNullOrEmpty(music.Score))
            arguments.AddRange(["--score", music.Score]);
        if (music.Order is not null)
            arguments.AddRange(["--order", string.Join(' ', music.Order)]);
        if (music.Tempo is { } tempo)
            arguments.AddRange(["--tempo", tempo.ToString(CultureInfo.InvariantCulture)]);
        var (exit, _, error) = Run(program, arguments);
        return exit == 0 ? null : error.Trim() is { Length: > 0 } said ? said : "shosa-music failed.";
    }

    private static (int Exit, string Output, string Error) Run(string program, List<string> arguments)
    {
        try
        {
            var start = new ProcessStartInfo(program)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,   // a language server's own stdin is the editor's messages
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (string argument in arguments)
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60_000))
            {
                process.Kill(entireProcessTree: true);
                return (-1, "", "shosa-music did not answer within a minute.");
            }
            return (process.ExitCode, output.Result, error.Result);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return (-1, "", ex.Message);
        }
    }

    /// <summary>
    /// SHOSA_MUSIC, then beside the running program (or in its `music` folder), then — in a
    /// checkout — the build of Shosa.Music, the same configuration as this one first.
    /// </summary>
    private static string? Find()
    {
        string name = OperatingSystem.IsWindows() ? "shosa-music.exe" : "shosa-music";
        if (Environment.GetEnvironmentVariable("SHOSA_MUSIC") is { Length: > 0 } named)
            return File.Exists(named) ? named : null;

        string home = AppContext.BaseDirectory;
        foreach (string candidate in new[] { Path.Combine(home, name), Path.Combine(home, "music", name) })
        {
            if (File.Exists(candidate))
                return candidate;
        }

        string[] configurations = home.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}") ? ["Release", "Debug"] : ["Debug", "Release"];
        for (var up = new DirectoryInfo(home); up is not null; up = up.Parent)
        {
            string project = Path.Combine(up.FullName, "Shosa.Music", "bin");
            if (!Directory.Exists(project))
                continue;
            foreach (string configuration in configurations)
            {
                string candidate = Path.Combine(project, configuration, "net10.0", name);
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }
        return null;
    }
}
