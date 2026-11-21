using Shosa.Core.Semantics;

namespace Shosa.Core.Motion;

/// <summary>
/// The mouth of a body that sings (`dancer alice … sings melody`, DESIGN.md 99): which of VRM's five
/// mouth expressions a syllable opens to, and how open each is at every frame.
/// </summary>
public static class Mouth
{
    /// <summary>VRM's mouth expressions, in the order <see cref="ClipFigure.Mouth"/> holds their weights.</summary>
    public static readonly string[] Shapes = ["aa", "ih", "ou", "ee", "oh"];

    /// <summary>How far each shape opens at its fullest: VRM's own 1 is a wide-open mouth, more than singing needs.</summary>
    private static readonly float[] Fullest = [0.85f, 0.6f, 0.7f, 0.65f, 0.75f];

    /// <summary>Seconds the mouth starts opening before the syllable's beat (the consonant), and is open after it.</summary>
    private const double Lead = 0.05, Rise = 0.03;
    /// <summary>Seconds it starts closing before the voice stops, and is closed after.</summary>
    private const double Release = 0.06, Fall = 0.03;

    /// <summary>
    /// The shape a syllable opens to: aa, ih, ou, ee, oh, or nn (closed: ん, っ, a hum); null for ー, which
    /// holds the shape before it. Kana by the vowel of its last kana (きゃ is a); other writing by its first vowel letter.
    /// </summary>
    public static string? Vowel(string text)
    {
        string? kana = null;
        foreach (char raw in text)
        {
            char c = raw is >= 'ァ' and <= 'ヶ' ? (char)(raw - 0x60) : raw;   // katakana as hiragana
            string? v = c switch
            {
                'ー' or '〜' or '～' => "-",
                'ん' or 'っ' => "nn",
                _ when "あかさたなはまやらわがざだばぱぁゃゎ".Contains(c) => "aa",
                _ when "いきしちにひみりぎじぢびぴぃゐ".Contains(c) => "ih",
                _ when "うくすつぬふむゆるぐずづぶぷぅゅゔ".Contains(c) => "ou",
                _ when "えけせてねへめれげぜでべぺぇゑ".Contains(c) => "ee",
                _ when "おこそとのほもよろをごぞどぼぽぉょ".Contains(c) => "oh",
                _ => null,
            };
            if (v is not null)
                kana = v;
        }
        if (kana is not null)
            return kana == "-" ? null : kana;

        foreach (char c in text.ToLowerInvariant())
        {
            switch (c)
            {
                case 'a': return "aa";
                case 'i' or 'y': return "ih";
                case 'u': return "ou";
                case 'e': return "ee";
                case 'o': return "oh";
            }
        }
        return "nn";
    }

    /// <summary>Seconds the lips take to meet before a syllable that starts with them (ま, ば, ぱ), stay shut, and open from its beat.</summary>
    private const double Shut = 0.04, Closed = 0.06, Part = 0.05;

    /// <summary>
    /// Whether a syllable starts with the lips meeting: ま, ば, ぱ and their rows in kana (katakana too);
    /// other writing when its first letter is m, b or p.
    /// </summary>
    public static bool Bilabial(string text)
    {
        foreach (char raw in text)
        {
            char c = raw is >= 'ァ' and <= 'ヶ' ? (char)(raw - 0x60) : raw;
            if (c is >= 'ぁ' and <= 'ゖ')
                return "まみむめもばびぶべぼぱぴぷぺぽ".Contains(c);
            if (char.IsLetter(c))
                return char.ToLowerInvariant(c) is 'm' or 'b' or 'p';
        }
        return false;
    }

    /// <summary>
    /// The weights of <see cref="Shapes"/> at every frame, five to a frame: each syllable opens its shape
    /// from a little before its beat, holds it while the voice sounds, and closes it as the voice stops,
    /// so one syllable running into the next passes from shape to shape. Before a syllable that starts with
    /// the lips (<see cref="Bilabial"/>) every shape closes, so や ま does not stay open across the m.
    /// </summary>
    public static List<float[]> Frames(IReadOnlyList<SyllableIr> sung, IReadOnlyList<double> secondsOfFrame, Func<double, double> secondsAt)
    {
        var spans = sung
            .Select(s => (T0: secondsAt(s.Beat), T1: secondsAt(s.Beat + s.Beats), Shape: Array.IndexOf(Shapes, s.Vowel)))
            .Where(s => s.Shape >= 0 && s.T1 > s.T0)
            .OrderBy(s => s.T0)
            .ToList();
        var lips = sung
            .Where(s => s.Beats > 0 && Bilabial(s.Text))
            .Select(s => secondsAt(s.Beat))
            .Order()
            .ToList();
        var frames = new List<float[]>(secondsOfFrame.Count);
        int first = 0;
        foreach (double t in secondsOfFrame)
        {
            var weights = new float[Shapes.Length];
            while (first < spans.Count && spans[first].T1 + Fall < t)
                first++;
            for (int i = first; i < spans.Count && spans[i].T0 - Lead <= t; i++)
            {
                var (t0, t1, shape) = spans[i];
                double open = Ramp(t, t0 - Lead, t0 + Rise);
                double close = 1 - Ramp(t, Math.Max(t1 - Release, t0 + Rise), t1 + Fall);
                // A short syllable does not open as far: there is no time to.
                float level = (float)(Math.Min(open, close) * Math.Min(1, 0.55 + (t1 - t0) * 2.5)) * Fullest[shape];
                weights[shape] = Math.Max(weights[shape], level);
            }
            // The lips meet before the beat of ま, ば, ぱ and part on it.
            double apart = 1;
            foreach (double t0 in lips)
            {
                if (t0 - Closed - Shut > t)
                    break;
                apart = Math.Min(apart, Math.Max(1 - Ramp(t, t0 - Closed - Shut, t0 - Closed), Ramp(t, t0, t0 + Part)));
            }
            if (apart < 1)
                for (int k = 0; k < weights.Length; k++)
                    weights[k] *= (float)apart;
            frames.Add(weights);
        }
        return frames;
    }

    private static double Ramp(double t, double from, double to) =>
        t <= from ? 0 : t >= to ? 1 : (t - from) / (to - from);
}
