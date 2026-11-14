namespace Shosa.Core.Motion;

/// <summary>Right-handed, y up. In a figure's frame x is its left and z is where it faces.</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static readonly Vec3 Up = new(0, 1, 0);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    public double Dot(Vec3 o) => X * o.X + Y * o.Y + Z * o.Z;
    public Vec3 Cross(Vec3 o) => new(Y * o.Z - Z * o.Y, Z * o.X - X * o.Z, X * o.Y - Y * o.X);
    public double Length => Math.Sqrt(Dot(this));

    public Vec3 Normalized()
    {
        double length = Length;
        return length < 1e-12 ? new Vec3(0, -1, 0) : this * (1 / length);
    }

    public static Vec3 Lerp(Vec3 a, Vec3 b, double t) => a + (b - a) * t;

    /// <summary>Along the great circle between two unit vectors.</summary>
    public static Vec3 Slerp(Vec3 a, Vec3 b, double t)
    {
        double dot = Math.Clamp(a.Dot(b), -1, 1);
        if (dot > 0.9995)
            return Lerp(a, b, t).Normalized();
        if (dot < -0.9995)
        {
            // Opposite: no great circle is singled out. Go round by a fixed perpendicular.
            var axis = Math.Abs(a.Y) < 0.9 ? Up : new Vec3(0, 0, 1);
            var mid = (axis - a * axis.Dot(a)).Normalized();
            return t < 0.5 ? Slerp(a, mid, t * 2) : Slerp(mid, b, (t - 0.5) * 2);
        }
        double theta = Math.Acos(dot), sin = Math.Sin(theta);
        return a * (Math.Sin((1 - t) * theta) / sin) + b * (Math.Sin(t * theta) / sin);
    }

    /// <summary>Turn about the vertical; positive turns forward (z) toward left (x).</summary>
    public Vec3 RotateY(double angle)
    {
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        return new Vec3(X * cos + Z * sin, Y, -X * sin + Z * cos);
    }

    public static Vec3 Forward(double yaw) => new(Math.Sin(yaw), 0, Math.Cos(yaw));
    public static Vec3 Left(double yaw) => new(Math.Cos(yaw), 0, -Math.Sin(yaw));
}
