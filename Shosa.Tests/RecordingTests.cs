using Shosa.Core.Music;
using Shosa.Core.Semantics;

namespace Shosa.Tests;

/// <summary>A song made from a recording keeps the recording's time: `NAME.orecchiante.json` beside it (DESIGN.md 97).</summary>
public sealed class RecordingTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shosa-recording-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Song(string sidecar, bool audio = true)
    {
        string song = Path.Combine(_folder, "song.lys");
        File.WriteAllText(song, "");
        File.WriteAllText(Path.Combine(_folder, "song.orecchiante.json"), sidecar);
        if (audio)
            File.WriteAllBytes(Path.Combine(_folder, "song.mp3"), [0]);
        else
            File.Delete(Path.Combine(_folder, "song.mp3"));
        return song;
    }

    // Two bars of 4/4 and one of 2/4, as a song with a change of time plays them.
    private static SongInfo Plain() => new()
    {
        Tempo = 60, BeatsPerBar = 4, BeatUnit = 4,
        Sections = ["A"], Plays = [new SongPlay("A", 10)],
        Meters = [new MeterIr(0, 4, 4), new MeterIr(8, 2, 4)],
    };

    private const string ThreeBars = """
        { "audio": "song.mp3", "duration": 9,
          "bars": [ { "start": 1.5, "end": 3.5, "beats": [1.5, 2.0, 2.5, 3.0] },
                    { "start": 3.5, "end": 6.0 },
                    { "start": 6.0, "end": 7.0, "beats": [6.0, 6.5] },
                    { "start": 7.0, "end": 8.0 } ] }
        """;

    [Fact]
    public void Every_beat_takes_the_tempo_of_the_recording_and_the_bars_land_where_it_has_them()
    {
        var song = RecordingSidecar.Apply(Plain(), Song(ThreeBars));
        Assert.Null(song.RecordingError);
        Assert.Equal(1.5, song.Recording!.Offset);
        Assert.EndsWith("song.mp3", song.Recording.Audio);
        Assert.Equal(10, song.Tempos.Count);   // one for every beat; the fourth bar is past the song's end
        Assert.Equal(120, song.Tempos[0].Bpm); // half a second a beat
        Assert.Equal(96, song.Tempos[4].Bpm);  // the second bar has no beats: 2.5 s evenly over 4
        // The seconds of the beats add up to each bar line of the recording, from its first.
        var map = TempoMap.Of(song.Tempos, song.Tempo);
        Assert.Equal(2.0, TempoMap.Seconds(map, 4), 6);   // bar 2 at 3.5 s, 2 s after bar 1
        Assert.Equal(4.5, TempoMap.Seconds(map, 8), 6);   // bar 3 at 6.0 s
        Assert.Equal(5.5, TempoMap.Seconds(map, 10), 6);  // the end at the next bar line, 7.0 s
        // The song in the cache is not changed.
        Assert.Empty(Plain().Tempos);
    }

    [Fact]
    public void A_recording_that_does_not_fit_leaves_the_song_as_written_and_says_why()
    {
        var short1 = RecordingSidecar.Apply(Plain(), Song("""{ "audio": "song.mp3", "bars": [ { "start": 0, "end": 4 } ] }"""));
        Assert.Null(short1.Recording);
        Assert.Contains("only 1", short1.RecordingError);
        Assert.Empty(short1.Tempos);

        var silent = RecordingSidecar.Apply(Plain(), Song(ThreeBars, audio: false));
        Assert.Null(silent.Recording);
        Assert.Contains("not beside it", silent.RecordingError);
    }

    [Fact]
    public void A_song_with_nothing_beside_it_is_the_song()
    {
        var plain = Plain();
        string song = Path.Combine(_folder, "alone.lys");
        File.WriteAllText(song, "");
        Assert.Same(plain, RecordingSidecar.Apply(plain, song));
    }
}
