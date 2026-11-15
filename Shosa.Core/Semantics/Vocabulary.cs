namespace Shosa.Core.Semantics;

/// <summary>
/// Every word of the language, with what it means, in one table. The binder checks against
/// it; completion, hover and the one-file language description for an AI are all meant to be
/// generated from it, so a word is added in exactly one place.
/// </summary>
public static class Vocabulary
{
    public sealed record Word(string Name, string Doc);

    /// <summary>Track names. `arms` and `steps` are spellings; the rest are channels as they are.</summary>
    public static readonly Word[] Tracks =
    [
        new("body", "The whole body. Takes the poses and moves you have defined, `then` and `skip`; a narrower track written beside it wins for its own part."),
        new("steps", "The feet on the floor: steps, turns, jumps, changes of height. Where the weight is."),
        new("arms", "Both arms at once, mirrored: `side` is away from the midline for each."),
        new("leftArm", "The left arm: where the hand points from the shoulder, or a place on the body it goes to."),
        new("rightArm", "The right arm."),
        new("leftLeg", "The left leg when it carries no weight: where it points from the hip."),
        new("rightLeg", "The right leg when it carries no weight."),
        new("hips", "The hips turned over feet that stay where they stand: `left` (30 degrees), `right.20`, `center`. The chest goes round with them; the head does not."),
        new("torso", "The spine: `up` is upright, `fwd.high` leans forward 45 degrees."),
        new("twist", "The chest turned on the hips, about the spine: `left` (30 degrees), `right.20`, `center`. The feet and the hips stay."),
        new("head", "Where the face looks: `fwd` is straight ahead. It nods with the chest's lean, but does not turn with a `twist`."),
    ];

    /// <summary>What the `twist` track says. A number after the side is degrees, up to <see cref="MaxTwist"/>.</summary>
    public static readonly Word[] TwistWords =
    [
        new("left", "The chest turns to the figure's left, the right shoulder comes forward: 30 degrees, or `left.N` for N degrees (up to 45)."),
        new("right", "The chest turns to the figure's right, the left shoulder comes forward: 30 degrees, or `right.N`."),
        new("center", "No twist: the shoulders square over the hips."),
    ];

    /// <summary>What the `hips` track says: the same three words as a twist, of the hips over the feet.</summary>
    public static readonly Word[] HipsWords =
    [
        new("left", "The hips turn to the figure's left, the right hip comes forward: 30 degrees, or `left.N` for N degrees (up to 45). The chest and the shoulders go round with them."),
        new("right", "The hips turn to the figure's right, the left hip comes forward: 30 degrees, or `right.N`."),
        new("center", "The hips square over the feet."),
    ];

    public const double DefaultTwist = 30, MaxTwist = 45;

    public static readonly Word[] Directions =
    [
        new("fwd", "Forward: the way the figure faces."),
        new("back", "Backward."),
        new("side", "Away from the body's midline: left for a left limb, right for a right limb."),
        new("across", "Toward and past the midline: the opposite of `side`."),
        new("left", "To the figure's left. For the torso and the head; a limb says `side` or `across`."),
        new("right", "To the figure's right. For the torso and the head."),
    ];

    public static readonly Word[] Levels =
    [
        new("low", "45 degrees below horizontal. For the feet: knees bent (the hips 16 cm lower; a deep crouch is `both.low@large`)."),
        new("mid", "Horizontal (the default). For the feet: normal standing height."),
        new("high", "45 degrees above horizontal. For the feet: on the toes."),
    ];

    public static readonly Word[] Poles =
    [
        new("up", "Straight up."),
        new("down", "Down: an arm hanging at the side as an arm nobody wrote does (the hand by the thigh, a little forward), a free leg back on the floor."),
    ];

    public static readonly Word[] Anchors =
    [
        new("hip", "The hand on the hip of its own side."),
        new("chest", "The hand in front of the chest."),
        new("shoulder", "The hand on its own shoulder."),
        new("head", "The hand beside the head."),
        new("chin", "The hand in front of the chin: one hand in thought, one fist held close. (Both arms there cross at the elbows: a guard is `fwd.25@bent`.)"),
        new("belly", "The hand on the belly."),
        new("across.shoulder", "The hand on the opposite shoulder. With `arms`: arms folded."),
        new("clap", "The hand at the midline in front of the chest. With `arms`: the two hands meet."),
    ];

    /// <summary>Places on another figure: `b.hand`. For an arm (reach it) or the head (look at it).</summary>
    public static readonly Word[] Parts =
    [
        new("hand", "The other figure's hand of the same side. If it reaches back, the two hands meet half-way, at the height of a handshake."),
        new("shoulder", "The other figure's shoulder of the same side."),
        new("head", "The other figure's head."),
        new("chest", "The other figure's chest."),
    ];

    public static readonly Word[] Feet =
    [
        new("left", "The left foot steps and takes the weight."),
        new("right", "The right foot steps and takes the weight."),
        new("both", "No foot moves: only the height changes (`both.low`, `both.high`)."),
    ];

    public static readonly Word[] StepDirections =
    [
        new("fwd", "One stride ahead of the supporting foot."),
        new("back", "One stride behind the supporting foot."),
        new("side", "Apart from the supporting foot, to the stepping foot's own side."),
        new("across", "Crossing in front of the supporting foot."),
        new("place", "Beside the supporting foot: a step on the spot."),
    ];

    public static readonly Word[] SupportActions =
    [
        new("together", "The free foot closes to the supporting foot; the weight is on both."),
        new("turn", "A pivot on the supporting foot: `turn.left.quarter`, `turn.right.half`."),
        new("jump", "Off the floor, landing on both feet: `jump`, `jump.fwd`, `jump.apart`, `jump.together`."),
        new("shift", "The weight moves and neither foot leaves the floor: `shift.left`, `shift.right`, `shift.center`."),
        new("lift", "Only for a free body (`@free`, or `free` on the dancer): the feet hang at a height in metres, `lift(0.4)`. `lift.fwd(0.4)` also travels."),
        new("land", "Only for a free body: back down to the floor."),
    ];

    public static readonly Word[] ShiftWords =
    [
        new("left", "The weight over the left foot."),
        new("right", "The weight over the right foot."),
        new("center", "The weight between the feet."),
    ];

    public static readonly Word[] JumpWords =
    [
        new("place", "Land where you took off (the default)."),
        new("fwd", "Land 0.4 m ahead."),
        new("back", "Land 0.3 m behind."),
        new("left", "Land 0.4 m to the figure's left: a dodge is `jump.left@small`."),
        new("right", "Land 0.4 m to the figure's right."),
        new("apart", "Land with the feet 0.6 m apart."),
        new("together", "Land with the feet at standing width."),
    ];

    public static readonly Word[] TurnAmounts =
    [
        new("eighth", "45 degrees."),
        new("quarter", "90 degrees."),
        new("half", "180 degrees."),
        new("full", "360 degrees."),
    ];

    public static readonly Word[] Annotations =
    [
        new("smooth", "The way TO this word eases in and out (the default)."),
        new("sharp", "Go at the last moment: the part waits, then gets there in no more than 0.15 s before its beat. An accent — a punch, a clap. Fast, so it can be too fast (SH2003)."),
        new("linear", "The way TO this word is at constant speed: no slowing as it arrives."),
        new("bent", "Fold the elbow or the knee: the hand or foot stays in direction but comes closer."),
        new("body", "For an arm: measure the direction against the torso instead of the room, so the arm leans when the torso leans."),
        new("round", "For the torso: the back is rounded. The direction still says where the chest is from the hips; the spine bows back from that line, and the chest, the shoulders and the head lean further forward (`torso { fwd.80@round }` is a fighter's guard, `up@round` a slouch). Without it the back is straight, as in a bow from the hips."),
        new("small", "A shorter step or jump. On `both.low` / `together.low`: a slighter knee bend."),
        new("large", "A longer step or jump. On `both.low` / `together.low`: a deeper crouch."),
        new("mirror", "Left and right swapped: `kick@mirror` kicks with the other leg."),
        new("free", "The rules of weight, gravity and speed do not apply to this event. Bones still keep their length."),
        new("turn", "For a step or a jump: turn while doing it, `left.fwd@turn(left.quarter)`, `jump@turn(right.half)`. The step lands where it would after the turn."),
    ];

    public const string Hold = "then";
    public const string Skip = "skip";
    /// <summary>`q`: the group in braces before it, again.</summary>
    public const string Again = "q";

    /// <summary>Every word that means something on its own, so that a definition cannot take its name.</summary>
    public static IEnumerable<string> Reserved =>
        new[] { Tracks, Directions, Levels, Poles, Anchors, Feet, StepDirections, SupportActions, JumpWords, TurnAmounts, TwistWords, HipsWords }
            .SelectMany(words => words).Select(w => w.Name).Append(Hold).Append(Skip).Append(Again);

    public static double TurnDegrees(string amount) => amount switch
    {
        "eighth" => 45,
        "quarter" => 90,
        "half" => 180,
        "full" => 360,
        _ => double.NaN,
    };

    public static bool Has(Word[] words, string name) => words.Any(w => w.Name == name);

    /// <summary>The closest known word, for "did you mean", or null when nothing is close.</summary>
    public static string? Nearest(string name, IEnumerable<string> candidates)
    {
        string? best = null;
        int bestDistance = Math.Max(2, name.Length / 3) + 1;
        foreach (var candidate in candidates)
        {
            int distance = EditDistance(name.ToLowerInvariant(), candidate.ToLowerInvariant());
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }
        return best;
    }

    private static int EditDistance(string a, string b)
    {
        var row = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
            row[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            int diagonal = row[0];
            row[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }
        return row[b.Length];
    }
}
