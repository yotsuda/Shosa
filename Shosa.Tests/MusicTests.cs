using System.Runtime.CompilerServices;
using Shosa.Core;
using Shosa.Core.Music;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

/// <summary>Dancing to a song: `music "song.lys"` (DESIGN.md 12.8 and 19).</summary>
public class MusicTests
{
    /// <summary>A song in 3/4 at 120: A is 12 beats, B is 7 with an upbeat of 1, played A B A.</summary>
    private static SongInfo? Waltz(string file, string? score, IReadOnlyList<string>? order)
    {
        var lengths = new Dictionary<string, SongPlay> { ["A"] = new("A", 12), ["B"] = new("B", 7, 1) };
        if (file != "waltz.lys")
            return new SongInfo { Error = $"There is no file '{file}' beside this one." };
        return new SongInfo
        {
            Tempo = 120,
            BeatsPerBar = 3,
            Sections = ["A", "B"],
            Plays = (order ?? ["A", "B", "A"]).Where(lengths.ContainsKey).Select(n => lengths[n]).ToList(),
        };
    }

    private const string Dance = """
        music "waltz.lys"
        role solo
        dancer solo humanoid on solo
        score { dancer solo }
        section A { solo { arms { side:3 | up:3 | } } }
        section B { solo { arms { fwd:3 | down:3 | } } }

        """;

    /// <summary>One body dancing to a song: the order is the song's, so there is no form.</summary>
    private static string To(string song, string sections) =>
        $"music \"{song}\"\n{Samples.SoloCast}score {{ dancer solo }}\n{sections}\n";

    private static ShosaCompilation Compile(string text, string? score = null) => ShosaCompilation.Compile(text, score, Waltz);

    [Fact]
    public void Tempo_time_and_order_come_from_the_song()
    {
        var compilation = Compile(Dance);
        var clip = compilation.Clip;

        Assert.Empty(compilation.Diagnostics);
        Assert.Single(compilation.Clips);
        Assert.Equal((120.0, 3, 31.0), (clip.Tempo, clip.BeatsPerBar, clip.TotalBeats));
        Assert.Equal(new[] { ("A", 0.0, 12.0), ("B", 12.0, 19.0), ("A", 19.0, 31.0) }, clip.Sections.Select(s => (s.Name, s.T0, s.T1)).ToArray());
        Assert.Equal(new MusicIr("waltz.lys", null, null, null), clip.Music);

        // A section shorter than the song's says nothing for the rest; B's dance begins after the upbeat.
        // Each word is there on the beat it is written at, counted from its section's first bar line.
        var arm = compilation.Ir.Dancers[0].Tracks[Channels.LeftArm];
        Assert.Equal(new[] { 0.0, 3, 13, 16, 19, 22 }, arm.Select(s => s.At).ToArray());
        Assert.Equal(new[] { 0.0, 3, 13, 16, 19, 22 }, arm.Select(s => s.T1).ToArray());
        Assert.Equal(new[] { 3.0, 6, 16, 19, 22, 25 }, arm.Select(s => s.Until).ToArray());
    }

    [Fact]
    public void An_upbeat_moves_the_bars_and_belongs_to_the_bar_before()
    {
        var clip = Compile(Dance).Clip;

        // A is bars 1-4; B's upbeat is the fourth beat of a bar it does not start; B is bars 5-6; A again from 7.
        Assert.Equal(new[] { (0.0, 1), (1.0, 5), (0.0, 7) }, clip.Sections.Select(s => (s.Pickup, s.Bar)).ToArray());
        Assert.Equal((2, 1.0), Bars.At(clip.Sections, clip.Meters, 4));
        Assert.Equal((4, 2.0), Bars.At(clip.Sections, clip.Meters, 12));
        Assert.Equal((5, 0.0), Bars.At(clip.Sections, clip.Meters, 13));
        Assert.Equal((6, 2.0), Bars.At(clip.Sections, clip.Meters, 18));
        Assert.Equal((7, 0.0), Bars.At(clip.Sections, clip.Meters, 19));
    }

    [Fact]
    public void An_event_across_several_bar_lines_is_told_how_to_be_tied_over_each()
    {
        var long_ = Compile(To("waltz.lys", "section A { solo { arms { skip:9 side:3 | } } }"));
        var short_ = Compile(To("waltz.lys", "section A { solo { arms { side:2 up:3 fwd:1 | } } }"));

        Assert.Contains("'skip' runs across 2 bar lines: tie it over each, skip:3~ | :3~ | :3 — or leave the bar lines out of this row", Assert.Single(long_.Diagnostics).Message);
        Assert.Contains("'up' runs across the bar line: tie it over, up:1~ | :2", Assert.Single(short_.Diagnostics).Message);
    }

    [Fact]
    public void What_the_file_writes_itself_overrides_the_song()
    {
        const string text = Dance + """
            tempo 90
            form { B B }
            form once { A }
            score slow { dancer solo  form once  tempo 60 }

            """;
        var whole = Compile(text);
        var slow = Compile(text, "slow");

        Assert.Empty(whole.Diagnostics);
        Assert.Equal((90.0, 14.0), (whole.Clip.Tempo, whole.Clip.TotalBeats));
        Assert.Equal(new[] { "B", "B" }, whole.Clip.Music!.Order);
        Assert.Equal(90.0, whole.Clip.Music.Tempo);
        Assert.Equal((60.0, 12.0, "slow"), (slow.Clip.Tempo, slow.Clip.TotalBeats, slow.Clip.Name));
    }

    [Fact]
    public void A_tempo_in_per_cent_is_of_the_songs()
    {
        // What the 8th writing trial worked out by hand: the song at three quarters of its speed, whatever that is.
        const string text = Dance + """
            form once { A }
            score slow { dancer solo  form once  tempo 75% }
            score note { dancer solo  form once  tempo 2 = 30 }

            """;
        var slow = Compile(text, "slow");
        Assert.Empty(slow.Diagnostics);
        Assert.Equal(90.0, slow.Clip.Tempo);
        Assert.Equal(90.0, slow.Clip.Music!.Tempo);
        Assert.Equal(60.0, Compile(text, "note").Clip.Tempo);   // a minim is two of the song's beats

        // At the top of the file it is of the song's too, and a score's own is of that.
        var both = Compile("tempo 50%\n" + text, "slow");
        Assert.Empty(both.Diagnostics);
        Assert.Equal(45.0, both.Clip.Tempo);

        // A form over the song's sections still writes none.
        var form = Compile(Dance + "form { tempo 50% A }\n");
        Assert.Equal("SH1035", Assert.Single(form.Diagnostics).Code);
    }

    [Fact]
    public void A_section_the_file_does_not_have_is_stood_through()
    {
        var compilation = Compile(To("waltz.lys", "section B { solo { arms { fwd:3 | } } }"));

        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(31.0, compilation.Clip.TotalBeats);
        // B's one word is there on B's first bar line, after its upbeat. A is stood through: the arm
        // sets out in A's last bar, not at the start of the song.
        var arm = Assert.Single(compilation.Ir.Dancers[0].Tracks[Channels.LeftArm]);
        Assert.Equal((13.0, 13.0, 16.0), (arm.At, arm.T1, arm.Until));
        Assert.Equal(9.0, arm.T0);
    }

    [Theory]
    [InlineData("music \"waltz.lys\"\ntime 4/4\nsection A { solo { arms { side:3 | } } }", "SH1902", "time 4/4")]
    [InlineData("music \"waltz.lys\"\nsection B { solo { arms { side:3 | % | % | } } }", "SH1903", "B")]             // 9 beats in the 6 after the upbeat
    [InlineData("music \"waltz.lys\"\nsection Bridge { solo { arms { side:3 | } } }", "SH1905", "Bridge")]
    [InlineData("music \"waltz.lys\"\nsection A { solo { arms { side:3 | } } }\nform { A Bridge }", "SH1005", "Bridge")]
    [InlineData("music \"polka.lys\"\nsection A { solo { arms { side:4 | } } }", "SH1901", "music \"polka.lys\"")]
    public void What_does_not_fit_the_song_is_said(string text, string code, string at)
    {
        // The cast and the score come after, so the places in `text` stay where they are.
        var compilation = Compile(text + "\n" + Samples.SoloCast + "score { dancer solo }\n");

        var diagnostic = Assert.Single(compilation.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.Equal(at, text.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
    }

    [Fact]
    public void A_song_that_cannot_be_read_leaves_the_file_as_it_is_written()
    {
        var unread = Compile("tempo 100\n" + To("polka.lys", "section A { solo { arms { side:4 | } } }"));
        var nobody = ShosaCompilation.Compile(Dance);

        Assert.Equal((100.0, 4, 4.0), (unread.Clip.Tempo, unread.Clip.BeatsPerBar, unread.Clip.TotalBeats));
        Assert.Null(unread.Clip.Music);
        // Compiled from the text alone there is nothing to read a song with: said once, as information.
        Assert.Equal(("SH1900", DiagnosticSeverity.Info), (Assert.Single(nobody.Diagnostics).Code, nobody.Diagnostics[0].Severity));
        // With no song to say the order, the score plays the sections as they are written.
        Assert.Equal(new[] { "A", "B" }, Assert.Single(nobody.Clips).Sections.Select(s => s.Name).ToArray());
    }

    /// <summary>
    /// A song that changes on the way: A is two bars of 4/4 at 120 and slows to 60 after the
    /// first; B is two bars of 3/4, still at 60.
    /// </summary>
    private static SongInfo? Changing(string file, string? score, IReadOnlyList<string>? order) => new()
    {
        Tempo = 120,
        BeatsPerBar = 4,
        Sections = ["A", "B"],
        Plays = [new("A", 8), new("B", 6)],
        Tempos = [new(0, 120), new(4, 60)],
        Meters = [new(0, 4, 4), new(8, 3, 4)],
    };

    private const string ChangingDance = """
        music "changing.lys"
        role solo
        dancer solo humanoid on solo
        score { dancer solo }
        section A { solo { arms { side:4 | up:2!top side:2 | } } }
        section B { solo { arms { fwd:3 | down:3 | } } }

        """;

    [Fact]
    public void A_change_of_tempo_in_the_song_moves_the_seconds_not_the_beats()
    {
        var compilation = ShosaCompilation.Compile(ChangingDance, null, Changing);
        var clip = compilation.Clip;

        Assert.Empty(compilation.Diagnostics);
        Assert.Equal((120.0, 14.0), (clip.Tempo, clip.TotalBeats));
        Assert.Equal(new TempoIr[] { new(0, 120), new(4, 60) }, clip.Tempos);
        // Four beats at 120 are 2 seconds; the ten after them, at 60, are 10.
        Assert.Equal(new[] { 2.0, 4.0, 12.0 }, new[] { 4.0, 6, 14 }.Select(clip.SecondsAt).ToArray());
        Assert.Equal(12 * clip.Fps + 1, clip.Figures[0].Frames.Count);
        Assert.Equal(new[] { 4.0, 5.0 }, new[] { 2 * clip.Fps, 3 * clip.Fps }.Select(clip.BeatOfFrame).ToArray());
        // The mark is on the first beat of the bar at 60: two seconds in.
        Assert.Equal((4.0, 2.0), (Assert.Single(clip.Marks).Beat, clip.Marks[0].Seconds));
    }

    [Fact]
    public void A_tempo_written_here_keeps_the_proportions_of_the_songs_tempos()
    {
        var clip = ShosaCompilation.Compile("tempo 60\n" + ChangingDance, null, Changing).Clip;

        Assert.Equal(new TempoIr[] { new(0, 60), new(4, 30) }, clip.Tempos);
        Assert.Equal(60.0, clip.Music!.Tempo);
    }

    [Fact]
    public void A_change_of_time_in_the_song_changes_the_bars()
    {
        var compilation = ShosaCompilation.Compile(ChangingDance, null, Changing);
        var clip = compilation.Clip;

        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(new MeterIr[] { new(0, 4, 4), new(8, 3, 4) }, clip.Meters);
        Assert.Equal(new[] { 1, 3 }, clip.Sections.Select(s => s.Bar).ToArray());
        Assert.Equal(new[] { (2, 3.0), (3, 0.0), (4, 0.0), (4, 2.0) }, new[] { 7.0, 8, 11, 13 }.Select(b => Bars.At(clip.Sections, clip.Meters, b)).ToArray());

        // A bar of four where the song is in three, and `%` doing a bar of three again.
        var four = ShosaCompilation.Compile(To("changing.lys", "section B { solo { arms { fwd:4 | } } }"), null, Changing);
        var again = ShosaCompilation.Compile(To("changing.lys", "section B { solo { arms { fwd:1 side:2 | % | } } }"), null, Changing);
        Assert.Contains("time 3/4 has 3", Assert.Single(four.Diagnostics).Message);
        Assert.Empty(again.Diagnostics);
        Assert.Equal(new[] { 8.0, 9, 11, 12 }, again.Ir.Dancers[0].Tracks[Channels.LeftArm].Select(s => s.At).ToArray());
        Assert.Equal(new[] { 9.0, 11, 12, 14 }, again.Ir.Dancers[0].Tracks[Channels.LeftArm].Select(s => s.Until).ToArray());
    }

    [Fact]
    public void Seconds_are_counted_at_the_tempo_their_section_begins_in()
    {
        string text = To("changing.lys", "section A { solo { arms { side:1s } } }\nsection B { solo { arms { fwd:1s } } }");
        var arm = ShosaCompilation.Compile(text, null, Changing).Ir.Dancers[0].Tracks[Channels.LeftArm];

        // A second is two beats at 120 and one at 60: that long each word has the arm.
        Assert.Equal(new[] { (0.0, 2.0), (8.0, 9.0) }, arm.Select(s => (s.At, s.Until)).ToArray());
    }

    private static string SamplePath([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "..", "samples", "dance.shosa");

    [Fact]
    public void The_changes_of_a_Lily_song_reach_the_clip()
    {
        // samples/turning.lys: 4/4 at 120, 90 in the fourth bar; then 3/4, at 120 again.
        string path = Path.Combine(Path.GetDirectoryName(SamplePath())!, "turning.shosa");
        var compilation = ShosaCompilation.Compile(File.ReadAllText(path), null, SongReader.For(path));
        var clip = compilation.Clip;

        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(new TempoIr[] { new(0, 120), new(12, 90), new(16, 120) }, clip.Tempos);
        Assert.Equal(new MeterIr[] { new(0, 4, 4), new(16, 3, 4) }, clip.Meters);
        Assert.Equal(28.0, clip.TotalBeats);
        Assert.Equal(14.6667, clip.SecondsAt(28), 4);
        // The notes come in the same beats: the Waltz begins on beat 16, and its last note is a bar of three.
        var notes = new SongReader(Path.GetDirectoryName(path)!).Notes(clip.Music!).Notes;
        Assert.Contains(notes, n => n[0] == 16);
        Assert.Equal(new[] { 25.0, 3.0 }, notes[^1][..2]);
    }

    [Fact]
    public void The_sample_is_danced_to_its_Lily_song()
    {
        // The real thing: shosa-music reads samples/song.lys with Lily#.
        Assert.NotNull(SongReader.Program);
        var compilation = ShosaCompilation.Compile(File.ReadAllText(SamplePath()), null, SongReader.For(SamplePath()));

        Assert.Empty(compilation.Diagnostics);
        Assert.Equal((108.0, 4, 32.0), (compilation.Clip.Tempo, compilation.Clip.BeatsPerBar, compilation.Clip.TotalBeats));
        Assert.Equal(new[] { "Verse", "Chorus" }, compilation.Clip.Sections.Select(s => s.Name).ToArray());
    }

    [Fact]
    public void The_notes_of_the_song_come_in_the_beats_of_the_clip()
    {
        var compilation = ShosaCompilation.Compile(File.ReadAllText(SamplePath()), null, SongReader.For(SamplePath()));
        var song = new SongReader(Path.GetDirectoryName(SamplePath())!).Notes(compilation.Clip.Music!);

        Assert.Null(song.Error);
        Assert.True(song.Notes.Count >= 26, $"{song.Notes.Count} notes");   // the tune alone has 26
        Assert.All(song.Notes, n => Assert.Equal(7, n.Length));
        // The instrument and the channel a sound font plays it with: a program or none, never the drums' channel for a tune.
        Assert.All(song.Notes, n => Assert.True(n[5] is >= -1 and <= 127 && n[6] is >= 0 and <= 15 && (n[6] == 9) == (n[4] == 9)));
        Assert.Equal(0, song.Notes[0][0]);
        Assert.Equal(song.Notes.Select(n => n[0]).Order().ToArray(), song.Notes.Select(n => n[0]).ToArray());
        Assert.All(song.Notes, n => Assert.True(n[0] + n[1] <= compilation.Clip.TotalBeats + 1e-6 && n[2] is > 20 and < 110 && n[3] is >= 1 and <= 127));
        // The Chorus begins on its a' four bars in: a note starts on beat 16.
        Assert.Contains(song.Notes, n => n[0] == 16);
    }

    [Fact]
    public void The_page_of_shosa_preview_carries_the_notes_and_the_synthesiser()
    {
        var compilation = ShosaCompilation.Compile(File.ReadAllText(SamplePath()), null, SongReader.For(SamplePath()));
        var song = new SongReader(Path.GetDirectoryName(SamplePath())!).Notes(compilation.Clip.Music!);

        string with = Shosa.Core.Output.PreviewHtml.Render(compilation.Clip, "", song.Notes);
        string without = Shosa.Core.Output.PreviewHtml.Render(compilation.Clip, "");

        // The same sound.js the VS Code preview bundles, as a function of the page's one script.
        Assert.Contains("function createSound()", with);
        Assert.DoesNotContain("export function", with);
        Assert.Contains("\"notes\":[[0,", with);
        Assert.Contains("\"notes\":null", without);
        // Without the song the page clicks on the beats instead.
        Assert.Contains("function clicksOf(clip)", without);
        Assert.Contains("player.setSong(notes ?? clicksOf(clip))", without);
    }

    [Fact]
    public void A_saved_song_brings_the_dance_up_to_date()
    {
        // The editor watches .lys files; the dance is compiled again without being edited.
        string folder = Directory.CreateTempSubdirectory("shosa-song").FullName;
        try
        {
            string song = Path.Combine(folder, "song.lys"), dance = Path.Combine(folder, "dance.shosa");
            string lys = File.ReadAllText(Path.Combine(Path.GetDirectoryName(SamplePath())!, "song.lys"));
            File.WriteAllText(song, lys);
            string uri = new Uri(dance).AbsoluteUri;

            var sent = new List<string>();
            var server = new Shosa.Lsp.LanguageServer(sent.Add);
            void Tell(string method, object parameters) =>
                server.Handle(System.Text.Json.JsonSerializer.Serialize(new { jsonrpc = "2.0", method, @params = parameters }));
            (int Diagnostics, double Tempo) Last()
            {
                using var diagnostics = System.Text.Json.JsonDocument.Parse(sent.Last(m => m.Contains("publishDiagnostics")));
                using var clip = System.Text.Json.JsonDocument.Parse(sent.Last(m => m.Contains("\"shosa/clip\"")));
                return (diagnostics.RootElement.GetProperty("params").GetProperty("diagnostics").GetArrayLength(),
                    clip.RootElement.GetProperty("params").GetProperty("clip").GetProperty("tempo").GetDouble());
            }

            string text = To("song.lys", "section Verse { solo { arms { side:4 | } } }");
            Tell("textDocument/didOpen", new { textDocument = new { uri, version = 1, text } });
            Assert.Equal((0, 108.0), Last());

            // VS Code writes a Windows drive's colon as %3A; the song is still found beside the file.
            if (OperatingSystem.IsWindows())
            {
                string encoded = uri.Replace(":/", "%3A/").Replace("file%3A/", "file:/");
                Assert.Contains("%3A", encoded);
                Tell("textDocument/didOpen", new { textDocument = new { uri = encoded, languageId = "shosa", version = 1, text } });
                Assert.Equal((0, 108.0), Last());
                Tell("textDocument/didClose", new { textDocument = new { uri = encoded } });
            }

            // The song gets a new tempo and loses the name the dance uses.
            sent.Clear();
            File.WriteAllText(song, lys.Replace("tempo 108", "tempo 96").Replace("Verse", "Intro"));
            File.SetLastWriteTimeUtc(song, DateTime.UtcNow.AddSeconds(2));
            Tell("workspace/didChangeWatchedFiles", new { changes = new[] { new { uri = new Uri(song).AbsoluteUri, type = 2 } } });
            Assert.Equal((1, 96.0), Last());   // SH1905: the song has no Verse
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_music_line_is_completed_from_the_songs_beside_the_file()
    {
        string folder = Directory.CreateTempSubdirectory("shosa-complete").FullName;
        try
        {
            string lys = File.ReadAllText(Path.Combine(Path.GetDirectoryName(SamplePath())!, "song.lys"));
            File.WriteAllText(Path.Combine(folder, "my-song.lys"), lys + "\nscore practice {\n  staff melody\n}\n");
            Directory.CreateDirectory(Path.Combine(folder, "tunes"));
            File.WriteAllText(Path.Combine(folder, "tunes", "other.lys"), lys);
            File.WriteAllText(Path.Combine(folder, "notes.txt"), "");
            string uri = new Uri(Path.Combine(folder, "dance.shosa")).AbsoluteUri;

            string? answer = null;
            var server = new Shosa.Lsp.LanguageServer(message => answer = message);
            void Tell(string method, object parameters) =>
                server.Handle(System.Text.Json.JsonSerializer.Serialize(new { jsonrpc = "2.0", method, @params = parameters }));
            List<System.Text.Json.JsonElement> Complete(string text)
            {
                // The cursor is at the end of the first line.
                Tell("textDocument/didOpen", new { textDocument = new { uri, languageId = "shosa", version = 1, text } });
                int character = text.IndexOf('\n') is >= 0 and var end ? end : text.Length;
                server.Handle(System.Text.Json.JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0", id = 1, method = "textDocument/completion",
                    @params = new { textDocument = new { uri }, position = new { line = 0, character } },
                }));
                return System.Text.Json.JsonDocument.Parse(answer!).RootElement.GetProperty("result").EnumerateArray().ToList();
            }
            string[] Labels(string text) => Complete(text).Select(i => i.GetProperty("label").GetString()!).ToArray();

            // Inside the quotes: the .lys files, the nearer first, written with '/'.
            Assert.Equal(new[] { "my-song.lys", "tunes/other.lys" }, Labels("music \""));
            // The name replaces what has been typed since the quote: an editor would stop at the dash.
            var item = Complete("music \"my-so\nsection A { solo { arms { up } } }\n")[0];
            var range = item.GetProperty("textEdit").GetProperty("range");
            Assert.Equal(7, range.GetProperty("start").GetProperty("character").GetInt32());
            Assert.Equal(12, range.GetProperty("end").GetProperty("character").GetInt32());
            Assert.Equal("my-song.lys", item.GetProperty("textEdit").GetProperty("newText").GetString());

            Assert.Equal(new[] { "score" }, Labels("music \"my-song.lys\" "));
            Assert.Equal(new[] { "practice" }, Labels("music \"my-song.lys\" score "));
            Assert.Empty(Labels("music \"tunes/other.lys\" score "));
            // In a score, on one line with the rest.
            Assert.Equal(new[] { "practice" }, Labels("score slow { tempo 60 music \"my-song.lys\" score pr"));
            Assert.Empty(Labels("title \""));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_language_server_hands_the_notes_to_the_preview()
    {
        string? answer = null;
        var server = new Shosa.Lsp.LanguageServer(message => answer = message);
        server.Handle(System.Text.Json.JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0", id = 1, method = "shosa/song",
            @params = new { uri = new Uri(Path.GetFullPath(SamplePath())).AbsoluteUri, file = "song.lys", score = (string?)null, order = new[] { "Chorus" } },
        }));

        using var reply = System.Text.Json.JsonDocument.Parse(answer!);
        var result = reply.RootElement.GetProperty("result");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, result.GetProperty("error").ValueKind);
        // The Chorus alone: 12 notes of the tune at least, all within its 16 beats.
        Assert.True(result.GetProperty("notes").GetArrayLength() >= 12);
        Assert.All(result.GetProperty("notes").EnumerateArray(), n => Assert.True(n[0].GetDouble() < 16));
    }
}
