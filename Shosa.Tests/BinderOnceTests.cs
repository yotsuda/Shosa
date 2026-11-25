using Shosa.Core;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

namespace Shosa.Tests;

/// <summary>
/// The binder finds what a word means once and gives a copy of it each time the word is danced, and walks a row only
/// for its length without making anything of its words (DESIGN.md 120.14). What comes out is what it was when every
/// word was bound from nothing every time: here, the things that would go wrong first if a copy were shared or a
/// length walked short.
/// </summary>
public class BinderOnceTests
{
    private const string Twice = """
        tempo 120
        time 4/4
        role solo
        dancer solo humanoid on solo
        move wave { rightArm { side.up(30):1 down:1 } }
        section A {
          solo {
            rightArm { { fwd.up(20):1 down:1 } q | wave:4 | wave q:2 | }
          }
        }
        form { A A }
        score { dancer solo }

        """;

    /// <summary>A section played twice: each play has segments of its own, alike but for when they are.</summary>
    [Fact]
    public void A_word_danced_twice_is_two_segments()
    {
        var compiled = ShosaCompilation.Compile(Twice);
        Assert.Empty(compiled.Diagnostics);
        var clip = compiled.Score.Clips[0];
        // { 1 1 } q | wave:4 | wave q:2 | is 2 + 2, then 4, then 2 + 2: twelve beats, played twice.
        Assert.Equal(24, clip.TotalBeats, 9);

        var arm = clip.Dancers[0].Tracks[Channels.RightArm];
        var first = arm.Where(s => s.At < 12).ToList();
        var second = arm.Where(s => s.At >= 12).ToList();
        Assert.Equal(first.Count, second.Count);
        Assert.True(first.Count >= 10);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.NotSame(first[i], second[i]);
            Assert.Equal(first[i].At + 12, second[i].At, 9);
            Assert.Equal(first[i].Until + 12, second[i].Until, 9);
            Assert.Equal(first[i].Kind, second[i].Kind);
            Assert.Equal(first[i].Dir, second[i].Dir);
            Assert.Equal(first[i].Src, second[i].Src);
        }
        // No two segments of the row are one object, whichever word and play they came from.
        Assert.Equal(arm.Count, arm.Distinct(ReferenceEqualityComparer.Instance).Count());
    }

    /// <summary>What is wrong with a word is said once, however often its row is walked: for its length, and for each play.</summary>
    [Fact]
    public void A_wrong_word_is_said_once()
    {
        var compiled = ShosaCompilation.Compile(Twice.Replace("fwd.up(20):1", "fwd.up(200):1"));
        var said = Assert.Single(compiled.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, said.Severity);
        // The section is as long as it was: the word that is wrong still takes its time.
        Assert.Equal(24, compiled.Score.Clips[0].TotalBeats, 9);
    }

    /// <summary>A dancer and its mirror image dance the same words: each has its own meaning of them.</summary>
    [Fact]
    public void A_word_means_its_own_for_each_dancer()
    {
        string two = Twice
            .Replace("dancer solo humanoid on solo", "dancer one humanoid on solo at -1 0\ndancer two humanoid on solo at 1 0 mirror")
            .Replace("score { dancer solo }", "score { dancer one  dancer two }");
        var compiled = ShosaCompilation.Compile(two);
        Assert.Empty(compiled.Diagnostics);
        var clip = compiled.Score.Clips[0];
        var one = clip.Dancers[0].Tracks[Channels.RightArm];
        var two2 = clip.Dancers[1].Tracks[Channels.LeftArm];
        Assert.Equal(one.Count, two2.Count);
        for (int i = 0; i < one.Count; i++)
        {
            Assert.NotSame(one[i], two2[i]);
            Assert.Equal(one[i].At, two2[i].At, 9);
            if (one[i].Dir is { } dir)
                Assert.Equal(new[] { -dir[0], dir[1], dir[2] }, two2[i].Dir);
        }
    }
}
