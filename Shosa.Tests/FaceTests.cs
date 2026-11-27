using System.Text;
using System.Text.Json;
using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Output;
using Shosa.Core.Semantics;

namespace Shosa.Tests;

/// <summary>The face: `face { smile(50):3 smile(50).winkRight:1 }`, and the eyes blinking by themselves (DESIGN.md 131).</summary>
public class FaceTests
{
    private const int Happy = 0, Angry = 1, Sad = 2, Relaxed = 3, Surprised = 4, LeftEye = 5, RightEye = 6;

    // 60 beats a minute: a beat is a second.
    private static ShosaCompilation Compile(string tracks, string head = "tempo 60\ntime 4/4\n", string cast = Samples.SoloCast) =>
        ShosaCompilation.Compile(Samples.Solo(tracks, head).Replace(Samples.SoloCast, cast));

    private static float[] Face(ShosaCompilation compilation, double beat) =>
        compilation.Clip.Figures[0].Face![(int)Math.Round(beat * compilation.Clip.Fps)];

    [Fact]
    public void A_word_is_the_whole_face_on_its_beat()
    {
        var compilation = Compile("    steps { stand:8 }\n    face { smile(50):2 smile(60).winkRight:1 sad:1 | relaxed.closed:2 surprised(80):1 neutral:1 }");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal([0.5f, 0, 0, 0, 0, 0, 0], Face(compilation, 0));
        Assert.Equal([0.5f, 0, 0, 0, 0, 0, 0], Face(compilation, 1.5));
        Assert.Equal([0.6f, 0, 0, 0, 0, 0, 1], Face(compilation, 2));
        Assert.Equal([0, 0, 1, 0, 0, 0, 0], Face(compilation, 3));
        Assert.Equal([0, 0, 0, 1, 0, 1, 1], Face(compilation, 5));
        Assert.Equal([0, 0, 0, 0, 0.8f, 0, 0], Face(compilation, 6));
        Assert.Equal(new float[7], Face(compilation, 7.5));
    }

    [Fact]
    public void It_changes_in_a_moment_before_its_beat_and_not_through_the_word_before()
    {
        var compilation = Compile("    steps { stand:8 }\n    face { neutral:4 smile.closed:4 }");
        // A look takes the last 0.2 s, an eye the last 0.08 s.
        Assert.Equal(0, Face(compilation, 3.75)[Happy]);
        Assert.InRange(Face(compilation, 3.9)[Happy], 0.3, 0.7);
        Assert.Equal(0, Face(compilation, 3.9)[LeftEye]);
        Assert.InRange(Face(compilation, 3.9667)[LeftEye], 0.3, 0.9);
        Assert.Equal(1, Face(compilation, 4)[Happy]);
        Assert.Equal(1, Face(compilation, 4)[RightEye]);
    }

    [Fact]
    public void A_mirrored_dancer_and_a_mirrored_word_wink_the_other_eye()
    {
        var word = Compile("    steps { stand:4 }\n    face { winkRight:2 winkRight:2@mirror }");
        Assert.Empty(word.Diagnostics);
        Assert.Equal((0f, 1f), (Face(word, 1)[LeftEye], Face(word, 1)[RightEye]));
        Assert.Equal((1f, 0f), (Face(word, 3)[LeftEye], Face(word, 3)[RightEye]));
        var dancer = Compile("    steps { stand:4 }\n    face { winkRight:4 }", cast: "role solo\ndancer solo humanoid on solo mirror\n");
        Assert.Empty(dancer.Diagnostics);
        Assert.Equal((1f, 0f), (Face(dancer, 1)[LeftEye], Face(dancer, 1)[RightEye]));
    }

    [Fact]
    public void A_pose_holds_a_face_and_the_faces_words_are_not_reserved()
    {
        var compilation = Compile("    body { cheer:2 smile:2 }\n    face { skip:2 smile(30):2 }",
            "tempo 60\ntime 4/4\npose cheer { arms { up }  face { smile } }\nmove smile { arms { side:1 down:1 } }\n");
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(1, Face(compilation, 1)[Happy]);
        Assert.Equal(0.3f, Face(compilation, 3)[Happy]);
    }

    [Fact]
    public void Without_a_row_and_without_life_there_is_no_face_and_a_then_of_the_body_holds_none()
    {
        Assert.Null(Compile("    steps { stand:4 }\n    arms { side:4 }").Clip.Figures[0].Face);
        Assert.Null(ShosaCompilation.Compile(Samples.Read("fighter")).Clip.Figures[0].Face);
    }

    [Fact]
    public void The_eyes_blink_by_themselves_but_not_beside_a_wink()
    {
        using var life = Solver.Life(true);
        var plain = Compile("    steps { stand:4 | then:7bar }").Clip;
        var face = plain.Figures[0].Face!;
        Assert.Equal(plain.Figures[0].Frames.Count, face.Count);
        // Both eyes together, shut all the way, a few times in 32 seconds, and open at the start and the end.
        Assert.All(face, row => Assert.Equal(row[LeftEye], row[RightEye]));
        int blinks = Enumerable.Range(1, face.Count - 1).Count(f => face[f][LeftEye] >= 1 && face[f - 1][LeftEye] < 1);
        Assert.InRange(blinks, 5, 15);
        Assert.Equal(0, face[0][LeftEye]);
        Assert.Equal(0, face[^1][LeftEye]);
        Assert.All(face, row => Assert.Equal(0, row[Happy]));
        // The same every time; another dancer at other moments.
        Assert.Equal(face, Compile("    steps { stand:4 | then:7bar }").Clip.Figures[0].Face!);
        var seconds = Enumerable.Range(0, 900).Select(f => f / 30.0).ToList();
        var anna = Shosa.Core.Motion.Face.Frames(null, seconds, beat => beat, "anna", blinks: true, 30, loop: false)!;
        var bella = Shosa.Core.Motion.Face.Frames(null, seconds, beat => beat, "bella", blinks: true, 30, loop: false)!;
        Assert.Contains(Enumerable.Range(0, 900), f => anna[f][LeftEye] != bella[f][LeftEye]);
        Assert.Null(Shosa.Core.Motion.Face.Frames(null, seconds, beat => beat, "anna", blinks: false, 30, loop: false));

        // A wink through the whole dance: the open eye does not blink beside it.
        var wink = Compile("    steps { stand:4 | then:7bar }\n    face { winkLeft }").Clip.Figures[0].Face!;
        Assert.All(wink, row => Assert.Equal((1f, 0f), (row[LeftEye], row[RightEye])));
    }

    [Fact]
    public void A_figure_of_sticks_is_not_sent_its_face_and_a_character_is()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var sticks = Compile("    steps { stand:4 }\n    face { smile:4 }");
        Assert.False(JsonDocument.Parse(JsonSerializer.Serialize(sticks.Clip, options)).RootElement.GetProperty("figures")[0].TryGetProperty("face", out _));
        var character = Compile("    steps { stand:4 }\n    face { smile:4 }", cast: "role solo\ndancer solo humanoid \"girl.vrm\" on solo\n");
        Assert.Empty(character.Diagnostics);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(character.Clip, options)).RootElement;
        Assert.Equal(7, json.GetProperty("figures")[0].GetProperty("face")[0].GetArrayLength());
    }

    [Fact]
    public void A_VRM_Animation_keys_the_looks_and_the_eyes()
    {
        var compilation = Compile("    steps { stand:4 }\n    face { smile:2 winkLeft:2 }");
        var glb = Gltf.RenderVrma(compilation.Clip, compilation.Clip.Figures[0]);
        var json = JsonDocument.Parse(Encoding.UTF8.GetString(glb, 20, BitConverter.ToInt32(glb, 12))).RootElement;
        var preset = json.GetProperty("extensions").GetProperty("VRMC_vrm_animation").GetProperty("expressions").GetProperty("preset");
        Assert.Equal(Vocabulary.FaceShapes, preset.EnumerateObject().Select(p => p.Name).ToArray());
        int node = preset.GetProperty("happy").GetProperty("node").GetInt32();
        Assert.Equal("Expression_happy", json.GetProperty("nodes")[node].GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("grin:4", "SH1012", "not a word of the face")]
    [InlineData("smile.sad:4", "SH1012", "one look")]
    [InlineData("closed.smile:4", "SH1012", "after the eyes")]
    [InlineData("closed.winkLeft:4", "SH1012", "said once")]
    [InlineData("smile(150):4", "SH1013", "percent")]
    [InlineData("neutral(50):4", "SH1012", "no brackets")]
    [InlineData("smile:4@sharp", "SH1010", "not for the face")]
    public void What_a_face_cannot_say(string word, string code, string says)
    {
        var compilation = Compile($"    steps {{ stand:4 }}\n    face {{ {word} }}");
        var diagnostic = Assert.Single(compilation.Diagnostics);
        Assert.Equal(code, diagnostic.Code);
        Assert.Contains(says, diagnostic.Message);
    }
}
