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
        new("height", "How high the body stands on its feet: `low` (the knees bent), `low(30cm)`, `mid`, `high` (on the toes); and `sit(0.45m)`, on a seat that high. Kept until it is changed."),
        new("arms", "Both arms at once, mirrored: `side` is away from the midline for each."),
        new("leftArm", "The left arm: where the hand points from the shoulder, or a place on the body it goes to."),
        new("rightArm", "The right arm."),
        new("leftLeg", "The left leg when it carries no weight: where it points from the hip."),
        new("rightLeg", "The right leg when it carries no weight."),
        new("legs", "Both legs at once, mirrored, when neither carries the weight: in the air of a jump, and off the feet (`steps { hands }`, `{ seat }` …): `legs { side.up(30) }` is a straddle."),
        new("turnout", "Both legs turned in their hip sockets: `out(80)` (degrees each), `in(20)`, `parallel`. The feet and the knees point that way; the hips do not turn."),
        new("hips", "The hips turned over feet that stay where they stand: `left(30)` (degrees), `right(20)`, `center`. The shoulders and the head do not go round with them."),
        new("torso", "The spine: `up` is upright, `up.fwd(10)` leans 10 degrees forward, `fwd` is folded level."),
        new("shoulders", "The line of the shoulders turned from the feet, as seen from above: `left(30)` (degrees), `right(85)`, `center`. The chest turns on the hips to put it there; the feet stay."),
        new("head", "Where the face looks: `fwd` is straight ahead. It nods with the chest's lean, but does not turn with the hips or the shoulders."),
        new("hands", "Both hands at once, mirrored: the shape of the fingers (`fist`, `open`, `point`, `peace` …), and which way the fingers and the palm face (`open.fingers(up).palm(fwd)`)."),
        new("leftHand", "The left hand: the shape of its fingers, and which way they and the palm face."),
        new("rightHand", "The right hand."),
        new("face", "The face of a character: its look (`smile`, `sad`, `angry`, `relaxed`, `surprised`, `neutral`), and the eyes (`closed` stays shut, `winkLeft` and `winkRight` shut and open again): `face { smile:4 | smile.winkRight:4 | }`. It changes in a moment just before its beat. The eyes blink by themselves, written or not."),
        new("quality", "How the parts go to their words from here on, for every word that does not say so itself: the names of the `@` marks, without the `@`. `quality { through }` passes every word without stopping, one flowing line (as `@through` on each); `quality { sharp }` hits every word at the last moment (as `@sharp` on each); `smooth` is the default again. Timed like any row (`quality { through:8 | sharp:4 smooth:4 | }`), and kept until the next word of the row. A word's own `@` wins. Not for `steps`."),
        new("juggle", "What the hands throw (`juggles balls` on the dancer line), in siteswap as jugglers write it: `juggle { every 1/2  3:4 | 531531 3 3 | }`. A word with no length is its throws as written, one throwing beat each (`531` once through); with a length, the pattern over and over for that long (`3:4`); `every` says how many beats from one throw to the next. While a pattern is thrown, the arms are the juggle row's."),
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
        new("low", "The knees bent, the hips 16 cm lower than standing straight in the same place; `low(30cm)` 30 cm lower (a deep crouch is `low(40cm)`)."),
        new("mid", "Standing, the knees soft (bent 12°): the height a row nobody wrote stands at."),
        new("straight", "Standing on straight legs (a ballet dancer's, a soldier at attention): the hips half a centimetre higher than `mid`."),
        new("high", "On the toes, the legs straight."),
        new("pointe", "On the tips of the toes, the feet upright under the legs (pointe shoes): the hips higher than `high` by most of the foot's length."),
        new("sit", "Sitting on a seat, the feet on the floor: `sit(0.45m)` on a seat 0.45 m above the floor (a chair, and what `sit` alone means; a sofa about 0.35 m, a bar stool 0.7 m; 0.1 m to 0.8 m). The hips go back and down onto it and carry the weight there; any other height stands up again."),
    ];

    public static readonly Word[] Poles =
    [
        new("up", "Straight up. After a direction, toward up: `fwd.up(30)` is 30 degrees above `fwd` (`fwd.up` 45). Before one, from straight up: `up.fwd(10)` is 10 degrees forward of upright (a torso's slight lean)."),
        new("down", "Straight down; for an arm, hanging as an arm nobody wrote does (the hand by the thigh, a little forward); for a free leg, back on the floor. After a direction, toward down: `side.down(20)`; `fwd.down` 45."),
        new(Floor, "For an arm, after its direction: the hand on the floor that way, as far out as the straight arm reaches from the shoulder (`side.floor`, `fwd.floor`; `down.floor` under the shoulder; `back.floor.bent(60)` nearer). For a leg, after its direction instead of up or down: the leg straight that way, the foot pointed and its toe on the floor (a tendu): `rightLeg { side.floor }`, `fwd.floor`, `back.floor`. How far down follows from how high the hip is (deeper in a plié). Out from the floor and back to it, the toe slides along the floor; a brush up (a dégagé) passes through it: `side.floor:1/2 side.down(45):1/2`."),
    ];

    /// <summary>The leg's direction closed by the floor: `side.floor`, a tendu.</summary>
    public const string Floor = "floor";

    /// <summary>Words that come after a direction and say the shape the part arrives in. Written in this order.</summary>
    public static readonly Word[] Shapes =
    [
        new("bent", "For an arm or a leg: bend the elbow or the knee, the hand or foot staying in its direction and coming closer. `bent` is a right angle; `bent(40)` 40 degrees (0 straight, up to 150): `fwd.up(25).bent`."),
        new("elbow", "For an arm: which way the elbow points, as an arm's direction: `fwd.across(15).down(25).bent(50).elbow(side)` holds it out to the side (ballet's rounded arms; the `across` brings the hands together). Without it the elbow hangs down, back and a little out. Only the part square to the arm counts; on a straight arm it turns the arm about its length."),
        new("knee", "For a leg in the air: which way the knee points, as a leg's direction: `back.up(20).bent.knee(side.up)` (an attitude, the knee above the foot). Without it the knee points where its foot does (`turnout`)."),
        new("pointed", "For a leg in the air: the foot stretched on along the shin, the toes pointed: `rightLeg { fwd.down(45).pointed }` (a dégagé, a battement, an arabesque's back foot). Without it (and without `flexed`) a lifted foot lies square, as it stood."),
        new("flexed", "For a leg in the air: the foot pulled up, the toes toward the shin and the heel pushed out: `rightLeg { fwd.flexed }` (a flexed-foot kick, a heel dig, a character step)."),
        new("facing", "For the torso, off the feet: which way the chest faces, as a direction: `torso { down.facing(fwd) }` (a handstand facing the front; a cartwheel's upside-down). Without it the chest faces as the shortest tip from upright leaves it: `fwd` face down, `back` face up, `down` (tipped over forward) looking back."),
        new("round", "For the torso: the back is rounded. The direction still says where the chest is from the hips; the spine bows back from that line, and the chest, the shoulders and the head lean further forward (`torso { up.fwd(10).round }` is a fighter's guard, `up.round` a slouch). Without it the back is straight, as in a bow from the hips."),
        new("tilt", "For the head: the line of the eyes tilted from level, an ear toward its shoulder: `fwd.tilt(left(15))` (up to 45; `tilt(left(0))` holds the eyes level on a leaning torso). Without it the head sits square on the neck and leans with the torso."),
        new("body", "For an arm: the direction is measured against the torso instead of the room, so the arm leans when the torso leans: `up.body`."),
    ];

    /// <summary>The shape words, in the order they are written.</summary>
    /// <summary>The most a head tilts (`tilt(left(45))`), degrees from level.</summary>
    public const double MaxTilt = 45;

    public static int ShapeOrder(string word) => Array.FindIndex(Shapes, w => w.Name == word);

    public static readonly Word[] Anchors =
    [
        new("hip", "The hand on the hip of its own side."),
        new("chest", "The hand in front of the chest."),
        new("shoulder", "The hand on its own shoulder."),
        new("head", "The hand beside the head."),
        new("chin", "The hand in front of the chin: one hand in thought, one fist held close. (Both arms there cross at the elbows: a guard is `fwd.up(25).bent`.)"),
        new("forehead", "The hand at the forehead, the elbow out to the side: a salute, a hand shading the eyes, wiping the brow. It goes with the head (a head turned, tilted or looking up takes it along)."),
        new("belly", "The hand on the belly."),
        new("shoulder.across", "The hand on the opposite shoulder. With `arms`: arms folded."),
        new("clap", "The hand at the midline in front of the chest. With `arms`: the two hands meet."),
        new("thigh", "The hand on the front of the thigh of its own side, half-way down it, wherever the leg is (standing, lifted, sitting)."),
        new("knee", "The hand on the knee of its own side, wherever the leg is: `arms { knee }` with a bow and bent knees is hands on the knees."),
    ];

    /// <summary>Places on the other leg, for a leg in the air: where its toe goes. The bend follows from the place.</summary>
    public static readonly Word[] LegAnchors =
    [
        new("otherKnee", "The toe at the front of the other leg's knee: a retiré (passé). The knee points where the foot does (`turnout`), or as `knee(...)` says: `otherKnee.knee(fwd)`."),
        new("otherKnee.back", "The toe at the back of the other leg's knee."),
        new("otherAnkle", "The toe at the front of the other leg's ankle: a coupé (sur le cou-de-pied)."),
        new("otherAnkle.back", "The toe at the back of the other leg's ankle."),
        new("otherShin", "The toe at the front of the other leg's shin, half-way between the ankle and the knee (a low retiré)."),
        new("otherShin.back", "The toe at the back of the other leg's calf."),
        new("otherThigh", "The toe at the front of the other leg's thigh, half-way up it (a high retiré; the foot of a tree pose)."),
        new("otherThigh.back", "The toe at the back of the other leg's thigh."),
    ];

    /// <summary>
    /// The shapes of a hand (`hands { fist }`): what the five fingers do. Each is a row of <see cref="HandShape"/>:
    /// how far the thumb is folded in and each finger curled, 0 (straight; the thumb out to its side) to 90 (closed
    /// into the fist; the thumb across the palm), and how many degrees the fingers are spread from each other.
    /// They are words of a hand's row only, and not reserved: a file may still name a move `point` or a pose `open`
    /// (in a hand's row the hand's own word is meant).
    /// </summary>
    public static readonly Word[] HandShapes =
    [
        new("relaxed", "The hand as it hangs: the fingers a little curled, more toward the little finger. What a hand nobody wrote is."),
        new("open", "The hand flat: the fingers straight and together, the thumb beside them. A blade of a hand, a stop sign, a salute."),
        new("spread", "The hand flat and the fingers spread wide, the thumb out: jazz hands, a high five, paper."),
        new("fist", "The fingers closed into the palm, the thumb across them: a punch, a guard, rock."),
        new("point", "The first finger straight, the others closed, the thumb across them: pointing; one."),
        new("peace", "The first and the middle finger straight and apart, the others closed: a V, scissors; two."),
        new("thumbUp", "A fist with the thumb straight out of it: with the palm turned in (`thumbUp.palm(across)`) the thumb points up."),
        new("gun", "The first finger straight and the thumb up from it, the others closed: a finger gun, an L."),
        new("rock", "The first and the little finger straight, the middle two closed under the thumb: horns."),
        new("shaka", "The thumb and the little finger out, the middle three closed: hang loose, a telephone."),
        new("ok", "The tips of the thumb and the first finger together in a ring, the other three straight and a little spread."),
        new("pinch", "The tips of the thumb and the first finger together, the other three closed: holding something small, a pinch of salt."),
        new("heart", "The four fingers curved over together and the thumb curved under them: half a heart; the two hands side by side, fingertips and thumb tips touching, make a whole one."),
        new("claw", "Every finger half bent and spread, the thumb too: a cat's paw, a monster's hand."),
        new("soft", "A ballet dancer's hand: the fingers long and barely curved, the middle finger a little in toward the thumb, the first finger a little lifted."),
        new("count", "Fingers held up to count, the rest closed: `count(3)` is three (first, middle and ring); `count(0)` a fist, `count(1)` is `point`, `count(2)` `peace`, `count(5)` the whole hand, spread."),
    ];

    /// <summary>The fingers of a hand, as a word after its shape changes one: `fist.index(0)`.</summary>
    public static readonly Word[] Fingers =
    [
        new("thumb", "The thumb alone, after the hand's shape: `thumb(0)` straight out to its side, `thumb(90)` folded across the palm."),
        new("index", "The first finger alone, after the hand's shape: how far it is curled, `index(0)` straight, `index(90)` closed into the palm: `fist.index(0)` points."),
        new("middle", "The middle finger alone: `middle(0)` straight to `middle(90)` closed."),
        new("ring", "The ring finger alone: `ring(0)` straight to `ring(90)` closed."),
        new("little", "The little finger alone: `little(0)` straight to `little(90)` closed."),
    ];

    /// <summary>Which way a hand is turned on its wrist, after its shape and its fingers. Written in this order.</summary>
    public static readonly Word[] HandTurns =
    [
        new("fingers", "Which way the fingers point, as an arm's direction: `open.fingers(up)` (a stop sign on an arm held forward). The wrist bends that way, up to 80 degrees from the forearm; without it the fingers go on along the forearm."),
        new("palm", "Which way the palm faces, as an arm's direction: `open.palm(up)`, `spread.fingers(up).palm(fwd)`. The hand turns about its fingers to face it there as nearly as it can (only the part square to the fingers counts); without it the palm lies as the arm has it."),
    ];

    /// <summary>
    /// The looks of a face (`face { smile }`, DESIGN.md 131): each is one of VRM's expressions of feeling, as much as
    /// its bracket says in percent (`smile(50)`; without one, all of it). Words of the face's row only, and not reserved.
    /// </summary>
    public static readonly Word[] FaceLooks =
    [
        new("neutral", "The face at rest: no look, the eyes open. What a face nobody wrote is."),
        new("smile", "A happy face (VRM's `happy`): `smile` is the whole of it — on most characters a wide smile, the eyes narrowed or shut with it; `smile(50)` half of it, a smile with the eyes open, which is what a dancer holds through a song."),
        new("sad", "A sad face (VRM's `sad`): the brows up in the middle, the mouth down. `sad(40)` a little."),
        new("angry", "An angry face (VRM's `angry`): the brows down, the mouth tight."),
        new("relaxed", "A soft, pleased face (VRM's `relaxed`): on most characters the eyes closed in a gentle smile."),
        new("surprised", "A surprised face (VRM's `surprised`): the eyes and the brows up, the mouth a little open."),
    ];

    /// <summary>The eyes of a face, after its look or alone (`smile.winkRight`, `closed`): how far shut, in percent in a bracket (`closed(50)`).</summary>
    public static readonly Word[] FaceEyes =
    [
        new("open", "Both eyes open, and they stay so: what a word that says no eyes is too (`smile(50).open` is `smile(50)`), written to say it."),
        new("closed", "Both eyes shut, and they STAY shut until a word opens them (`closed(50)`: half shut, sleepy). Alone or after a look: `relaxed.closed`."),
        new("closedLeft", "The dancer's left eye shut and staying shut, the right open: a wink held (a photo pose)."),
        new("closedRight", "The dancer's right eye shut and staying shut."),
        new("winkLeft", "A wink: the dancer's left eye shuts on the beat and opens again by itself, about a third of a second later; the look stays. Alone or after a look: `smile(60).winkLeft`."),
        new("winkRight", "A wink of the dancer's right eye: `smile(60).winkRight`."),
        new("blink", "Both eyes shut on the beat and open again at once: a blink on purpose (the eyes blink by themselves too)."),
    ];

    /// <summary>The weights a face's word gives, in this order: VRM's names for them (a frame of the clip's `face`, and the expressions a `.vrma` keys).</summary>
    public static readonly string[] FaceShapes = ["happy", "angry", "sad", "relaxed", "surprised", "blinkLeft", "blinkRight"];

    /// <summary>Which of <see cref="FaceShapes"/> a look is; -1 for `neutral`.</summary>
    public static int LookShape(string word) => word switch { "smile" => 0, "angry" => 1, "sad" => 2, "relaxed" => 3, "surprised" => 4, _ => -1 };

    /// <summary>The most a wrist bends from the forearm's line (`fingers(…)`), degrees.</summary>
    public const double MaxWrist = 80;

    /// <summary>The thumb and the four fingers of a hand shape: how far each is closed, 0..90; and the degrees between two fingers.</summary>
    public readonly record struct HandShape(double Thumb, double Index, double Middle, double Ring, double Little, double Spread);

    /// <summary>What a hand shape's word does to the fingers; `count` by its number.</summary>
    public static HandShape? ShapeOfHand(string word, int count = 0) => word switch
    {
        "relaxed" => new(30, 20, 26, 32, 38, 4),
        "open" => new(35, 0, 0, 0, 0, 0),
        "spread" => new(0, 0, 0, 0, 0, 18),
        "fist" => new(75, 90, 90, 90, 90, 0),
        "point" => new(75, 0, 90, 90, 90, 0),
        "peace" => new(80, 0, 0, 90, 90, 14),
        "thumbUp" => new(0, 90, 90, 90, 90, 0),
        "gun" => new(0, 0, 90, 90, 90, 0),
        "rock" => new(80, 0, 90, 90, 0, 8),
        "shaka" => new(0, 90, 90, 90, 0, 10),
        "ok" => new(58, 52, 8, 4, 0, 10),
        "pinch" => new(58, 52, 75, 80, 85, 0),
        "heart" => new(45, 55, 55, 55, 55, 0),
        "claw" => new(40, 50, 50, 50, 50, 16),
        "soft" => new(40, 6, 22, 14, 10, 5),
        "count" => count switch
        {
            <= 0 => new(75, 90, 90, 90, 90, 0),
            1 => new(75, 0, 90, 90, 90, 0),
            2 => new(80, 0, 0, 90, 90, 14),
            3 => new(85, 0, 0, 0, 90, 12),
            4 => new(85, 0, 0, 0, 0, 12),
            _ => new(0, 0, 0, 0, 0, 18),
        },
        _ => null,
    };

    /// <summary>Degrees `@bent` bends an elbow or a knee when it has no number, and the most it can.</summary>
    public const double DefaultBend = 90, MaxBend = 150;

    /// <summary>Places on another figure: `b.hand`. For an arm (reach it) or the head (look at it).</summary>
    public static readonly Word[] Parts =
    [
        new("hand", "The other dancer's hand of the same side. If it reaches back, the two hands meet half-way, at the height of a handshake."),
        new("leftHand", "The other dancer's left hand, whichever arm reaches: `leftArm { b.rightHand.low }` beside b is hand in hand. If that hand reaches back for this one, the two meet half-way."),
        new("rightHand", "The other dancer's right hand."),
        new("shoulder", "The other dancer's shoulder of the same side."),
        new("head", "The other dancer's head."),
        new("chest", "The other dancer's chest."),
    ];

    /// <summary>
    /// In place of a role, in an arm's row: whoever stands next to this dancer on that arm's side, of any role
    /// (`leftArm { neighbor.hand }`). A role many dance can say it (DESIGN.md 134).
    /// </summary>
    public const string Neighbor = "neighbor";

    public static readonly Word NeighborWord = new(Neighbor, "Whoever stands next to the dancer on this arm's side, of any role: `neighbor.hand` (hand in hand, beside the hips; `.mid`, `.high`), `neighbor.shoulder`. With nobody on that side the arm hangs (`.high`: it goes up, open).");

    /// <summary>Places on the neighbour: `neighbor.hand`, `neighbor.shoulder`.</summary>
    public static readonly Word[] NeighborParts =
    [
        new("hand", "The neighbour's hand on this side (the inner hands of two side by side). If it reaches back (`neighbor.hand` too, or this dancer's hand by its role), the two meet half-way, beside the hips; `.mid` at the shoulders, `.high` above the heads."),
        new("shoulder", "The neighbour's shoulder on this side: a hand on it."),
    ];

    /// <summary>The side of the other dancer's hand a reach names, for an arm of <paramref name="side"/> (1 left, -1 right); 0 when the part is not a hand.</summary>
    public static int HandSide(string? part, int side, bool neighbor = false) => part switch
    {
        "hand" => neighbor ? -side : side,
        "leftHand" => 1,
        "rightHand" => -1,
        _ => 0,
    };

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
        new("across", "Crossing over the supporting foot's line, 0.08 m past it (`across(0.13m)`: how far the feet cross, as `side` says how far apart), just in front of it (0.12 m; with `back`, just behind). How far in front is said by `fwd`: `fwd(0.08m).across(0.13m)`."),
        new("place", "Beside the supporting foot, at standing width: a step on the spot. After a step across or behind it is beside THAT foot, so the stance moves (a balancé's third step is `rightFoot.fwd(0.08m)` after `leftFoot.back(0.08m)`, not `place`)."),
    ];

    public static readonly Word[] SupportActions =
    [
        new("together", "The free foot closes to the supporting foot, touching (turned out, the heels touch); the weight is on both."),
        new("stand", "The free foot closes to standing width (0.2 m, ankle to ankle); the weight is on both."),
        new("turn", "A pivot on the supporting foot: `turn.left(90)`, `turn.right(180)` (degrees, up to 1080)."),
        new("jump", "Off the floor, landing on both feet: `jump`, `jump.fwd`, `jump.left(0.6m)`, `jump.apart`, `jump.stand`, `jump.together`."),
        new("hop", "Off the floor from the foot that carries the weight, landing on the same foot: `hop`, `hop.fwd`. The other foot is held off the floor. From both feet, it lands on the foot whose leg its row does not throw out (a sissonne)."),
        new("leap", "Off the floor from the foot that carries the weight, landing on the other: `leap.fwd` (a running step), `leap.left` (a small jump to the side). The foot it took off from is held off the floor."),
        new("shift", "The weight moves and neither foot leaves the floor: `shift.left`, `shift.right`, `shift.center`."),
        new("go", "Walk to the dancer's own place in a formation, and stand there on the beat: `go.vee` (`formation vee { alice at -1 0  bob at 1 0 }`), `go.home` (its `at`). The steps are made for it, and it stands facing as it faced."),
        new("lift", "Only for a free body (`@free`, or `free` on the dancer): the feet hang at a height, `lift(0.4m)`. `lift(0.4m).fwd` also travels (0.4 m, or `fwd(0.6m)`)."),
        new("land", "Only for a free body: back down to the floor."),
    ];

    /// <summary>
    /// What carries the weight when the feet do not (DESIGN.md 128): words of the `steps` row, each put on the floor from
    /// where the weight was (`hands.fwd(0.5m)`), and `feet` to stand again. A part of the body is on the floor there, and
    /// the torso's row says how the body lies from it.
    /// </summary>
    public static readonly Word[] Bases =
    [
        new("hands", "The weight on both hands, a shoulder's width apart on the floor, the shoulders over them: a handstand (`torso { down }`), a plank (`torso { fwd.up(15) }`), a freeze, the hands of floor footwork. `hands.fwd(0.5m)` puts them half a metre ahead of where the weight was. The arms are straight, or bent as their row says (`arms { down.bent(90) }`)."),
        new("leftHand", "The weight on the left hand alone, the left shoulder over it; the right arm is its row's."),
        new("rightHand", "The weight on the right hand alone."),
        new("head", "The weight on the top of the head (a headstand, a headspin: `torso { down }`); the hands may help (`arms { side.floor }`)."),
        new("back", "Lying on the upper back: `torso { back }` flat on the back, `torso { back.down(25) }` rolled up onto the shoulders, the hips in the air (a backspin)."),
        new("front", "Lying on the chest, face down: `torso { fwd }` flat (the worm's low point), `torso { fwd.down(20) }` the hips up."),
        new("seat", "Sitting on the floor: the hips on it, the torso upright or as its row says (`torso { back }` lies down), the legs out in front unless their rows say (`legs { fwd.floor.bent(90) }`: the knees up)."),
        new("knees", "Kneeling on both knees, the shins on the floor behind, the hips over the knees; the `height` row sits back toward the heels (`low(30cm)`)."),
        new("leftKnee", "Kneeling on the left knee, the right foot on the floor in front (a knee drop, a proposal)."),
        new("rightKnee", "Kneeling on the right knee, the left foot on the floor in front."),
        new("feet", "Back on the feet, standing where the weight was (`feet.back(0.5m)`: half a metre behind it), at standing width."),
    ];

    /// <summary>What follows the formation's name in a `go` (DESIGN.md 132): `go.vee.front`, `go.vee.steps(8)`.</summary>
    public static readonly Word[] GoWords =
    [
        new("front", "The way there bows toward the audience, to pass in front of someone: 0.5 m at its middle, or `front(0.8m)`."),
        new("back", "The way there bows upstage, to pass behind someone: `back(0.8m)`."),
        new("right", "The way there bows to the walker's own right as it walks (not the stage's): 0.5 m at its middle, or `right(0.8m)`. Everybody of a formation walking to the next place with the same word goes round one circle."),
        new("left", "The way there bows to the walker's own left: `left(0.8m)`; the circle the other way round."),
        new("steps", "How many steps it takes, the last one closing the feet: `go.vee.steps(8)` (2 to 64). Not written: one a beat, or two a beat to a place far away."),
    ];

    /// <summary>The formation every file has: each dancer at its own `at` (`go.home`).</summary>
    public const string Home = "home";

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
        new("up", "How high the body rises, always with the height: `hop.up(5cm)`, `jump.fwd(0.3m).up(20cm)`. It takes the time that height needs and waits on the floor through the rest of the word before. Not written, the time before it decides."),
        new("apart", "For a jump: land with the feet 0.6 m apart, or `apart(0.8m)`."),
        new("stand", "For a jump: land with the feet at standing width."),
        new("together", "For a jump: land with the feet touching (turned out, the heels)."),
        new("change", "For a jump: land with the feet as they stood, mirrored left for right: the foot that was behind lands in front, as far ahead and as crossed (a changement: fifth, right foot front, lands in fifth, left foot front)."),
        new("beat", "For a jump: the legs beat in the air. They close crossed the other way round from how they land, and open again to land: `jump.beat` from fifth is an entrechat quatre (it lands as it stood), `jump.change.beat` a royale. `beat(2)` beats twice, each time the other way round (`jump.change.beat(2)`: an entrechat six); up to 3."),
        new("leftFoot", "For a jump, last: the stance it lands in, as a step of that foot from the other would stand: `jump.rightFoot.fwd(0.08m).across(0.13m)` lands in fifth with the right foot in front, from wherever the feet were (an échappé closing from second). Where it lands (`fwd`, `left`, `up(…)`) is written before the foot."),
        new("rightFoot", "For a jump, last: the stance it lands in, named by the right foot (see `leftFoot`)."),
    ];

    /// <summary>What the `quality` row says: the marks that say how a part gets to its word, as words (DESIGN.md 130). A new one is a mark and a quality at once.</summary>
    public static readonly string[] Qualities = ["smooth", "sharp", "linear", "through"];

    /// <summary>The most a turn says in one word, degrees: a triple turn.</summary>
    public const double MaxTurn = 1080;

    /// <summary>How the part gets to its word, and what is done to the word: never the shape it arrives in (that is the target's).</summary>
    public static readonly Word[] Annotations =
    [
        new("smooth", "The way TO this word eases in and out (the default)."),
        new("sharp", "Go at the last moment: the part waits, then gets there in no more than 0.15 s before its beat. An accent — a punch, a clap. Fast, so it can be too fast (SH2003)."),
        new("linear", "The way TO this word is at constant speed: no slowing as it arrives."),
        new("through", "Pass this word without stopping: the way to it arrives at speed and the way on to the next word leaves at that speed, the two one curve (no corner at the word) — a point an arm goes through on an arc (`rightArm { down side:1@through up:1 }`), a wave. On a word the part stops at (the last, or before `then`) it says nothing (SH2017). Not for steps."),
        new("mirror", "Left and right swapped: `kick:2@mirror` kicks with the other leg."),
        new("free", "The rules of weight, gravity and speed do not apply to this event. Bones still keep their length."),
        new("turn", "For a step or a jump: turn while doing it, `leftFoot.fwd@turn(left(90))`, `jump@turn(right(180))`. The step lands where it would after the turn."),
    ];

    /// <summary>
    /// Juggling tricks, put on a pattern with `@` (DESIGN.md 118.12): how the hands throw and catch it. What is thrown
    /// (the siteswap) does not change. Every throw of the word; one throw is a word of one throw (`3:1/2@underThrow`).
    /// </summary>
    public static readonly Word[] Tricks =
    [
        new("mills", "Mills' Mess: six throws round. The arms crossed, the right hand over throws from the left side in; the arms uncrossing, the left hand throws; crossing the other way, the right hand under throws from low on the left. The three go up from left to right; then the same mirrored. Best on 3."),
        new("windmill", "Windmill: the first hand's arm always on top, every ball thrown on that hand's side and caught on the other, all one way round. That hand throws over the top from its own side and catches crossed over, high on the other side; the other hand catches on its own side and throws from under the top arm. Best on 3. `@mirror` puts the left arm on top."),
        new("penguinCatch", "Penguin catch: caught behind the hip, the arm straight and the palm turned back; thrown in front, palm up, above the shoulders."),
        new("clawCatch", "Claw catch: the palm down, the ball thrown up to the face and snatched from above there, near its top. The catching hand has to come up to the face after its own throw and bring the ball down again: at `every 1/2` it suits a tempo of about 113 to 197 (SH2023 says when not)."),
        new("reverse", "Reverse: thrown from outside, over the top, and caught inside: the cascade turned the other way."),
        new("tennis", "Tennis: one ball (the word's first thrown across) thrown from well outside and higher, over the top of the others, to and fro like a tennis ball over the net; the other balls go on as a cascade from nearer the middle and a little lower. Best on 3."),
        new("halfShower", "Half-shower: the first hand (the right; `@mirror` the left) always throws from well outside, higher, over the top; the other always from inside, lower, under it. Best on 3."),
        new("takeouts", "Takeouts, on 423: a hand throws its 3 from its own side to the other hand as it sets off on a quick swipe across in front, takes its own 4 from above near the middle as it falls and carries it on to the far side, stops once there and carries it round a big circle, the ball just before the forehead, out past its own side and down below, up into its next 4 thrown from inside (the 2); under that circle the other hand throws its own 4 up near the middle. The hands take turns. Written on over a bar line (`423:4~@takeouts | :2 …`), as each word starts 423 again."),
        new("backThrow", "Thrown behind the back: the hand throws from behind the waist, over the other shoulder, the shoulders turned well round. Throw it high enough to clear the shoulder (a 5)."),
        new("backCatch", "Caught behind the back: the ball comes down over the shoulder it was thrown on, and the hand reaches behind the waist to meet it, the shoulders turned well round. Throw it high enough to come over the shoulder (a 5)."),
        new("underThrow", "Thrown under the leg: the hand throws from under the thigh of its own side. Lift that leg in its row at that moment (`rightLeg { fwd.bent }`: the thigh level, the knee bent); one throw is a word of its own (`3:1/2@underThrow`)."),
    ];

    public const string Hold = "then";
    /// <summary>For an arm: as `then`, but the hand keeps its place in the room.</summary>
    public const string Stay = "stay";
    public static readonly Word StayWord = new(Stay, "For an arm: as `then`, but the hand stays where it is in the room (on a counter, a rail) and the arm follows it while the body leans, bows or sinks. It is timed as `then`: in `rightArm { fwd.down(40).bent(30):4 stay:1 down }` the hand stays in the room until beat 4 and goes down in beat 5. Out of reach it is a warning (SH2015).");
    public const string Skip = "skip";
    /// <summary>`q`: the group in braces before it, again.</summary>
    public const string Again = "q";

    /// <summary>Every word that means something on its own, so that a definition cannot take its name.</summary>
    public static IEnumerable<string> Reserved =>
        new[] { Tracks, Directions, Levels, Poles, Anchors, LegAnchors, Shapes, Feet, StepDirections, SupportActions, JumpWords, ShouldersWords, HipsWords, TurnoutWords, Bases }
            .SelectMany(words => words).Select(w => w.Name).Append(Hold).Append(Stay).Append(Skip).Append(Again);

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
