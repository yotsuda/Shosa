using Shosa.Core.Semantics;

namespace Shosa.Core.Motion;

/// <summary>
/// The face of a character (`face { smile:4 | smile.winkRight:1 smile:3 | }`, DESIGN.md 131): how much of each of
/// VRM's expressions of feeling it has at every frame, and how far each eye is shut. The eyes blink by themselves.
/// </summary>
public static class Face
{
    /// <summary>Seconds a look takes to come (and the one before it to go), and an eye to shut or open: both just before the word's beat.</summary>
    public const double LookSeconds = 0.2, EyeSeconds = 0.08;

    /// <summary>A blink: seconds the lids take to shut, stay shut, and open.</summary>
    private const double BlinkShut = 0.05, BlinkHeld = 0.04, BlinkOpen = 0.09;
    /// <summary>Seconds from one blink to the next: no fewer, and how many more at the most.</summary>
    private const double BlinkEvery = 2.2, BlinkSpread = 3.2;

    private const int Eyes = 5;   // the first of the two eyes among Vocabulary.FaceShapes

    /// <summary>
    /// The weights of <see cref="Vocabulary.FaceShapes"/> at every frame. A word's face is there on its beat: the look
    /// before it gives way to it over the last <see cref="LookSeconds"/> before (less, when the word before is
    /// shorter), the eyes over <see cref="EyeSeconds"/>. With <paramref name="blinks"/> the eyes blink every few
    /// seconds, at moments that depend on <paramref name="who"/> alone (two dancers do not blink together; the same
    /// dance blinks the same every time), but not while the row has an eye shut. Null when there is nothing to
    /// say: no row and no blinking.
    /// </summary>
    public static List<float[]>? Frames(IReadOnlyList<Seg>? row, IReadOnlyList<double> secondsOfFrame, Func<double, double> secondsAt,
        string who, bool blinks, double totalSeconds, bool loop)
    {
        var words = (row ?? []).Where(s => s.Kind == SegKinds.Face && s.Face is { Length: >= 7 })
            .Select(s => (At: secondsAt(s.At), From: secondsAt(Math.Min(s.T0, s.At)), Face: s.Face!))
            .OrderBy(w => w.At)
            .ToList();
        if (words.Count == 0 && !blinks)
            return null;
        // A loop follows itself: its first word is where its end goes.
        if (loop && words.Count > 0 && words[0].At <= 1e-6 && totalSeconds > 0)
            words.Add((totalSeconds, words[^1].At, words[0].Face));

        var blinkAt = blinks ? Blinks(who, totalSeconds, loop) : [];
        var rest = new double[Vocabulary.FaceShapes.Length];
        var frames = new List<float[]>(secondsOfFrame.Count);
        int now = -1, blink = 0;
        foreach (double t in secondsOfFrame)
        {
            while (now + 1 < words.Count && words[now + 1].At <= t + 1e-9)
                now++;
            var here = now >= 0 ? words[now].Face : loop && words.Count > 0 ? words[^1].Face : rest;
            var weights = new float[rest.Length];
            for (int k = 0; k < weights.Length; k++)
            {
                double value = here[k];
                if (now + 1 < words.Count)
                {
                    var next = words[now + 1];
                    double room = next.At - Math.Max(next.From, now >= 0 ? words[now].At : 0);
                    double lead = Math.Min(k >= Eyes ? EyeSeconds : LookSeconds, Math.Max(room, 0));
                    if (lead > 1e-9 && t > next.At - lead)
                        value += (next.Face[k] - value) * Smooth((t - (next.At - lead)) / lead);
                }
                weights[k] = (float)Math.Round(value, 3);
            }
            if (blinkAt.Count > 0)
            {
                while (blink + 1 < blinkAt.Count && blinkAt[blink] + BlinkShut + BlinkHeld + BlinkOpen < t)
                    blink++;
                double since = t - blinkAt[blink];
                double shut = since < 0 ? 0
                    : since < BlinkShut ? since / BlinkShut
                    : since < BlinkShut + BlinkHeld ? 1
                    : Math.Max(0, 1 - (since - BlinkShut - BlinkHeld) / BlinkOpen);
                // An eye the row has shut is a wink, or a face asleep: no blink beside it.
                shut *= 1 - Math.Max(weights[Eyes], weights[Eyes + 1]);
                if (shut > 0)
                {
                    weights[Eyes] = Math.Max(weights[Eyes], (float)Math.Round(shut, 3));
                    weights[Eyes + 1] = Math.Max(weights[Eyes + 1], (float)Math.Round(shut, 3));
                }
            }
            frames.Add(weights);
        }
        return frames;
    }

    /// <summary>When the blinks begin, in seconds: all inside the clip, and in a loop none over its join.</summary>
    private static List<double> Blinks(string who, double totalSeconds, bool loop)
    {
        // (A hash of the name that is the same in every run: string.GetHashCode is not.)
        uint state = 2166136261;
        foreach (char c in who)
            state = (state ^ c) * 16777619;
        double Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / (double)0x1000000;
        }
        var at = new List<double>();
        double whole = BlinkShut + BlinkHeld + BlinkOpen;
        for (double t = 0.8 + Next() * BlinkEvery; t + whole < totalSeconds - (loop ? 0.1 : 0); t += BlinkEvery + Next() * BlinkSpread)
            at.Add(t);
        return at;
    }

    private static double Smooth(double x) => x <= 0 ? 0 : x >= 1 ? 1 : x * x * (3 - 2 * x);
}
