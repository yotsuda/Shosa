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
        new("steps", "The feet on the floor: steps, turns, jumps. Where the weight is."),
        new("height", "How high the body stands on its feet: `low` (the knees bent), `low(30cm)`, `mid`, `high` (on the toes). Kept until it is changed."),
        new("arms", "Both arms at once, mirrored: `side` is away from the midline for each."),
        new("leftArm", "The left arm: where the hand points from the shoulder, or a place on the body it goes to."),
        new("rightArm", "The right arm."),
        new("leftLeg", "The left leg when it carries no weight: where it points from the hip."),
        new("rightLeg", "The right leg when it carries no weight."),
        new("turnout", "Both legs turned in their hip sockets: `out(80)` (degrees each), `in(20)`, `parallel`. The feet and the knees point that way; the hips do not turn."),
        new("hips", "The hips turned over feet that stay where they stand: `left(30)` (degrees), `right(20)`, `center`. The shoulders and the head do not go round with them."),
        new("torso", "The spine: `up` is upright, `up.fwd(10)` leans 10 degrees forward, `fwd` is folded level."),
        new("shoulders", "The line of the shoulders turned from the feet, as seen from above: `left(30)` (degrees), `right(85)`, `center`. The chest turns on the hips to put it there; the feet stay."),
        new("head", "Where the face looks: `fwd` is straight ahead. It nods with the chest's lean, but does not turn with the hips or the shoulders."),
    ];

    /// <summary>What the `shoulders` track says. A number after the side is degrees from the feet, up to <see cref="MaxShoulders"/>.</summary>
    public static readonly Word[] ShouldersWords =
    [
        new("left", "The shoulders turn to the dancer's left, the right shoulder comes forward: `left(N)`, N degrees from the feet (up to 90)."),
        new("right", "The shoulders turn to the dancer's right, the left shoulder comes forward: `right(N)`."),
        new("center", "The shoulders square over the feet."),
    ];

    /// <summary>What the `hips` track says: the same three words, of the hips over the feet, up to <see cref="MaxHips"/>.</summary>
    public static readonly Word[] HipsWords =
    [
        new("left", "The hips turn to the dancer's left, the right hip comes forward: `left(N)`, N degrees from the feet (up to 45)."),
        new("right", "The hips turn to the dancer's right, the left hip comes forward: `right(N)`."),
        new("center", "The hips square over the feet."),
    ];

    /// <summary>
    /// Degrees, always written: `left(20)`. The hips turn up to 45 over the feet, the shoulders
    /// up to 90 from them; the chest turns on the hips, about the spine, up to <see cref="MaxChestTurn"/>.
    /// </summary>
    public const double MaxHips = 45, MaxShoulders = 90, MaxChestTurn = 60;

    /// <summary>What the `turnout` track says: each leg turned out (or in) from the way the body faces, as seen from above.</summary>
    public static readonly Word[] TurnoutWords =
    [
        new("out", "Both legs turn outward from the hips: each foot, and its knee, N degrees out from the way the body faces: `out(N)` (up to 90: a dancer's full turnout, the feet in one line; 45 to 60 for one who has not trained)."),
        new("in", "Both legs turn inward: each foot N degrees in: `in(N)` (up to 45). Toes toward each other."),
        new("parallel", "The legs are not turned: the feet and the knees point the way the body faces."),
    ];

    /// <summary>The most each leg may turn out and in, degrees.</summary>
    public const double MaxOut = 90, MaxIn = 45;

    public static readonly Word[] Directions =
    [
        new("fwd", "Forward: the way the dancer faces."),
        new("back", "Backward."),
        new("side", "Away from the body's midline: left for a left limb, right for a right limb."),
        new("across", "Toward and past the midline: the opposite of `side`."),
        new("left", "To the dancer's left. For the torso and the head; a limb says `side` or `across`."),
        new("right", "To the dancer's right. For the torso and the head."),
    ];

    public static readonly Word[] Levels =
    [
        new("low", "The knees bent, the hips 16 cm lower; `low(30cm)` as far down as that (a deep crouch is `low(40cm)`)."),
        new("mid", "Standing straight (the height a row nobody wrote stands at)."),
        new("high", "On the toes."),
    ];

    public static readonly Word[] Poles =
    [
        new("up", "Straight up. After a direction, toward up: `fwd.up(30)` is 30 degrees above `fwd` (`fwd.up` 45). Before one, from straight up: `up.fwd(10)` is 10 degrees forward of upright (a torso's slight lean)."),
        new("down", "Straight down; for an arm, hanging as an arm nobody wrote does (the hand by the thigh, a little forward); for a free leg, back on the floor. After a direction, toward down: `side.down(20)`; `fwd.down` 45."),
    ];

    /// <summary>Words that come after a direction and say the shape the part arrives in. Written in this order.</summary>
    public static readonly Word[] Shapes =
    [
        new("bent", "For an arm or a leg: bend the elbow or the knee, the hand or foot staying in its direction and coming closer. `bent` is a right angle; `bent(40)` 40 degrees (0 straight, up to 150): `fwd.up(25).bent`."),
        new("elbow", "For an arm: which way the elbow points, as an arm's direction: `fwd.down(15).bent(40).elbow(side)` holds it out to the side (ballet's rounded arms). Without it the elbow hangs down, back and a little out. Only the part square to the arm counts; on a straight arm it turns the arm about its length."),
        new("knee", "For a leg in the air: which way the knee points, as a leg's direction: `back.up(20).bent.knee(side.up)` (an attitude, the knee above the foot). Without it the knee points where its foot does (`turnout`)."),
        new("round", "For the torso: the back is rounded. The direction still says where the chest is from the hips; the spine bows back from that line, and the chest, the shoulders and the head lean further forward (`torso { up.fwd(10).round }` is a fighter's guard, `up.round` a slouch). Without it the back is straight, as in a bow from the hips."),
        new("body", "For an arm: the direction is measured against the torso instead of the room, so the arm leans when the torso leans: `up.body`."),
    ];

    /// <summary>The shape words, in the order they are written.</summary>
    public static int ShapeOrder(string word) => Array.FindIndex(Shapes, w => w.Name == word);

    public static readonly Word[] Anchors =
    [
        new("hip", "The hand on the hip of its own side."),
        new("chest", "The hand in front of the chest."),
        new("shoulder", "The hand on its own shoulder."),
        new("head", "The hand beside the head."),
        new("chin", "The hand in front of the chin: one hand in thought, one fist held close. (Both arms there cross at the elbows: a guard is `fwd.up(25).bent`.)"),
        new("belly", "The hand on the belly."),
        new("shoulder.across", "The hand on the opposite shoulder. With `arms`: arms folded."),
        new("clap", "The hand at the midline in front of the chest. With `arms`: the two hands meet."),
    ];

    /// <summary>Places on the other leg, for a leg in the air: where its toe goes. The bend follows from the place.</summary>
    public static readonly Word[] LegAnchors =
    [
        new("otherKnee", "The toe at the front of the other leg's knee: a retiré (passé). The knee points where the foot does (`turnout`), or as `knee(...)` says: `otherKnee.knee(fwd)`."),
        new("otherKnee.back", "The toe at the back of the other leg's knee."),
        new("otherAnkle", "The toe at the front of the other leg's ankle: a coupé (sur le cou-de-pied)."),
        new("otherAnkle.back", "The toe at the back of the other leg's ankle."),
    ];

    /// <summary>Degrees `@bent` bends an elbow or a knee when it has no number, and the most it can.</summary>
    public const double DefaultBend = 90, MaxBend = 150;

    /// <summary>Places on another figure: `b.hand`. For an arm (reach it) or the head (look at it).</summary>
    public static readonly Word[] Parts =
    [
        new("hand", "The other dancer's hand of the same side. If it reaches back, the two hands meet half-way, at the height of a handshake."),
        new("shoulder", "The other dancer's shoulder of the same side."),
        new("head", "The other dancer's head."),
        new("chest", "The other dancer's chest."),
    ];

    public static readonly Word[] Feet =
    [
        new("leftFoot", "The left foot steps and takes the weight: `leftFoot.fwd`, `leftFoot.side(0.4m)`."),
        new("rightFoot", "The right foot steps and takes the weight."),
    ];

    public static readonly Word[] StepDirections =
    [
        new("fwd", "One stride (0.5 m) ahead of the supporting foot, or as far as its bracket says: `fwd(0.3m)`."),
        new("back", "One stride behind the supporting foot: `back(0.3m)`."),
        new("side", "Apart from the supporting foot, to the stepping foot's own side: the feet 0.55 m apart, or `side(0.4m)`."),
        new("across", "Crossing just in front of the supporting foot (0.12 m; with `back`, just behind), or `across(0.2m)`."),
        new("place", "Beside the supporting foot: a step on the spot."),
    ];

    public static readonly Word[] SupportActions =
    [
        new("together", "The free foot closes to the supporting foot, touching (turned out, the heels touch); the weight is on both."),
        new("stand", "The free foot closes to standing width (0.2 m, ankle to ankle); the weight is on both."),
        new("turn", "A pivot on the supporting foot: `turn.left(90)`, `turn.right(180)` (degrees, up to 1080)."),
        new("jump", "Off the floor, landing on both feet: `jump`, `jump.fwd`, `jump.left(0.6m)`, `jump.apart`, `jump.stand`, `jump.together`."),
        new("hop", "Off the floor from the foot that carries the weight, landing on the same foot: `hop`, `hop.fwd`. The other foot is held off the floor."),
        new("leap", "Off the floor from the foot that carries the weight, landing on the other: `leap.fwd` (a running step), `leap.left` (a small jump to the side). The foot it took off from is held off the floor."),
        new("shift", "The weight moves and neither foot leaves the floor: `shift.left`, `shift.right`, `shift.center`."),
        new("lift", "Only for a free body (`@free`, or `free` on the dancer): the feet hang at a height, `lift(0.4m)`. `lift(0.4m).fwd` also travels (0.4 m, or `fwd(0.6m)`)."),
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
        new("fwd", "Land ahead: 0.4 m for a jump, 0.25 m for a hop, 0.8 m for a leap, or `fwd(0.6m)`."),
        new("back", "Land behind, as far as `fwd` goes ahead: `back(0.3m)`."),
        new("left", "Land to the dancer's left (0.4 m; a leap 0.6 m): a dodge is `jump.left(0.25m)`."),
        new("right", "Land to the dancer's right."),
        new("apart", "For a jump: land with the feet 0.6 m apart, or `apart(0.8m)`."),
        new("stand", "For a jump: land with the feet at standing width."),
        new("together", "For a jump: land with the feet touching (turned out, the heels)."),
    ];

    /// <summary>The most a turn says in one word, degrees: a triple turn.</summary>
    public const double MaxTurn = 1080;

    /// <summary>How the part gets to its word, and what is done to the word: never the shape it arrives in (that is the target's).</summary>
    public static readonly Word[] Annotations =
    [
        new("smooth", "The way TO this word eases in and out (the default)."),
        new("sharp", "Go at the last moment: the part waits, then gets there in no more than 0.15 s before its beat. An accent — a punch, a clap. Fast, so it can be too fast (SH2003)."),
        new("linear", "The way TO this word is at constant speed: no slowing as it arrives."),
        new("mirror", "Left and right swapped: `kick:2@mirror` kicks with the other leg."),
        new("free", "The rules of weight, gravity and speed do not apply to this event. Bones still keep their length."),
        new("turn", "For a step or a jump: turn while doing it, `leftFoot.fwd@turn(left(90))`, `jump@turn(right(180))`. The step lands where it would after the turn."),
    ];

    public const string Hold = "then";
    public const string Skip = "skip";
    /// <summary>`q`: the group in braces before it, again.</summary>
    public const string Again = "q";

    /// <summary>Every word that means something on its own, so that a definition cannot take its name.</summary>
    public static IEnumerable<string> Reserved =>
        new[] { Tracks, Directions, Levels, Poles, Anchors, LegAnchors, Shapes, Feet, StepDirections, SupportActions, JumpWords, ShouldersWords, HipsWords, TurnoutWords }
            .SelectMany(words => words).Select(w => w.Name).Append(Hold).Append(Skip).Append(Again);

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
