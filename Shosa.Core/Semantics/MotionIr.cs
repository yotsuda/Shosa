namespace Shosa.Core.Semantics;

/// <summary>
/// The intermediate representation: what each part of each figure must do, and when.
/// The language sits in front of it and the solver behind it, so the surface syntax can
/// change freely as long as it still binds to this. It is plain data and serialises to JSON
/// (`shosa ir`), which is also how a solver experiment is fed without a parser.
///
/// Conventions. Time is in beats from the start of the form. Space is in metres in the
/// figure's own frame: x to the figure's left, y up, z the way it faces (right-handed).
/// Every segment remembers the source span it came from: that is what lets the preview light
/// up the word being performed, and a click in the preview find its word.
/// </summary>
public sealed class MotionIr
{
    public string Title { get; set; } = "";
    public double Tempo { get; set; } = 90;
    public int BeatsPerBar { get; set; } = 4;
    public int BeatUnit { get; set; } = 4;
    public double TotalBeats { get; set; }
    public List<FigureIr> Figures { get; } = [];
    public List<PhraseIr> Phrases { get; } = [];
}

public sealed class FigureIr
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Rig { get; set; } = "humanoid";
    /// <summary>Start position on the floor: x to stage left, z toward the audience.</summary>
    public double X { get; set; }
    public double Z { get; set; }
    /// <summary>0 faces the audience; positive turns to the figure's own left.</summary>
    public double FacingDeg { get; set; }
    /// <summary>Channel name (see <see cref="Channels"/>) to its segments, in time order.</summary>
    public Dictionary<string, List<Seg>> Tracks { get; } = [];
}

/// <summary>One play of a phrase in the form.</summary>
public sealed record PhraseIr(string Name, double T0, double T1, int Src, int Len);

public static class Channels
{
    public const string Support = "support";
    public const string LeftArm = "leftArm";
    public const string RightArm = "rightArm";
    public const string LeftLeg = "leftLeg";
    public const string RightLeg = "rightLeg";
    public const string Torso = "torso";
    public const string Head = "head";
}

public static class SegKinds
{
    public const string Hold = "hold";          // every channel: stay as you are
    public const string Dir = "dir";            // gesture: point the part along Dir
    public const string Anchor = "anchor";      // arm: bring the hand to a place on the body
    public const string Step = "step";          // support: move Foot, then carry the weight on it
    public const string Together = "together";  // support: close the free foot to the supporting one
    public const string Level = "level";        // support: both feet, change of height
    public const string Turn = "turn";          // support: pivot by TurnDeg
    public const string Jump = "jump";          // support: leave the floor and land on both feet
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

    /// <summary>Dir: unit vector in the figure's frame (x left, y up, z forward).</summary>
    public double[]? Dir { get; set; }
    /// <summary>Dir: how far the limb is extended, 1 = straight.</summary>
    public double Ext { get; set; } = 1;
    /// <summary>Dir, arms: the direction is in the torso's frame (it leans with the torso), not the room's.</summary>
    public bool Body { get; set; }
    /// <summary>Anchor: hip | chest | shoulder | head.</summary>
    public string? Anchor { get; set; }

    /// <summary>Step: left | right.</summary>
    public string? Foot { get; set; }
    /// <summary>Step: metres from the supporting foot, toward the stepping foot's own side.</summary>
    public double Dx { get; set; }
    /// <summary>Step and Jump: metres forward.</summary>
    public double Dz { get; set; }
    /// <summary>Step and Level: -1 knees bent, 0 normal, 1 on the toes.</summary>
    public double? Level { get; set; }
    /// <summary>Turn: degrees, positive to the left.</summary>
    public double TurnDeg { get; set; }
    /// <summary>Jump: distance between the feet on landing; null keeps it.</summary>
    public double? Spread { get; set; }

    /// <summary>smooth | sharp | linear.</summary>
    public string Ease { get; set; } = "smooth";

    /// <summary>Source span of the event this came from.</summary>
    public int Src { get; set; }
    public int Len { get; set; }
}
