using System.Text.Json;
using Shosa.Core.Output;
using Shosa.Core.Semantics;

namespace Shosa.Tests;

/// <summary>`shosa song`: a song as a dance is written to it, the words on their bars and beats (DESIGN.md 123).</summary>
public class SongReportTests
{
    private static List<JsonElement[]> Syllables(params (double Beat, double Beats, string Text)[] syllables) =>
        syllables.Select(s => JsonSerializer.Deserialize<JsonElement[]>(JsonSerializer.Serialize(new object[] { s.Beat, s.Beats, s.Text }))!).ToList();

    /// <summary>4/4 at 120: Intro 4 beats with no words, Verse 9 beats after an upbeat of 1.</summary>
    private static SongInfo Song(List<SongSung>? sung) => new()
    {
        Tempo = 120,
        Sections = ["Intro", "Verse", "Bridge"],
        Plays = [new SongPlay("Intro", 4), new SongPlay("Verse", 9, Pickup: 1)],
        Sung = sung,
    };

    private static string Report(SongInfo song) => string.Join("\n", SongReport.Lines(song, "song.lys"));

    [Fact]
    public void Lists_the_sections_as_played_with_their_bars_and_seconds()
    {
        string report = Report(Song([]));
        Assert.Contains("song song.lys: 4/4 at 120 (a beat is a crotchet), 3 bars, 13 beats, 6.5 s", report);
        Assert.Matches(@"Intro\s+bars 1-1\s+1 bar\s+beats 0-4\s+0-2 s", report);
        Assert.Matches(@"Verse\s+bars 2-3\s+2 bars after an upbeat of 1\s+beats 4-13\s+2-6.5 s", report);
        Assert.Contains("sections the song has but does not play: Bridge", report);
        Assert.Contains("nothing is sung", report);
    }

    [Fact]
    public void Puts_each_syllable_on_the_beat_of_its_bar_and_joins_the_parts_that_sing_alike()
    {
        // The Verse's upbeat is beat 4; its first bar line is beat 5.
        var words = Syllables((4.5, 0.5, "あ"), (5, 1.5, "い"), (8, 0.5, "う"), (9.5, 2, "え"));
        string report = Report(Song([new("lead", "Alto", words), new("echo", null, words), new("solo", null, Syllables((10, 1, "お")))]));
        Assert.Contains("sung by: lead (Alto), echo, solo.", report);
        Assert.Contains("Intro: no words", report);
        // The upbeat is the end of a bar: あ on its "4 and"; that bar would have begun at beat 1.
        Assert.Matches(@"upbeat \(beat 1\)\s+lead\+echo: 4\.5あ   ""あ""", report);
        // い at 1 held 1.5, then a rest of 1.5 beats before う at 4; a rest of 1 again before え.
        Assert.Matches(@"bar 1 \(beat 5\)\s+lead\+echo: 1い~1\.5 / 4う   ""いう""", report);
        Assert.Matches(@"bar 2 \(beat 9\)\s+lead\+echo: / 1\.5え~2   ""え""", report);
        Assert.Matches(@"bar 2 \(beat 9\)\s+solo: 2お~1   ""お""", report);
    }

    [Fact]
    public void Words_in_letters_are_quoted_with_spaces_between_them_and_kana_run_on()
    {
        // As Orecchiante 0.18.0 writes We're An American Band. A Lily# older than 0.18.0 does not say
        // that Lit goes on with tle's word, so they stay apart.
        var english = Syllables((5, 0.5, "Last"), (5.5, 0.5, "night,"), (6, 0.5, "in"), (6.5, 0.5, "Lit"), (7, 0.75, "tle‿Rock"));
        Assert.Contains(@"""Last night, in Lit tle‿Rock""", Report(Song([new("vocal", null, english)])));
        // Lily# 0.18.0: a fourth element true (request 8).
        english[3] = JsonSerializer.Deserialize<JsonElement[]>("[6.5, 0.5, \"Lit\", true]")!;
        Assert.Contains(@"""Last night, in Little‿Rock""", Report(Song([new("vocal", null, english)])));
        var mixed = Syllables((5, 0.5, "Oh"), (5.5, 0.5, "あ"), (6, 0.5, "い"), (6.5, 0.5, "yeah"));
        Assert.Contains(@"""Ohあいyeah""", Report(Song([new("vocal", null, mixed)])));
    }

    [Fact]
    public void Tells_a_section_played_again_by_its_verse_as_a_dance_writes_it()
    {
        var song = new SongInfo
        {
            Tempo = 120,
            Sections = ["Verse", "Chorus"],
            Plays = [new SongPlay("Verse", 4), new SongPlay("Chorus", 4), new SongPlay("Verse", 4)],
            Sung = [new("vox", null, Syllables((1, 1, "a"), (9, 1, "b")))],
        };
        string report = Report(song);
        Assert.Matches(@"Verse, verse 1\s+bars 1-1", report);
        Assert.Matches(@"Chorus\s+bars 2-2", report);
        Assert.Matches(@"Verse, verse 2\s+bars 3-3", report);
        Assert.Matches(@"    Verse, verse 2\n      bar 1 \(beat 8\)\s+vox: / 2b~1", report);
    }

    [Fact]
    public void Says_when_the_Lily_that_read_the_song_does_not_tell_the_words()
    {
        Assert.Contains("not said by this Lily#", Report(Song(null)));
    }

    [Fact]
    public void A_dance_without_a_song_is_told_to_give_the_song()
    {
        string folder = Path.Combine(Path.GetTempPath(), "shosa-songreport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "walk.shosa");
            Assert.Null(SongReport.Read(path, Samples.Walk, null, out string? problem));
            Assert.Contains("is danced to no song", problem);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
