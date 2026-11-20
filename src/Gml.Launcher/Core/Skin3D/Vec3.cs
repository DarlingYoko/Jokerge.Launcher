using System;

namespace Gml.Launcher.Core.Skin3D;

/// <summary>Point or direction in model space: pixels of the skin, Y up, the player looks along +Z.</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);

    public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vec3 Cross(Vec3 a, Vec3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    /// <summary>Rotation around the X axis; positive turns +Y towards +Z.</summary>
    public Vec3 RotateX(double angle)
    {
        if (angle == 0) return this;
        var (sin, cos) = Math.SinCos(angle);
        return new Vec3(X, Y * cos - Z * sin, Y * sin + Z * cos);
    }

    /// <summary>Rotation around the Y axis; positive turns +Z towards +X.</summary>
    public Vec3 RotateY(double angle)
    {
        if (angle == 0) return this;
        var (sin, cos) = Math.SinCos(angle);
        return new Vec3(X * cos + Z * sin, Y, -X * sin + Z * cos);
    }

    /// <summary>Rotation around the Z axis; positive turns +X towards +Y.</summary>
    public Vec3 RotateZ(double angle)
    {
        if (angle == 0) return this;
        var (sin, cos) = Math.SinCos(angle);
        return new Vec3(X * cos - Y * sin, X * sin + Y * cos, Z);
    }
}

/// <summary>
///     2D affine map from texel coordinates of a face to screen pixels, in the layout of Avalonia's
///     <c>Matrix(m11, m12, m21, m22, m31, m32)</c>.
/// </summary>
public readonly record struct Affine2D(double M11, double M12, double M21, double M22, double OffsetX, double OffsetY)
{
    public (double X, double Y) Apply(double s, double t) =>
        (OffsetX + s * M11 + t * M21, OffsetY + s * M12 + t * M22);
}
