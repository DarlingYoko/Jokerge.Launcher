using System.Collections.Generic;

namespace Gml.Launcher.Core.Skin3D;

/// <summary>
///     Builds the player model in skin pixels: feet at y = 0, head top at y = 32, the player faces +Z and its
///     right side is -X (on the viewer's left).
/// </summary>
public static class SkinModelBuilder
{
    private const double HatInflate = 0.5;
    private const double LayerInflate = 0.25;

    public static IReadOnlyList<SkinBox> Build(SkinLayout layout, bool hasCape)
    {
        var armWidth = layout.IsSlim ? 3 : 4;
        var boxes = new List<SkinBox>();

        AddPart(boxes, SkinPart.Head, new Vec3(-4, 24, -4), (8, 8, 8), new Vec3(0, 24, 0),
            (0, 0), (32, 0), HatInflate, mirror: false);
        AddPart(boxes, SkinPart.Body, new Vec3(-4, 12, -2), (8, 12, 4), new Vec3(0, 24, 0),
            (16, 16), layout.IsLegacy ? null : (16, 32), LayerInflate, mirror: false);

        AddPart(boxes, SkinPart.RightArm, new Vec3(-4 - armWidth, 12, -2), (armWidth, 12, 4),
            new Vec3(-4 - armWidth / 2.0, 22, 0), (40, 16), layout.IsLegacy ? null : (40, 32), LayerInflate,
            mirror: false);
        AddPart(boxes, SkinPart.LeftArm, new Vec3(4, 12, -2), (armWidth, 12, 4),
            new Vec3(4 + armWidth / 2.0, 22, 0), layout.IsLegacy ? (40, 16) : (32, 48),
            layout.IsLegacy ? null : (48, 48), LayerInflate, mirror: layout.IsLegacy);

        AddPart(boxes, SkinPart.RightLeg, new Vec3(-4, 0, -2), (4, 12, 4), new Vec3(-2, 12, 0),
            (0, 16), layout.IsLegacy ? null : (0, 32), LayerInflate, mirror: false);
        AddPart(boxes, SkinPart.LeftLeg, new Vec3(0, 0, -2), (4, 12, 4), new Vec3(2, 12, 0),
            layout.IsLegacy ? (0, 16) : (16, 48), layout.IsLegacy ? null : (0, 48), LayerInflate,
            mirror: layout.IsLegacy);

        if (hasCape)
            boxes.Add(BuildCape());

        return boxes;
    }

    private static void AddPart(List<SkinBox> boxes, SkinPart part, Vec3 min, (int W, int H, int D) size,
        Vec3 pivot, (int U, int V) inner, (int U, int V)? outer, double inflate, bool mirror)
    {
        boxes.Add(Box(part, false, min, size, pivot, inner, 0, mirror));
        if (outer is { } overlay)
            boxes.Add(Box(part, true, min, size, pivot, overlay, inflate, mirror));
    }

    private static SkinBox Box(SkinPart part, bool isOverlay, Vec3 min, (int W, int H, int D) size, Vec3 pivot,
        (int U, int V) uv, double inflate, bool mirror)
    {
        var grow = new Vec3(inflate, inflate, inflate);
        var boxMin = min - grow;
        var boxSize = new Vec3(size.W, size.H, size.D) + grow * 2;
        var box = new SkinBox(part, isOverlay, boxMin, boxSize, pivot, Faces(boxMin, boxSize, uv, size));

        if (!mirror)
            return box;

        // Legacy left limbs reuse the right limb texture, flipped like a mirror image.
        var centerX = box.Center.X;
        return box.Map(p => p with { X = 2 * centerX - p.X }, d => d with { X = -d.X });
    }

    private static SkinBox BuildCape()
    {
        // 10x16x1 behind the back, hanging from the shoulders. Built facing +Z, then turned around so the
        // outer side of the texture faces backwards.
        var min = new Vec3(-5, 8, -3);
        var size = new Vec3(10, 16, 1);
        var box = new SkinBox(SkinPart.Cape, false, min, size, new Vec3(0, 24, -2),
            Faces(min, size, (0, 0), (10, 16, 1)));
        var center = box.Center;

        return box.Map(p => new Vec3(2 * center.X - p.X, p.Y, 2 * center.Z - p.Z), d => new Vec3(-d.X, d.Y, -d.Z));
    }

    /// <summary>Standard Minecraft box unwrap: top/bottom row above right/front/left/back.</summary>
    private static List<SkinFace> Faces(Vec3 min, Vec3 size, (int U, int V) uv, (int W, int H, int D) tex)
    {
        var (u, v) = uv;
        var (w, h, d) = tex;
        var max = min + size;
        var sx = new Vec3(size.X, 0, 0);
        var sy = new Vec3(0, size.Y, 0);
        var sz = new Vec3(0, 0, size.Z);

        return
        [
            new SkinFace(new TexRect(u + d, v, w, d), new Vec3(min.X, max.Y, min.Z), sx, sz, new Vec3(0, 1, 0)),
            new SkinFace(new TexRect(u + d + w, v, w, d), new Vec3(min.X, min.Y, max.Z), sx, sz * -1,
                new Vec3(0, -1, 0)),
            new SkinFace(new TexRect(u, v + d, d, h), new Vec3(min.X, max.Y, min.Z), sz, sy * -1, new Vec3(-1, 0, 0)),
            new SkinFace(new TexRect(u + d, v + d, w, h), new Vec3(min.X, max.Y, max.Z), sx, sy * -1, new Vec3(0, 0, 1)),
            new SkinFace(new TexRect(u + d + w, v + d, d, h), new Vec3(max.X, max.Y, max.Z), sz * -1, sy * -1,
                new Vec3(1, 0, 0)),
            new SkinFace(new TexRect(u + d + w + d, v + d, w, h), new Vec3(max.X, max.Y, min.Z), sx * -1, sy * -1,
                new Vec3(0, 0, -1))
        ];
    }
}
