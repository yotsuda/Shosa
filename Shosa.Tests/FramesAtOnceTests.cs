using System.Text.Json;
using Shosa.Core.Motion;

namespace Shosa.Tests;

/// <summary>
/// A long clip's frames are found on several processors at once, each stretch of time by solvers of its own
/// (DESIGN.md 120.10). What a figure is at a moment does not depend on which moments were asked before: however
/// the frames are cut, down to one frame a solver, every number and every diagnostic is the same.
/// </summary>
public class FramesAtOnceTests
{
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_sample_is_the_same_however_its_frames_are_cut(string name)
    {
        // Set for the process while this runs: the tests beside it are then cut too, and are the same for it.
        var before = Solver.FramesAtOnce;
        try
        {
            Solver.FramesAtOnce = int.MaxValue;   // all on one
            var one = Samples.Compile(name);
            foreach (int frames in new[] { 1, 7, 64 })
            {
                Solver.FramesAtOnce = frames;
                var cut = Samples.Compile(name);
                Assert.Equal(one.Diagnostics, cut.Diagnostics);
                Assert.Equal(JsonSerializer.Serialize(one.Clips), JsonSerializer.Serialize(cut.Clips));
                foreach (var (x, y) in one.Clips.SelectMany(c => c.Figures).Zip(cut.Clips.SelectMany(c => c.Figures)))
                {
                    Assert.Equal(x.Yaws, y.Yaws);
                    Assert.True(x.Rotations.Zip(y.Rotations).All(r => r.First.AsSpan().SequenceEqual(r.Second)));
                }
            }
        }
        finally
        {
            Solver.FramesAtOnce = before;
        }
    }
}
