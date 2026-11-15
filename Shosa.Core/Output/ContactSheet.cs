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
    private const uint Ink = 0x30343A, LeftColor = 0x2F6FDF, RightColor = 0xD9482B, Grid = 0xD8DCE2, Faint = 0x9AA1AB, HipColor = 0x6B7280,MarkColor = 0x1E8E5A, SectionColor = 0x7A4FBF;
    private const int CellWidth = 190, CellHeight = 264, Columns = 8, Margin = 14;
    // The text: two pixels a dot, so a character is 12 wide and a line 16 high. Under the floor
    // line of a cell there is each figure from above, and room for two lines of names.
    private const int TextScale = 2, Advance = Canvas.GlyphAdvance * TextScale, LineHeight = Canvas.GlyphHeight * TextScale, Above = 60, UnderFloor = 40 + Above;

    /// <summary>The default distance between poses, in beats, for a stretch of this length.</summary>
    public static double DefaultStep(double beats) => beats <= 8 ? 0.5 : beats <= 32 ? 1 : 2;

    /// <param name="step">Beats between poses; null picks <see cref="DefaultStep"/>.</param>
    /// <param name="from">First beat shown.</param>
    /// <param name="to">Last beat shown; null is the end.</param>
    /// <param name="viewDegrees">How far round toward stage left the camera stands: 90 sees a front-facing figure in profile.</param>
    public static Canvas Render(MotionClip clip, double? step = null, double from = 0, double? to = null, double viewDegrees = FrontView)
    {
        double yaw = viewDegrees * Math.PI / 180;
        from = Math.Clamp(from, 0, clip.TotalBeats);
        double end = Math.Clamp(to ?? clip.TotalBeats, from, clip.TotalBeats);
        var beats = Beats(clip, step, from, end);
        from = beats[0];
        int count = beats.Count;
        int columns = Math.Min(Columns, count);
        int rows = (count + columns - 1) / columns;

        const int floorHeight = 230;
        var canvas = new Canvas(Margin * 2 + columns * CellWidth, Margin * 2 + rows * CellHeight + floorHeight);

        // One scale for every cell: the widest the group of figures ever gets must fit.
        double halfWidth = 1.0;
        for (int i = 0; i < count; i++)
        {
            double frame = clip.FrameAt(beats[i]);
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

        string tempoShown = "";
        for (int i = 0; i < count; i++)
        {
            double beat = beats[i];
            double frame = clip.FrameAt(beat);
            int cellX = Margin + (i % columns) * CellWidth, cellY = Margin + (i / columns) * CellHeight;
            double originX = cellX + CellWidth / 2.0, floorY = cellY + CellHeight - UnderFloor;

            // Not a multiple of the bar's length when the song begins with an upbeat.
            bool barStart = Semantics.Bars.At(clip.Sections, clip.Meters, beat).Beat < 1e-9;
            canvas.Line(cellX + 6, floorY, cellX + CellWidth - 6, floorY, 1.2, Grid);
            // What is written at the top of the cell is drawn after the figures, and out of their way
            // (a head or a raised hand is up there too): see Write below.
            var written = new List<(int Line, int Indent, string Text, uint Color)>
            {
                (0, 0, FormatBeat(beat), barStart ? Ink : Faint),
            };

            double after = i == 0 ? from - 1e-6 : beats[i - 1] + 1e-6;   // the pose before has what came up to itself
            double upTo = (i == count - 1 ? Math.Max(beat, end) : beat) + 1e-6;

            // A clip of several sections says where each begins: its name beside the beat of the
            // first pose in it (and of the first pose shown, whichever section that is in).
            Semantics.SectionIr? entered = null;
            foreach (var section in clip.Sections)
            {
                if (section.T0 <= upTo && (section.T0 >= after || i == 0))
                    entered = section;
            }
            if (entered is not null && clip.Sections.Count > 1)
                written.Add((0, (FormatBeat(beat).Length + 1) * Advance, entered.Name, SectionColor));

            // The tempo, under the beat of the first pose and of the first pose of a section that
            // is played at another: two scores that differ only in speed are not the same picture.
            int line = 0;
            if (i == 0 || entered is not null)
            {
                string tempo = TempoLabel(clip, i == 0 ? beat : entered!.T0 + entered.Pickup);
                if (tempo != tempoShown)
                    written.Add((++line, 0, tempoShown = tempo, SectionColor));
            }

            // A mark is written on the first pose at or after its moment (what has arrived is
            // still there), with its beat when the pose is not exactly on it.
            foreach (var mark in clip.Marks)
            {
                if (mark.Beat < after || mark.Beat > upTo)
                    continue;
                string text = "!" + mark.Name + (Math.Abs(mark.Beat - beat) < 1e-6 ? "" : " " + FormatBeat(mark.Beat));
                written.Add((++line, 0, text, MarkColor));
            }

            var centre = Centre(clip, frame);
            DrawAbove(canvas, clip, frame, centre, cellX, originX, floorY, scale, yaw);
            if (clip.Figures.Count > 1)
                DrawNames(canvas, clip, frame, centre, cellX, originX, floorY, scale, yaw);
            foreach (var figure in clip.Figures)
                DrawFigure(canvas, clip, figure, frame, centre, originX, floorY, scale, yaw);

            // A line under the first (the tempo, a mark) goes to the right edge of the cell when a
            // figure stands in it at the left and not at the right. Where a figure is under it all
            // the same, the letters are whole: each has a white edge, and the figure gives way.
            foreach (var (row, indent, text, color) in written)
            {
                int width = Canvas.TextWidth(text, TextScale), y = cellY + 4 + row * (LineHeight + 2);
                int x = cellX + 6 + indent, right = cellX + CellWidth - 6 - width;
                bool Covered(int at) => clip.Figures.Any(f => Covers(clip, f, frame, centre, originX, floorY, scale, yaw, at - 3, y - 3, width + 6, LineHeight + 4));
                if (row > 0 && right > x && Covered(x) && !Covered(right))
                    x = right;
                foreach (var (dx, dy) in new[] { (-2, 0), (2, 0), (0, -2), (0, 2), (-2, -2), (2, -2), (-2, 2), (2, 2), (-1, 0), (1, 0), (0, -1), (0, 1) })
                    canvas.Text(x + dx, y + dy, text, TextScale, 0xFFFFFF);
                canvas.Text(x, y, text, TextScale, color);
            }
        }

        DrawFloor(canvas, clip, Margin, Margin + rows * CellHeight + 8, canvas.Width - Margin * 2, floorHeight - 16);
        return canvas;
    }

    /// <summary>
    /// The beats of the poses. With a step asked for, or a first beat, they are that far apart
    /// from there. Otherwise each section has its own row of them, from its first bar line —
    /// and where they would be two beats apart in a time of three, six, nine or twelve beats to
    /// the bar, they are three apart: the first beat of every bar is among them.
    /// </summary>
    private static List<double> Beats(MotionClip clip, double? step, double from, double end)
    {
        const int most = 400;
        var beats = new List<double>();
        double usual = step is > 0 ? step.Value : DefaultStep(end - from);
        if (step is > 0 || from > 0 || clip.Sections.Count == 0)
        {
            for (int i = 0; i < most && from + i * usual <= end + 1e-9; i++)
                beats.Add(from + i * usual);
            return beats;
        }
        for (int s = 0; s < clip.Sections.Count; s++)
        {
            var section = clip.Sections[s];
            double first = section.T0 + section.Pickup;
            double apart = usual == 2 && Semantics.Bars.MeterAt(clip.Meters, first).BeatsPerBar % 3 == 0 ? 3 : usual;
            bool last = s == clip.Sections.Count - 1;
            // Back from the first bar line to the section's start (a song's upbeat), then on to its end.
            for (double beat = first - Math.Floor((first - section.T0) / apart + 1e-9) * apart; beats.Count < most; beat += apart)
            {
                if (beat > Math.Min(end, section.T1) + 1e-9 || !last && beat > section.T1 - 1e-9)
                    break;
                beats.Add(beat);
            }
        }
        if (beats.Count == 0)
            beats.Add(from);
        return beats;
    }

    /// <summary>The tempo at a beat as the language writes it, by the note that is felt, and the time: `4=120 4/4`, in 6/8 `4.=96 6/8`.</summary>
    private static string TempoLabel(MotionClip clip, double beat)
    {
        var meter = Semantics.Bars.MeterAt(clip.Meters, beat);
        double bpm = Semantics.TempoMap.BpmAt(Semantics.TempoMap.Of(clip.Tempos, clip.Tempo), beat);
        string tempo = meter is { BeatUnit: 8, BeatsPerBar: 6 or 9 or 12 }
            ? "4.=" + FormatBeat(bpm / 3)
            : FormattableString.Invariant($"{meter.BeatUnit}={FormatBeat(bpm)}");
        return FormattableString.Invariant($"{tempo} {meter.BeatsPerBar}/{meter.BeatUnit}");
    }

    private static string FormatBeat(double beat) =>
        Math.Round(beat, 3).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

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
    public const double FrontView = 40;
    private static readonly double CamPitch = 12 * Math.PI / 180;

    private static (double X, double Y, double Depth) Project(Vec3 p, double yaw)
    {
        double right = p.X * Math.Cos(yaw) - p.Z * Math.Sin(yaw);
        double depth = -p.X * Math.Sin(yaw) - p.Z * Math.Cos(yaw);
        return (right, p.Y * Math.Cos(CamPitch) + depth * Math.Sin(CamPitch), depth);
    }

    private static string Label(ClipFigure figure) => figure.Name.Length > 0 ? figure.Name : figure.Id;

    // Who is who: each dancer's name under the floor line, below where it stands. Two that
    // stand too close for both names take a line each. The figures are drawn over the names.
    private static void DrawNames(Canvas canvas, MotionClip clip, double frame, Vec3 centre,
        int cellX, double originX, double floorY, double scale, double yaw)
    {
        var rowEnds = new[] { double.MinValue, double.MinValue };
        var placed = clip.Figures
            .Select(f => (Name: Label(f), X: originX + Project(f.Joint(frame, Rig.Pelvis) - centre, yaw).X * scale))
            .OrderBy(f => f.X);
        foreach (var (name, x) in placed)
        {
            int width = Canvas.TextWidth(name, TextScale);
            double left = Math.Clamp(x - width / 2.0, cellX + 4, Math.Max(cellX + 4, cellX + CellWidth - 4 - width));
            int row = left > rowEnds[0] + 8 ? 0 : left > rowEnds[1] + 8 ? 1 : rowEnds[0] <= rowEnds[1] ? 0 : 1;
            rowEnds[row] = left + width;
            canvas.Text((int)Math.Round(left), (int)floorY + 5 + Above + row * (LineHeight + 1), name, TextScale, Faint);
        }
    }

    // Each figure from above, under where it stands, with the audience at the bottom as on the
    // floor plan: the line of the hips (grey), the line of the shoulders over it (left half blue,
    // right half red), the head and the nose. What the pose beside it cannot say is read here:
    // how far the chest is turned on the hips, which way the figure faces, where the face looks.
    // Every figure has a place of its own in the row; where they stand is on the floor plan.
    private static void DrawAbove(Canvas canvas, MotionClip clip, double frame, Vec3 centre,
        int cellX, double originX, double floorY, double scale, double yaw)
    {
        int count = clip.Figures.Count;
        double slot = Math.Min(Above, (CellWidth - 8.0) / count), size = 120 * slot / Above;
        var placed = clip.Figures
            .Select(f => (Figure: f, X: originX + Project(f.Joint(frame, Rig.Pelvis) - centre, yaw).X * scale))
            .OrderBy(f => f.X).ToList();
        var xs = new double[count];
        for (int i = 0; i < count; i++)
            xs[i] = Math.Max(placed[i].X, i == 0 ? cellX + 4 + slot / 2 : xs[i - 1] + slot);
        for (int i = count - 1; i >= 0; i--)
            xs[i] = Math.Min(xs[i], i == count - 1 ? cellX + CellWidth - 4 - slot / 2 : xs[i + 1] - slot);

        double middleY = floorY + 3 + Above / 2.0;
        for (int i = 0; i < count; i++)
        {
            var figure = placed[i].Figure;
            // Centred between the hips and the chest, so that a lean stays in its row.
            var origin = (figure.Joint(frame, Rig.Pelvis) + figure.Joint(frame, Rig.Chest)) * 0.5;
            (double X, double Y) At(Vec3 p) => (xs[i] + (p.X - origin.X) * size, middleY + (p.Z - origin.Z) * size);
            (double X, double Y) Joint(int joint) => At(figure.Joint(frame, joint));

            var (leftHip, rightHip) = (Joint(Rig.LHip), Joint(Rig.RHip));
            canvas.Line(leftHip.X, leftHip.Y, rightHip.X, rightHip.Y, 3, Faint);
            var middle = At((figure.Joint(frame, Rig.LShoulder) + figure.Joint(frame, Rig.RShoulder)) * 0.5);
            var (leftShoulder, rightShoulder) = (Joint(Rig.LShoulder), Joint(Rig.RShoulder));
            canvas.Line(middle.X, middle.Y, leftShoulder.X, leftShoulder.Y, 3, LeftColor);
            canvas.Line(middle.X, middle.Y, rightShoulder.X, rightShoulder.Y, 3, RightColor);
            // The hips are narrower than the shoulders and half under the head: a dot at each end, over the
            // shoulder line, says which way they are turned when the shoulders are square over them.
            canvas.Disc(leftHip.X, leftHip.Y, 2.6, HipColor);
            canvas.Disc(rightHip.X, rightHip.Y, 2.6, HipColor);

            var head = Joint(Rig.Head);
            var nose = Joint(Rig.Nose);
            double radius = size * 0.06;
            canvas.Disc(head.X, head.Y, radius, 0xFFFFFF);
            canvas.Ring(head.X, head.Y, radius, 1.6, Ink);
            double length = Math.Sqrt((nose.X - head.X) * (nose.X - head.X) + (nose.Y - head.Y) * (nose.Y - head.Y));
            if (length > 1)
                canvas.Line(head.X + (nose.X - head.X) / length * radius, head.Y + (nose.Y - head.Y) / length * radius,
                    head.X + (nose.X - head.X) / length * radius * 2.4, head.Y + (nose.Y - head.Y) / length * radius * 2.4, 2, Ink);
        }
    }

    private static void DrawFigure(Canvas canvas, MotionClip clip, ClipFigure figure, double frame, Vec3 centre,
        double originX, double floorY, double scale, double yaw)
    {
        (double X, double Y, double Depth) At(int joint)
        {
            var (x, y, depth) = Project(figure.Joint(frame, joint) - centre, yaw);
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
        // Drawn longer than it is: a nod or a glance has to be readable at this size.
        double tipX = head.X + (nose.X - head.X) * 1.7, tipY = head.Y + (nose.Y - head.Y) * 1.7;
        canvas.Line(nose.X, nose.Y, tipX, tipY, 2.2, Ink);
        canvas.Disc(tipX, tipY, 2.2, Ink);
    }

    /// <summary>Whether a figure as <see cref="DrawFigure"/> draws it (its bones, its head) comes into a rectangle of the picture.</summary>
    private static bool Covers(MotionClip clip, ClipFigure figure, double frame, Vec3 centre,
        double originX, double floorY, double scale, double yaw, int x, int y, int width, int height)
    {
        (double X, double Y) At(int joint)
        {
            var (px, py, _) = Project(figure.Joint(frame, joint) - centre, yaw);
            return (originX + px * scale, floorY - py * scale);
        }
        bool Inside(double px, double py, double reach) =>
            px > x - reach && px < x + width + reach && py > y - reach && py < y + height + reach;

        var head = At(Rig.Head);
        if (Inside(head.X, head.Y, scale * 0.095 + 1))
            return true;
        foreach (var (a, b, _) in Rig.Bones)
        {
            var (p, q) = (At(a), At(b));
            int steps = (int)Math.Ceiling(Math.Max(Math.Abs(q.X - p.X), Math.Abs(q.Y - p.Y)) / 3) + 1;
            for (int i = 0; i <= steps; i++)
            {
                if (Inside(p.X + (q.X - p.X) * i / steps, p.Y + (q.Y - p.Y) * i / steps, 2))
                    return true;
            }
        }
        return false;
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
            if (clip.Figures.Count > 1)
                canvas.Text((int)Sx(start.X) - Canvas.TextWidth(Label(figure), TextScale) / 2, Math.Max(y + 4, (int)Sy(start.Z) - 12 - LineHeight), Label(figure), TextScale, Ink);
        }
    }
}
