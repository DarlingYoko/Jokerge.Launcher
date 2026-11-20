using Gml.Launcher.Core.Skin3D;

namespace Gml.Launcher.Tests.Skin3D;

public class SkinModelBuilderTests
{
    private static readonly SkinLayout Classic = new(IsLegacy: false, IsSlim: false);

    [Test]
    public void Modern_HasInnerAndOuterLayerForEveryPart_PlusCape()
    {
        var boxes = SkinModelBuilder.Build(Classic, hasCape: true);

        Assert.That(boxes, Has.Count.EqualTo(13));
        Assert.That(boxes.Count(b => b.IsOverlay), Is.EqualTo(6));
        Assert.That(boxes.Count(b => b.Part == SkinPart.Cape), Is.EqualTo(1));
        Assert.That(boxes.All(b => b.Faces.Count == 6), Is.True);
    }

    [Test]
    public void Legacy_HasOnlyHatOverlay_AndNoCapeWhenNotRequested()
    {
        var boxes = SkinModelBuilder.Build(Classic with { IsLegacy = true }, hasCape: false);

        Assert.That(boxes, Has.Count.EqualTo(7));
        Assert.That(boxes.Single(b => b.IsOverlay).Part, Is.EqualTo(SkinPart.Head));
    }

    [Test]
    public void HeadFront_MapsTexelOriginToTopLeftAsSeenByViewer()
    {
        var front = Face(SkinModelBuilder.Build(Classic, false), SkinPart.Head, false, new TexRect(8, 8, 8, 8));

        Assert.That(front.Origin, Is.EqualTo(new Vec3(-4, 32, 4)));
        Assert.That(front.U, Is.EqualTo(new Vec3(8, 0, 0)));
        Assert.That(front.V, Is.EqualTo(new Vec3(0, -8, 0)));
        Assert.That(front.Normal, Is.EqualTo(new Vec3(0, 0, 1)));
    }

    [Test]
    public void HatFront_UsesOverlayTexture_AndIsInflated()
    {
        var hat = Face(SkinModelBuilder.Build(Classic, false), SkinPart.Head, true, new TexRect(40, 8, 8, 8));

        Assert.That(hat.Origin, Is.EqualTo(new Vec3(-4.5, 32.5, 4.5)));
        Assert.That(hat.U, Is.EqualTo(new Vec3(9, 0, 0)));
    }

    [Test]
    public void SlimArms_AreThreePixelsWide()
    {
        var boxes = SkinModelBuilder.Build(Classic with { IsSlim = true }, false);
        var rightArm = boxes.Single(b => b.Part == SkinPart.RightArm && !b.IsOverlay);

        Assert.That(rightArm.Size.X, Is.EqualTo(3));
        Assert.That(rightArm.Faces.Any(f => f.Texture == new TexRect(44, 20, 3, 12)), Is.True);
    }

    [Test]
    public void LegacyLeftArm_ReusesRightArmTexture_Mirrored()
    {
        var boxes = SkinModelBuilder.Build(Classic with { IsLegacy = true }, false);
        var front = Face(boxes, SkinPart.LeftArm, false, new TexRect(44, 20, 4, 12));

        Assert.That(front.Normal, Is.EqualTo(new Vec3(0, 0, 1)));
        Assert.That(front.U.X, Is.LessThan(0), "mirrored texture runs right to left");
    }

    [Test]
    public void CapeOuterSide_FacesBackwards()
    {
        var outer = Face(SkinModelBuilder.Build(Classic, true), SkinPart.Cape, false, new TexRect(1, 1, 10, 16));

        Assert.That(outer.Normal.Z, Is.LessThan(-0.99));
    }

    [Test]
    public void EveryFace_IsSpannedByUAndV_AndPointsOutOfItsBox()
    {
        foreach (var box in SkinModelBuilder.Build(Classic with { IsSlim = true }, true))
        foreach (var face in box.Faces)
        {
            var center = face.Origin + face.U * 0.5 + face.V * 0.5;
            Assert.That(Vec3.Dot(face.Normal, face.U), Is.EqualTo(0).Within(1e-9), $"{box.Part} {face.Texture}");
            Assert.That(Vec3.Dot(face.Normal, face.V), Is.EqualTo(0).Within(1e-9), $"{box.Part} {face.Texture}");
            Assert.That(Vec3.Dot(face.Normal, center - box.Center), Is.GreaterThan(0), $"{box.Part} {face.Texture}");
            Assert.That(Vec3.Dot(Vec3.Cross(face.V, face.U), face.Normal), Is.Not.EqualTo(0));
        }
    }

    private static SkinFace Face(IReadOnlyList<SkinBox> boxes, SkinPart part, bool overlay, TexRect texture)
        => boxes.Single(b => b.Part == part && b.IsOverlay == overlay).Faces.Single(f => f.Texture == texture);
}
