namespace Shosa.Core.Motion;

/// <summary>
/// One bone of the skeleton that leaves Shosa: where it sits from its parent in the rest pose,
/// and which point of <see cref="Rig.Joints"/> it starts at (and ends at, for a leaf).
/// </summary>
public sealed record SkeletonBone(string Name, int Parent, Vec3 Offset, int Joint = -1, Vec3? End = null, int EndJoint = -1);

/// <summary>
/// The humanoid as a hierarchy of bones with rotations, which is what BVH, glTF and every
/// game engine want; the solver itself thinks in joint positions.
///
/// The rest pose is a T-pose facing +z with +x to the figure's left, and the bones carry the
/// names of the VRM humanoid, so the same table serves glTF and <c>.vrma</c>.
///
/// The spine starts exactly at the hips (a bone of no length): the solver leans the torso
/// about the pelvis point while the hip sockets stay level, and only this reproduces it.
///
/// The shoulders (collar bones) never turn: the solver has no joint there. They are in the
/// table because a character has them. An engine that keeps a rotation relative to the parent
/// bone's own axes (Godot's retargeting) plays the upper arm wrong on such a character when
/// the animation's upper arm hangs from the chest instead.
/// </summary>
public static class Skeleton
{
    private static readonly Vec3 X = new(1, 0, 0), Z = new(0, 0, 1);
    private static readonly Vec3 FootRest = new(0, -0.06, Rig.FootLength);
    private const double HandLength = 0.08, NoseOut = 0.12, CollarHalf = 0.03;
    /// <summary>How far round a palm is turned the thumb's way in (pronated) to face where its row sends it, radians: further is nearer the other way round.</summary>
    private const double PalmTurnsIn = 120 * Math.PI / 180;

    public const int Hips = 0, Spine = 1, Chest = 2, Neck = 3, Head = 4,
        LShoulder = 5, LUpperArm = 6, LLowerArm = 7, LHand = 8, RShoulder = 9, RUpperArm = 10, RLowerArm = 11, RHand = 12,
        LUpperLeg = 13, LLowerLeg = 14, LFoot = 15, RUpperLeg = 16, RLowerLeg = 17, RFoot = 18;

    public static readonly SkeletonBone[] Bones =
    [
        new("hips", -1, new Vec3(0, Rig.AnkleHeight + Rig.Leg, 0), Rig.Pelvis),
        new("spine", Hips, default, Rig.Pelvis),
        new("chest", Spine, new Vec3(0, Rig.Spine / 2, 0), Rig.Waist),
        new("neck", Chest, new Vec3(0, Rig.Spine / 2, 0), Rig.Chest),
        new("head", Neck, new Vec3(0, Rig.HeadUp, 0), Rig.Head, new Vec3(0, 0, NoseOut), Rig.Nose),

        new("leftShoulder", Chest, new Vec3(CollarHalf, Rig.Spine / 2 - Rig.ShoulderDrop, 0)),
        new("leftUpperArm", LShoulder, new Vec3(Rig.ShoulderHalf - CollarHalf, 0, 0), Rig.LShoulder),
        new("leftLowerArm", LUpperArm, new Vec3(Rig.UpperArm, 0, 0), Rig.LElbow),
        new("leftHand", LLowerArm, new Vec3(Rig.Forearm, 0, 0), Rig.LHand, new Vec3(HandLength, 0, 0)),
        new("rightShoulder", Chest, new Vec3(-CollarHalf, Rig.Spine / 2 - Rig.ShoulderDrop, 0)),
        new("rightUpperArm", RShoulder, new Vec3(-(Rig.ShoulderHalf - CollarHalf), 0, 0), Rig.RShoulder),
        new("rightLowerArm", RUpperArm, new Vec3(-Rig.UpperArm, 0, 0), Rig.RElbow),
        new("rightHand", RLowerArm, new Vec3(-Rig.Forearm, 0, 0), Rig.RHand, new Vec3(-HandLength, 0, 0)),

        new("leftUpperLeg", Hips, new Vec3(Rig.HipHalf, 0, 0), Rig.LHip),
        new("leftLowerLeg", LUpperLeg, new Vec3(0, -Rig.Thigh, 0), Rig.LKnee),
        new("leftFoot", LLowerLeg, new Vec3(0, -Rig.Shin, 0), Rig.LAnkle, FootRest, Rig.LToe),
        new("rightUpperLeg", Hips, new Vec3(-Rig.HipHalf, 0, 0), Rig.RHip),
        new("rightLowerLeg", RUpperLeg, new Vec3(0, -Rig.Thigh, 0), Rig.RKnee),
        new("rightFoot", RLowerLeg, new Vec3(0, -Rig.Shin, 0), Rig.RAnkle, FootRest, Rig.RToe),
    ];

    // ---- The fingers (DESIGN.md 126). The solver has no joints for them: a hand's row says how far each is closed,
    // and the bones are turned from that. They come after <see cref="Bones"/>, the left hand's fifteen and then the
    // right's, under the VRM humanoid's names, and only in a file whose dancers write a hand's row.

    /// <summary>The fingers in the order a hand's shape counts them, and the three bones of each from the palm out.</summary>
    private static readonly string[] FingerNames = ["Thumb", "Index", "Middle", "Ring", "Little"];
    private static readonly string[][] FingerJoints = [["Metacarpal", "Proximal", "Distal"], ["Proximal", "Intermediate", "Distal"]];
    /// <summary>Where each finger starts from the wrist of a left hand at rest (the fingers along +x, the thumb's side +z), and how long its three bones are.</summary>
    private static readonly (Vec3 Root, double[] Lengths)[] FingerRest =
    [
        (new Vec3(0.025, -0.005, 0.020), [0.040, 0.030, 0.025]),
        (new Vec3(0.085, 0, 0.025), [0.040, 0.025, 0.020]),
        (new Vec3(0.088, 0, 0.005), [0.045, 0.028, 0.020]),
        (new Vec3(0.082, 0, -0.014), [0.041, 0.026, 0.020]),
        (new Vec3(0.073, 0, -0.031), [0.032, 0.019, 0.017]),
    ];
    /// <summary>The way a left thumb points at rest: out of the palm's edge, forward of the fingers.</summary>
    private static readonly Vec3 ThumbRest = new(0.6, 0, 0.8);
    /// <summary>Degrees each bone of a finger turns closed into the fist, from the palm out; and of the thumb, down under the palm and in toward the fingers.</summary>
    private static readonly double[] FingerCurl = [80, 95, 65];
    private const double ThumbUnder = 40, ThumbIn = 25, ThumbMiddle = 45, ThumbTip = 50;
    /// <summary>How far each finger goes to the thumb's side as the hand spreads, in shares of the degrees between two fingers.</summary>
    private static readonly double[] SpreadShare = [0, 1, 0, -1, -2];

    /// <summary>The bones of the fingers: fifteen of the left hand, then fifteen of the right, their parents numbered as in <see cref="AllBones"/>.</summary>
    public static readonly SkeletonBone[] FingerBones = MakeFingers();

    /// <summary><see cref="Bones"/> and then <see cref="FingerBones"/>: the skeleton of a file whose hands are written.</summary>
    public static readonly SkeletonBone[] AllBones = [.. Bones, .. FingerBones];

    /// <summary>The skeleton a file is written with: with the fingers when a hand's row is danced in it.</summary>
    public static SkeletonBone[] BonesWith(bool fingers) => fingers ? AllBones : Bones;

    private static SkeletonBone[] MakeFingers()
    {
        var made = new List<SkeletonBone>();
        for (int side = 1; side >= -1; side -= 2)
        {
            int hand = side == 1 ? LHand : RHand;
            string prefix = side == 1 ? "left" : "right";
            for (int finger = 0; finger < 5; finger++)
            {
                var (root, lengths) = FingerRest[finger];
                var way = finger == 0 ? ThumbRest.Normalized() : X;
                way = new Vec3(way.X * side, way.Y, way.Z);
                for (int joint = 0; joint < 3; joint++)
                {
                    int parent = joint == 0 ? hand : Bones.Length + made.Count - 1;
                    var offset = joint == 0 ? new Vec3(root.X * side, root.Y, root.Z) : way * lengths[joint - 1];
                    made.Add(new SkeletonBone(prefix + FingerNames[finger] + FingerJoints[finger == 0 ? 0 : 1][joint], parent, offset, End: joint == 2 ? way * lengths[2] : null));
                }
            }
        }
        return [.. made];
    }

    /// <summary>What a hand nobody wrote is: the relaxed hand, for both (<see cref="Semantics.Vocabulary.ShapeOfHand"/>).</summary>
    public static readonly float[] RelaxedHands = MakeRelaxed();

    private static float[] MakeRelaxed()
    {
        var shape = Semantics.Vocabulary.ShapeOfHand("relaxed")!.Value;
        float[] one = [(float)(shape.Thumb / 90), (float)(shape.Index / 90), (float)(shape.Middle / 90), (float)(shape.Ring / 90), (float)(shape.Little / 90), (float)shape.Spread];
        return [.. one, .. one];
    }

    /// <summary>
    /// How a bone of the fingers is turned from its parent: <paramref name="bone"/> counts <see cref="FingerBones"/>, and
    /// <paramref name="hands"/> is a frame of <see cref="ClipFigure.Hands"/> (for each hand, the left first: how far the
    /// thumb and the four fingers are closed, 0..1, and the degrees between two fingers). The fingers close about the
    /// knuckles' line, down toward the palm; the thumb goes down under the palm and in across it.
    /// </summary>
    public static Quat Finger(int bone, ReadOnlySpan<float> hands)
    {
        int side = bone < 15 ? 1 : -1, finger = bone % 15 / 3, joint = bone % 3, at = bone < 15 ? 0 : 6;
        double closed = Math.Clamp(hands[at + finger], 0, 1), spread = hands[at + 5];
        const double rad = Math.PI / 180;
        if (finger == 0)
        {
            // (About x the thumb goes down from the palm's plane on both hands; about y, in toward the fingers.)
            return joint switch
            {
                0 => About(Vec3.Up, side * closed * ThumbIn * rad) * About(X, closed * ThumbUnder * rad),
                1 => About(Vec3.Up, side * closed * ThumbMiddle * rad),
                _ => About(Vec3.Up, side * closed * ThumbTip * rad),
            };
        }
        var curl = About(Z, -side * closed * FingerCurl[joint] * rad);
        // A closed finger lies against its neighbours whatever the hand's spread.
        return joint == 0 ? About(Vec3.Up, -side * SpreadShare[finger] * spread * (1 - closed) * rad) * curl : curl;
    }

    private static Quat About(Vec3 axis, double angle)
    {
        double s = Math.Sin(angle / 2);
        return new Quat(axis.X * s, axis.Y * s, axis.Z * s, Math.Cos(angle / 2));
    }

    /// <summary>
    /// How a hand's row turns the hand on its wrist (`fingers(up)`, `palm(fwd)`), in the room: where the fingers point
    /// and the palm faces, and how much each is said (0 not at all: as the arm has it; 1 wholly, between two words in between).
    /// </summary>
    public readonly record struct HandTurn(Vec3 Fingers, double FingersShare, Vec3 Palm, double PalmShare)
    {
        public bool Says => FingersShare > 0 || PalmShare > 0;
    }

    /// <summary>
    /// The rotation of every bone relative to its parent, read off the joint positions of one
    /// pose. Positions do not say how a limb is twisted about its own length; the plane in
    /// which the elbow or knee bends does, so the hinge axis is taken from there.
    /// </summary>
    public static Quat[] Rotations(Vec3[] joints)
    {
        var local = new Quat[Bones.Length];
        Rotations(joints, local);
        return local;
    }

    /// <summary>The same, into <paramref name="local"/> (one for each of <see cref="Bones"/>): a clip asks for every frame of every figure.</summary>
    public static void Rotations(Vec3[] joints, Span<Quat> local) => Rotations(joints, local, default, default);

    /// <summary>The same, the hands turned on their wrists as their rows say (DESIGN.md 126).</summary>
    /// <param name="hipsUp">Off the feet (DESIGN.md 128), which way is up for the hips: tipped with the body on the hands, the head, the back. Null: level, as on the feet.</param>
    public static void Rotations(Vec3[] joints, Span<Quat> local, HandTurn leftHand, HandTurn rightHand, Vec3? hipsUp = null)
    {
        Span<Quat> world = stackalloc Quat[Bones.Length];

        // Hips: level, facing square to the line between the hip sockets.
        var fwd = (joints[Rig.LHip] - joints[Rig.RHip]).Cross(Vec3.Up).Normalized();
        var left = Vec3.Up.Cross(fwd);
        // What is up for the legs: the room's, or a tipped body's own.
        var bodyUp = Vec3.Up;
        if (hipsUp is { } tipped && (joints[Rig.LHip] - joints[Rig.RHip]) is var hipLine && hipLine.Length > 1e-6)
        {
            left = hipLine.Normalized();
            var up = tipped - left * tipped.Dot(left);
            if (up.Length > 1e-6)
            {
                bodyUp = up.Normalized();
                fwd = left.Cross(bodyUp);
            }
            else
                left = Vec3.Up.Cross(fwd);
        }
        world[Hips] = Quat.FromBasis(left, bodyUp, fwd);

        // The torso is two pieces, the spine below the waist and the chest above it; the neck and
        // the shoulders follow the chest. With a straight back the two are one.
        var across = joints[Rig.LShoulder] - joints[Rig.RShoulder];
        Quat Piece(Vec3 up)
        {
            var pieceLeft = (across - up * across.Dot(up)).Normalized();
            return Quat.FromBasis(pieceLeft, up, pieceLeft.Cross(up));
        }

        var torsoUp = (joints[Rig.Chest] - joints[Rig.Waist]).Normalized();
        var torsoLeft = (across - torsoUp * across.Dot(torsoUp)).Normalized();
        world[Chest] = world[LShoulder] = world[RShoulder] = Piece(torsoUp);

        // A twist is the shoulder line turned about the spine, away from where the hips put it
        // (the hip line, square to the line from the pelvis to the chest: a rounded back bows about
        // that, so it is square to both pieces). The spine takes half of the turn, the chest all
        // of it, and the neck none: the head is not turned by a twist.
        var line = (joints[Rig.Chest] - joints[Rig.Pelvis]).Normalized();
        var plain = left - line * left.Dot(line);
        if (plain.Length < 1e-6)
        {
            world[Spine] = Piece((joints[Rig.Waist] - joints[Rig.Pelvis]).Normalized());
            world[Neck] = world[Chest];
        }
        else
        {
            plain = plain.Normalized();
            double twist = Math.Atan2(plain.Cross(torsoLeft).Dot(torsoUp), plain.Dot(torsoLeft));
            var spineUp = (joints[Rig.Waist] - joints[Rig.Pelvis]).Normalized();
            var spineLeft = plain * Math.Cos(twist / 2) + spineUp.Cross(plain) * Math.Sin(twist / 2);
            world[Spine] = Quat.FromBasis(spineLeft, spineUp, spineLeft.Cross(spineUp));
            world[Neck] = Quat.FromBasis(plain, torsoUp, plain.Cross(torsoUp));
        }

        // A tilted head (`tilt`) bends the neck sideways: the neck turns from the torso's up to the
        // line from the chest to the head, the least turn that does it. Untilted, that line is the torso's up.
        var neckUp = (joints[Rig.Head] - joints[Rig.Chest]).Normalized();
        if (neckUp.Dot(torsoUp) < 1 - 1e-9)
            world[Neck] = Arc(torsoUp, neckUp) * world[Neck];

        // The head looks along the nose and stays upright on the neck.
        var gaze = (joints[Rig.Nose] - joints[Rig.Head]).Normalized();
        var headLeft = neckUp.Cross(gaze);
        headLeft = headLeft.Length < 1e-6 ? torsoLeft : headLeft.Normalized();
        world[Head] = Quat.FromBasis(headLeft, gaze.Cross(headLeft), gaze);

        // Two hands near each other (a clap, palms together) do not go on along the forearms: the forearms
        // come in from the shoulders at a slant, and the hands carried on would run into each other. From
        // 25 cm apart to 10 cm the hands turn until they lie side by side, palm to palm (DESIGN.md 95).
        var between = joints[Rig.RHand] - joints[Rig.LHand];
        double apart = between.Length;
        double near = apart >= 0.25 || apart < 1e-6 ? 0 : apart <= 0.10 ? 1 : Smooth((0.25 - apart) / 0.15);

        for (int side = 1; side >= -1; side -= 2)
        {
            int arm = side == 1 ? LUpperArm : RUpperArm, shoulder = side == 1 ? Rig.LShoulder : Rig.RShoulder;
            // In the T-pose an elbow folds the forearm forward: about -y on the left, +y on the right.
            var (upper, lower) = Limb(joints[shoulder], joints[shoulder + 1], joints[shoulder + 2],
                X * side, Vec3.Up * -side, Rig.ElbowPole(left, fwd, side));
            world[arm] = upper;
            world[arm + 1] = world[arm + 2] = lower;
            if (near > 0)
            {
                // Toward the other hand; the hand's way is the forearm's with that taken out.
                var toOther = between * (side / apart);
                var forearm = (joints[shoulder + 2] - joints[shoulder + 1]).Normalized();
                var level = forearm - toOther * forearm.Dot(toOther);
                if (level.Length > 1e-6)
                {
                    var way = (forearm + (level.Normalized() - forearm) * near).Normalized();
                    // The palm (down in the T-pose: along the elbow's hinge on the left, against it on the right)
                    // turns about the hand's way from where the forearm has it to the other hand, as far as the
                    // hands are near, and only as far as it already faces that way: from a palm square to the
                    // other hand or turned away (arms coming down overhead) either way round is as good, and the
                    // choice flipped the hand from one frame to the next.
                    var hinge = lower.Rotate(Vec3.Up * -side);
                    var from = hinge - way * hinge.Dot(way);
                    var to = toOther * side - way * (toOther * side).Dot(way);
                    if (from.Length > 1e-6 && to.Length > 1e-6)
                    {
                        from = from.Normalized();
                        to = to.Normalized();
                        double facing = Math.Max(0, from.Dot(to));
                        double turn = Math.Atan2(way.Dot(from.Cross(to)), from.Dot(to)) * near * facing;
                        var palm = from * Math.Cos(turn) + way.Cross(from) * Math.Sin(turn);
                        world[arm + 2] = Quat.Between(X * side, Vec3.Up * -side, way, palm);
                    }
                }
            }

            // A hand its row turns (`fingers(up)`, `palm(fwd)`): the wrist bends to point the fingers there, no further
            // from the forearm than a wrist goes, and the hand turns about its fingers to face the palm there. What
            // the row does not say stays as the arm has it: the fingers along the forearm, the palm as the elbow lies.
            var wrist = side == 1 ? leftHand : rightHand;
            if (wrist.Says)
            {
                var forearm = (joints[shoulder + 2] - joints[shoulder + 1]).Normalized();
                var hand = world[arm + 2];
                var fingers = hand.Rotate(X * side);
                if (wrist.FingersShare > 0 && wrist.Fingers.Length > 1e-6)
                {
                    var want = wrist.Fingers.Normalized();
                    double most = Semantics.Vocabulary.MaxWrist * Math.PI / 180;
                    double off = Math.Acos(Math.Clamp(forearm.Dot(want), -1, 1));
                    if (off > most)
                    {
                        var square = want - forearm * want.Dot(forearm);
                        want = square.Length < 1e-6 ? forearm : forearm * Math.Cos(most) + square.Normalized() * Math.Sin(most);
                    }
                    fingers = Vec3.Slerp(fingers, want, wrist.FingersShare);
                }
                // The palm as it lies, carried onto the fingers' new way; then turned about them to where it is sent.
                var palm = hand.Rotate(-Vec3.Up);
                palm -= fingers * palm.Dot(fingers);
                palm = palm.Length < 1e-6 ? hand.Rotate(Z) : palm.Normalized();
                if (wrist.PalmShare > 0)
                {
                    var want = wrist.Palm - fingers * wrist.Palm.Dot(fingers);
                    double square = wrist.Palm.Length > 1e-6 ? want.Length / wrist.Palm.Length : 0;
                    if (want.Length > 1e-6)
                    {
                        want = want.Normalized();
                        // The short way round, but a palm sent to face the other way (down to up, which half said is a
                        // quarter turn) always the same way round, the thumb going out: either is as short, and which it
                        // was changed from one frame to the next (48 degrees in a frame, a palm coming up a little as
                        // the arm went down to the floor). The two ways part where no forearm turns: 120 degrees thumb in.
                        double angle = Math.Atan2(fingers.Dot(palm.Cross(want)), palm.Dot(want));
                        if (angle * side > PalmTurnsIn)
                            angle -= 2 * Math.PI * side;
                        // Fingers that point where the palm is sent leave it no way to face there (SH2011 says so of a word;
                        // on the way from one word to the next the fingers can pass through it): the palm is turned less
                        // the nearer they are, within 30 degrees, and not at all there. (Turned in full, it faced up on
                        // one side of that way and down on the other: 46 degrees in a frame.)
                        angle *= wrist.PalmShare * Smooth(Math.Min(1, square / 0.5));
                        palm = palm * Math.Cos(angle) + fingers.Cross(palm) * Math.Sin(angle);
                    }
                }
                world[arm + 2] = Quat.Between(X * side, -Vec3.Up, fingers, palm);
            }

            int leg = side == 1 ? LUpperLeg : RUpperLeg, hip = side == 1 ? Rig.LHip : Rig.RHip;
            // A knee folds the shin back: about +x on both sides. It bends the way its foot points
            // (as the solver bends it), so a straight leg turned out is turned in the thigh too.
            var toes = joints[hip + 3] - joints[hip + 2];
            var footFwd = new Vec3(toes.X, 0, toes.Z);
            footFwd = footFwd.Length < 1e-6 ? fwd : footFwd.Normalized();
            // (A tipped body's straight leg would bend the way its hips face: its feet do not stand on the floor to say.)
            (upper, lower) = Limb(joints[hip], joints[hip + 1], joints[hip + 2], -Vec3.Up, X,
                hipsUp is null ? Rig.KneePole(Vec3.Up.Cross(footFwd), footFwd, side) : fwd + left * (0.15 * side));
            world[leg] = upper;
            world[leg + 1] = lower;

            // The foot points at the toe and lies flat from side to side (off the feet: along the line of the hips).
            var along = (joints[hip + 3] - joints[hip + 2]).Normalized();
            var footLeft = Vec3.Up.Cross(along);
            if (hipsUp is not null)
            {
                // Off the feet a foot may point anywhere (up, on along a leg held overhead): flat to the line of the
                // hips, which is where a foot standing square lies too; pointing along that line, to the knee's own hinge.
                // (The hinge alone turned over from one frame to the next as a leg went straight: 104 degrees.)
                // (From the one to the other as the foot comes to point along the line, between some 45 and 6 degrees
                // from it: taken all at once, where the two differ — a leg lying out to the side on the floor — the foot
                // turned over in a frame, 144 degrees.)
                footLeft = left - along * left.Dot(along);
                if (footLeft.Length < 0.7)
                {
                    var hinge = lower.Rotate(X);
                    var hinged = hinge - along * hinge.Dot(along);
                    double own = Smooth(Math.Clamp((footLeft.Length - 0.1) / 0.6, 0, 1));
                    footLeft = own <= 0 || footLeft.Length < 1e-6 || hinged.Length < 1e-6 ? hinged
                        : Vec3.Slerp(hinged.Normalized(), footLeft.Normalized(), own);
                }
            }
            footLeft = footLeft.Length < 1e-6 ? left : footLeft.Normalized();
            if (hipsUp is not null && Math.Abs(footLeft.Dot(along)) > 1e-6)
            {
                // (The fallback is not square to the foot: any way that is.)
                footLeft = (Math.Abs(along.Y) < 0.9 ? Vec3.Up : Z).Cross(along).Normalized();
            }
            world[leg + 2] = Quat.Between(FootRest.Normalized(), X, along, footLeft);
        }

        for (int i = 0; i < Bones.Length; i++)
            local[i] = Bones[i].Parent < 0 ? world[i] : world[Bones[i].Parent].Inverse * world[i];
    }

    private static double Smooth(double u) => u * u * (3 - 2 * u);

    /// <summary>The least rotation that takes one unit vector to another.</summary>
    private static Quat Arc(Vec3 from, Vec3 to)
    {
        var axis = from.Cross(to);
        double w = 1 + from.Dot(to);
        double length = Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z + w * w);
        return length < 1e-9 ? Quat.Identity : new Quat(axis.X / length, axis.Y / length, axis.Z / length, w / length);
    }

    private static (Quat Upper, Quat Lower) Limb(Vec3 root, Vec3 mid, Vec3 end, Vec3 restAxis, Vec3 restHinge, Vec3 pole)
    {
        Vec3 upper = (mid - root).Normalized(), lower = (end - mid).Normalized();
        var hinge = upper.Cross(lower);
        // A straight limb has no bending plane: use the one it would bend in.
        if (hinge.Length < 1e-6)
            hinge = pole.Cross(upper);
        if (hinge.Length < 1e-6)
            hinge = (Math.Abs(upper.Y) < 0.9 ? Vec3.Up : Z).Cross(upper);
        hinge = hinge.Normalized();
        return (Quat.Between(restAxis, restHinge, upper, hinge), Quat.Between(restAxis, restHinge, lower, hinge));
    }

    /// <summary>
    /// Forward kinematics: where every bone starts in the world, given the hips' position and
    /// the rotations. The inverse of <see cref="Rotations"/>, and what a reader of the
    /// exported file computes.
    /// </summary>
    public static (Vec3[] Heads, Vec3?[] Ends) Positions(Vec3 hips, IReadOnlyList<Quat> local)
    {
        var world = new Quat[Bones.Length];
        var heads = new Vec3[Bones.Length];
        var ends = new Vec3?[Bones.Length];
        for (int i = 0; i < Bones.Length; i++)
        {
            var bone = Bones[i];
            world[i] = bone.Parent < 0 ? local[i] : world[bone.Parent] * local[i];
            heads[i] = bone.Parent < 0 ? hips : heads[bone.Parent] + world[bone.Parent].Rotate(bone.Offset);
            if (bone.End is { } end)
                ends[i] = heads[i] + world[i].Rotate(end);
        }
        return (heads, ends);
    }
}
