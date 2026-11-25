using System.Text.Json;
using Shosa.Core;

namespace Shosa.Tests;

/// <summary>
/// The clip says how much each hand is at a place of the dancer's own body at every frame (`touch`), for the preview
/// to put a character's hand at that place on its own body (DESIGN.md 122).
/// </summary>
public class TouchTests
{
    private static ShosaCompilation Compile(string tracks) => ShosaCompilation.Compile(Samples.Solo(tracks, "tempo 60\ntime 4/4\n"));

    private static double At(ShosaCompilation c, string hand, string place, double beat) =>
        c.Clip.Figures[0].Touch!.Single(t => t.Hand == hand && t.Place == place).Weight[c.Clip.FrameOfBeat(beat)];

    [Fact]
    public void A_hand_is_at_its_place_from_the_word_until_it_sets_out_again()
    {
        // At the head on beat 2 (gone to through beats 0-2), there through the word, away over the `then` (4-6).
        var c = Compile("    rightArm { down:2 head:2 then:2 down:2 }");
        Assert.Empty(c.Diagnostics);
        var touch = Assert.Single(c.Clip.Figures[0].Touch!);
        Assert.Equal(("right", "head"), (touch.Hand, touch.Place));
        Assert.Equal(c.Clip.Figures[0].Frames.Count, touch.Weight.Count);

        Assert.Equal(0, At(c, "right", "head", 0), 3);
        Assert.InRange(At(c, "right", "head", 1), 0.2, 0.8);     // on its way
        Assert.Equal(1, At(c, "right", "head", 2), 3);
        Assert.Equal(1, At(c, "right", "head", 4), 3);           // stayed through its word
        Assert.InRange(At(c, "right", "head", 5), 0.2, 0.8);     // on its way down
        Assert.Equal(0, At(c, "right", "head", 6), 3);
    }

    [Fact]
    public void From_one_place_to_another_the_two_shares_make_one()
    {
        var c = Compile("    rightArm { head:2 chin:2 }");
        Assert.Equal(1, At(c, "right", "head", 0), 3);
        Assert.Equal(1, At(c, "right", "head", 1) + At(c, "right", "chin", 1), 2);
        Assert.Equal(1, At(c, "right", "chin", 2), 3);
    }

    [Fact]
    public void Both_arms_at_a_place_are_two_hands_and_a_direction_is_none()
    {
        var both = Compile("    arms { down:2 chin:2 }").Clip.Figures[0].Touch!;
        Assert.Equal(["left", "right"], both.Select(t => t.Hand).Order());
        Assert.All(both, t => Assert.Equal("chin", t.Place));

        Assert.Null(Compile("    arms { down:2 side.up(60).bent(130):2 }").Clip.Figures[0].Touch);
    }

    [Fact]
    public void The_clip_carries_it_for_the_preview()
    {
        var clip = Compile("    leftArm { down:2 forehead:2 }").Clip;
        var json = JsonDocument.Parse(JsonSerializer.Serialize(clip, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })).RootElement;
        var touch = json.GetProperty("figures")[0].GetProperty("touch")[0];
        Assert.Equal("left", touch.GetProperty("hand").GetString());
        Assert.Equal("forehead", touch.GetProperty("place").GetString());
        Assert.Equal(clip.Figures[0].Frames.Count, touch.GetProperty("weight").GetArrayLength());

        // A figure with no hand at a place has no `touch` at all.
        var none = JsonDocument.Parse(JsonSerializer.Serialize(Compile("    arms { down:2 side:2 }").Clip, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })).RootElement;
        Assert.False(none.GetProperty("figures")[0].TryGetProperty("touch", out _));
    }
}
