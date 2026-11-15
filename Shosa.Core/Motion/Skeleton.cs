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

    /// <summary>
    /// The rotation of every bone relative to its parent, read off the joint positions of one
    /// pose. Positions do not say how a limb is twisted about its own length; the plane in
    /// which the elbow or knee bends does, so the hinge axis is taken from there.
    /// </summary>
    public static Quat[] Rotations(Vec3[] joints)
    {
        var world = new Quat[Bones.Length];

        // Hips: level, facing square to the line between the hip sockets.
        var fwd = (joints[Rig.LHip] - joints[Rig.RHip]).Cross(Vec3.Up).Normalized();
        var left = Vec3.Up.Cross(fwd);
        world[Hips] = Quat.FromBasis(left, Vec3.Up, fwd);

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
        world[Spine] = Piece((joints[Rig.Waist] - joints[Rig.Pelvis]).Normalized());
        world[Chest] = world[Neck] = world[LShoulder] = world[RShoulder] = Piece(torsoUp);

        // The head looks along the nose and stays upright in the torso's frame.
        var gaze = (joints[Rig.Nose] - joints[Rig.Head]).Normalized();
        var headLeft = torsoUp.Cross(gaze);
        headLeft = headLeft.Length < 1e-6 ? torsoLeft : headLeft.Normalized();
        world[Head] = Quat.FromBasis(headLeft, gaze.Cross(headLeft), gaze);

        for (int side = 1; side >= -1; side -= 2)
        {
            int arm = side == 1 ? LUpperArm : RUpperArm, shoulder = side == 1 ? Rig.LShoulder : Rig.RShoulder;
            // In the T-pose an elbow folds the forearm forward: about -y on the left, +y on the right.
            var (upper, lower) = Limb(joints[shoulder], joints[shoulder + 1], joints[shoulder + 2],
                X * side, Vec3.Up * -side, Rig.ElbowPole(left, fwd, side));
            world[arm] = upper;
            world[arm + 1] = world[arm + 2] = lower;

            int leg = side == 1 ? LUpperLeg : RUpperLeg, hip = side == 1 ? Rig.LHip : Rig.RHip;
            // A knee folds the shin back: about +x on both sides.
            (upper, lower) = Limb(joints[hip], joints[hip + 1], joints[hip + 2], -Vec3.Up, X, Rig.KneePole(left, fwd, side));
            world[leg] = upper;
            world[leg + 1] = lower;

            // The foot points at the toe and lies flat from side to side.
            var along = (joints[hip + 3] - joints[hip + 2]).Normalized();
            var footLeft = Vec3.Up.Cross(along);
            footLeft = footLeft.Length < 1e-6 ? left : footLeft.Normalized();
            world[leg + 2] = Quat.Between(FootRest.Normalized(), X, along, footLeft);
        }

        var local = new Quat[Bones.Length];
        for (int i = 0; i < Bones.Length; i++)
            local[i] = Bones[i].Parent < 0 ? world[i] : world[Bones[i].Parent].Inverse * world[i];
        return local;
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
