using System;

namespace Gml.Launcher.Core.Skin3D;

/// <param name="IsLegacy">Pre-1.8 64x32 skin: no second layer except the hat, left limbs mirror the right ones.</param>
/// <param name="IsSlim">"Alex" model with 3 px wide arms.</param>
public readonly record struct SkinLayout(bool IsLegacy, bool IsSlim)
{
    /// <param name="alpha">Alpha of the texture pixel at (x, y).</param>
    public static SkinLayout Detect(int width, int height, Func<int, int, byte> alpha)
    {
        if (width <= 0 || height * 2 == width)
            return new SkinLayout(IsLegacy: true, IsSlim: false);

        // A slim right arm leaves texels 54..55 of rows 20..31 unused; a classic one paints its back there.
        var scale = width / 64;
        for (var y = 20 * scale; y < 32 * scale; y++)
        for (var x = 54 * scale; x < 56 * scale; x++)
        {
            if (alpha(x, y) != 0)
                return new SkinLayout(IsLegacy: false, IsSlim: false);
        }

        return new SkinLayout(IsLegacy: false, IsSlim: true);
    }

    /// <summary>The API hands out an empty texture for players without a cape.</summary>
    public static bool HasVisiblePixels(int width, int height, Func<int, int, byte> alpha)
    {
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (alpha(x, y) != 0)
                return true;
        }

        return false;
    }
}
