namespace Shosa.Core.Semantics;

/// <summary>
/// Juggling's own notation, read as jugglers write it (DESIGN.md 118): `3`, `531`, `b97`, `[33]3`,
/// `(4,2x)*`. One digit is one beat of throwing; the number is how many beats until that ball is
/// thrown again (a = 10, b = 11 …). In plain (asynchronous) siteswap the hands take turns, right first,
/// and an odd throw goes to the other hand; `(a,b)` is both hands at once (the right hand's throw
/// first, here), taking two beats, its numbers even, an `x` sending one across; `[ab]` throws two
/// from one hand at once; `*` after a pattern of pairs plays it again with the hands swapped.
///
/// Pure data and rules: what the binder checks and the solver times. Beats here are throwing beats,
/// counted from the pattern's start; the hand of a plain beat is not stored, it is the parity of the
/// beat in the run of throws it is a part of (<see cref="HandAt"/>).
/// </summary>
public static class Siteswap
{
    /// <summary>One ball leaving a hand: how many beats until it is thrown again, and (a pair's) whether it crosses.</summary>
    public readonly record struct Throw(int Value, bool Cross);

    /// <summary>
    /// One beat of a pattern. A plain beat: <see cref="Async"/> holds what the hand whose turn it is throws.
    /// A pair: <see cref="Right"/> and <see cref="Left"/> throw together, and the beat after it is <see cref="Empty"/>.
    /// </summary>
    public sealed record Beat(bool Sync, IReadOnlyList<Throw> Async, IReadOnlyList<Throw> Right, IReadOnlyList<Throw> Left)
    {
        public static readonly Beat Empty = new(true, [], [], []);
        /// <summary>What the hand throws on this beat (0 right, 1 left), given which hand's turn a plain beat is.</summary>
        public IReadOnlyList<Throw> Of(int hand, int turn) => Sync ? (hand == 0 ? Right : Left) : hand == turn ? Async : [];
    }

    /// <summary>A pattern as it is played: its beats in order (a pair is two), repeated.</summary>
    public sealed record Pattern(string Text, IReadOnlyList<Beat> Beats, int Balls)
    {
        /// <summary>The highest throw in it.</summary>
        public int Highest => Beats.SelectMany(b => b.Async.Concat(b.Right).Concat(b.Left)).Select(t => t.Value).DefaultIfEmpty(0).Max();
        public bool HasSync => Beats.Any(b => b.Sync);
    }

    /// <summary>How many throws a pattern thrown for so many beats makes, one every so many.</summary>
    public static int Throws(double beats, double every) => every > 0 ? (int)Math.Ceiling(beats / every - 1e-6) : 0;

    /// <summary>
    /// Thrown once through or less: a passage between two patterns, which need not be a pattern on its own (`4`
    /// once between `3` and `51` is the throw that changes them; four of it would be four balls).
    /// </summary>
    public static bool IsPassage(Pattern pattern, int throws) => throws <= pattern.Beats.Count;

    /// <summary>The hand whose turn a plain beat is: the right on the first beat of a run of throws, then each in turn.</summary>
    public static int HandAt(long beatInRun, bool leftFirst) => (int)(((beatInRun % 2) + 2) % 2) ^ (leftFirst ? 1 : 0);

    /// <summary>Where a throw comes down: the hand (0 right, 1 left). Plain: an odd number goes across. Pair: an `x` does.</summary>
    public static int Lands(int hand, Throw t, bool sync) => (sync ? t.Cross : t.Value % 2 == 1) ? 1 - hand : hand;

    /// <summary>
    /// Reads a pattern, or says what is wrong with it. Checks the notation, then (<paramref name="repeated"/>) that,
    /// thrown over and over, no two balls come down together. A passage — throws made once between two patterns,
    /// `4` between `3` and `51` — is read without that: it is checked where it is thrown, by what is in the air.
    /// </summary>
    public static (Pattern? Pattern, string? Error) Parse(string text, bool repeated = true)
    {
        var beats = new List<Beat>();
        int i = 0;
        bool star = false;

        string? ReadValue(out int value)
        {
            value = 0;
            if (i >= text.Length)
                return "a throw (a digit, or a to z for 10 and up) is missing at the end";
            char c = text[i];
            if (char.IsAsciiDigit(c))
                value = c - '0';
            else if (c is >= 'a' and <= 'z')
                value = c - 'a' + 10;
            else
                return $"'{c}' is not a throw: a throw is a digit, or a to z for 10 and up (a = 10, b = 11)";
            i++;
            return null;
        }

        // A hand's throws on one beat: one throw, or several in brackets. `x` crosses, in a pair only.
        string? ReadHand(bool sync, List<Throw> into)
        {
            bool multi = i < text.Length && text[i] == '[';
            if (multi)
                i++;
            do
            {
                if (ReadValue(out int value) is { } error)
                    return error;
                bool cross = false;
                if (i < text.Length && text[i] == 'x')
                {
                    if (!sync)
                        return "an x crosses a throw of a pair, (4,2x); in plain siteswap an odd number crosses by itself";
                    cross = true;
                    i++;
                }
                if (sync && value % 2 == 1)
                    return $"a pair's throws are even: both hands throw every second beat, so {value} cannot come back to a hand on its beat. Write {value - 1} or {value + 1}, with an x to cross";
                into.Add(new Throw(value, cross));
            }
            while (multi && i < text.Length && text[i] != ']');
            if (multi)
            {
                if (i >= text.Length)
                    return "a '[' is not closed: [33]3";
                i++;
                if (into.Count < 2)
                    return "brackets hold two or more throws made at once: [33]";
            }
            return null;
        }

        if (text.Length == 0)
            return (null, "a pattern is missing");
        while (i < text.Length)
        {
            char c = text[i];
            if (star)
                return (null, "'*' ends a pattern: (6x,4)*");
            if (c == '*')
            {
                if (!beats.Any(b => b.Sync))
                    return (null, "'*' plays a pattern of pairs again with the hands swapped: (6x,4)*. A plain pattern swaps its hands by itself when it is odd");
                star = true;
                i++;
            }
            else if (c == '(')
            {
                i++;
                List<Throw> right = [], left = [];
                if (ReadHand(true, right) is { } e1)
                    return (null, e1);
                if (i >= text.Length || text[i] != ',')
                    return (null, "a pair is two hands' throws, the right hand's first: (4,4), (6x,2)");
                i++;
                if (ReadHand(true, left) is { } e2)
                    return (null, e2);
                if (i >= text.Length || text[i] != ')')
                    return (null, "a '(' is not closed: (4,4)");
                i++;
                beats.Add(new Beat(true, [], right, left));
                beats.Add(Beat.Empty);
            }
            else if (c is ')' or ',' or ']')
            {
                return (null, $"'{c}' does not belong here");
            }
            else
            {
                var throws = new List<Throw>();
                if (ReadHand(false, throws) is { } error)
                    return (null, error);
                beats.Add(new Beat(false, throws, [], []));
            }
        }
        if (star)
        {
            // Again with the hands swapped: a plain beat's hand swaps by itself only when the length is odd.
            var swapped = beats.Select(b => b.Sync ? b with { Right = b.Left, Left = b.Right } : b).ToList();
            beats.AddRange(swapped);
        }

        if (repeated && Check(beats) is { } problem)
            return (null, problem);
        int period = Period(beats);
        int sum = 0;
        for (int k = 0; k < period; k++)
        {
            var beat = beats[k % beats.Count];
            sum += beat.Async.Concat(beat.Right).Concat(beat.Left).Sum(t => t.Value);
        }
        return (new Pattern(text, beats, sum / period), null);
    }

    /// <summary>The beats after which the pattern comes back to the same hands: twice its length when a plain beat would change hands.</summary>
    public static int Period(IReadOnlyList<Beat> beats) => beats.Count % 2 == 1 && beats.Any(b => !b.Sync) ? beats.Count * 2 : beats.Count;

    /// <summary>
    /// Played over and over, does every ball come down in a hand that throws it on that beat, one throw
    /// for one ball? Null if so, else what goes wrong first.
    /// </summary>
    private static string? Check(IReadOnlyList<Beat> beats)
    {
        int period = Period(beats);
        var landing = new int[period, 2];
        var throwing = new int[period, 2];
        for (int k = 0; k < period; k++)
        {
            var beat = beats[k % beats.Count];
            for (int hand = 0; hand < 2; hand++)
            {
                foreach (var t in beat.Of(hand, HandAt(k, false)))
                {
                    if (t.Value == 0)
                        continue;
                    throwing[k, hand]++;
                    landing[(k + t.Value) % period, Lands(hand, t, beat.Sync)]++;
                }
            }
        }
        // Two coming down together first: that is the throw to change. A hand left with nothing to throw follows from it.
        foreach (bool over in new[] { true, false })
        {
            for (int k = 0; k < period; k++)
            {
                for (int hand = 0; hand < 2; hand++)
                {
                    if (over ? landing[k, hand] <= throwing[k, hand] : landing[k, hand] >= throwing[k, hand])
                        continue;
                    string which = hand == 0 ? "right" : "left";
                    int beat = k % beats.Count + 1;
                    return over
                        ? $"{landing[k, hand]} balls come down in the {which} hand on beat {beat} of it, which throws {throwing[k, hand]} there: two throws land together (the average of the numbers is the number of balls, and each beat must catch what it throws)"
                        : $"the {which} hand throws on beat {beat} of it, but no ball comes down there to throw";
                }
            }
        }
        return null;
    }

    /// <summary>
    /// The balls a pattern expects to come down from before its first beat, played as if it had always been
    /// going: (beats after its start, hand). This is the state it starts in; a pattern can follow another
    /// directly when what is in the air at the change is exactly this.
    /// </summary>
    /// <param name="leftFirst">The left hand's turn on the first plain beat.</param>
    /// <param name="swapPairs">A pair's first number is the left hand's (`@mirror`).</param>
    public static List<(int Beat, int Hand)> Expects(Pattern pattern, bool leftFirst, bool swapPairs = false)
    {
        var expected = new List<(int, int)>();
        int period = Period(pattern.Beats), reach = pattern.Highest;
        int back = (reach / period + 1) * period;
        for (int k = -back; k < 0; k++)
        {
            var beat = pattern.Beats[((k % pattern.Beats.Count) + pattern.Beats.Count) % pattern.Beats.Count];
            for (int hand = 0; hand < 2; hand++)
            {
                foreach (var t in beat.Of(beat.Sync && swapPairs ? 1 - hand : hand, HandAt(k, leftFirst)))
                {
                    if (t.Value > 0 && k + t.Value >= 0)
                        expected.Add((k + t.Value, Lands(hand, t, beat.Sync)));
                }
            }
        }
        expected.Sort();
        return expected;
    }

    /// <summary>
    /// Plain throws that take what is in the air (beats from now that a ball comes down, the hands taking
    /// turns) to what a plain pattern expects: the shortest, up to <paramref name="most"/> throws, or null.
    /// Both are sets of beats: in plain siteswap the hand follows from the beat.
    /// </summary>
    public static string? Bridge(IReadOnlyCollection<int> inAir, IReadOnlyCollection<int> wanted, int highest, int most = 3)
    {
        if (inAir.Count != wanted.Count || inAir.Any(b => b < 0 || b > 30) || wanted.Any(b => b < 0 || b > 30))
            return null;
        long Mask(IEnumerable<int> beats) => beats.Aggregate(0L, (m, b) => m | (1L << b));
        long from = Mask(inAir), to = Mask(wanted);
        var frontier = new List<(long State, string Throws)> { (from, "") };
        for (int depth = 1; depth <= most; depth++)
        {
            var next = new List<(long, string)>();
            foreach (var (state, throws) in frontier)
            {
                if ((state & 1) == 0)
                {
                    if (state >> 1 == to)
                        return throws + "0";
                    next.Add((state >> 1, throws + "0"));
                    continue;
                }
                for (int value = 1; value <= Math.Max(highest, 9); value++)
                {
                    if ((state & (1L << value)) != 0)
                        continue;
                    long after = (state | (1L << value)) >> 1;
                    string written = throws + (value < 10 ? (char)('0' + value) : (char)('a' + value - 10));
                    if (after == to)
                        return written;
                    next.Add((after, written));
                }
            }
            frontier = next;
        }
        return null;
    }
}
