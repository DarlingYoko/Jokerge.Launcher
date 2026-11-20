using System.Collections.Generic;
using System.Linq;

namespace Gml.Launcher.Core.Skin3D;

/// <summary>A face that faces the viewer, with the affine map that draws its texture.</summary>
public sealed record ProjectedFace(SkinPart Part, bool IsOverlay, SkinFace Face, Affine2D Transform);

/// <summary>
///     Orthographic projection with back-face culling and painter's ordering. Boxes are convex and never
///     intersect, so drawing whole boxes far to near (inner layer before its overlay) hides what must be hidden.
///     Under an orthographic camera every face is a parallelogram on screen, so its texture is drawn with a plain
///     2D affine transform — no GPU or 3D library needed.
/// </summary>
public static class SkinProjector
{
    /// <summary>The model turns around the middle of its height.</summary>
    public static readonly Vec3 ModelCenter = new(0, 16, 0);

    private const double VisibilityEpsilon = 1e-6;

    public static IReadOnlyList<ProjectedFace> Project(IReadOnlyList<SkinBox> boxes, SkinPose pose, SkinCamera camera)
    {
        var ordered = boxes
            .Select((box, index) => (box, index, depth: ToView(PosePoint(box.Center, box, pose), camera).Z))
            .OrderBy(entry => entry.depth)
            .ThenBy(entry => entry.box.IsOverlay)
            .ThenBy(entry => entry.index);

        var result = new List<ProjectedFace>();
        foreach (var (box, _, _) in ordered)
        foreach (var face in box.Faces)
        {
            var normal = ToViewDirection(PoseDirection(face.Normal, box, pose), camera);
            if (normal.Z <= VisibilityEpsilon)
                continue;

            var origin = ToView(PosePoint(face.Origin, box, pose), camera);
            var u = ToViewDirection(PoseDirection(face.U, box, pose), camera);
            var v = ToViewDirection(PoseDirection(face.V, box, pose), camera);
            var scale = camera.Scale;
            var w = face.Texture.Width;
            var h = face.Texture.Height;

            var transform = new Affine2D(
                u.X * scale / w, -u.Y * scale / w,
                v.X * scale / h, -v.Y * scale / h,
                camera.CenterX + origin.X * scale, camera.CenterY - origin.Y * scale);

            result.Add(new ProjectedFace(box.Part, box.IsOverlay, face, transform));
        }

        return result;
    }

    private static Vec3 PosePoint(Vec3 point, SkinBox box, SkinPose pose) =>
        PoseDirection(point - box.Pivot, box, pose) + box.Pivot;

    private static Vec3 PoseDirection(Vec3 direction, SkinBox box, SkinPose pose)
    {
        var (rotX, rotZ) = pose[box.Part];
        return direction.RotateZ(rotZ).RotateX(rotX);
    }

    private static Vec3 ToView(Vec3 point, SkinCamera camera) => ToViewDirection(point - ModelCenter, camera);

    private static Vec3 ToViewDirection(Vec3 direction, SkinCamera camera) =>
        direction.RotateY(camera.Yaw).RotateX(camera.Pitch);
}
