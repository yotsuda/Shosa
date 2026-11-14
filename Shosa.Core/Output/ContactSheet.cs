using Shosa.Core.Motion;

namespace Shosa.Core.Output;

/// <summary>
/// The whole movement on one still image: a pose per beat, seen from the front and a little
/// to one side, and under them the floor from above with the path and the footprints.
/// Left limbs are blue and right limbs red, so a mirrored mistake shows at a glance.
/// This is to movement what the engraved page is to a score: what one looks at to check it.
/// </summary>
public static class ContactSheet
{
    private const uint Ink = 0x30343A, LeftColor = 0x2F6FDF, RightColor = 0xD9482B, Grid = 0xD8DCE2, Faint = 0x9AA1AB;
    private const int CellWidth = 190, CellHeight = 250, Columns = 8, Margin = 14;

    /// <summary>The default distance between poses, in beats, for a stretch of this length.</summary>
    public static double DefaultStep(double beats) => beats <= 8 ? 0.5 : beats <= 32 ? 1 : 2;

    /// <param name="step">Beats between poses; null picks <see cref="DefaultStep"/>.</param>
    /// <param name="from">First beat shown.</param>
    /// <param name="to">Last beat shown; null is the end.</param>
    public static Canvas Render(MotionClip clip, double? step = null, double from = 0, double? to = null)
    {
        from = Math.Clamp(from, 0, clip.TotalBeats);
        double end = Math.Clamp(to ?? clip.TotalBeats, from, clip.TotalBeats);
        double stepBeats = step is > 0 ? step.Value : DefaultStep(end - from);
        int count = Math.Min(400, (int)Math.Floor((end - from) / stepBeats + 1e-9) + 1);
        int columns = Math.Min(Columns, count);
        int rows = (count + columns - 1) / columns;

        const int floorHeight = 230;
        var canvas = new Canvas(Margin * 2 + columns * CellWidth, Margin * 2 + rows * CellHeight + floorHeight);

        // One scale for every cell: the widest the group of figures ever gets must fit.
        double halfWidth = 1.0;
        for (int i = 0; i < count; i++)
        {
            double frame = clip.FrameAt(from + i * stepBeats);
            var centre = Centre(clip, frame);
            foreach (var figure in clip.Figures)
            {
                for (int j = 0; j < clip.Joints.Length; j++)
                {
                    var p = figure.Joint(frame, j) - centre;
                    halfWidth = Math.Max(halfWidth, Math.Sqrt(p.X * p.X + p.Z * p.Z) + 0.15);
                }
            }
        }
        double scale = Math.Min(105, (CellWidth / 2.0 - 6) / halfWidth);

        for (int i = 0; i < count; i++)
        {
            double beat = from + i * stepBeats;
            double frame = clip.FrameAt(beat);
            int cellX = Margin + (i % columns) * CellWidth, cellY = Margin + (i / columns) * CellHeight;
            double originX = cellX + CellWidth / 2.0, floorY = cellY + CellHeight - 26;

            bool barStart = Math.Abs(beat / clip.BeatsPerBar - Math.Round(beat / clip.BeatsPerBar)) < 1e-9;
            canvas.Line(cellX + 6, floorY, cellX + CellWidth - 6, floorY, 1.2, Grid);
            canvas.Text(cellX + 6, cellY + 4, FormatBeat(beat), 2, barStart ? Ink : Faint);

            var centre = Centre(clip, frame);
            foreach (var figure in clip.Figures)
                DrawFigure(canvas, clip, figure, frame, centre, originX, floorY, scale);
        }

        DrawFloor(canvas, clip, Margin, Margin + rows * CellHeight + 8, canvas.Width - Margin * 2, floorHeight - 16);
        return canvas;
    }

    private static string FormatBeat(double beat) =>
        beat == Math.Floor(beat) ? ((int)beat).ToString() : beat.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

    private static Vec3 Centre(MotionClip clip, double frame)
    {
        var sum = new Vec3(0, 0, 0);
        foreach (var figure in clip.Figures)
            sum += figure.Joint(frame, Rig.Pelvis);
        var mean = sum * (1.0 / Math.Max(1, clip.Figures.Count));
        return new Vec3(mean.X, 0, mean.Z);
    }

    // The camera stands in front of the stage, 40 degrees round toward stage left and
    // 12 degrees above: far enough round that a forward reach or kick is not foreshortened
    // away. Facing the audience, a figure's left is on the picture's right.
    private static readonly double CamYaw = 40 * Math.PI / 180, CamPitch = 12 * Math.PI / 180;

    private static (double X, double Y, double Depth) Project(Vec3 p)
    {
        double right = p.X * Math.Cos(CamYaw) - p.Z * Math.Sin(CamYaw);
        double depth = -p.X * Math.Sin(CamYaw) - p.Z * Math.Cos(CamYaw);
        return (right, p.Y * Math.Cos(CamPitch) + depth * Math.Sin(CamPitch), depth);
    }

    private static void DrawFigure(Canvas canvas, MotionClip clip, ClipFigure figure, double frame, Vec3 centre,
        double originX, double floorY, double scale)
    {
        (double X, double Y, double Depth) At(int joint)
        {
            var (x, y, depth) = Project(figure.Joint(frame, joint) - centre);
            return (originX + x * scale, floorY - y * scale, depth);
        }

        // Far bones first, so that the near side is drawn over the far side.
        var bones = Rig.Bones.Select(b => (Bone: b, A: At(b.A), B: At(b.B)))
            .OrderByDescending(b => b.A.Depth + b.B.Depth);
        foreach (var (bone, a, b) in bones)
        {
            uint color = bone.Side == 'l' ? LeftColor : bone.Side == 'r' ? RightColor : Ink;
            canvas.Line(a.X, a.Y, b.X, b.Y, Math.Max(2.2, scale * 0.035), color);
        }

        var head = At(Rig.Head);
        canvas.Disc(head.X, head.Y, scale * 0.095, 0xFFFFFF);
        canvas.Ring(head.X, head.Y, scale * 0.095, 2, Ink);
        var nose = At(Rig.Nose);
        canvas.Disc(nose.X, nose.Y, 2.2, Ink);
    }

    private static void DrawFloor(Canvas canvas, MotionClip clip, int x, int y, int width, int height)
    {
        canvas.Rect(x, y, width, 1, Grid);

        double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
        foreach (var figure in clip.Figures)
        {
            for (int f = 0; f < figure.Frames.Count; f++)
            {
                foreach (int joint in new[] { Rig.LAnkle, Rig.RAnkle, Rig.Pelvis })
                {
                    var p = figure.Joint(f, joint);
                    minX = Math.Min(minX, p.X);
                    maxX = Math.Max(maxX, p.X);
                    minZ = Math.Min(minZ, p.Z);
                    maxZ = Math.Max(maxZ, p.Z);
                }
            }
        }
        if (minX > maxX)
            return;

        // Seen from above with the audience at the bottom: stage left (x) is to the right.
        double scale = Math.Min(140, Math.Min((width - 40) / Math.Max(0.5, maxX - minX), (height - 40) / Math.Max(0.5, maxZ - minZ)));
        double centreX = (minX + maxX) / 2, centreZ = (minZ + maxZ) / 2;
        double Sx(double worldX) => x + width / 2.0 + (worldX - centreX) * scale;
        double Sy(double worldZ) => y + height / 2.0 + (worldZ - centreZ) * scale;

        // A one-metre grid.
        for (double gx = Math.Floor(minX - 1); gx <= maxX + 1; gx++)
        {
            if (Sx(gx) > x && Sx(gx) < x + width)
                canvas.Line(Sx(gx), y + 4, Sx(gx), y + height, 1, 0xEEF0F3);
        }
        for (double gz = Math.Floor(minZ - 1); gz <= maxZ + 1; gz++)
        {
            if (Sy(gz) > y + 4 && Sy(gz) < y + height)
                canvas.Line(x, Sy(gz), x + width, Sy(gz), 1, 0xEEF0F3);
        }

        foreach (var figure in clip.Figures)
        {
            for (int f = 1; f < figure.Frames.Count; f++)
            {
                var a = figure.Joint(f - 1, Rig.Pelvis);
                var b = figure.Joint(f, Rig.Pelvis);
                canvas.Line(Sx(a.X), Sy(a.Z), Sx(b.X), Sy(b.Z), 1.6, Faint);
            }
            for (int f = 0; f < figure.Frames.Count; f++)
            {
                foreach (var (ankle, toe, color) in new[] { (Rig.LAnkle, Rig.LToe, LeftColor), (Rig.RAnkle, Rig.RToe, RightColor) })
                {
                    // On the floor means the toe is: on a rise the heel leaves it and the foot still stands.
                    var p = figure.Joint(f, ankle);
                    var q = figure.Joint(f, toe);
                    if (q.Y > 0.021)
                        continue;   // in the air: not a footprint
                    canvas.Line(Sx(p.X), Sy(p.Z), Sx(q.X), Sy(q.Z), 4, color);
                }
            }
            var start = figure.Joint(0, Rig.Pelvis);
            canvas.Ring(Sx(start.X), Sy(start.Z), 7, 2, Ink);
        }
    }
}
