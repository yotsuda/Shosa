using System.Collections.Concurrent;
using System.Text.Json;
using Shosa.Core.Semantics;

namespace Shosa.Core.Music;

/// <summary>
/// The recording a Lily# song was made from, read from the file beside it: `NAME.orecchiante.json`
/// (Orecchiante's SIDECAR.md, form 1; Lily# keeps the link between a chart and its recording there and
/// writes nothing of it in the .lys). Its `bars[]` are the bars the song plays through its form, in order,
/// each with the seconds of its start, its end and its beats in the recording. A dance to such a song keeps
/// the recording's time: a tempo for every beat, so a performance that speeds up and slows down is followed
/// beat by beat (DESIGN.md 97). Read here, not through Lily#: the owner writes both (2026-11-20).
/// </summary>
public static class RecordingSidecar
{
    private static readonly ConcurrentDictionary<string, (long Written, Sidecar? Read, string? Error)> Cache = new();

    private sealed class Sidecar
    {
        public string? Audio { get; set; }
        public double Duration { get; set; }
        public List<Bar> Bars { get; set; } = [];
    }

    private sealed class Bar
    {
        public double Start { get; set; }
        public double End { get; set; }
        public double[]? Beats { get; set; }
    }

    /// <summary>The file beside a song that says how a recording of it goes.</summary>
    public static string PathFor(string song) =>
        Path.Combine(Path.GetDirectoryName(song) ?? ".", Path.GetFileNameWithoutExtension(song) + ".orecchiante.json");

    /// <summary>
    /// The song as the recording beside it plays it: its tempos one for every beat and its <see cref="SongInfo.Recording"/>;
    /// or, when the recording does not fit the song, the song as it was with <see cref="SongInfo.RecordingError"/>. The song
    /// itself is not changed (it is kept in a cache): what is returned is a copy, or the song when there is no recording.
    /// </summary>
    public static SongInfo Apply(SongInfo song, string songPath)
    {
        string file = PathFor(songPath);
        if (song.Error is not null || !File.Exists(file))
            return song;
        var (read, error) = Read(file);
        var result = song.Copy();
        if (read is null)
        {
            result.RecordingError = error;
            return result;
        }

        string name = Path.GetFileName(file);
        if (string.IsNullOrEmpty(read.Audio))
        {
            result.RecordingError = $"{name} names no recording (\"audio\").";
            return result;
        }
        string audio = Path.Combine(Path.GetDirectoryName(file) ?? ".", read.Audio);
        if (!File.Exists(audio))
        {
            result.RecordingError = $"The recording {read.Audio} that {name} names is not beside it.";
            return result;
        }
        if (song.Plays.Any(p => p.Pickup > 0))
        {
            result.RecordingError = "A song that begins with an upbeat is not yet danced to its recording: it is danced to its own tempo.";
            return result;
        }

        // The song's bars, in beats, as it plays them: a bar is as many beats as the time in force says.
        double total = song.Plays.Sum(p => p.Beats);
        var meters = Bars.Of(song.Meters, song.BeatsPerBar, song.BeatUnit);
        var bars = new List<(double Beat, int Beats)>();
        for (double beat = 0; beat < total - 1e-6;)
        {
            int length = meters.Last(m => m.Beat <= beat + 1e-6).BeatsPerBar;
            bars.Add((beat, length));
            beat += length;
        }
        // The n-th bar timed is the n-th bar the song plays (as Lily# counts them). Bars timed past the song's end
        // (bar lines the chart no longer has at its end) are not danced; too few cannot be.
        if (read.Bars.Count < bars.Count)
        {
            result.RecordingError = $"The song plays {bars.Count} bars and {name} times only {read.Bars.Count}: the dance keeps the song's own tempo.";
            return result;
        }

        // A tempo for every beat: from the time of the beat to the time of the next. A bar whose beats are not
        // given (or not as many as the song's) has them evenly from its start to the next bar's.
        var tempos = new List<TempoIr>();
        for (int i = 0; i < bars.Count; i++)
        {
            var (beat, length) = bars[i];
            var bar = read.Bars[i];
            double end = i + 1 < bars.Count ? read.Bars[i + 1].Start : bar.End;
            if (end <= bar.Start + 1e-3)
            {
                result.RecordingError = $"Bar {i + 1} of {name} has no length.";
                return result;
            }
            double[] times = bar.Beats is { } given && given.Length == length && given[0] >= bar.Start - 0.05
                ? given
                : Enumerable.Range(0, length).Select(k => bar.Start + (end - bar.Start) * k / length).ToArray();
            for (int k = 0; k < length; k++)
            {
                double from = k == 0 ? bar.Start : times[k], to = k + 1 < length ? times[k + 1] : end;
                if (to <= from + 1e-3)
                {
                    result.RecordingError = $"Beat {k + 1} of bar {i + 1} of {name} has no length.";
                    return result;
                }
                tempos.Add(new TempoIr(beat + k, Math.Round(60 / (to - from), 3)));
            }
        }

        result.Tempos = tempos;
        result.Tempo = tempos[0].Bpm;
        result.Recording = new SongRecording(Path.GetFullPath(audio), read.Bars[0].Start, read.Duration);
        return result;
    }

    private static (Sidecar? Read, string? Error) Read(string file)
    {
        // The time it was written and its length: a file written twice within one tick of the clock is still read again.
        var info = new FileInfo(file);
        long written = info.LastWriteTimeUtc.Ticks ^ (info.Length << 40);
        if (Cache.TryGetValue(file, out var kept) && kept.Written == written)
            return (kept.Read, kept.Error);
        Sidecar? read = null;
        string? error = null;
        try
        {
            read = JsonSerializer.Deserialize<Sidecar>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (read is null || read.Bars.Count == 0)
            {
                error = $"{Path.GetFileName(file)} times no bars.";
                read = null;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            error = $"{Path.GetFileName(file)} could not be read: {ex.Message}";
        }
        Cache[file] = (written, read, error);
        return (read, error);
    }
}
