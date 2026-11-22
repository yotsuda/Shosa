using System.Text.Json;
using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

/// <summary>A dancer with a mic in a hand: `dancer alice … sings melody mic right` (DESIGN.md 117).</summary>
public class MicTests
{
    /// <summary>
    /// A song in 4/4 at 120 (a beat is half a second), 16 beats. Sung: あ い (beats 0, 1), a breath, う (3), then
    /// nothing for 4 s, then え (12, two beats). The breath is shorter than a phrase's join: one phrase, 0..4.
    /// </summary>
    private static readonly SongSource Song = (file, score, order) => new SongInfo
    {
        Tempo = 120,
        Sections = ["A"],
        Plays = [new SongPlay("A", 16)],
        Sung = [new("melody", null, Syllables((0, 1, "あ"), (1, 1, "い"), (3, 1, "う"), (12, 2, "え")))],
    };

    private static List<JsonElement[]> Syllables(params (double Beat, double Beats, string Text)[] syllables) =>
        syllables.Select(s => JsonSerializer.Deserialize<JsonElement[]>(JsonSerializer.Serialize(new object[] { s.Beat, s.Beats, s.Text }))!).ToList();

    private static string Dance(string mic = " mic right", string rows = "head { fwd:16 }") =>
        $"music \"song.lys\"\nrole solo\ndancer alice humanoid sings melody{mic} on solo\nsection A {{ solo {{ {rows} }} }}\nscore {{ dancer alice }}\n";

    private static ShosaCompilation Compile(string text) => ShosaCompilation.Compile(text, null, Song);

    private static int FrameAt(ShosaCompilation c, double beat) => c.Clip.FrameOfBeat(beat);

    private static double Up(ShosaCompilation c, double beat) => c.Clip.Figures[0].Mic!.Up[FrameAt(c, beat)];

    [Fact]
    public void The_mic_is_up_for_a_phrase_and_down_between_phrases()
    {
        var c = Compile(Dance());
        Assert.Empty(c.Diagnostics);
        Assert.Equal("right", c.Clip.Figures[0].Mic!.Hand);
        Assert.Equal(c.Clip.Figures[0].Frames.Count, c.Clip.Figures[0].Mic!.Up.Count);

        Assert.Equal(1, Up(c, 0), 3);       // the first syllable: up already
        Assert.Equal(1, Up(c, 2.5), 3);     // a breath inside the phrase: it stays up
        Assert.Equal(1, Up(c, 4.4), 3);     // a little after the last syllable
        Assert.Equal(0, Up(c, 8), 3);       // between phrases: in the hand
        double rising = Up(c, 11);          // 0.5 s before え: on the way up
        Assert.InRange(rising, 0.05, 0.95);
        Assert.Equal(1, Up(c, 11.6), 3);    // up a little before the syllable
    }

    [Fact]
    public void The_hand_is_at_the_mouth_while_singing_and_before_the_belly_between()
    {
        var c = Compile(Dance());
        var figure = c.Clip.Figures[0];
        int singing = FrameAt(c, 2), between = FrameAt(c, 8);

        // At the mouth: the wrist under the chin, in front of the face.
        var hand = figure.Joint(singing, Rig.RHand);
        var head = figure.Joint(singing, Rig.Head);
        Assert.InRange((hand - head).Length, 0.08, 0.25);
        Assert.InRange(head.Y - hand.Y, 0.05, 0.2);

        // Between: before the belly, the elbow bent; the other hand hangs as ever.
        var rest = figure.Joint(between, Rig.RHand);
        var chest = figure.Joint(between, Rig.Chest);
        var pelvis = figure.Joint(between, Rig.Pelvis);
        Assert.InRange(rest.Y, pelvis.Y, chest.Y - 0.15);
        Assert.True(rest.Z - chest.Z > 0.15, $"the hand is {rest.Z - chest.Z:0.00} m in front of the chest");
        Assert.True(figure.Angles(between).RightElbow > 70);
        Assert.True(figure.Joint(between, Rig.LHand).Y < pelvis.Y);
    }

    [Fact]
    public void Words_of_the_mic_hand_that_arrive_while_singing_are_a_warning()
    {
        // fwd at beat 2 is in the phrase; side at 8 is between; the left arm is free.
        var c = Compile(Dance(rows: "rightArm { skip:2 fwd:6 side:8 } leftArm { side:8 down:8 }"));
        var warning = Assert.Single(c.Diagnostics);
        Assert.Equal(("SH2018", DiagnosticSeverity.Warning), (warning.Code, warning.Severity));
        Assert.Equal("fwd", Dance(rows: "rightArm { skip:2 fwd:6 side:8 } leftArm { side:8 down:8 }").Substring(warning.Span.Start, 3));

        // The word between is danced: at beat 8 the right arm is out to the side.
        var figure = c.Clip.Figures[0];
        int at = FrameAt(c, 8);
        Assert.True(figure.Joint(at, Rig.RHand).X < figure.Joint(at, Rig.RShoulder).X - 0.4);

        // With the mic in the left hand, the right arm's words are all seen, and the left one's warn.
        var left = Compile(Dance(" mic left", "rightArm { skip:2 fwd:6 side:8 } leftArm { side:8 down:8 }"));
        Assert.Equal("SH2018", Assert.Single(left.Diagnostics).Code);   // side at beat 0
    }

    [Fact]
    public void Both_hands_hold_it_while_singing_and_the_right_one_between()
    {
        var c = Compile(Dance(" mic both"));
        Assert.Empty(c.Diagnostics);
        var figure = c.Clip.Figures[0];
        int singing = FrameAt(c, 2), between = FrameAt(c, 8);
        // The two hands together under the chin, the left a little lower.
        var right = figure.Joint(singing, Rig.RHand);
        var left = figure.Joint(singing, Rig.LHand);
        Assert.True((right - left).Length < 0.12, $"the hands are {(right - left).Length:0.00} m apart");
        Assert.True(left.Y < right.Y);
        // Between: the left hangs, the right holds the mic before the belly.
        var pelvis = figure.Joint(between, Rig.Pelvis);
        Assert.True(figure.Joint(between, Rig.LHand).Y < pelvis.Y);
        Assert.True(figure.Joint(between, Rig.RHand).Y > pelvis.Y);
    }

    [Fact]
    public void The_clip_says_the_hand_and_how_far_up_and_nothing_without_a_mic()
    {
        var json = JsonDocument.Parse(JsonSerializer.Serialize(Compile(Dance()).Clip, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })).RootElement;
        var mic = json.GetProperty("figures")[0].GetProperty("mic");
        Assert.Equal("right", mic.GetProperty("hand").GetString());
        Assert.Equal(json.GetProperty("figures")[0].GetProperty("frames").GetArrayLength(), mic.GetProperty("up").GetArrayLength());

        Assert.Null(Compile(Dance(mic: "")).Clip.Figures[0].Mic);
    }

    [Fact]
    public void A_mic_without_singing_stays_in_the_hand()
    {
        // No sings: held before the belly all the time, never up, and no word of that arm warns.
        var text = "role solo\ndancer alice humanoid mic left on solo\nsection A { solo { leftArm { side:4 down:4 } } }\nform { A }\nscore { dancer alice }\n";
        var c = ShosaCompilation.Compile(text);
        Assert.Empty(c.Diagnostics);
        Assert.All(c.Clip.Figures[0].Mic!.Up, up => Assert.Equal(0, up));
    }

    [Theory]
    [InlineData(" mic")]
    [InlineData(" mic up")]
    [InlineData(" mic right left")]
    public void A_mic_takes_one_hand(string mic) =>
        Assert.Equal("SH1006", Assert.Single(Compile(Dance(mic)).Diagnostics).Code);
}
