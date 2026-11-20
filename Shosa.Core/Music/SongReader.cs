using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Shosa.Core.Semantics;

namespace Shosa.Core.Music;

/// <summary>
/// Reads Lily# songs for the files of one folder, by asking Lily#'s own compiler, `lysc`
/// (`timeline`, `notes`, `midi`, `wav`: DESIGN.md 92). Nothing of Lily# is linked here, and
/// Lily# is a loose dependency: without it only dancing to a song is out of reach. What crosses
/// is a command line and JSON, whose shape `lysc --version --json` numbers (`protocol`).
///
/// An answer is kept for as long as the song's file is unchanged, so an editor that compiles
/// on every keystroke starts the program once per song, not once per key.
/// </summary>
public sealed class SongReader(string folder)
{
    /// <summary>The oldest shape of `lysc timeline` and `lysc notes` this reads.</summary>
    public const int Protocol = 1;

    private static readonly ConcurrentDictionary<string, SongInfo> Cache = new();
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly Lazy<(string? Program, string? Problem)> Found = new(Find);

    /// <summary>The reader for the songs a file names: they are looked for beside it.</summary>
    public static SongSource For(string shosaFile) =>
        new SongReader(Path.GetDirectoryName(Path.GetFullPath(shosaFile)) ?? ".").Read;

    /// <summary>Where `lysc` is, or null when no Lily# that can be used is installed (<see cref="Missing"/> says why).</summary>
    public static string? Program => Found.Value.Program;

    /// <summary>Why songs cannot be read, for the user; null when they can.</summary>
    public static string? Missing => Found.Value.Problem;

    public string PathOf(string file) => Path.GetFullPath(Path.Combine(folder, file));

    public SongInfo? Read(string file, string? score, IReadOnlyList<string>? order)
    {
        string path = PathOf(file);
        if (!File.Exists(path))
            return new SongInfo { Error = $"There is no file '{file}' beside this one." };
        if (Program is not { } program)
            return new SongInfo { Error = "The song cannot be read: " + Missing };

        string key = string.Join('\n', path, File.GetLastWriteTimeUtc(path).Ticks.ToString(CultureInfo.InvariantCulture), score, order is null ? "" : "order " + string.Join(' ', order));
        var song = Cache.GetOrAdd(key, _ =>
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
            return new SongInfo { Error = $"'{file}' could not be read: {(error.Trim() is { Length: > 0 } said ? said : "lysc gave no answer")}" };
        });
        // Played in its own order, a song made from a recording keeps the recording's time (DESIGN.md 97).
        return order is null ? RecordingSidecar.Apply(song, path) : song;
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
            return new SongNotes { Error = Missing };

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
        notes ??= new SongNotes { Error = $"'{music.File}' could not be read: {(error.Trim() is { Length: > 0 } said ? said : "lysc gave no answer")}" };
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
            return Missing;
        // lysc names what it writes after the song (SONG.mid, SONG-NAME.mid for a named score) in
        // a folder: it writes into a folder of its own here, and the one file is moved where asked.
        string scratch = Path.Combine(Path.GetTempPath(), "shosa-song-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string extension = soundFont is null ? ".mid" : ".wav";
            var arguments = new List<string> { soundFont is null ? "midi" : "wav", PathOf(music.File), "-d", scratch };
            if (soundFont is not null)
                arguments.AddRange(["--soundfont", soundFont]);
            if (!string.IsNullOrEmpty(music.Score))
                arguments.AddRange(["--score", music.Score]);
            if (music.Order is not null)
                arguments.AddRange(["--order", string.Join(' ', music.Order)]);
            if (music.Tempo is { } tempo)
                arguments.AddRange(["--tempo", tempo.ToString(CultureInfo.InvariantCulture)]);
            var (exit, _, error) = Run(program, arguments);
            if (exit != 0)
                return error.Trim() is { Length: > 0 } said ? said : "lysc failed.";
            // Without --score, lysc writes every score; the song's default is the one without a name.
            string stem = Path.GetFileNameWithoutExtension(music.File);
            string expected = Path.Combine(scratch, stem + (string.IsNullOrEmpty(music.Score) ? "" : "-" + music.Score) + extension);
            string[] written = Directory.GetFiles(scratch, "*" + extension);
            string? made = File.Exists(expected) ? expected : written.Length == 1 ? written[0] : null;
            if (made is null)
                return $"lysc wrote no {extension} for {(string.IsNullOrEmpty(music.Score) ? "the song" : "the score " + music.Score)}.";
            File.Move(made, output, overwrite: true);
            return null;
        }
        finally
        {
            try { Directory.Delete(scratch, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
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
                return (-1, "", "lysc did not answer within a minute.");
            }
            return (process.ExitCode, output.Result, error.Result);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return (-1, "", ex.Message);
        }
    }

    /// <summary>
    /// `lysc`: SHOSA_LYSC (the VS Code extension sets it to the one the Lily# extension names), then
    /// the PATH, then the newest Lily# extension installed in VS Code (`server/lysc`) — so `shosa` on
    /// a command line finds it too. The one found must say a <see cref="Protocol"/> this reads.
    /// </summary>
    private static (string? Program, string? Problem) Find()
    {
        const string Install = "Lily#, which reads songs, is needed to dance to one: install the Lily# extension for VS Code, or name its lysc in SHOSA_LYSC.";
        string name = OperatingSystem.IsWindows() ? "lysc.exe" : "lysc";
        string? found = null;
        if (Environment.GetEnvironmentVariable("SHOSA_LYSC") is { Length: > 0 } named)
        {
            if (!File.Exists(named))
                return (null, $"SHOSA_LYSC names {named}, which is not there. " + Install);
            found = named;
        }
        found ??= (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => { try { return Path.Combine(folder.Trim('"'), name); } catch (ArgumentException) { return ""; } })
            .FirstOrDefault(File.Exists);
        found ??= Installed(name);
        if (found is null)
            return (null, Install);

        // The shape of what it prints: `lysc --version --json` is {version, protocol}.
        var (exit, output, _) = Run(found, ["--version", "--json"]);
        int protocol = 0;
        string version = "?";
        try
        {
            if (exit == 0 && JsonDocument.Parse(output).RootElement is { ValueKind: JsonValueKind.Object } said)
            {
                if (said.TryGetProperty("protocol", out var p) && p.TryGetInt32(out int number))
                    protocol = number;
                if (said.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String)
                    version = v.GetString()!;
            }
        }
        catch (JsonException)
        {
        }
        return protocol >= Protocol
            ? (found, null)
            : (null, $"The Lily# found ({found}, version {version}) is too old to read songs for Shosa: update the Lily# extension.");
    }

    /// <summary>The lysc of the newest Lily# extension in VS Code's extensions folder, or null.</summary>
    private static string? Installed(string name)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (string editor in new[] { ".vscode", ".vscode-insiders" })
        {
            string extensions = Path.Combine(home, editor, "extensions");
            if (!Directory.Exists(extensions))
                continue;
            var newest = Directory.GetDirectories(extensions, "yotsuda.lilysharp-*")
                .Select(dir => (Dir: dir, Version: Version.TryParse(Path.GetFileName(dir)["yotsuda.lilysharp-".Length..].Split('-')[0], out var v) ? v : null))
                .Where(x => x.Version is not null && File.Exists(Path.Combine(x.Dir, "server", name)))
                .OrderByDescending(x => x.Version)
                .FirstOrDefault();
            if (newest.Dir is not null)
                return Path.Combine(newest.Dir, "server", name);
        }
        return null;
    }
}
