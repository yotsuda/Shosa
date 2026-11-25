namespace Shosa.Core.Semantics;

/// <summary>
/// What a file binds to: a clip for each `score` that was bound. One score is one movement,
/// and one file written.
/// </summary>
public sealed class ScoreIr
{
    /// <summary>The score that was asked for; "" for the unnamed one, or when every score was.</summary>
    public string Name { get; set; } = "";
    /// <summary>The names of every score the file has, as written ("" for the unnamed one).</summary>
    public List<string> Scores { get; } = [];
    public List<MotionIr> Clips { get; } = [];
    /// <summary>Every `move` of the file and the beats it takes when it is written without a length. For an editor to say.</summary>
    public Dictionary<string, double> MoveLengths { get; } = [];
    /// <summary>Every `pose` of the file (and of the files its `using` lines bring in).</summary>
    public List<string> Poses { get; } = [];
}

/// <summary>
/// The intermediate representation of one clip: what each part of each dancer must do, and when.
/// The language sits in front of it and the solver behind it, so the surface syntax can
/// change freely as long as it still binds to this. It is plain data and serialises to JSON
/// (`shosa ir`), which is also how a solver experiment is fed without a parser.
///
/// Definitions (`pose`, `move`), overlapping tracks and `&` are gone by here: every channel is
/// one row of segments. A segment may begin before the one in front of it has arrived (a
/// narrower track took the part over); the part then goes on from wherever it had got to.
///
/// Conventions. Time is in beats from the start of the clip. Space is in metres in the
/// dancer's own frame: x to its left, y up, z the way it faces (right-handed).
/// Every segment remembers the source span it came from: that is what lets the preview light
/// up the word being performed, and a click in the preview find its word.
/// </summary>
public sealed class MotionIr
{
    /// <summary>The name of the score this is the movement of ("" for the unnamed one).</summary>
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Beats per minute at the start.</summary>
    public double Tempo { get; set; } = 90;
    /// <summary>The time at the start.</summary>
    public int BeatsPerBar { get; set; } = 4;
    public int BeatUnit { get; set; } = 4;
    /// <summary>
    /// Every tempo of the clip, the first at beat 0. More than one only when the song it is
    /// danced to changes tempo on the way. Empty (an IR written by hand) means <see cref="Tempo"/> throughout.
    /// </summary>
    public List<TempoIr> Tempos { get; set; } = [];
    /// <summary>Every time of the clip, the first at beat 0; empty means the one above throughout.</summary>
    public List<MeterIr> Meters { get; set; } = [];
    public int Fps { get; set; } = 30;
    public double TotalBeats { get; set; }
    /// <summary>The end joins the start: solved as if the clip followed itself.</summary>
    public bool Loop { get; set; }
    /// <summary>The travel over the floor is taken out of the output and reported as a velocity.</summary>
    public bool Inplace { get; set; }
    /// <summary>The song this is danced to, and how it is to sound beside the dance.</summary>
    public MusicIr? Music { get; set; }
    public List<DancerIr> Dancers { get; } = [];
    public List<SectionIr> Sections { get; } = [];
    public List<MarkIr> Marks { get; } = [];
}

/// <summary>A body and what it does in this clip.</summary>
public sealed class DancerIr
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>The figure whose choreography it dances.</summary>
    public string Figure { get; set; } = "";
    public string Rig { get; set; } = "humanoid";
    /// <summary>The character file (VRM) the preview puts on this body, as written (from the .shosa file's folder); null for none.</summary>
    public string? Model { get; set; }
    /// <summary>How tall the preview draws the character, in metres to the top of its hair (`height 1.5m`); null for its own height.</summary>
    public double? ModelHeight { get; set; }
    /// <summary>Start position on the floor: x to stage left, z toward the audience.</summary>
    public double X { get; set; }
    public double Z { get; set; }
    /// <summary>0 faces the audience; positive turns to the dancer's own left.</summary>
    public double FacingDeg { get; set; }
    /// <summary>The rules of weight and gravity never apply to this body.</summary>
    public bool Free { get; set; }
    /// <summary>
    /// What this body sings (`dancer alice … sings melody`): the syllables of that part of the song, in the
    /// clip's beats, each with the shape of the mouth it opens to (DESIGN.md 99). Null when it sings nothing.
    /// </summary>
    public List<SyllableIr>? Sings { get; set; }
    /// <summary>
    /// The hand that holds a mic (`mic right`): "right", "left" or "both" (two hands while singing, the right
    /// one between); null for none. While it sings the mic is at the mouth (DESIGN.md 117).
    /// </summary>
    public string? Mic { get; set; }
    /// <summary>
    /// What the hands throw (`juggles balls`): "balls", "clubs" or "rings"; null for nothing. What they throw
    /// and when is the juggle track (DESIGN.md 118).
    /// </summary>
    public string? Juggles { get; set; }
    /// <summary>Channel name (see <see cref="Channels"/>) to its segments, in time order.</summary>
    public Dictionary<string, List<Seg>> Tracks { get; } = [];
}

/// <summary>A syllable sung from <paramref name="Beat"/> for <paramref name="Beats"/> beats.</summary>
/// <param name="Text">As the song has it (そ, ら, ー, ん, la).</param>
/// <param name="Vowel">The mouth: aa, ih, ou, ee, oh (VRM's expressions), or nn for closed (ん, っ).</param>
public sealed record SyllableIr(double Beat, double Beats, string Text, string Vowel);

/// <summary>One play of a section in the clip.</summary>
/// <param name="Pickup">Beats of it before its first bar line (a song's upbeat): its bars, and the dance of it, begin at T0 + Pickup.</param>
/// <param name="Bar">The number of its first bar, counting the clip's bars from 1. An upbeat belongs to the bar before.</param>
public sealed record SectionIr(string Name, double T0, double T1, int Src, int Len, double Pickup = 0, int Bar = 1);

/// <summary>From <paramref name="Beat"/> on, the clip goes at <paramref name="Bpm"/> beats a minute.</summary>
public sealed record TempoIr(double Beat, double Bpm);

/// <summary>
/// From <paramref name="Beat"/> on, a bar has <paramref name="BeatsPerBar"/> beats, each the note
/// of <paramref name="BeatUnit"/> (4 a crotchet, 8 a quaver). A beat is always what the time in
/// force counts, so a change from 4/4 to 6/8 changes what a beat is.
/// </summary>
public sealed record MeterIr(double Beat, int BeatsPerBar, int BeatUnit);

/// <summary>Beats to seconds and back, through a clip's tempos.</summary>
public static class TempoMap
{
    /// <summary>The tempos as written, or the one tempo of a clip that has none written.</summary>
    public static IReadOnlyList<TempoIr> Of(IReadOnlyList<TempoIr> tempos, double tempo) =>
        tempos.Count > 0 ? tempos : [new TempoIr(0, tempo)];

    public static double BpmAt(IReadOnlyList<TempoIr> tempos, double beat)
    {
        double bpm = tempos[0].Bpm;
        // (By index: asked many times a frame, and a `foreach` over the interface makes an enumerator each time.)
        for (int i = 0; i < tempos.Count; i++)
        {
            if (tempos[i].Beat <= beat + 1e-9)
                bpm = tempos[i].Bpm;
        }
        return bpm;
    }

    public static double Seconds(IReadOnlyList<TempoIr> tempos, double beat)
    {
        double seconds = 0;
        for (int i = 0; i < tempos.Count; i++)
        {
            double end = i + 1 < tempos.Count ? Math.Min(beat, tempos[i + 1].Beat) : beat;
            seconds += (end - tempos[i].Beat) * 60 / tempos[i].Bpm;
            if (i + 1 < tempos.Count && beat <= tempos[i + 1].Beat)
                break;
        }
        return seconds;
    }

    public static double Beat(IReadOnlyList<TempoIr> tempos, double seconds)
    {
        for (int i = 0; i < tempos.Count; i++)
        {
            double span = i + 1 < tempos.Count ? (tempos[i + 1].Beat - tempos[i].Beat) * 60 / tempos[i].Bpm : double.PositiveInfinity;
            if (seconds <= span)
                return tempos[i].Beat + seconds * tempos[i].Bpm / 60;
            seconds -= span;
        }
        return 0;
    }
}

/// <summary>
/// Where the bars of a clip are. They are not at multiples of the bar's length when a section
/// begins with an upbeat, or when the time changes on the way.
/// </summary>
public static class Bars
{
    private const double Epsilon = 1e-9;

    /// <summary>The times as written, or the one time of a clip that has none written.</summary>
    public static IReadOnlyList<MeterIr> Of(IReadOnlyList<MeterIr> meters, int beatsPerBar, int beatUnit = 4) =>
        meters.Count > 0 ? meters : [new MeterIr(0, Math.Max(1, beatsPerBar), beatUnit)];

    /// <summary>The time in force at a beat.</summary>
    public static MeterIr MeterAt(IReadOnlyList<MeterIr> meters, double beat)
    {
        var meter = meters[0];
        foreach (var candidate in meters)
        {
            if (candidate.Beat <= beat + Epsilon)
                meter = candidate;
        }
        return meter;
    }

    /// <summary>The bar a beat is in (from 1; 0 is an upbeat before the first) and the beat within that bar, from 0.</summary>
    public static (int Bar, double Beat) At(IReadOnlyList<SectionIr> sections, IReadOnlyList<MeterIr> meters, double beat)
    {
        SectionIr? section = null;
        foreach (var candidate in sections)
        {
            if (candidate.T0 <= beat + Epsilon)
                section = candidate;
        }
        double line = section is null ? 0 : section.T0 + section.Pickup;
        int first = section?.Bar ?? 1;
        if (beat < line - Epsilon)
            return (first - 1, MeterAt(meters, beat).BeatsPerBar + beat - line);
        var (bars, into) = Walk(meters, line, beat);
        return (first + bars, into);
    }

    /// <summary>How many bars begin from one bar line up to (not at) a later beat.</summary>
    public static int Count(IReadOnlyList<MeterIr> meters, double line, double end)
    {
        var (bars, into) = Walk(meters, line, end);
        return bars + (into > Epsilon ? 1 : 0);
    }

    /// <summary>The whole bars between a bar line and a beat after it, and how far the beat is into its own bar.</summary>
    private static (int Bars, double Into) Walk(IReadOnlyList<MeterIr> meters, double line, double beat)
    {
        int bars = 0;
        for (int i = 0; i < meters.Count; i++)
        {
            double end = i + 1 < meters.Count ? meters[i + 1].Beat : double.PositiveInfinity;
            if (end <= line + Epsilon)
                continue;
            int perBar = Math.Max(1, meters[i].BeatsPerBar);
            if (beat < end - Epsilon)
            {
                int whole = (int)Math.Floor((beat - line) / perBar + Epsilon);
                return (bars + whole, Math.Max(0, beat - line - whole * perBar));
            }
            // A new time begins on a bar line; a bar it cuts short still counts as one.
            bars += (int)Math.Ceiling((end - line) / perBar - Epsilon);
            line = end;
        }
        return (bars, 0);
    }
}

/// <summary>A moment named by the author (`!hit`), for the engine: the beat its word is written at, which is when the part arrives.</summary>
/// <param name="Section">The section that writes the mark, and <paramref name="Play"/>: which play of it in the score (1 the first).</param>
public sealed record MarkIr(double Beat, string Name, string Dancer, int Src, int Len, string Section = "", int Play = 1);

public static class Channels
{
    /// <summary>Where the feet and the weight are. Its events cannot be cut short: a step is taken or it is not.</summary>
    public const string Support = "support";
    /// <summary>
    /// How high the body stands on its feet. Written by the same `steps` track as the support,
    /// but a part of its own: a narrower track can take the height and leave the steps.
    /// </summary>
    public const string Height = "height";
    public const string LeftArm = "leftArm";
    public const string RightArm = "rightArm";
    public const string LeftLeg = "leftLeg";
    public const string RightLeg = "rightLeg";
    /// <summary>
    /// Both legs turned in the hip sockets, about their own length: the feet and the knees point
    /// out from (or in toward) the way the body faces. The hips stay square. A state: it lasts until
    /// a word changes it. Its segments are `Turned`, TurnDeg each leg's turn, positive outward.
    /// </summary>
    public const string Turnout = "turnout";
    /// <summary>
    /// The hips turned on the feet, about the vertical: the feet stay where they stand. The
    /// shoulders are a part of their own: they stay where their row has them while the hips turn.
    /// </summary>
    public const string Hips = "hips";
    public const string Torso = "torso";
    /// <summary>
    /// The line of the shoulders, turned from the way the feet face, as it lies on the floor. The
    /// chest turns about the spine on the hips to put it there. A part of its own, beside the
    /// torso's lean: the two are different axes and move at different times (a guard keeps its lean through every punch).
    /// </summary>
    public const string Shoulders = "shoulders";
    public const string Head = "head";

    public static readonly string[] All = [Support, Height, LeftArm, RightArm, LeftLeg, RightLeg, Turnout, Hips, Torso, Shoulders, Head];

    /// <summary>
    /// What the hands throw and catch, in siteswap (DESIGN.md 118). Not a part of the body, so not in
    /// <see cref="All"/> (`body` does not write it): while it has a word the arms are its, over their rows.
    /// Its segments are `Juggle`, from T0 to T1, a throw every <see cref="Seg.Every"/> beats.
    /// </summary>
    public const string Juggle = "juggle";

    public static string Mirror(string channel) => channel switch
    {
        LeftArm => RightArm,
        RightArm => LeftArm,
        LeftLeg => RightLeg,
        RightLeg => LeftLeg,
        _ => channel,
    };
}

public static class SegKinds
{
    public const string Hold = "hold";          // every channel: stay as you are
    public const string Stay = "stay";          // arm: the hand stays where it is in the room, the arm following it (`stay`)
    public const string Release = "release";    // leg: `skip` in a leg's row gives the leg back to `steps` over its time (DESIGN.md 83.4)
    public const string Dir = "dir";            // gesture: point the part along Dir
    public const string Anchor = "anchor";      // arm: bring the hand to a place on the body; leg: the toe to the other leg
    public const string Reach = "reach";        // arm: to a place on another dancer; head: look at it
    public const string Turned = "turned";      // hips and shoulders: their line turned TurnDeg from the way the feet face, on the floor
    public const string Step = "step";          // support: move Foot, then carry the weight on it
    public const string Together = "together";  // support: close the free foot to the supporting one
    public const string Level = "level";        // height: stand at Level (from `both.low`, or the level a step arrives at)
    public const string Turn = "turn";          // support: pivot by TurnDeg, or to face Target
    public const string Jump = "jump";          // support: leave the floor and land on both feet
    public const string Shift = "shift";        // support: the weight goes over Foot (left | right | center)
    public const string Lift = "lift";          // support, free: hang with the feet Height above the floor
    public const string Land = "land";          // support, free: back to the floor
    public const string Juggle = "juggle";      // juggle: throw Pattern from T0 to T1, a throw every Every beats
}

/// <summary>
/// One segment of one channel: from T0 to T1 the part moves to its target and is there at T1.
/// A flat record with optional fields rather than a class per kind, so that it is one JSON shape.
///
/// The word it came from is written AT a beat, and that is the beat the part arrives (T1): the
/// way there (T0..T1) is made in the time of the word before. The word then has the part
/// UNTIL the next one. (A jump lands at At and T1 is a little after it; in a loop the way to
/// the first word is at the end, one length of the loop later than At.)
/// </summary>
public sealed class Seg
{
    public double T0 { get; set; }
    public double T1 { get; set; }
    /// <summary>The beat the word is written at, and the beat its time ends: when it is the word being performed.</summary>
    public double At { get; set; }
    public double Until { get; set; }
    public string Kind { get; set; } = SegKinds.Hold;

    /// <summary>Dir: unit vector in the dancer's frame (x left, y up, z forward).</summary>
    public double[]? Dir { get; set; }
    /// <summary>Dir, limbs: degrees the elbow or the knee is bent, 0 = straight (`bent(N)`). The hand or foot stays in Dir and comes closer.
    /// Null: not written (an arm `down` then hangs with the elbow a little bent; `down.bent(0)` is straight).</summary>
    public double? Bend { get; set; }
    /// <summary>Limbs: which way the elbow or the knee points (`@elbow(...)`, `@knee(...)`), in the dancer's frame (in the torso's with Body); null: the solver's own way.</summary>
    public double[]? Pole { get; set; }
    /// <summary>Dir, arms: the direction is in the torso's frame (it leans with the torso), not the room's.</summary>
    public bool Body { get; set; }
    /// <summary>Dir and Reach, head: degrees the line of the eyes is tilted from level, positive with the left ear down (`tilt(left(15))`); null: the head sits square on the neck.</summary>
    public double? Tilt { get; set; }
    /// <summary>Dir, torso: the back is rounded. The direction is still the line from the pelvis to the chest; the spine bows back from it.</summary>
    public bool Round { get; set; }
    /// <summary>Dir, legs (`side.floor`): the leg straight that way, the foot pointed and its toe on the floor (a tendu). Dir is level; how far down follows from how high the hip is.</summary>
    public bool Floor { get; set; }
    /// <summary>Anchor, arms: hip | chest | shoulder | head | chin | belly | shoulder.across | clap. Legs: otherKnee | otherKnee.back | otherAnkle | otherAnkle.back.</summary>
    public string? Anchor { get; set; }
    /// <summary>Reach and Turn: the id of the other dancer.</summary>
    public string? Target { get; set; }
    /// <summary>Reach: hand | shoulder | head | chest.</summary>
    public string? Part { get; set; }

    /// <summary>Step: left | right. Shift: left | right | center.</summary>
    public string? Foot { get; set; }
    /// <summary>Step: metres from the supporting foot, toward the stepping foot's own side. Jump and Lift: metres to the dancer's left.</summary>
    public double Dx { get; set; }
    /// <summary>Step, Jump and Lift: metres forward.</summary>
    public double Dz { get; set; }
    /// <summary>Level: -1 knees bent, 0 normal, 1 on the toes. Reach: the height two hands meet at.</summary>
    public double? Level { get; set; }
    /// <summary>Level: standing on straight legs (`straight`, and `high` on the toes) rather than the soft knees of `mid`.</summary>
    public bool Straight { get; set; }
    /// <summary>Level (`sit(0.45m)`): sitting on a seat this many metres above the floor, the feet on the floor; null: standing.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? Seat { get; set; }
    /// <summary>Turn: degrees, positive to the left. Step and Jump: the turn made during it. Turned: how far the line of the hips or the shoulders is turned from the feet.</summary>
    public double TurnDeg { get; set; }
    /// <summary>Jump: distance between the feet on landing; null keeps it, -1 touching (as `together` closes). Together: null touching, else the width (`stand`).</summary>
    public double? Spread { get; set; }
    /// <summary>Jump (`jump.change`): the feet land as they stood mirrored left for right: the one behind lands in front (a changement).</summary>
    public bool Change { get; set; }
    /// <summary>Jump: the foot it lands on. both (`jump`), same — the one it took off from (`hop`), other — the one that carried no weight (`leap`).</summary>
    public string? Lands { get; set; }
    /// <summary>Lift: metres between the floor and the feet.</summary>
    public double Height { get; set; }
    /// <summary>Jump (`hop.up(5cm)`): metres the body rises; null: as high as the time before it gives.</summary>
    public double? Rise { get; set; }
    /// <summary>Jump: how far through the segment (0..1) the feet come down; 0.85 if not said. Before it the bend and the air, after it the give.</summary>
    public double? Land { get; set; }
    /// <summary>Step: a stance, not a movement (it came from a `pose`). Already standing so, nothing moves.</summary>
    public bool Stance { get; set; }
    /// <summary>Step: the first word of a loop's feet, already made at its start, as the way back to it at the
    /// end; the beat the word before it begins. If the feet already stand so, it is a way of standing: no foot
    /// moves, and the weight goes back between them from this beat (DESIGN.md 88).</summary>
    public double? Kept { get; set; }

    /// <summary>Juggle: the siteswap as written (`531`, `(4,2x)*`).</summary>
    public string? Pattern { get; set; }
    /// <summary>Juggle: beats from one throw to the next (`every 1/2`).</summary>
    public double? Every { get; set; }
    /// <summary>Juggle (`@mirror`): the left hand throws the first plain beat, or the first number of a pair.</summary>
    public bool LeftFirst { get; set; }
    /// <summary>Juggle: the tricks the hands throw it with (`@mills`, `@penguinCatch`, `@clawCatch`, `@reverse`, `@tennis`, `@backThrow`, `@backCatch`, `@underThrow`); null for none.</summary>
    public List<string>? Tricks { get; set; }

    /// <summary>smooth | sharp | linear.</summary>
    public string Ease { get; set; } = "smooth";
    /// <summary>Weight, gravity and speed limits are not applied (`@free`, or a free dancer).</summary>
    public bool Free { get; set; }

    /// <summary>Source span of the event this came from (the one written in the section).</summary>
    public int Src { get; set; }
    public int Len { get; set; }

    public Seg Clone() => (Seg)MemberwiseClone();
}
