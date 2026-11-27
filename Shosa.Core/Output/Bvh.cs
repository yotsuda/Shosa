using System.Globalization;
using System.Text;
using Shosa.Core.Motion;

namespace Shosa.Core.Output;

/// <summary>
/// One figure as a BVH file: the skeleton of <see cref="Skeleton"/>, then the hips' position
/// and every bone's rotation at every frame. BVH holds one skeleton, so each figure gets its own file.
/// A figure whose hands are written (a `hands` row) has the bones of its fingers too (DESIGN.md 126).
///
/// Lengths are metres unless scaled (much BVH in the wild is in centimetres: scale 100).
/// The figure faces +z at facing 0, which is what BVH readers take as forward.
/// </summary>
public static class Bvh
{
    public static string Render(MotionClip clip, ClipFigure figure, double scale = 1)
    {
        var bones = Skeleton.BonesWith(figure.Hands is not null);
        var text = new StringBuilder();
        text.Append("HIERARCHY\n");
        // The bones in the order they are written, which is the order of a frame's numbers.
        var order = new List<int>();
        WriteBone(text, bones, 0, 0, scale, order);

        text.Append("MOTION\n");
        text.Append(CultureInfo.InvariantCulture, $"Frames: {figure.Rotations.Count}\n");
        text.Append(CultureInfo.InvariantCulture, $"Frame Time: {1.0 / clip.Fps:0.#######}\n");

        // Angles are kept near the frame before, so a reader that interpolates the three
        // numbers does not swing through 360 degrees where an angle wraps.
        var previous = new double[bones.Length * 3];
        for (int f = 0; f < figure.Rotations.Count; f++)
        {
            var hips = figure.Joint(f, Rig.Pelvis) * scale;
            text.Append(CultureInfo.InvariantCulture, $"{hips.X:0.####} {hips.Y:0.####} {hips.Z:0.####}");
            foreach (int b in order)
            {
                var (z, x, y) = figure.Rotation(f, b).ToEulerZxy();
                z = Near(z, previous[b * 3]);
                x = Near(x, previous[b * 3 + 1]);
                y = Near(y, previous[b * 3 + 2]);
                (previous[b * 3], previous[b * 3 + 1], previous[b * 3 + 2]) = (z, x, y);
                text.Append(CultureInfo.InvariantCulture, $" {z:0.####} {x:0.####} {y:0.####}");
            }
            text.Append('\n');
        }
        return text.ToString();
    }

    private static double Near(double angle, double previous) => angle + 360 * Math.Round((previous - angle) / 360);

    /// <summary>Bones are written depth first (the order of <see cref="Skeleton.Bones"/>; the fingers, which come after them all, inside their hands).</summary>
    private static void WriteBone(StringBuilder text, SkeletonBone[] bones, int index, int depth, double scale, List<int> order)
    {
        var bone = bones[index];
        order.Add(index);
        string indent = new(' ', depth * 2);
        // The root's offset is left at zero: readers disagree on whether to add it to the position channels.
        var offset = (bone.Parent < 0 ? default : bone.Offset) * scale;

        text.Append(indent).Append(bone.Parent < 0 ? "ROOT " : "JOINT ").Append(bone.Name).Append('\n');
        text.Append(indent).Append("{\n");
        text.Append(CultureInfo.InvariantCulture, $"{indent}  OFFSET {offset.X:0.####} {offset.Y:0.####} {offset.Z:0.####}\n");
        text.Append(indent).Append(bone.Parent < 0
            ? "  CHANNELS 6 Xposition Yposition Zposition Zrotation Xrotation Yrotation\n"
            : "  CHANNELS 3 Zrotation Xrotation Yrotation\n");

        bool leaf = true;
        for (int child = index + 1; child < bones.Length; child++)
        {
            if (bones[child].Parent == index)
            {
                leaf = false;
                WriteBone(text, bones, child, depth + 1, scale, order);
            }
        }

        // (A hand with fingers ends in them, not in its own end.)
        if (leaf && bone.End is { } end)
        {
            end *= scale;
            text.Append(indent).Append("  End Site\n");
            text.Append(indent).Append("  {\n");
            text.Append(CultureInfo.InvariantCulture, $"{indent}    OFFSET {end.X:0.####} {end.Y:0.####} {end.Z:0.####}\n");
            text.Append(indent).Append("  }\n");
        }
        text.Append(indent).Append("}\n");
    }
}
