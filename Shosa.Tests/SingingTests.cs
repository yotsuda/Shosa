using System.Text;
using System.Text.Json;
using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Output;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

/// <summary>A dancer whose mouth sings a part of the song: `dancer alice … sings melody` (DESIGN.md 99).</summary>
public class SingingTests
{
    /// <summary>A song in 4/4 at 120 (a beat is half a second): A is 8 beats, and `singer on melody` sings そ ら ー ん きゃ.</summary>
    private static SongSource Song(List<SongSung>? sung) => (file, score, order) => new SongInfo
    {
        Tempo = 120,
        Sections = ["A"],
        Plays = [new SongPlay("A", 8)],
        Sung = sung,
    };

    private static readonly List<SongSung> Melody =
    [
        new("melody", null, Syllables((0, 1, "そ"), (1, 1, "ら"), (2, 1, "ー"), (3, 1, "ん"), (4, 2, "きゃ"))),
    ];

    private static List<JsonElement[]> Syllables(params (double Beat, double Beats, string Text)[] syllables) =>
        syllables.Select(s => JsonSerializer.Deserialize<JsonElement[]>(JsonSerializer.Serialize(new object[] { s.Beat, s.Beats, s.Text }))!).ToList();

    private static string Dance(string sings = " sings melody", string music = "music \"song.lys\"\n") =>
        $"{music}role solo\ndancer alice humanoid \"girl.vrm\"{sings} on solo\nsection A {{ solo {{ arms {{ side:4 down:4 }} }} }}\n" +
        (music.Length == 0 ? "form { A }\n" : "") + "score { dancer alice }\n";

    [Fact]
    public void Each_syllable_opens_the_mouth_to_its_vowel_on_its_beat()
    {
        var compilation = ShosaCompilation.Compile(Dance(), null, Song(Melody));
        Assert.Empty(compilation.Diagnostics);

        // ー holds the shape before it; ん closes; きゃ is a.
        var sung = compilation.Ir.Dancers[0].Sings!;
        Assert.Equal(new[] { "oh", "aa", "aa", "nn", "aa" }, sung.Select(s => s.Vowel).ToArray());

        var figure = compilation.Clip.Figures[0];
        var mouth = figure.Mouth!;
        Assert.Equal(figure.Frames.Count, mouth.Count);
        int At(double seconds) => (int)Math.Round(seconds * compilation.Clip.Fps);
        int aa = Array.IndexOf(Mouth.Shapes, "aa"), oh = Array.IndexOf(Mouth.Shapes, "oh");
        Assert.True(mouth[At(0.25)][oh] > 0.5);       // そ
        Assert.True(mouth[At(0.75)][aa] > 0.5);       // ら
        Assert.True(mouth[At(1.25)][aa] > 0.5);       // ー: still a
        Assert.True(mouth[At(1.75)].Max() < 0.01);    // ん: shut
        Assert.True(mouth[At(2.5)][aa] > 0.5);        // きゃ
        Assert.True(mouth[At(3.5)].Max() < 0.01);     // sung no more

        // What the language server sends: the weights for every frame, five to a frame.
        var json = JsonDocument.Parse(JsonSerializer.Serialize(compilation.Clip, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })).RootElement;
        Assert.Equal(5, json.GetProperty("figures")[0].GetProperty("mouth")[0].GetArrayLength());
    }

    [Fact]
    public void A_VRM_Animation_keys_the_five_mouth_expressions()
    {
        var compilation = ShosaCompilation.Compile(Dance(), null, Song(Melody));
        var glb = Gltf.RenderVrma(compilation.Clip, compilation.Clip.Figures[0]);
        int length = BitConverter.ToInt32(glb, 12);
        var json = JsonDocument.Parse(Encoding.UTF8.GetString(glb, 20, length)).RootElement;
        var preset = json.GetProperty("extensions").GetProperty("VRMC_vrm_animation").GetProperty("expressions").GetProperty("preset");
        Assert.Equal(Mouth.Shapes, preset.EnumerateObject().Select(p => p.Name).ToArray());
        var nodes = json.GetProperty("nodes");
        int node = preset.GetProperty("aa").GetProperty("node").GetInt32();
        Assert.Equal("Expression_aa", nodes[node].GetProperty("name").GetString());
        Assert.Contains(json.GetProperty("animations")[0].GetProperty("channels").EnumerateArray(),
            c => c.GetProperty("target").GetProperty("node").GetInt32() == node && c.GetProperty("target").GetProperty("path").GetString() == "translation");

        // Without sings there is nothing of the mouth.
        var silent = ShosaCompilation.Compile(Dance(sings: ""), null, Song(Melody));
        Assert.Null(silent.Clip.Figures[0].Mouth);
        var plain = Gltf.RenderVrma(silent.Clip, silent.Clip.Figures[0]);
        Assert.DoesNotContain("expressions", Encoding.UTF8.GetString(plain, 20, BitConverter.ToInt32(plain, 12)));
    }

    [Fact]
    public void A_part_nobody_sings_is_a_warning_that_names_the_parts_sung()
    {
        var compilation = ShosaCompilation.Compile(Dance(" sings melodie"), null, Song(Melody));
        var warning = Assert.Single(compilation.Diagnostics);
        Assert.Equal(("SH1908", DiagnosticSeverity.Warning), (warning.Code, warning.Severity));
        Assert.Contains("melody", warning.Message);
        Assert.Null(compilation.Clip.Figures[0].Mouth);

        Assert.Equal("SH1908", Assert.Single(ShosaCompilation.Compile(Dance(), null, Song([])).Diagnostics).Code);
    }

    [Fact]
    public void A_Lily_sharp_that_does_not_say_what_is_sung_leaves_the_mouth_shut()
    {
        var compilation = ShosaCompilation.Compile(Dance(), null, Song(null));
        var info = Assert.Single(compilation.Diagnostics);
        Assert.Equal(("SH1909", DiagnosticSeverity.Info), (info.Code, info.Severity));
        Assert.Null(compilation.Clip.Figures[0].Mouth);
    }

    [Fact]
    public void Sings_without_a_song_or_a_part_is_an_error()
    {
        Assert.Equal("SH1908", Assert.Single(ShosaCompilation.Compile(Dance(music: "")).Diagnostics).Code);
        Assert.Equal("SH1006", Assert.Single(ShosaCompilation.Compile(Dance(" sings"), null, Song(Melody)).Diagnostics).Code);
    }

    [Fact]
    public void The_lips_meet_before_a_syllable_that_starts_with_them()
    {
        // や ま: the same a, but the m shuts the mouth in between.
        var sung = new List<SongSung> { new("melody", null, Syllables((0, 1, "や"), (1, 1, "ま"), (2, 1, "あ"))) };
        var compilation = ShosaCompilation.Compile(Dance(), null, Song(sung));
        var mouth = compilation.Clip.Figures[0].Mouth!;
        int At(double seconds) => (int)Math.Round(seconds * compilation.Clip.Fps);
        int aa = Array.IndexOf(Mouth.Shapes, "aa");
        Assert.True(mouth[At(0.25)][aa] > 0.5);       // や
        Assert.True(mouth[At(0.47)].Max() < 0.01);    // the lips meet just before ま's beat
        Assert.True(mouth[At(0.75)][aa] > 0.5);       // ま
        Assert.True(mouth[At(1.0)][aa] > 0.3);        // ま あ: no lips between, it does not shut
    }

    [Theory]
    [InlineData("ま", true)]
    [InlineData("ビ", true)]
    [InlineData("ぴゃ", true)]
    [InlineData("な", false)]
    [InlineData("ー", false)]
    [InlineData("baby", true)]
    [InlineData("love", false)]
    public void Which_syllables_start_with_the_lips(string text, bool bilabial) => Assert.Equal(bilabial, Mouth.Bilabial(text));

    [Theory]
    [InlineData("か", "aa")]
    [InlineData("シ", "ih")]
    [InlineData("きゅ", "ou")]
    [InlineData("ね", "ee")]
    [InlineData("ろ", "oh")]
    [InlineData("ん", "nn")]
    [InlineData("っ", "nn")]
    [InlineData("ー", null)]
    [InlineData("love", "oh")]
    [InlineData("hm", "nn")]
    public void The_vowel_of_a_syllable(string text, string? vowel) => Assert.Equal(vowel, Mouth.Vowel(text));
}
