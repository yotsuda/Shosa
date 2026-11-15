namespace Shosa.Core.Motion;

/// <summary>
/// How a jump uses its time and its knees (DESIGN.md 84.2 and 84.5). The knees bend before it as deep as it
/// goes high, measured down from standing straight, and the bend takes time in proportion to how deep it is:
/// a little jump bends a little and is quick off the floor (a skip of a rope), a high one bends 12 cm and
/// takes 0.7 of its time in the air to do it.
/// </summary>
public static class JumpShape
{
    private const double G = 9.81;

    /// <summary>How far below standing straight the hips go before a jump this high (metres).</summary>
    public static double Bend(double height) => Math.Min(0.12, 0.04 + 0.5 * height);

    /// <summary>How far below standing straight the knees give on landing from it, with time for it.</summary>
    public static double Give(double height) => Math.Min(0.08, 0.02 + 0.4 * height);

    /// <summary>How high a time in the air carries the body: h = g T^2 / 8.</summary>
    public static double Height(double airSeconds) => G * airSeconds * airSeconds / 8;

    /// <summary>The time the bend takes before a time in the air.</summary>
    public static double BendSeconds(double airSeconds) => 0.7 * airSeconds * Bend(Height(airSeconds)) / 0.12;

    /// <summary>The time before the landing a written height needs: the bend and the air.</summary>
    public static double BeforeSeconds(double height)
    {
        double air = Math.Sqrt(8 * height / G);
        return air + BendSeconds(air);
    }

    /// <summary>Of a time before the landing, how much is in the air (the rest is the bend).</summary>
    public static double AirIn(double beforeSeconds)
    {
        if (beforeSeconds <= 0)
            return 0;
        // The bend grows with the height, which grows with the air: a few rounds settle it.
        double air = beforeSeconds / 1.7;
        for (int i = 0; i < 40; i++)
            air = beforeSeconds / (1 + 0.7 * Bend(Height(air)) / 0.12);
        return air;
    }
}
