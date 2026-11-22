using Shosa.Core.Semantics;

namespace Shosa.Core.Motion;

/// <summary>
/// A dancer's juggle row timed (DESIGN.md 118): every throw, which ball, from which hand to which, when it
/// leaves and when it is caught, and where each hand is meanwhile. Built from the row's patterns and the
/// clip's tempos; the solver puts the hands where this says, and the balls' flights are drawn between the
/// hands as they are at the throw and at the catch.
///
/// Siteswap counts in throwing beats, not in words: the patterns of a row written one against the other are
/// one run of throws, the hands taking turns through it, and a ball thrown in one pattern comes down in the
/// next. Whether a pattern can follow the one before is decided here, by what is in the air at the change.
/// A run ends where the row has no word (or `skip`): the balls still in the air are caught and held, and the
/// next run starts again from the hands.
/// </summary>
public sealed class Juggling
{
    /// <summary>Of a throwing beat, how long a hand holds a ball before it throws it: the rest of its two beats it is empty.</summary>
    public const double Dwell = 1.3;
    /// <summary>The shortest flight, of a ball handed across (a 1): beats.</summary>
    private const double Handover = 0.25;
    /// <summary>Seconds the hands take to come to the juggling from what the arms did, and to go back.</summary>
    public const double Ramp = 0.35;
    private const double G = 9.81;

    /// <summary>A ball leaving a hand and coming down in one. A hold (a 2 that stays) is not a throw.</summary>
    /// <param name="Trick">A throw from under the leg or behind the back ("under", "back"), and the word it is in: what the solver checks.</param>
    public sealed record Flight(int Ball, int From, int To, double Thrown, double Caught, int Value, string? Trick = null, Seg? Seg = null);

    /// <summary>What went wrong, said at the word it is about.</summary>
    public sealed record Problem(Seg Seg, string Code, bool IsError, string Message);

    public string Prop { get; }
    public int Balls { get; private set; }
    public List<Flight> Flights { get; } = [];
    public List<Problem> Problems { get; } = [];
    /// <summary>The runs of throws, in seconds: from the first throw to the last catch.</summary>
    public List<(double From, double To)> Runs { get; } = [];
    /// <summary>The juggle words in force, in beats: from where the hands come to the juggling to where they leave it.</summary>
    public List<(double From, double To)> Busy { get; } = [];

    /// <summary>Where each ball is before it is first thrown: the hand (0 right, 1 left).</summary>
    private readonly List<int> _startHand = [];
    /// <summary>A hand's places to be, in seconds, and what the hand does there.</summary>
    /// <summary>A hand's places, in seconds: where, how fast, and how the hand is held there (<see cref="Style"/>).</summary>
    private readonly List<(double T, Vec3 At, Vec3 Velocity, Style Style)>[] _keys = [[], []];
    /// <summary>How many throws each hand has made, for a trick that changes from one throw to the next (Mills' Mess: over, then under).</summary>
    private readonly int[] _throwsBy = [0, 0];

    /// <summary>How a hand is held (DESIGN.md 118.12): the prop's own way (a ball on the palm, a ring by an edge), or a trick's.</summary>
    public enum Style { Own = 0, Claw = 1, Penguin = 2, Back = 3, Under = 4 }

    private readonly IReadOnlyList<TempoIr> _tempos;
    /// <summary>Words thrown as a way between two patterns, not patterns of the row's balls themselves: nothing is expected at their start.</summary>
    private readonly HashSet<Seg> _passages = [];

    private Juggling(string prop, IReadOnlyList<TempoIr> tempos)
    {
        Prop = prop;
        _tempos = tempos;
    }

    private double Sec(double beat) => TempoMap.Seconds(_tempos, Math.Max(0, beat));

    public static Juggling? Build(DancerIr dancer, IReadOnlyList<TempoIr> tempos)
    {
        if (dancer.Juggles is not { } prop || !dancer.Tracks.TryGetValue(Channels.Juggle, out var segs) || segs.Count == 0)
            return null;
        var juggling = new Juggling(prop, tempos);
        juggling.Plan(segs);
        return juggling;
    }

    // ------------------------------------------------------------------ the throws

    private sealed record Slot(double Beat, Seg? Seg, Siteswap.Pattern? Pattern, int Index, int InRun);

    private void Plan(List<Seg> segs)
    {
        // A pattern, or a passage between two (thrown once through or less): a passage is judged only by what is in the air.
        var parsed = new List<(Seg Seg, Siteswap.Pattern Pattern, bool Passage, bool Valid)>();
        foreach (var seg in segs)
        {
            var (pattern, _) = Siteswap.Parse(seg.Pattern ?? "");
            bool valid = pattern is not null;
            pattern ??= Siteswap.Parse(seg.Pattern ?? "", repeated: false).Pattern;
            if (pattern is null)
                continue;
            bool passage = Siteswap.IsPassage(pattern, Siteswap.Throws(seg.T1 - seg.T0, seg.Every ?? 0.5));
            if (valid || passage)
                parsed.Add((seg, pattern, passage, valid));
        }
        if (parsed.Count == 0)
            return;
        var first = parsed.FirstOrDefault(p => p.Valid && !p.Passage);
        if (first.Seg is null)
            first = parsed.FirstOrDefault(p => p.Valid);
        if (first.Seg is null)
            return;
        Balls = first.Pattern.Balls;

        // Runs: patterns written one against the other. One with another number of balls cannot be thrown.
        var runs = new List<List<(Seg Seg, Siteswap.Pattern Pattern)>>();
        double lastEnd = double.NegativeInfinity;
        foreach (var (seg, pattern, passage, valid) in parsed)
        {
            bool joined = runs.Count > 0 && Math.Abs(seg.T0 - lastEnd) <= 1e-6;
            if (!joined && !valid)
            {
                // A passage leads from one pattern to another: from the hands there is nothing in the air for it to change.
                Problems.Add(new Problem(seg, "SH2019", true,
                    $"'{seg.Pattern}' is not a pattern on its own, only a way from one pattern to the next, and nothing is thrown before it here. Start from a pattern of {Balls} ('{first.Seg.Pattern}')."));
                lastEnd = double.NegativeInfinity;
                continue;
            }
            // Within a run a passage is judged by what is in the air; what starts a run starts from the hands, with the row's balls.
            if ((!passage || !joined) && pattern.Balls != Balls)
            {
                Problems.Add(new Problem(seg, "SH2019", true,
                    $"'{seg.Pattern}' is {pattern.Balls} balls, and the row began with {Balls} ('{first.Seg.Pattern}'): the hands juggle the same balls all through. Write a pattern of {Balls}, or begin with {pattern.Balls}."));
                lastEnd = double.NegativeInfinity;
                continue;
            }
            if (!joined)
                runs.Add([]);
            runs[^1].Add((seg, pattern));
            if (!valid || pattern.Balls != Balls)
                _passages.Add(seg);
            lastEnd = seg.T1;
        }

        // Where the balls are between runs: in the hands, each ball in one.
        var held = new List<int>[] { [], [] };
        bool fresh = true;
        double heldSince = 0;
        foreach (var run in runs)
        {
            // A run starts from a pattern of the row's balls: one that starts with a valid pattern of another count was dropped above.
            if (run[0].Pattern.Balls != Balls)
                continue;
            PlanRun(run, held, ref fresh, ref heldSince);
        }
    }

    private void PlanRun(List<(Seg Seg, Siteswap.Pattern Pattern)> run, List<int>[] held, ref bool first, ref double heldSince)
    {
        // The throwing beats of the run, and some after it, where the balls still in the air come down.
        var slots = new List<Slot>();
        int index = 0;
        double every = run[0].Seg.Every ?? 0.5;
        foreach (var (seg, pattern) in run)
        {
            every = seg.Every ?? 0.5;
            for (int k = 0; ; k++)
            {
                double beat = seg.T0 + k * every;
                if (beat >= seg.T1 - 1e-6)
                    break;
                slots.Add(new Slot(beat, seg, pattern, k, index++));
            }
        }
        if (slots.Count == 0)
            return;
        int highest = run.Max(r => r.Pattern.Highest);
        double after = slots[^1].Beat;
        for (int k = 1; k <= highest + 2; k++)
            slots.Add(new Slot(after + k * every, null, null, -1, index++));

        double SlotSec(int s) => Sec(slots[Math.Min(s, slots.Count - 1)].Beat);
        double BeatSec(int s) => s + 1 < slots.Count ? SlotSec(s + 1) - SlotSec(s) : Sec(slots[s].Beat + every) - SlotSec(s);

        // What comes down where: slot → (ball, hand, the flight that brings it, if any).
        var arriving = new Dictionary<int, List<(int Ball, int Hand, int Flight)>>();
        void Arrive(int slot, int ball, int hand, int flight)
        {
            if (!arriving.TryGetValue(slot, out var list))
                arriving[slot] = list = [];
            list.Add((ball, hand, flight));
        }

        // The run starts from the hands: each ball the first pattern expects is in the hand that throws it.
        var (firstSeg, firstPattern) = run[0];
        var expects = Siteswap.Expects(firstPattern, firstSeg.LeftFirst, firstSeg.LeftFirst);
        double start = SlotSec(0);
        if (first)
        {
            // The first balls: in the hands from the start of the clip.
            first = false;
            for (int b = 0; b < Balls; b++)
            {
                _startHand.Add(expects[b].Hand);
                held[expects[b].Hand].Add(b);
            }
        }
        foreach (var (beat, hand) in expects)
        {
            int ball;
            if (held[hand].Count > 0)
            {
                ball = held[hand][0];
                held[hand].RemoveAt(0);
            }
            else
            {
                // Not in that hand: it is handed across before the run begins.
                ball = held[1 - hand][0];
                held[1 - hand].RemoveAt(0);
                double at = Math.Max(heldSince, start - 2 * Ramp);
                Flights.Add(new Flight(ball, 1 - hand, hand, at - 0.15, at, 1));
            }
            Arrive(beat, ball, hand, -1);
        }
        foreach (var hand in new[] { 0, 1 })
            _keys[hand].Add((start - Ramp, Ready(hand), Still, Style.Own));

        double lastCatch = start;
        Seg? previous = null;
        for (int s = 0; s < slots.Count; s++)
        {
            var slot = slots[s];
            if (slot.Seg is null)
                continue;
            // A new pattern: what is in the air must be what it expects. If not, say so, and put the balls where it expects them.
            if (slot.Index == 0 && previous is not null && !ReferenceEquals(previous, slot.Seg) && !_passages.Contains(slot.Seg))
                Change(previous, slot, s, arriving, slots);
            previous = slot.Seg;

            var beat = slot.Pattern!.Beats[slot.Index % slot.Pattern.Beats.Count];
            int turn = Siteswap.HandAt(slot.InRun, slot.Seg.LeftFirst);
            double thrown = SlotSec(s), T = BeatSec(s);
            for (int hand = 0; hand < 2; hand++)
            {
                // A pair's first number is the right hand's; @mirror gives it to the left.
                int writer = beat.Sync && slot.Seg.LeftFirst ? 1 - hand : hand;
                var throws = beat.Of(writer, turn).Where(t => t.Value > 0).ToList();
                var here = arriving.TryGetValue(s, out var list) ? list.Where(a => a.Hand == hand).ToList() : [];
                if (here.Count != throws.Count && Problems.Count(p => ReferenceEquals(p.Seg, slot.Seg)) == 0)
                {
                    Problems.Add(new Problem(slot.Seg, "SH2019", true, here.Count > throws.Count
                        ? $"'{slot.Seg.Pattern}' cannot be thrown here: on beat {Fmt(slot.Beat)} {here.Count} balls come down in the {Name(hand)} hand, which throws {throws.Count} there."
                        : $"'{slot.Seg.Pattern}' cannot be thrown here: on beat {Fmt(slot.Beat)} the {Name(hand)} hand has no ball to throw."));
                }
                for (int k = 0; k < throws.Count && k < here.Count; k++)
                {
                    var t = throws[k];
                    int ball = here[k].Ball, to = Siteswap.Lands(hand, t, beat.Sync), land = s + t.Value;
                    if (to == hand && t.Value == 2)
                    {
                        Arrive(land, ball, hand, -1);   // held through: a 2 stays in the hand
                        continue;
                    }
                    double caught = Math.Max(SlotSec(land) - Dwell * BeatSec(Math.Min(land, slots.Count - 1)), thrown + Handover * T);
                    Flights.Add(new Flight(ball, hand, to, thrown, caught, t.Value,
                        slot.Seg.Tricks?.FirstOrDefault(x => x is "under" or "back"), slot.Seg));
                    Arrive(land, ball, to, Flights.Count - 1);
                    var catcher = slots[Math.Min(land, slots.Count - 1)].Seg ?? slot.Seg;
                    Swing(hand, to, t.Value, thrown, caught, T, slot.Seg.Tricks, catcher.Tricks, _throwsBy[hand]++);
                    lastCatch = Math.Max(lastCatch, caught);
                }
                // Balls that came down and are not thrown: held, and thrown on the hand's next turn.
                for (int k = throws.Count; k < here.Count; k++)
                    Arrive(s + 2, here[k].Ball, hand, -1);
            }
        }

        // The balls that come down after the last throw are held.
        int lastThrow = slots.FindLastIndex(x => x.Seg is not null);
        foreach (var (slot, list) in arriving.Where(a => a.Key > lastThrow))
            foreach (var a in list)
                held[a.Hand].Add(a.Ball);
        foreach (var hand in new[] { 0, 1 })
        {
            _keys[hand].Add((lastCatch + 0.2, Ready(hand), Still, Style.Own));
            _keys[hand].Sort((a, b) => a.T.CompareTo(b.T));
            Soften(hand);
        }
        foreach (var (seg, _) in run.Where(r => r.Seg.Tricks?.Contains("mills") == true))
            _mills.Add((Sec(seg.T0), Sec(seg.T1)));
        heldSince = lastCatch + 0.2;
        Runs.Add((start, lastCatch));
        Busy.Add((run[0].Seg.T0, run[^1].Seg.T1));
    }

    /// <summary>
    /// The change from one pattern to the next: what is in the air must come down as the new pattern expects.
    /// When it does not, the change is an error, with the throws that would make it; the balls are then sent
    /// where the new pattern expects them, so that what comes after is still drawn.
    /// </summary>
    private void Change(Seg previous, Slot slot, int s, Dictionary<int, List<(int Ball, int Hand, int Flight)>> arriving, List<Slot> slots)
    {
        var inAir = arriving.Where(a => a.Key >= s).SelectMany(a => a.Value.Select(v => (Beat: a.Key - s, v.Hand, v.Ball, v.Flight))).OrderBy(a => a.Beat).ToList();
        // The hands take turns through the run: the new pattern's first beat is the hand whose turn it is.
        bool leftFirst = Siteswap.HandAt(slot.InRun, slot.Seg!.LeftFirst) == 1;
        var expects = Siteswap.Expects(slot.Pattern!, leftFirst, slot.Seg.LeftFirst);
        if (inAir.Select(a => (a.Beat, a.Hand)).SequenceEqual(expects))
            return;

        string hint = "";
        var prevPattern = Siteswap.Parse(previous.Pattern ?? "").Pattern;
        if (prevPattern is not null && !prevPattern.HasSync && !slot.Pattern!.HasSync)
        {
            var bridge = Siteswap.Bridge(inAir.Select(a => a.Beat).ToList(), expects.Select(e => e.Beat).ToList(), Math.Max(prevPattern.Highest, slot.Pattern.Highest));
            if (bridge is not null)
                hint = $" Throw {bridge} between them, for {Beats(bridge.Length * (slot.Seg.Every ?? 0.5))} of the time before: … {previous.Pattern}:… {bridge}:{Beats(bridge.Length * (slot.Seg.Every ?? 0.5))} {slot.Seg.Pattern}:… (a change of pattern often needs a throw or two of its own).";
        }
        Problems.Add(new Problem(slot.Seg, "SH2019", true,
            $"'{slot.Seg.Pattern}' cannot follow '{previous.Pattern}' here: the balls in the air come down on other beats than it throws them (a change works when what is in the air is what the new pattern would have thrown before it).{hint}"));

        // Send them where they are expected, nearest first.
        foreach (var list in arriving.Where(a => a.Key >= s).ToList())
            arriving.Remove(list.Key);
        for (int k = 0; k < inAir.Count && k < expects.Count; k++)
        {
            var (beat, hand) = expects[k];
            int land = s + beat;
            if (!arriving.TryGetValue(land, out var list))
                arriving[land] = list = [];
            list.Add((inAir[k].Ball, hand, inAir[k].Flight));
            if (inAir[k].Flight >= 0)
            {
                var f = Flights[inAir[k].Flight];
                double landSec = Sec(slots[Math.Min(land, slots.Count - 1)].Beat);
                Flights[inAir[k].Flight] = f with { To = hand, Caught = Math.Max(f.Thrown + 0.1, landSec - 0.15) };
            }
        }
    }

    private static string Name(int hand) => hand == 0 ? "right" : "left";
    private static string Fmt(double beats) => Math.Round(beats, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A length as it is written: 2, 1/2, 3/4.</summary>
    private static string Beats(double beats)
    {
        foreach (int d in new[] { 1, 2, 3, 4, 6, 8, 16 })
        {
            double n = beats * d;
            if (Math.Abs(n - Math.Round(n)) < 1e-6)
                return d == 1 ? $"{Math.Round(n)}" : $"{Math.Round(n)}/{d}";
        }
        return Fmt(beats);
    }

    // ------------------------------------------------------------------ the hands

    // The hands' places, in the frame of the juggling: x to the dancer's left, y up, z forward, from a point
    // before the belly (the solver puts it there). A ball is thrown from inside and caught outside, so each
    // hand goes round a small loop: in and down holding a ball, out and up empty.
    private static double Side(int hand) => hand == 0 ? -1 : 1;
    // A ball handed across (a 1) is passed between hands that come no nearer than 24 cm: on a character with
    // narrow shoulders (a child) the figure's spread shrinks, and at 7 cm from the middle the hands met.
    // Thrown from 15 cm off the middle: from 10–12 cm the forearms, their angles put on a child's narrow
    // shoulders, crossed the middle of the body.
    private static Vec3 Ready(int hand) => new(Side(hand) * 0.23, 0, 0);
    // Rings go up and down, the hand an edge (the thumb up): caught high and outside, carried down and in, swung up from
    // low and let go inside (the user: thrown where they were caught, a ring going up met the one coming down).
    private bool Rings => Prop == "rings";
    // Where the hand lets go: for a ring a little below where it caught, after the swing up from low.
    private Vec3 Throws(int hand, int value, bool across) => value == 1
        ? new Vec3(Side(hand) * 0.12, -0.01, 0.02)
        : Rings ? new Vec3(Side(hand) * 0.13, 0.10, 0.02)
        : new Vec3(Side(hand) * (across ? 0.15 : 0.28), 0, 0.01);
    // A throw that comes back to its own hand (an even one: 441, the box (4,2x)*, the fountain) goes up on the hand's own
    // side, the hands well apart (the user: in the box they looked better further out). Thrown 28 cm out, caught 34.
    private Vec3 Catches(int hand, int value, bool across = true) => value == 1
        ? new Vec3(Side(hand) * 0.16, -0.01, 0.02)
        : Rings ? new Vec3(Side(hand) * 0.31, 0.18, 0.02)
        : Clubs ? new Vec3(Side(hand) * (across ? 0.40 : 0.42), 0.08, -0.02)
        : new Vec3(Side(hand) * (across ? 0.32 : 0.34), 0.06, -0.02);
    // A club is half a metre long: caught well out to the side, the elbow out, or two meet in the air (the user).
    private bool Clubs => Prop == "clubs";

    private static readonly Vec3 Still = new(0, 0, 0);
    /// <summary>How far a hand swings up to throw, metres: a ring from low (the arm's whole swing), a ball a short flick.</summary>
    // A ring's swing starts no lower than keeps the elbow bent (the user: from 24 cm below, the arm was straight there).
    private double Lift => Rings ? 0.16 : 0.10;
    /// <summary>Seconds a hand goes on up after it lets go, slowing to a stop.</summary>
    private const double FollowThrough = 0.08;

    /// <summary>
    /// The hands' way for one throw (the user: the arm has to swing up as fast as the ring goes). The thrower sinks to
    /// a stop, swings up and lets go at the speed the ball leaves at — up and toward where it comes down — and goes on
    /// up a little, slowing; the catcher meets the ball going down with it, and slows it. A hand handed one across
    /// (a 1) only passes it.
    /// </summary>
    private void Swing(int hand, int to, int value, double thrown, double caught, double beat,
        List<string>? throwTricks = null, List<string>? catchTricks = null, int nth = 0)
    {
        bool across = to != hand;
        var from = Throws(hand, value, across);
        var into = Catches(to, value, across);
        Style throwStyle = Style.Own, catchStyle = Style.Own;
        if (value >= 2)
            (from, into, throwStyle, catchStyle) = Trick(hand, to, value, from, into, throwTricks, catchTricks, nth);
        double flight = caught - thrown;
        if (value < 2 || flight < 0.05)
        {
            _keys[hand].Add((thrown, from, Still, throwStyle));
            _keys[to].Add((caught, into, Still, catchStyle));
            return;
        }
        // Leaves at the speed that brings it to the catch in its time under gravity.
        var leaves = new Vec3((into.X - from.X) / flight, (into.Y - from.Y) / flight + G * flight / 2, (into.Z - from.Z) / flight);
        var arrives = new Vec3(leaves.X, leaves.Y - G * flight, leaves.Z);
        bool millsThrow = throwTricks?.Contains("mills") == true, millsCatch = catchTricks?.Contains("mills") == true;
        if (millsThrow || millsCatch)
        {
            // Mills' Mess is soft: the hands never stop and never snap, the forearms going slowly round each other (the
            // user: crossed and uncrossed at the speed of the balls it was a body no one has). Only the places; the speed
            // at each is from the places either side, once they are all known (Smooth).
            if (millsThrow)
                AddSmooth(hand, (thrown, from, Still, throwStyle));
            else
            {
                _keys[hand].Add((thrown - Math.Min(0.05, 0.45 * Dwell * beat), from - leaves * Math.Min(0.025, 0.2 * Dwell * beat), Still, throwStyle));
                _keys[hand].Add((thrown, from, leaves, throwStyle));
            }
            if (millsCatch)
                AddSmooth(to, (caught, into, Still, catchStyle));
            else
                _keys[to].Add((caught, into, arrives * 0.3, catchStyle));
            return;
        }
        // From a stop to that speed, evenly faster: over as many seconds as cover the lift, but no more than a part of the hold.
        double speed = Math.Max(0.5, leaves.Length);
        // (A throw every few hundredths of a second, SH2020, leaves less than that: the swing is as short as the hold.)
        double most = Math.Max(0.01, 0.45 * Dwell * beat);
        double swing = Math.Clamp(2 * Lift / speed, Math.Min(0.05, most), most);
        double follow = Math.Min(FollowThrough, 0.35 * (2 - Dwell) * beat);
        _keys[hand].Add((thrown - swing, from - leaves * (swing / 2), Still, throwStyle));
        _keys[hand].Add((thrown, from, leaves, throwStyle));
        _keys[hand].Add((thrown + follow, from + leaves * (follow / 2), Still, throwStyle));
        // The catch gives with the ball, a third of its speed, and the hand stops at the bottom before the next swing.
        _keys[to].Add((caught, into, arrives * 0.3, catchStyle));
    }

    /// <summary>
    /// Where a trick puts a throw and a catch, and how the hand is held there (DESIGN.md 118.12). The throw is the word's
    /// it is thrown in; the catch is the word's the ball is thrown again in (a cascade into a penguin catches by the hip).
    /// </summary>
    private (Vec3 From, Vec3 Into, Style ThrowStyle, Style CatchStyle) Trick(int hand, int to, int value, Vec3 from, Vec3 into,
        List<string>? throwTricks, List<string>? catchTricks, int nth)
    {
        bool Has(List<string>? tricks, string trick) => tricks?.Contains(trick) == true;
        Style throwStyle = Style.Own, catchStyle = Style.Own;
        double s = Side(hand), c = Side(to);

        // Reverse: thrown from outside, over the top, and caught inside — the places of the throw and the catch swapped.
        if (Has(throwTricks, "reverse"))
            from = new Vec3(s * Math.Abs(Catches(hand, value, to != hand).X), from.Y + 0.04, from.Z);
        if (Has(catchTricks, "reverse"))
            into = new Vec3(c * Math.Abs(Throws(to, value, to != hand).X), into.Y, into.Z);
        // Claw: the palm down, the ball snatched from above, high at the top of the loop; thrown with the palm down too.
        if (Has(catchTricks, "claw"))
        {
            into = new Vec3(into.X, into.Y + 0.12, into.Z + 0.04);
            catchStyle = Style.Claw;
        }
        if (Has(throwTricks, "claw"))
            throwStyle = Style.Claw;
        // Penguin: the arm straight down, the hand by the hip, fingers down: caught and thrown from there.
        if (Has(throwTricks, "penguin"))
        {
            from = new Vec3(s * 0.25, -0.30, -0.02);
            throwStyle = Style.Penguin;
        }
        if (Has(catchTricks, "penguin"))
        {
            into = new Vec3(c * 0.27, -0.28, -0.02);
            catchStyle = Style.Penguin;
        }
        // Mills' Mess: every throw is made with the arms crossed, the throwing hand reaching to the other side, over the
        // other arm and then under it, one throw and the next; it uncrosses to catch on its own side. The hands roll round
        // each other (the user: the forearms turn smoothly, the hands going well up and down): crossed over, high and in
        // front, it comes down outside to catch low; crossed under, low and behind, it comes up outside to catch high.
        if (Has(throwTricks, "mills"))
        {
            bool over = nth % 2 == 0;
            from = new Vec3(-s * 0.05, over ? 0.12 : -0.10, over ? 0.08 : -0.06);
        }
        if (Has(catchTricks, "mills"))
        {
            // After crossing over (its last throw) it catches low; after crossing under, high.
            bool cameOver = (_throwsBy[to] - 1) % 2 == 0;
            into = new Vec3(c * 0.22, cameOver ? -0.06 : 0.10, 0.0);
        }
        // Behind the back: from behind the waist, toward the other side, over the other shoulder.
        if (Has(throwTricks, "back"))
        {
            from = new Vec3(-s * 0.06, -0.08, -0.52);
            throwStyle = Style.Back;
        }
        // Under the leg: from under the thigh of its own side (lifted in its row), inside it.
        if (Has(throwTricks, "under"))
        {
            from = new Vec3(s * 0.08, -0.26, -0.06);
            throwStyle = Style.Under;
        }
        return (from, into, throwStyle, catchStyle);
    }

    /// <summary>Places whose speed is not the ball's but the hand's own way through them: a trick that goes round softly (Mills).</summary>
    private readonly HashSet<(int Hand, double T)> _smooth = [];

    private void AddSmooth(int hand, (double T, Vec3 At, Vec3 Velocity, Style Style) key)
    {
        _keys[hand].Add(key);
        _smooth.Add((hand, key.T));
    }

    /// <summary>
    /// The speed at each soft place, once a hand's places are in order: the way from the place before to the place after,
    /// a little slower (a Catmull-Rom curve), so the hand goes round through them without stopping.
    /// </summary>
    private void Soften(int hand)
    {
        var keys = _keys[hand];
        for (int i = 1; i + 1 < keys.Count; i++)
        {
            if (!_smooth.Contains((hand, keys[i].T)))
                continue;
            var (t0, a, _, _) = keys[i - 1];
            var (t2, b, _, _) = keys[i + 1];
            if (t2 - t0 < 1e-6)
                continue;
            keys[i] = keys[i] with { Velocity = (b - a) * (0.85 / (t2 - t0)) };
        }
    }

    /// <summary>The juggling's Mills' Mess at a time (seconds), 0..1: how much the body goes with the crossing arms.</summary>
    private readonly List<(double From, double To)> _mills = [];

    /// <summary>
    /// How far the shoulders turn with the arms (radians, to the dancer's left), where the shoulders' row says nothing: in
    /// Mills' Mess the body goes with the arm that crosses (the user: the whole body soft). The hand reaching across brings
    /// its shoulder forward.
    /// </summary>
    public double ShoulderTurn(double sec)
    {
        double w = 0;
        foreach (var (from, to) in _mills)
            w = Math.Max(w, sec >= from && sec <= to ? 1 : sec < from ? Smooth(1 - (from - sec) / 0.4) : Smooth(1 - (sec - to) / 0.4));
        return w <= 0 ? 0 : w * 0.45 * (Hand(0, sec).X + Hand(1, sec).X);
    }

    /// <summary>How a hand is held at a time (seconds): as at the nearer of its places either side.</summary>
    public Style StyleAt(int hand, double sec)
    {
        var keys = _keys[hand];
        if (keys.Count == 0)
            return Style.Own;
        for (int i = 0; i + 1 < keys.Count; i++)
        {
            if (sec > keys[i + 1].T)
                continue;
            return sec - keys[i].T < keys[i + 1].T - sec ? keys[i].Style : keys[i + 1].Style;
        }
        return keys[^1].Style;
    }

    /// <summary>Where a hand is at a time (seconds), in the juggling's frame: through its places at their speeds.</summary>
    public Vec3 Hand(int hand, double sec)
    {
        var keys = _keys[hand];
        if (keys.Count == 0)
            return Ready(hand);
        if (sec <= keys[0].T)
            return keys[0].At;
        for (int i = 0; i + 1 < keys.Count; i++)
        {
            var (t0, a, va, _) = keys[i];
            var (t1, b, vb, _) = keys[i + 1];
            if (sec > t1)
                continue;
            double span = t1 - t0;
            if (span < 1e-6)
                return b;
            // Cubic Hermite: at each place with its speed.
            double u = (sec - t0) / span, u2 = u * u, u3 = u2 * u;
            return a * (2 * u3 - 3 * u2 + 1) + va * ((u3 - 2 * u2 + u) * span) + b * (-2 * u3 + 3 * u2) + vb * ((u3 - u2) * span);
        }
        return keys[^1].At;
    }

    /// <summary>How much the juggling has the arms at a time (seconds): 0 the rows, 1 the juggling.</summary>
    public double Weight(double sec)
    {
        double w = 0;
        foreach (var (from, to) in Runs)
        {
            double a = from - Ramp, b = to + 0.2;
            double here = sec >= a && sec <= b ? 1
                : sec < a ? Smooth(1 - (a - sec) / Ramp)
                : Smooth(1 - (sec - b) / Ramp);
            w = Math.Max(w, here);
        }
        return w;
    }

    private static double Smooth(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }

    // ------------------------------------------------------------------ the balls

    /// <summary>
    /// Where a ball is at a time (seconds), given where the hands' palms are: in a hand (From = To, U = 0), or in
    /// the air from one hand's palm at the throw to the other's at the catch (U through the flight), on the
    /// parabola that takes that time under gravity. Spin: turns of a club, in radians.
    /// </summary>
    public (Vec3 At, int From, int To, double U, double Spin) Ball(int ball, double sec, Func<int, double, Vec3> palm, Func<int, double, int> stacked)
    {
        Flight? last = null;
        foreach (var f in Flights)
        {
            if (f.Ball != ball || f.Thrown > sec)
                continue;
            if (sec <= f.Caught)
            {
                double tf = f.Caught - f.Thrown, s = sec - f.Thrown;
                var p0 = palm(f.From, f.Thrown);
                var p1 = palm(f.To, f.Caught);
                double u = tf < 1e-6 ? 1 : s / tf;
                var at = p0 + (p1 - p0) * u + new Vec3(0, G / 2 * s * (tf - s), 0);
                int spins = f.Value >= 3 ? Math.Max(1, (f.Value - 1) / 2) : 0;
                return (at, f.From, f.To, u, 2 * Math.PI * spins * u);
            }
            if (last is null || f.Caught > last.Caught)
                last = f;
        }
        int hand = last?.To ?? (ball < _startHand.Count ? _startHand[ball] : ball % 2);
        var held = palm(hand, sec) + new Vec3(0, 0.055 * stacked(ball, sec), 0);
        return (held, hand, hand, 0, 0);
    }

    /// <summary>The hand a ball is in at a time, or -1 in the air.</summary>
    public int HandOf(int ball, double sec)
    {
        Flight? last = null;
        foreach (var f in Flights)
        {
            if (f.Ball != ball || f.Thrown > sec)
                continue;
            if (sec <= f.Caught)
                return -1;
            if (last is null || f.Caught > last.Caught)
                last = f;
        }
        return last?.To ?? (ball < _startHand.Count ? _startHand[ball] : ball % 2);
    }

    /// <summary>The highest any ball goes above the hand it leaves, metres, and the word it is thrown in.</summary>
    public IEnumerable<(double Rise, double Thrown)> Rises() =>
        Flights.Select(f => (G * Math.Pow(f.Caught - f.Thrown, 2) / 8, f.Thrown));
}
