using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Output;
using Shosa.Core.Semantics;

namespace Shosa.Tests;

/// <summary>The hands: `hands { fist }`, `rightHand { open.fingers(up).palm(fwd) }` (DESIGN.md 126).</summary>
public class HandTests
{
    private static ShosaCompilation Compile(string tracks, string head = "tempo 60\ntime 4/4\n") => ShosaCompilation.Compile(Samples.Solo(tracks, head));

    private static float[] Hands(ShosaCompilation compilation, double beat) =>
        compilation.Clip.Figures[0].Hands![compilation.Clip.FrameOfBeat(beat)];

    private static void Same(float[] expected, float[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], actual[i], 3);
    }

    /// <summary>Where a hand's fingers point and its palm faces, in the room.</summary>
    private static (Vec3 Fingers, Vec3 Palm) Turned(ShosaCompilation compilation, double beat, int side)
    {
        var figure = compilation.Clip.Figures[0];
        int frame = compilation.Clip.FrameOfBeat(beat);
        var world = new Quat[Skeleton.Bones.Length];
        for (int b = 0; b < world.Length; b++)
            world[b] = Skeleton.Bones[b].Parent < 0 ? figure.Rotation(frame, b) : world[Skeleton.Bones[b].Parent] * figure.Rotation(frame, b);
        var hand = world[side == 1 ? Skeleton.LHand : Skeleton.RHand];
        return (hand.Rotate(new Vec3(side, 0, 0)), hand.Rotate(-Vec3.Up));
    }

    [Fact]
    public void A_shape_closes_the_fingers_it_names_and_a_finger_of_its_own_changes_one()
    {
        var compilation = Compile("    steps { stand:8 }\n    hands { fist:2 point:2 fist.index(0).little(45):2 count(3):2 }");
        Assert.Empty(compilation.Diagnostics);
        Same([75 / 90f, 1, 1, 1, 1, 0], Hands(compilation, 0)[..6]);
        Assert.Equal(Hands(compilation, 0)[..6], Hands(compilation, 0)[6..]);            // both hands alike
        Same([75 / 90f, 0, 1, 1, 1, 0], Hands(compilation, 2)[..6]);
        Same([75 / 90f, 0, 1, 1, 0.5f, 0], Hands(compilation, 4)[..6]);
        Same([85 / 90f, 0, 0, 0, 1, 12], Hands(compilation, 6)[..6]);
        // It goes as a gesture does, through all of the word before: half-way between the fist and the point at beat 1.
        Assert.InRange(Hands(compilation, 1)[1], 0.4, 0.6);
    }

    [Fact]
    public void A_hand_no_row_writes_is_relaxed_and_a_clip_with_no_hand_row_has_no_fingers()
    {
        Assert.Null(Compile("    steps { stand:4 }\n    arms { side:4 }").Clip.Figures[0].Hands);
        // (A `then` of the whole body holds nothing of a hand.)
        Assert.Null(ShosaCompilation.Compile(Samples.Read("fighter")).Clip.Figures[0].Hands);
        var oneHand = Compile("    steps { stand:4 }\n    rightHand { fist:4 }");
        Same(Skeleton.RelaxedHands[..6], Hands(oneHand, 2)[..6]);
        Assert.Equal(1, Hands(oneHand, 2)[7]);
    }

    [Fact]
    public void The_fingers_point_and_the_palm_faces_where_the_row_says()
    {
        var compilation = Compile("    steps { stand:4 }\n    arms { fwd:4 }\n    hands { open.fingers(up).palm(fwd):4 }");
        Assert.Empty(compilation.Diagnostics);
        foreach (int side in new[] { 1, -1 })
        {
            var (fingers, palm) = Turned(compilation, 2, side);
            // The wrist bends no further than a wrist goes: 80 degrees from the forearm, which points forward.
            Assert.InRange(Math.Acos(fingers.Dot(Vec3.Up)) * 180 / Math.PI, 5, 15);
            Assert.True(palm.Z > 0.98, $"palm {palm}");
        }
        // The palm alone: the fingers stay on along the forearm, the hand turns about them.
        var up = Compile("    steps { stand:4 }\n    arms { side:4 }\n    hands { open.palm(up):4 }");
        var (along, facing) = Turned(up, 2, 1);
        Assert.True(along.X > 0.99 && facing.Y > 0.99, $"fingers {along}, palm {facing}");
        // `side` is each hand's own side, so the two are mirrored.
        var across = Compile("    steps { stand:4 }\n    arms { fwd:4 }\n    hands { open.palm(across):4 }");
        Assert.True(Turned(across, 2, 1).Palm.X < -0.99 && Turned(across, 2, -1).Palm.X > 0.99);
    }

    [Fact]
    public void A_pose_holds_its_hands_and_a_hands_word_is_not_reserved()
    {
        // `point` is a move here and a hand's shape in the hand's row.
        string source = """
            tempo 60
            role solo
            dancer solo humanoid on solo
            pose guard { arms { fwd.up(25).bent }  hands { fist } }
            move point { rightArm { fwd:2 }  rightHand { point:2 } }
            section Main { solo { body { guard:2 point:2 } } }
            form { Main }
            score { dancer solo }
            """;
        var compilation = ShosaCompilation.Compile(source);
        Assert.Empty(compilation.Diagnostics);
        Assert.Equal(1, Hands(compilation, 0)[1]);        // the guard's fists
        Assert.Equal(0, Hands(compilation, 3.9)[7], 1);   // the right first finger out
        Assert.Equal(1, Hands(compilation, 3.9)[1]);      // the left hand still a fist
    }

    [Theory]
    [InlineData("hands { fwd:4 }", "SH1012")]
    [InlineData("hands { fist.open:4 }", "SH1012")]
    [InlineData("hands { fist.index:4 }", "SH1013")]
    [InlineData("hands { fist.index(120):4 }", "SH1013")]
    [InlineData("hands { open.palm(up).fingers(fwd):4 }", "SH1012")]
    [InlineData("hands { open.palm:4 }", "SH1013")]
    [InlineData("hands { count:4 }", "SH1013")]
    [InlineData("hands { count(7):4 }", "SH1013")]
    [InlineData("hands { fist(3):4 }", "SH1012")]
    [InlineData("hands { open.fingers(up).palm(up):4 }", "SH2011")]
    public void What_is_not_a_hands_word_is_said(string row, string code)
    {
        var compilation = Compile("    steps { stand:4 }\n    " + row);
        Assert.Equal(code, Assert.Single(compilation.Diagnostics).Code);
    }

    [Fact]
    public void A_file_whose_hands_are_written_has_the_bones_of_the_fingers()
    {
        var compilation = Compile("    steps { stand:2 }\n    arms { fwd:2 }\n    hands { fist.fingers(up):2 }");
        var clip = compilation.Clip;
        static List<string> Bones(string bvh) => [.. bvh.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("JOINT ") || l.StartsWith("ROOT ")).Select(l => l.Split(' ')[1])];
        string text = Bvh.Render(clip, clip.Figures[0]);
        var bones = Bones(text);
        Assert.Equal(Skeleton.AllBones.Length, bones.Count);
        Assert.Contains("leftIndexIntermediate", bones);
        Assert.Contains("rightThumbMetacarpal", bones);
        // A frame has the hips' place and three numbers for every bone, in the order the bones are written.
        Assert.Equal(3 + 3 * bones.Count, text.TrimEnd().Split('\n')[^1].Split(' ').Length);
        Assert.True(bones.IndexOf("leftIndexProximal") < bones.IndexOf("rightShoulder"), "the fingers are inside their hand");
        // A closed first finger: its tip is nearer the wrist than its knuckle is long from it, and below the palm.
        Assert.Equal(Vocabulary.ShapeOfHand("fist")!.Value.Index / 90, Hands(compilation, 1)[1], 3);
        var q = clip.Figures[0].Rotation(0, Skeleton.Bones.Length + 3);   // leftIndexProximal
        Assert.InRange(2 * Math.Acos(Math.Abs(q.W)) * 180 / Math.PI, 79, 81);
        // And a file with no hand row is written as before.
        var plain = Compile("    steps { stand:2 }").Clip;
        Assert.Equal(Skeleton.Bones.Length, Bones(Bvh.Render(plain, plain.Figures[0])).Count);
    }
}
