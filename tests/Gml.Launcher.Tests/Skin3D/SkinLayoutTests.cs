using Gml.Launcher.Core.Skin3D;

namespace Gml.Launcher.Tests.Skin3D;

public class SkinLayoutTests
{
    [Test]
    public void Detect_64x32_IsLegacyAndClassic()
    {
        var layout = SkinLayout.Detect(64, 32, (_, _) => 255);

        Assert.That(layout, Is.EqualTo(new SkinLayout(IsLegacy: true, IsSlim: false)));
    }

    [Test]
    public void Detect_OpaqueArmBack_IsClassic()
    {
        Assert.That(SkinLayout.Detect(64, 64, (_, _) => 255).IsSlim, Is.False);
    }

    [Test]
    public void Detect_TransparentColumnsBesideSlimArm_IsSlim()
    {
        // Slim arms are 3 px wide: x 54..55 of rows 20..31 stay empty.
        byte Alpha(int x, int y) => x is 54 or 55 && y is >= 20 and <= 31 ? (byte)0 : (byte)255;

        Assert.That(SkinLayout.Detect(64, 64, Alpha).IsSlim, Is.True);
    }

    [Test]
    public void Detect_HdSkin_ScalesProbePixels()
    {
        byte Alpha(int x, int y) => x is >= 108 and <= 111 && y is >= 40 and <= 63 ? (byte)0 : (byte)255;

        Assert.That(SkinLayout.Detect(128, 128, Alpha).IsSlim, Is.True);
    }

    [Test]
    public void HasVisiblePixels_FalseForFullyTransparentTexture()
    {
        Assert.That(SkinLayout.HasVisiblePixels(64, 32, (_, _) => 0), Is.False);
        Assert.That(SkinLayout.HasVisiblePixels(64, 32, (x, y) => x == 5 && y == 7 ? (byte)1 : (byte)0), Is.True);
    }
}
