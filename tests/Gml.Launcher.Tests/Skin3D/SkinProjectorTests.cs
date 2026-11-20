using Gml.Launcher.Core.Skin3D;

namespace Gml.Launcher.Tests.Skin3D;

public class SkinProjectorTests
{
    private static readonly IReadOnlyList<SkinBox> Model =
        SkinModelBuilder.Build(new SkinLayout(IsLegacy: false, IsSlim: false), hasCape: true);

    private static readonly TexRect HeadFront = new(8, 8, 8, 8);
    private static readonly TexRect HeadBack = new(24, 8, 8, 8);

    [Test]
    public void FacingViewer_FrontIsVisible_BackIsCulled()
    {
        var faces = SkinProjector.Project(Model, SkinPose.Rest, new SkinCamera(0, 0, 10, 100, 200));

        Assert.That(faces.Any(f => f.Face.Texture == HeadFront), Is.True);
        Assert.That(faces.Any(f => f.Face.Texture == HeadBack), Is.False);
    }

    [Test]
    public void TurnedAround_BackIsVisible_FrontIsCulled()
    {
        var faces = SkinProjector.Project(Model, SkinPose.Rest, new SkinCamera(Math.PI, 0, 10, 100, 200));

        Assert.That(faces.Any(f => f.Face.Texture == HeadBack), Is.True);
        Assert.That(faces.Any(f => f.Face.Texture == HeadFront), Is.False);
    }

    [Test]
    public void Transform_MapsTexelsOntoScreen()
    {
        var head = SkinProjector.Project(Model, SkinPose.Rest, new SkinCamera(0, 0, 10, 100, 200))
            .Single(f => f.Face.Texture == HeadFront);

        // Model centre (0,16,0) sits at (100,200); head front spans x -4..4, y 24..32.
        Assert.That(head.Transform.Apply(0, 0), Is.EqualTo((60.0, 40.0)));
        Assert.That(head.Transform.Apply(8, 8), Is.EqualTo((140.0, 120.0)));
    }

    [Test]
    public void Cape_IsDrawnBeforeBodyFromFront_AndAfterFromBehind()
    {
        static (int cape, int body) Order(double yaw)
        {
            var faces = SkinProjector.Project(Model, SkinPose.Rest, new SkinCamera(yaw, 0, 10, 0, 0)).ToList();
            return (faces.FindIndex(f => f.Part == SkinPart.Cape), faces.FindIndex(f => f.Part == SkinPart.Body));
        }

        var front = Order(0);
        var back = Order(Math.PI);

        Assert.That(front.cape, Is.LessThan(front.body));
        Assert.That(back.cape, Is.GreaterThan(back.body));
    }

    [Test]
    public void Overlay_IsDrawnAfterItsInnerLayer()
    {
        var faces = SkinProjector.Project(Model, SkinPose.Rest, new SkinCamera(0.4, 0.2, 10, 0, 0)).ToList();

        var lastInnerHead = faces.FindLastIndex(f => f.Part == SkinPart.Head && !f.IsOverlay);
        var firstHat = faces.FindIndex(f => f.Part == SkinPart.Head && f.IsOverlay);

        Assert.That(firstHat, Is.GreaterThan(lastInnerHead));
    }

    [Test]
    public void Pose_SwingsArmAroundShoulder()
    {
        var armFront = new TexRect(44, 20, 4, 12);
        var camera = new SkinCamera(0, 0, 10, 0, 0);
        var rest = SkinProjector.Project(Model, SkinPose.Rest, camera).Single(f => f.Face.Texture == armFront);
        var raised = SkinProjector.Project(Model, SkinPose.Rest.With(SkinPart.RightArm, 0, -0.5), camera)
            .Single(f => f.Face.Texture == armFront);

        var restHand = rest.Transform.Apply(0, 12);
        var raisedHand = raised.Transform.Apply(0, 12);

        Assert.That(raisedHand.X, Is.LessThan(restHand.X), "the hand moves outwards (to the player's right)");
        // Texel (2,2) of the arm front lies on the shoulder pivot (x -6, y 22).
        var (restX, restY) = rest.Transform.Apply(2, 2);
        var (raisedX, raisedY) = raised.Transform.Apply(2, 2);
        Assert.That(raisedX, Is.EqualTo(restX).Within(1e-9));
        Assert.That(raisedY, Is.EqualTo(restY).Within(1e-9), "the shoulder pivot stays in place");
    }
}
