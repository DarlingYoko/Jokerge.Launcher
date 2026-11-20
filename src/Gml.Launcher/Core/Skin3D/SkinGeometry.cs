using System;
using System.Collections.Generic;
using System.Linq;

namespace Gml.Launcher.Core.Skin3D;

public enum SkinPart
{
    Head,
    Body,
    RightArm,
    LeftArm,
    RightLeg,
    LeftLeg,
    Cape
}

/// <summary>Rectangle of a texture in texels of the standard 64-wide layout.</summary>
public readonly record struct TexRect(int X, int Y, int Width, int Height);

/// <summary>
///     One textured side of a box: texel (s, t) of <see cref="Texture" /> lands at
///     <c>Origin + U * s / Width + V * t / Height</c>.
/// </summary>
public sealed record SkinFace(TexRect Texture, Vec3 Origin, Vec3 U, Vec3 V, Vec3 Normal)
{
    public SkinFace Map(Func<Vec3, Vec3> point, Func<Vec3, Vec3> direction) =>
        new(Texture, point(Origin), direction(U), direction(V), direction(Normal));
}

/// <summary>A cuboid of the model. Overlay boxes share the pivot and centre of their inner box.</summary>
public sealed record SkinBox(SkinPart Part, bool IsOverlay, Vec3 Min, Vec3 Size, Vec3 Pivot,
    IReadOnlyList<SkinFace> Faces)
{
    public Vec3 Center => Min + Size * 0.5;

    public SkinBox Map(Func<Vec3, Vec3> point, Func<Vec3, Vec3> direction) =>
        this with { Faces = Faces.Select(face => face.Map(point, direction)).ToList() };
}
