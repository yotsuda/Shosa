namespace Shosa.Core.Motion;

/// <summary>A rotation as a unit quaternion. Same frame as <see cref="Vec3"/>: right-handed, y up.</summary>
public readonly record struct Quat(double X, double Y, double Z, double W)
{
    public static readonly Quat Identity = new(0, 0, 0, 1);

    public Quat Inverse => new(-X, -Y, -Z, W);

    /// <summary>The rotation that does <paramref name="b"/> first, then <paramref name="a"/>.</summary>
    public static Quat operator *(Quat a, Quat b) => new(
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

    public Vec3 Rotate(Vec3 v)
    {
        var q = new Vec3(X, Y, Z);
        var t = q.Cross(v) * 2;
        return v + t * W + q.Cross(t);
    }

    /// <summary>The rotation that takes the x, y and z axes to three orthonormal, right-handed vectors.</summary>
    public static Quat FromBasis(Vec3 x, Vec3 y, Vec3 z)
    {
        double trace = x.X + y.Y + z.Z;
        Quat q;
        if (trace > 0)
        {
            double s = Math.Sqrt(trace + 1) * 2;
            q = new Quat((y.Z - z.Y) / s, (z.X - x.Z) / s, (x.Y - y.X) / s, s / 4);
        }
        else if (x.X > y.Y && x.X > z.Z)
        {
            double s = Math.Sqrt(1 + x.X - y.Y - z.Z) * 2;
            q = new Quat(s / 4, (y.X + x.Y) / s, (z.X + x.Z) / s, (y.Z - z.Y) / s);
        }
        else if (y.Y > z.Z)
        {
            double s = Math.Sqrt(1 + y.Y - x.X - z.Z) * 2;
            q = new Quat((y.X + x.Y) / s, s / 4, (z.Y + y.Z) / s, (z.X - x.Z) / s);
        }
        else
        {
            double s = Math.Sqrt(1 + z.Z - x.X - y.Y) * 2;
            q = new Quat((z.X + x.Z) / s, (z.Y + y.Z) / s, s / 4, (x.Y - y.X) / s);
        }
        double length = Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
        return new Quat(q.X / length, q.Y / length, q.Z / length, q.W / length);
    }

    /// <summary>
    /// The rotation that takes one frame to another, each given by two of its axes: a bone's
    /// direction and something perpendicular to it (a hinge axis, the side of a foot).
    /// </summary>
    public static Quat Between(Vec3 restAxis, Vec3 restSide, Vec3 axis, Vec3 side) =>
        FromBasis(axis, side, axis.Cross(side)) * FromBasis(restAxis, restSide, restAxis.Cross(restSide)).Inverse;

    /// <summary>
    /// Degrees about z, x and y for the order BVH files use: turn about y first, then x, then z
    /// (R = Rz Rx Ry).
    /// </summary>
    public (double Z, double X, double Y) ToEulerZxy()
    {
        Vec3 cx = Rotate(new Vec3(1, 0, 0)), cy = Rotate(new Vec3(0, 1, 0)), cz = Rotate(new Vec3(0, 0, 1));
        // Row r, column c of the matrix is the r-th component of the c-th rotated axis.
        double m21 = cy.Z;
        const double deg = 180 / Math.PI;
        if (Math.Abs(m21) > 1 - 1e-9)
            return (Math.Atan2(cx.Y, cx.X) * deg, Math.Sign(m21) * 90, 0);
        return (Math.Atan2(-cy.X, cy.Y) * deg, Math.Asin(m21) * deg, Math.Atan2(-cx.Z, cz.Z) * deg);
    }
}
