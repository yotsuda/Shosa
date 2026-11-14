namespace Shosa.Core.Semantics;

/// <summary>
/// What one `score` of a file binds to: its clips. A file without a `form` gives one clip per
/// `section`; with a form, the form is the one clip.
/// </summary>
public sealed class ScoreIr
{
    /// <summary>"" for the file's default score.</summary>
    public string Name { get; set; } = "";
    /// <summary>The names of every score the file has, the default one ("") first.</summary>
    public List<string> Scores { get; } = [];
    public List<MotionIr> Clips { get; } = [];
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
    /// <summary>The section's name, or the score's or form's for a clip made from a form ("" if unnamed).</summary>
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public double Tempo { get; set; } = 90;
    public int BeatsPerBar { get; set; } = 4;
    public int BeatUnit { get; set; } = 4;
    public int Fps { get; set; } = 30;
    public double TotalBeats { get; set; }
    /// <summary>The end joins the start: solved as if the clip followed itself.</summary>
    public bool Loop { get; set; }
    /// <summary>The travel over the floor is taken out of the output and reported as a velocity.</summary>
    public bool Inplace { get; set; }
    /// <summary>The song this is danced to (not read yet).</summary>
    public string? Music { get; set; }
    public List<DancerIr> Dancers { get; } = [];
    public List<SectionIr> Sections { get; } = [];
    public List<MarkIr> Marks { get; } = [];
}

/// <summary>A body and what it does in this clip.</summary>
public sealed class DancerIr
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>The figure whose choreography it dances ("" in a file without figures).</summary>
    public string Figure { get; set; } = "";
    public string Rig { get; set; } = "humanoid";
    /// <summary>Start position on the floor: x to stage left, z toward the audience.</summary>
    public double X { get; set; }
    public double Z { get; set; }
    /// <summary>0 faces the audience; positive turns to the dancer's own left.</summary>
    public double FacingDeg { get; set; }
    /// <summary>The rules of weight and gravity never apply to this body.</summary>
    public bool Free { get; set; }
    /// <summary>Channel name (see <see cref="Channels"/>) to its segments, in time order.</summary>
    public Dictionary<string, List<Seg>> Tracks { get; } = [];
}

/// <summary>One play of a section in the clip.</summary>
public sealed record SectionIr(string Name, double T0, double T1, int Src, int Len);

/// <summary>A moment named by the author (`!hit`), for the engine: the beat the part arrives.</summary>
public sealed record MarkIr(double Beat, string Name, string Dancer, int Src, int Len);

public static class Channels
{
    public const string Support = "support";
    public const string LeftArm = "leftArm";
    public const string RightArm = "rightArm";
    public const string LeftLeg = "leftLeg";
    public const string RightLeg = "rightLeg";
    public const string Torso = "torso";
    public const string Head = "head";

    public static readonly string[] All = [Support, LeftArm, RightArm, LeftLeg, RightLeg, Torso, Head];

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
    public const string Dir = "dir";            // gesture: point the part along Dir
    public const string Anchor = "anchor";      // arm: bring the hand to a place on the body
    public const string Reach = "reach";        // arm: to a place on another dancer; head: look at it
    public const string Step = "step";          // support: move Foot, then carry the weight on it
    public const string Together = "together";  // support: close the free foot to the supporting one
    public const string Level = "level";        // support: both feet, change of height
    public const string Turn = "turn";          // support: pivot by TurnDeg, or to face Target
    public const string Jump = "jump";          // support: leave the floor and land on both feet
    public const string Shift = "shift";        // support: the weight goes over Foot (left | right | center)
    public const string Lift = "lift";          // support, free: hang with the feet Height above the floor
    public const string Land = "land";          // support, free: back to the floor
}

/// <summary>
/// One segment of one channel: from T0 to T1 the part moves to its target and is there at T1.
/// A flat record with optional fields rather than a class per kind, so that it is one JSON shape.
/// </summary>
public sealed class Seg
{
    public double T0 { get; set; }
    public double T1 { get; set; }
    public string Kind { get; set; } = SegKinds.Hold;

    /// <summary>Dir: unit vector in the dancer's frame (x left, y up, z forward).</summary>
    public double[]? Dir { get; set; }
    /// <summary>Dir: how far the limb is extended, 1 = straight.</summary>
    public double Ext { get; set; } = 1;
    /// <summary>Dir, arms: the direction is in the torso's frame (it leans with the torso), not the room's.</summary>
    public bool Body { get; set; }
    /// <summary>Anchor: hip | chest | shoulder | head | chin | belly | across.shoulder | clap.</summary>
    public string? Anchor { get; set; }
    /// <summary>Reach and Turn: the id of the other dancer.</summary>
    public string? Target { get; set; }
    /// <summary>Reach: hand | shoulder | head | chest.</summary>
    public string? Part { get; set; }

    /// <summary>Step: left | right. Shift: left | right | center.</summary>
    public string? Foot { get; set; }
    /// <summary>Step: metres from the supporting foot, toward the stepping foot's own side.</summary>
    public double Dx { get; set; }
    /// <summary>Step, Jump and Lift: metres forward.</summary>
    public double Dz { get; set; }
    /// <summary>Step and Level: -1 knees bent, 0 normal, 1 on the toes.</summary>
    public double? Level { get; set; }
    /// <summary>Turn: degrees, positive to the left. Step and Jump: the turn made during it.</summary>
    public double TurnDeg { get; set; }
    /// <summary>Jump: distance between the feet on landing; null keeps it.</summary>
    public double? Spread { get; set; }
    /// <summary>Lift: metres between the floor and the feet.</summary>
    public double Height { get; set; }
    /// <summary>Step: a stance, not a movement (it came from a `pose`). Already standing so, nothing moves.</summary>
    public bool Stance { get; set; }

    /// <summary>smooth | sharp | linear.</summary>
    public string Ease { get; set; } = "smooth";
    /// <summary>Weight, gravity and speed limits are not applied (`@free`, or a free dancer).</summary>
    public bool Free { get; set; }

    /// <summary>Source span of the event this came from (the one written in the section).</summary>
    public int Src { get; set; }
    public int Len { get; set; }

    public Seg Clone() => (Seg)MemberwiseClone();
}
