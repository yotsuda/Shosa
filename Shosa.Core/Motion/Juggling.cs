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
    /// <param name="Mills">Thrown and caught in Mills' Mess: its balls are all in one plane before the body.</param>
    /// <param name="MillsThird">In Mills' Mess, which of each three throws it is (0 the first, over the top; 1 the second; 2 the third, from under), else -1.</param>
    public sealed record Flight(int Ball, int From, int To, double Thrown, double Caught, int Value, string? Trick = null, Seg? Seg = null, bool Mills = false, int MillsThird = -1);

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
            double slotThrown = SlotSec(s), T = BeatSec(s);
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
                    double caught = Math.Max(SlotSec(land) - Dwell * BeatSec(Math.Min(land, slots.Count - 1)), slotThrown + Handover * T);
                    var landingSeg = slots[Math.Min(land, slots.Count - 1)].Seg;
                    // A claw catch (the user): thrown up to the face and snatched from above there at its top. Asked for before
                    // its top: the catching hand can be at the face no sooner than it has thrown its own ball (two throws before
                    // this one lands) and come up (0.18 s), and has to bring the ball down again for its next throw, and for
                    // the hands of a quick juggler that leaves no tempo at which the ball is still rising at the face (DESIGN.md
                    // 118.14). So it is caught when the hand gets there, at the face, near its top, and a tempo at which it
                    // falls further first, or the hand is hurried, is said (SH2023).
                    if (landingSeg?.Tricks?.Contains("claw") == true && t.Value >= 3 && land - 2 > s)
                    {
                        double reach = (ClawFace - Throws(to, 3, true).Y) / ClawHandSpeed;
                        double free = SlotSec(land - 2) + reach;
                        // And it has to bring the ball down from the face to throw it again on its turn.
                        double latest = SlotSec(land) - ClawCarry;
                        caught = Math.Max(Math.Min(free, latest), slotThrown + Handover * T);
                        double rise = ClawFace - Throws(hand, t.Value, to != hand).Y, air = free - slotThrown;
                        // The longest it can be in the air to fall no more than ClawDrop from its top to the face.
                        double longest = Math.Sqrt(2 * (rise + ClawDrop) / G) + Math.Sqrt(2 * ClawDrop / G);
                        double between = SlotSec(land - 2) - slotThrown, round = SlotSec(land) - SlotSec(land - 2);
                        // The tempo it suits, as parts of this one: quicker for the ball not to fall far, slower for the hand to
                        // get up and back down between its own two throws.
                        double quickest = round / (reach + ClawCarry), slowest = between / Math.Max(1e-3, longest - reach);
                        bool hurried = free > latest + 0.02;
                        if (air > longest || hurried)
                            ClawTiming.Add((landingSeg, air, longest, reach, slowest, quickest, hurried));
                    }
                    double thrown = slotThrown;
                    // Mills' second throw (and its mirror, the fifth) goes higher (the user): longer in the air, thrown a
                    // little before the beat.
                    // Thrown and caught round the body (Mills' Mess or the windmill, the same one at both ends): its ball in
                    // the plane before the body, the hands soft.
                    bool windmill = Windmill(slot.Seg.Tricks);
                    bool millsFlight = Round(slot.Seg.Tricks) && Round(landingSeg?.Tricks) && windmill == Windmill(landingSeg?.Tricks);
                    if (millsFlight && !windmill && slot.Index % 3 == 1)
                        thrown -= MillsSecondEarlier * T;
                    // The first (and the fourth) as high as the second (the user): a little before its beat too, but not the
                    // word's very first throw, which the hands come in to from what the arms did.
                    if (millsFlight && !windmill && slot.Index % 3 == 0 && slot.Index > 0)
                        thrown -= MillsFirstEarlier * T;
                    Flights.Add(new Flight(ball, hand, to, thrown, caught, t.Value,
                        slot.Seg.Tricks?.FirstOrDefault(x => x is "under" or "back"), slot.Seg, millsFlight, millsFlight && !windmill ? slot.Index % 3 : -1));
                    Arrive(land, ball, to, Flights.Count - 1);
                    var landing = slots[Math.Min(land, slots.Count - 1)];
                    var catcher = landing.Seg;   // none: caught for good at the end of the run, as a plain catch (not Mills' middle)
                    // Which of Mills' six throws this is, and which the ball is caught for: counted in its word, mirrored
                    // when the word's first throw is the left hand's.
                    bool Mirrored(Slot x) => x.Seg is not null && Siteswap.HandAt(x.InRun - x.Index, x.Seg.LeftFirst) == 1;
                    _mills6 = (slot.Index % 6, Mirrored(slot), landing.Seg is null ? (slot.Index + t.Value) % 6 : landing.Index % 6,
                        landing.Seg is null ? Mirrored(slot) : Mirrored(landing), windmill);
                    Swing(hand, to, t.Value, thrown, caught, T, slot.Seg.Tricks, catcher?.Tricks, _throwsBy[hand]++);
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
        // Before a claw catch, no stop on the hand's way up (the top of its own throw's follow-through: see Swing).
        foreach (var (clawHand, clawAt) in _clawCatches)
            _keys[clawHand].RemoveAll(key => key.T > clawAt - ClawWindow && key.T < clawAt - ClawOverBefore - 1e-6 && key.Velocity.Length < 1e-9 && !_smooth.Contains((clawHand, key.T)));
        _clawCatches.Clear();
        foreach (var hand in new[] { 0, 1 })
        {
            _keys[hand].Add((lastCatch + 0.2, Ready(hand), Still, Style.Own));
            _keys[hand].Sort((a, b) => a.T.CompareTo(b.T));
            Soften(hand);
        }
        foreach (var (seg, _) in run.Where(r => Round(r.Seg.Tricks)))
        {
            _mills.Add((Sec(seg.T0), Sec(seg.T1)));
            // The back sways with Mills' six throws round; the windmill's top arm is always the same one.
            if (Windmill(seg.Tricks))
                continue;
            var firstSlot = slots.First(x => ReferenceEquals(x.Seg, seg));
            double beatSec = Sec(seg.T0 + (seg.Every ?? 0.5)) - Sec(seg.T0);
            _millsSway.Add((Sec(seg.T0), Sec(seg.T1), beatSec, Siteswap.HandAt(firstSlot.InRun, seg.LeftFirst) == 1));
        }
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
        : new Vec3(Side(hand) * (across ? 0.15 : SameThrowX), 0, across ? 0.01 : SameThrowZ);
    // A throw that comes back to its own hand (an even one: 441, the box (4,2x)*, the fountain) goes up on the hand's own
    // side, the hands well apart (the user: in the box they looked better further out). Thrown 28 cm out, caught 34.
    private Vec3 Catches(int hand, int value, bool across = true) => value == 1
        ? new Vec3(Side(hand) * 0.16, -0.01, 0.02)
        : Rings ? new Vec3(Side(hand) * 0.31, 0.18, 0.02)
        : Clubs ? new Vec3(Side(hand) * (across ? 0.40 : 0.42), 0.08, -0.02)
        : new Vec3(Side(hand) * (across ? 0.32 : SameCatchX), 0.06, across ? -0.02 : SameCatchZ);
    // A ball thrown back to its own hand (441, the box): thrown in front and inside, caught behind and outside, so the one going
    // up and the one coming down in the same hand's column pass apart, and the one going up is in front of a ball crossing over
    // into that hand (the user: in 441 a ball going up met one coming down; thrown at 28 cm and caught at 34 they passed 4 cm apart).
    private const double SameThrowX = 0.34, SameThrowZ = 0.08, SameCatchX = 0.42, SameCatchZ = -0.06;
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
        // Mills' Mess and the windmill go round the body the same soft way, each from its own table of places.
        bool millsThrow = Round(throwTricks), millsCatch = Round(catchTricks);
        // The throw over the top, the hand going round high after it, and its ball met high and followed down: in the windmill
        // the top hand's (the first).
        bool Top(int k) => _mills6.Windmill ? k % 2 == 0 : k % 3 == 0;
        if (millsThrow || millsCatch)
        {
            // Mills' Mess is soft: the hands never stop and never snap, the forearms going slowly round each other (the
            // user: crossed and uncrossed at the speed of the balls it was a body no one has). Only the places; the speed
            // at each is from the places either side, once they are all known (Smooth).
            if (millsThrow)
            {
                // Each hand goes round a loop on the plane before the body, once a throw (the user: circles, never turning
                // off at a sudden angle): caught high near the middle going in and down with the ball, round the bottom,
                // out and up to the throw, and on up and in with the ball to the next catch.
                var caughtAt = MillsPlace(_mills6.ThrowK, _mills6.ThrowMirror, _mills6.Windmill).Catch;
                var bottom = new Vec3((from.X + caughtAt.X) / 2, Math.Min(from.Y, caughtAt.Y) - MillsLoopDepth, from.Z);
                AddSmooth(hand, (thrown - MillsBottomAt * Dwell * beat, bottom, Still, throwStyle));
                AddSmooth(hand, (thrown, from, Still, throwStyle));
                // Up and across with the ball.
                _launch[(hand, thrown)] = leaves.Y * MillsLaunch;
                _launchAcross[(hand, thrown)] = leaves.X * MillsLaunch;
                // The first throw, over the top: the hand goes on up after the ball and comes round high, in an arc, to
                // the next catch near the middle (the user: it dropped at once).
                if (Top(_mills6.ThrowK))
                {
                    var next = MillsPlace(_mills6.ThrowK + 2, _mills6.ThrowMirror, _mills6.Windmill).Catch;
                    double gap = (2 - Dwell) * beat;
                    var way = Vec3.Lerp(from, next, MillsOverAcross);
                    var over = new Vec3(way.X, Math.Max(from.Y, next.Y) + (_mills6.Windmill ? WindmillOverRise : MillsOverRise), way.Z);
                    AddSmooth(hand, (thrown + MillsOverAt * gap, over, Still, throwStyle));
                }
            }
            else
            {
                _keys[hand].Add((thrown - Math.Min(0.05, 0.45 * Dwell * beat), from - leaves * Math.Min(0.025, 0.2 * Dwell * beat), Still, throwStyle));
                _keys[hand].Add((thrown, from, leaves, throwStyle));
            }
            if (millsCatch)
            {
                // Caught with the hand going the way the ball comes down, not stopping it dead (the user).
                AddSmooth(to, (caught, into, Still, catchStyle));
                _arrive[(to, caught)] = arrives * (Top(_mills6.CatchK) ? MillsArriveFirst : MillsArrive);
                // The first throw's ball: the hand meets it high, coming down with it a little before, and goes on down with
                // it well after, from high to low as it falls (the user).
                if (Top(_mills6.CatchK))
                {
                    AddSmooth(to, (caught - MillsMeetBefore, into - arrives * (MillsMeetBefore * MillsArriveFirst), Still, catchStyle));
                    AddSmooth(to, (caught + MillsGiveAfter, into + arrives * (MillsGiveAfter * MillsArriveFirst * 0.7), Still, catchStyle));
                }
            }
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
        if (catchStyle == Style.Claw)
        {
            // A claw comes down on the ball from above (the user): up a little over where it meets it, then down onto it
            // as it comes up, and on down with it. (Coming up to it and going down with it, the hand turned right round at
            // the catch.) The hand goes on up from its own throw without stopping at the top of its follow-through: it
            // has only a moment to get up to the face (stopped there first, it jumped up 0.5 m in a frame).
            _clawCatches.Add((to, caught));
            AddSmooth(to, (caught - ClawOverBefore, into + new Vec3(0, ClawOver, 0), Still, catchStyle));
            _keys[to].Add((caught, into, new Vec3(arrives.X * 0.3, -ClawSnatch, arrives.Z * 0.3), catchStyle));
            return;
        }
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
        // Claw: the palm down, the ball snatched from above at the face on its way up (the user); thrown with the palm down too.
        if (Has(catchTricks, "claw"))
        {
            into = new Vec3(into.X, ClawFace, into.Z + 0.04);
            catchStyle = Style.Claw;
        }
        if (Has(throwTricks, "claw"))
            throwStyle = Style.Claw;
        // Penguin: the arm straight down, the hand by the hip, fingers down: caught and thrown from there.
        // Penguin: the arm straight down by the side and turned in at the shoulder, the palm turned right round to face back,
        // the ball caught behind the hip (the user); then the arm swings forward past the thigh and throws it up across the
        // front (from behind the hip it went through the body).
        if (Has(throwTricks, "penguin"))
        {
            from = new Vec3(s * 0.20, -0.22, 0.05);
            throwStyle = Style.Penguin;
        }
        if (Has(catchTricks, "penguin"))
        {
            into = new Vec3(c * 0.30, -0.26, -0.30);   // outside the hip: coming down nearer in, it grazed the body
            catchStyle = Style.Penguin;
        }
        // Mills' Mess, as the user laid it out: six throws round, three from the left and three, mirrored, from the right.
        // Arms crossed, the right hand over, on the left, throws from outside in; the arms uncrossing, the left hand throws;
        // the arms crossing the other way, the right hand under, low on the left, throws in. The three go up near one another
        // from left to right; then the same mirrored. Caught a little further out than thrown, and carried in to the throw.
        if (Round(throwTricks))
            from = MillsPlace(_mills6.ThrowK, _mills6.ThrowMirror, Windmill(throwTricks)).Throw;
        if (Round(catchTricks))
            into = MillsPlace(_mills6.CatchK, _mills6.CatchMirror, Windmill(catchTricks)).Catch;
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
    /// <summary>A soft throw's upward speed: the ball's as it leaves the hand, which the hand has there too.</summary>
    private readonly Dictionary<(int Hand, double T), double> _launch = [];
    /// <summary>Mills' throws: the hand goes across with the ball as well as up, on round its loop toward the middle (the user: circles).</summary>
    private readonly Dictionary<(int Hand, double T), double> _launchAcross = [];
    /// <summary>A soft catch that goes with the ball: the hand's speed there, along the ball's way down.</summary>
    private readonly Dictionary<(int Hand, double T), Vec3> _arrive = [];

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
            var tangent = (b - a) * (0.85 / (t2 - t0));
            // At a throw the hand goes up as fast as the ball leaves it (the user: the ball flew off a hand that stayed);
            // across and forward the way round goes on as before.
            if (_launch.TryGetValue((hand, keys[i].T), out double up))
                tangent = new Vec3(_launchAcross.TryGetValue((hand, keys[i].T), out double across) ? across : tangent.X, up, tangent.Z);
            if (_arrive.TryGetValue((hand, keys[i].T), out var moving))
                tangent = moving;
            keys[i] = keys[i] with { Velocity = tangent };
        }
    }

    /// <summary>The juggling's Mills' Mess at a time (seconds), 0..1: how much the body goes with the crossing arms.</summary>
    private readonly List<(double From, double To)> _mills = [];
    /// <summary>Each Mills word for the back's sway: when, seconds from one throw to the next, and whether its first throw is the left hand's.</summary>
    private readonly List<(double From, double To, double Beat, bool Mirrored)> _millsSway = [];

    /// <summary>
    /// How far the shoulders turn with the arms (radians, to the dancer's left), where the shoulders' row says nothing: in
    /// Mills' Mess the body goes with the arm that crosses (the user: the whole body soft). The hand reaching across brings
    /// its shoulder forward.
    /// </summary>
    public double ShoulderTurn(double sec)
    {
        double w = MillsWeight(sec);
        return w <= 0 ? 0 : w * 0.45 * (Hand(0, sec).X + Hand(1, sec).X);
    }

    /// <summary>
    /// How far the back leans to the side (the torso's sideways part, to the dancer's left), where the torso's row says
    /// nothing: in Mills' Mess the back sways away from the arm that crosses on top, its shoulder up, so the arm below has
    /// room under it (the user: the back swaying left and right, for the height between the two at the throw).
    /// </summary>
    public double SideLean(double sec)
    {
        // A slow, even sway, once each way in the six throws (the user: right, left, right, left, smoothly; following the
        // hands it leaned twice to each side, rattling): furthest to the side of the arm below at the throw from below
        // (the third and the sixth), the shoulder of the arm on top up, so the throw has room under it.
        double w = MillsWeight(sec);
        if (w <= 0)
            return 0;
        foreach (var (from, to, beat, mirrored) in _millsSway)
        {
            if (sec < from - 0.4 || sec > to + 0.4)
                continue;
            double phase = (sec - from) / (6 * beat);
            double lean = -MillsLean * Math.Cos(2 * Math.PI * (phase - 2.0 / 6));
            return w * (mirrored ? -lean : lean);
        }
        return 0;
    }

    /// <summary>How far Mills' back leans at the most (the torso's sideways part): some 10 degrees (17 was too much: the user).</summary>
    private const double MillsLean = 0.18;

    /// <summary>
    /// How much a hand holds a ball around a time (seconds), 0..1: 1 holding (the catch and the throw included), and on the
    /// empty side less as it is further in time from holding, to 0 at 0.3 s, so what is done only to a hand that holds
    /// comes and goes smoothly. (Sampled at a few times either side, it jumped in the short gap between a throw and the
    /// next catch, and the elbow jumped with it: the user.)
    /// </summary>
    public double HoldShare(int hand, double sec)
    {
        _holding ??= Holdings();
        double nearest = double.PositiveInfinity;
        foreach (var (from, to) in _holding[hand])
        {
            if (sec >= from && sec <= to)
                return 1;
            nearest = Math.Min(nearest, sec < from ? from - sec : sec - to);
        }
        return Smooth(1 - nearest / HoldRamp);
    }

    private const double HoldRamp = 0.3;
    private List<(double From, double To)>[]? _holding;

    /// <summary>Each hand's stretches of holding a ball, seconds: from a catch (or the start) to that ball's next throw.</summary>
    private List<(double From, double To)>[] Holdings()
    {
        var held = new List<(double, double)>[] { [], [] };
        for (int b = 0; b < Balls; b++)
        {
            var mine = Flights.Where(f => f.Ball == b).OrderBy(f => f.Thrown).ToList();
            double since = double.NegativeInfinity;
            int hand = b < _startHand.Count ? _startHand[b] : b % 2;
            foreach (var f in mine)
            {
                held[hand].Add((since, f.Thrown));
                (since, hand) = (f.Caught, f.To);
            }
            held[hand].Add((since, double.PositiveInfinity));
        }
        return held;
    }

    /// <summary>How much Mills' Mess has the juggling at a time (seconds), 0..1.</summary>
    public double MillsAt(double sec) => MillsWeight(sec);

    private double MillsWeight(double sec)
    {
        double w = 0;
        foreach (var (from, to) in _mills)
            w = Math.Max(w, sec >= from && sec <= to ? 1 : sec < from ? Smooth(1 - (from - sec) / 0.4) : Smooth(1 - (sec - to) / 0.4));
        return w;
    }

    /// <summary>The throw being planned in Mills' Mess (or the windmill): which of the six (two) it is, and which the ball is caught for.</summary>
    private (int ThrowK, bool ThrowMirror, int CatchK, bool CatchMirror, bool Windmill) _mills6;

    /// <summary>Thrown round the body: Mills' Mess or the windmill.</summary>
    private static bool Round(List<string>? tricks) => tricks is not null && (tricks.Contains("mills") || tricks.Contains("windmill"));
    private static bool Windmill(List<string>? tricks) => tricks?.Contains("windmill") == true;

    /// <summary>
    /// Where the hand throws and catches on each of Mills' six throws (x to the dancer's left, as written for a word whose
    /// first throw is the right hand's): 0 the right hand over, on the left; 1 the left hand on its own side, the arms
    /// uncrossing; 2 the right hand under, low on the left; 3–5 the same mirrored.
    /// </summary>
    private static readonly (Vec3 Throw, Vec3 Catch)[] MillsLeft =
    [
        // Close in before the body: Mills is a small rolling of the forearms (caught 30 cm out, a short-armed character's
        // arm went out straight).
        // All three thrown from the same place out on the left, along one way, and caught near the middle of the body (the
        // user: the three on one path, thrown from outside, caught near the middle). The wrists differ so the balls,
        // which lie past the wrist along the forearm, leave from one place: a crossed forearm points out to the left.
        // Crossing, the arm on top goes well up and in front and the one below well down and behind, so the forearms turn
        // round each other and do not go through each other (the user); the third throw is from below the crossed arms.
        // All three balls in one plane a little before the body, the same distance out (the user: seen from above, the
        // three at different distances from the body). The arms keep apart up and down, and the back leans.
        // Each catch is higher than the hand's throw before it, so the hand goes on up and in with that ball to the catch
        // and round a loop, not down at a sudden angle (the user: circles).
        (new Vec3(0.29, 0.12, 0.03), new Vec3(0.03, 0.25, 0.03)),   // the first: thrown further out, caught high (the user)
        // The second: thrown well out, a little after the first and up its way, chasing it (the user); caught low under the
        // arm on top (higher, the forearms met) and past the middle, clear of the third coming up.
        (new Vec3(0.40, 0.12, 0.03), new Vec3(0.10, -0.02, 0.03)),
        // The third: thrown as far out as the arm reaches across, bent (4 cm further it went straight). Its ball is caught
        // well out on the hand's own side: the hand that threw the first swings its forearm out round the body (the elbow
        // kept in, Solver) and comes back under the other arm, the arms not seeming to pass through each other (the user).
        (new Vec3(0.30, -0.08, 0.03), new Vec3(-0.40, 0.12, 0.03)),
    ];

    /// <summary>How far below the lower of its throw and catch each Mills loop goes round, metres.</summary>
    private const double MillsLoopDepth = 0.08;
    /// <summary>How much before its beat Mills' first throw leaves the hand, as a part of a throwing beat: as high as the second.</summary>
    private const double MillsFirstEarlier = 0.25;
    /// <summary>After Mills' first throw, how far above the higher of the throw and the next catch the hand comes round, metres.</summary>
    private const double MillsOverRise = 0.25;
    /// <summary>After the windmill's top hand throws, how far above the higher of the throw and its next catch it comes round, metres.</summary>
    private const double WindmillOverRise = 0.12;
    /// <summary>When the top of that arc is, as a part of the time from the throw to the next catch.</summary>
    private const double MillsOverAt = 0.45;
    /// <summary>Where the top of that arc is, from the throw (0) to the next catch (1).</summary>
    private const double MillsOverAcross = 0.4;
    /// <summary>How much before its beat Mills' second throw leaves the hand, as a part of a throwing beat: longer in the air,
    /// so higher (caught later instead, the hand under the crossed arms waited up against the arm on top).</summary>
    private const double MillsSecondEarlier = 0.6;
    /// <summary>When the bottom of the loop is, as a part of the hold before the throw.</summary>
    private const double MillsBottomAt = 0.4;
    /// <summary>How fast the hand goes with the ball at a Mills throw, as a part of the ball's speed.</summary>
    private const double MillsLaunch = 0.8;
    /// <summary>How fast the hand goes with the ball at a Mills catch, as a part of the ball's speed.</summary>
    private const double MillsArrive = 0.6;
    /// <summary>The first throw's ball is caught high and the hand goes down with it nearly as fast (the user: from high to low as it falls).</summary>
    private const double MillsArriveFirst = 0.9;
    /// <summary>How long before the first throw's catch the hand is already coming down with the ball, seconds.</summary>
    private const double MillsMeetBefore = 0.09;
    /// <summary>How long after the first throw's catch the hand goes on down with the ball, seconds.</summary>
    private const double MillsGiveAfter = 0.14;

    /// <summary>
    /// The windmill's two throws round (x to the dancer's left, as written for a word whose first throw is the right hand's):
    /// every ball thrown on the right and caught on the left, all one way round, the underarm throws on the same path as
    /// the ones over the top (libraryofjuggling.com, "Windmill"; twjc.jugglingedge.com tutorial 48, "Spiral"). Both hands go
    /// to and fro, the right arm always the one on top. It is Mills' Mess one way round only (the user): of Mills' six
    /// throws, the hand on its own side and the other hand from under the arm on top, again and again, never mirrored —
    /// here Mills' second and third mirrored, so the right arm is the one on top. The right hand throws from its own side and
    /// catches high near the middle, crossed over (as Mills' first ball is caught); the left hand reaches under the right
    /// arm, throws from low on the right, and catches out on its own side (as Mills' third ball is caught), its forearm
    /// swinging round. The hands go round Mills' soft loops. (Each hand on its own half, the top hand to and fro over the top
    /// and the bottom one under, turned sharply at the ends; both round one circle, the forearms went through each other.)
    /// The top hand catches higher than Mills' 25 cm: every ball goes one way on one path, and the top hand's own ball was
    /// coming up through the middle just as the one from under the arm came down there into the hand (7.7 cm apart); lower,
    /// to the left or a little earlier the forearms met or the balls still did.
    /// </summary>
    private static readonly (Vec3 Throw, Vec3 Catch)[] WindmillRight =
    [
        (new Vec3(-0.40, 0.12, 0.03), new Vec3(0.03, 0.38, 0.03)),
        (new Vec3(-0.30, -0.08, 0.03), new Vec3(0.40, 0.12, 0.03)),
    ];

    /// <summary>Where a claw catches, metres above the juggling's middle (before the belly): about the face.</summary>
    private const double ClawFace = 0.55;
    /// <summary>
    /// How fast a hand comes up from its own throw to the face to claw the next ball, on the average, m/s (a quick juggler's):
    /// some 0.2 s for the 0.6 m. (At 0.12 s it went up 0.7 m in 0.1 s, 7 m/s: it seemed to jump there.)
    /// </summary>
    private const double ClawHandSpeed = 3.0;
    /// <summary>How far over the catch a claw comes up before it comes down on the ball (metres), how long before (seconds), and how fast it comes down at the catch (m/s).</summary>
    private const double ClawOver = 0.04, ClawOverBefore = 0.04, ClawSnatch = 0.6;
    /// <summary>Seconds before a claw catch within which the hand's stops are taken out (the top of its own throw's follow-through).</summary>
    private const double ClawWindow = 0.25;
    /// <summary>The claw catches of the run being planned: the hand, seconds.</summary>
    private readonly List<(int Hand, double At)> _clawCatches = [];
    /// <summary>Seconds a hand takes to bring a clawed ball down from the face to throw it again.</summary>
    private const double ClawCarry = 0.12;
    /// <summary>How far a clawed ball may fall from its top before it is caught at the face, metres: further is SH2023.</summary>
    private const double ClawDrop = 0.05;
    /// <summary>
    /// Claw catches the tempo does not suit (SH2023): the word; seconds from the throw until the catching hand can be up at
    /// the face, and the most that can be for the ball to fall no more than ClawDrop from its top; seconds the hand takes to
    /// come up; the tempo it suits, as parts of this one (at least, at most); and whether the hand cannot get up and back in time.
    /// </summary>
    public List<(Seg Seg, double Air, double Most, double Reach, double Slowest, double Quickest, bool TooQuick)> ClawTiming { get; } = [];

    private static (Vec3 Throw, Vec3 Catch) MillsPlace(int k, bool mirrored, bool windmill = false)
    {
        if (windmill)
        {
            var (wt, wc) = WindmillRight[((k % 2) + 2) % 2];
            return mirrored ? (new Vec3(-wt.X, wt.Y, wt.Z), new Vec3(-wc.X, wc.Y, wc.Z)) : (wt, wc);
        }
        int at = ((k % 6) + 6) % 6;
        var (t, c) = MillsLeft[at % 3];
        bool right = (at >= 3) ^ mirrored;
        Vec3 Flip(Vec3 v) => right ? new Vec3(-v.X, v.Y, v.Z) : v;
        return (Flip(t), Flip(c));
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

    /// <summary>
    /// Where a hand is at a time (seconds), in the juggling's frame. In Mills' Mess the way through the places is softened
    /// over a short window (a Gaussian, <see cref="MillsSoften"/> seconds), so each hand goes round in a smooth loop on
    /// the plane before the body and never turns off at a sudden angle (the user).
    /// </summary>
    public Vec3 Hand(int hand, double sec)
    {
        double w = MillsWeight(sec);
        if (w <= 0)
            return RawHand(hand, sec);
        Vec3 sum = new(0, 0, 0);
        double total = 0;
        for (int i = -12; i <= 12; i++)
        {
            double tau = i * MillsSoften / 4, k = Math.Exp(-0.5 * (i / 4.0) * (i / 4.0));
            sum += RawHand(hand, sec + tau) * k;
            total += k;
        }
        return RawHand(hand, sec) * (1 - w) + sum * (w / total);
    }

    /// <summary>How long Mills' hands are softened over (the Gaussian's spread), seconds.</summary>
    private const double MillsSoften = 0.065;

    /// <summary>Where a hand is at a time (seconds), in the juggling's frame: through its places at their speeds.</summary>
    private Vec3 RawHand(int hand, double sec)
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
