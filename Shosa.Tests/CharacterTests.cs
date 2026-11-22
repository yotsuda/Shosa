using System.Text.Json;
using Shosa.Core;
using Shosa.Core.Motion;

namespace Shosa.Tests;

/// <summary>The character a dancer line names for the preview: `dancer alice humanoid "girl.vrm"` (DESIGN.md 96).</summary>
public class CharacterTests
{
    private const string Tracks = "section P {\n  a {\n    arms { side:2 down:2 }\n  }\n  b {\n    arms { side.up:2 down:2 }\n  }\n}\nform { P }\nscore { dancer alice  dancer bob }\n";

    [Fact]
    public void A_dancer_line_may_name_a_VRM_file_and_only_that_figure_carries_its_bone_turns()
    {
        var compilation = ShosaCompilation.Compile("role a\nrole b\ndancer alice humanoid \"models/girl.vrm\" on a at -1 0\ndancer bob humanoid on b at 1 0\n" + Tracks);
        Assert.Empty(compilation.Diagnostics);
        var clip = compilation.Clip;
        Assert.Equal("models/girl.vrm", clip.Figures.Single(f => f.Id == "alice").Model);
        Assert.Null(clip.Figures.Single(f => f.Id == "bob").Model);

        // What the language server sends the preview: rotations for the dressed figure only, by VRM bone names.
        var json = JsonDocument.Parse(JsonSerializer.Serialize(clip, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })).RootElement;
        var figures = json.GetProperty("figures").EnumerateArray().ToList();
        Assert.Equal(figures[0].GetProperty("frames").GetArrayLength(), figures[0].GetProperty("turns").GetArrayLength());
        Assert.Equal(Skeleton.Bones.Length * 4, figures[0].GetProperty("turns")[0].GetArrayLength());
        Assert.False(figures[1].TryGetProperty("turns", out _));
        Assert.False(figures[1].TryGetProperty("model", out _));
        Assert.Equal("leftUpperArm", json.GetProperty("skeletonBones")[Skeleton.LUpperArm].GetString());
        Assert.True(json.GetProperty("hipsHeight").GetDouble() > 0.8);
    }

    [Theory]
    [InlineData("height 1.5m", 1.5)]
    [InlineData("height 150cm", 1.5)]
    [InlineData("height 1.5", 1.5)]
    public void Height_says_how_tall_the_character_is_and_changes_nothing_of_the_movement(string clause, double metres)
    {
        var plain = ShosaCompilation.Compile("role a\nrole b\ndancer alice humanoid \"girl.vrm\" on a at -1 0\ndancer bob humanoid \"boy.vrm\" on b at 1 0\n" + Tracks);
        var sized = ShosaCompilation.Compile($"role a\nrole b\ndancer alice humanoid \"girl.vrm\" {clause} on a at -1 0\ndancer bob humanoid \"boy.vrm\" on b at 1 0\n" + Tracks);
        Assert.Empty(sized.Diagnostics);
        Assert.Equal(metres, sized.Clip.Figures.Single(f => f.Id == "alice").ModelHeight!.Value, 9);
        Assert.Null(sized.Clip.Figures.Single(f => f.Id == "bob").ModelHeight);
        Assert.Equal(plain.Clip.Figures[0].Frames.Last(), sized.Clip.Figures[0].Frames.Last());

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var figures = JsonDocument.Parse(JsonSerializer.Serialize(sized.Clip, options)).RootElement.GetProperty("figures").EnumerateArray().ToList();
        Assert.Equal(metres, figures[0].GetProperty("modelHeight").GetDouble(), 9);
        Assert.False(figures[1].TryGetProperty("modelHeight", out _));
    }

    [Theory]
    [InlineData("height 0")]
    [InlineData("height -1m")]
    [InlineData("height 6m")]
    [InlineData("height")]
    [InlineData("height tall")]
    [InlineData("height 1.5ft")]
    public void A_height_that_is_not_a_height_is_an_error(string clause)
    {
        var compilation = ShosaCompilation.Compile($"role a\nrole b\ndancer alice humanoid \"girl.vrm\" {clause} on a\ndancer bob humanoid on b at 1 0\n" + Tracks);
        var error = Assert.Single(compilation.Diagnostics);
        Assert.Equal("SH1006", error.Code);
        Assert.Null(compilation.Clip.Figures[0].ModelHeight);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"girl.png\"")]
    public void A_character_that_is_not_a_VRM_file_is_an_error(string model)
    {
        var compilation = ShosaCompilation.Compile($"role a\nrole b\ndancer alice humanoid {model} on a\ndancer bob humanoid on b at 1 0\n" + Tracks);
        var error = Assert.Single(compilation.Diagnostics);
        Assert.Equal("SH1006", error.Code);
        Assert.Null(compilation.Clip.Figures[0].Model);
    }
}
