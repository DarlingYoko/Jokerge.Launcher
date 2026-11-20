using System;

namespace Gml.Launcher.Core.Skin3D;

/// <summary>Rotation of every part around its pivot: first around Z (sideways), then around X (swing).</summary>
public sealed class SkinPose
{
    private const double CapeRestAngle = 0.12;
    private readonly (double X, double Z)[] _angles;

    private SkinPose((double X, double Z)[] angles)
    {
        _angles = angles;
    }

    public static SkinPose Rest { get; } = new SkinPose(new (double X, double Z)[Enum.GetValues<SkinPart>().Length])
        .With(SkinPart.Cape, CapeRestAngle, 0);

    public (double X, double Z) this[SkinPart part] => _angles[(int)part];

    public SkinPose With(SkinPart part, double rotX, double rotZ)
    {
        var angles = ((double X, double Z)[])_angles.Clone();
        angles[(int)part] = (rotX, rotZ);
        return new SkinPose(angles);
    }

    /// <summary>Calm breathing: arms sway a little, the cape follows.</summary>
    public static SkinPose Idle(double seconds)
    {
        var breath = Math.Sin(seconds * 1.6);
        var sway = 0.06 + 0.03 * breath;

        return Rest
            .With(SkinPart.RightArm, 0.04 * breath, -sway)
            .With(SkinPart.LeftArm, -0.04 * breath, sway)
            .With(SkinPart.Cape, CapeRestAngle + 0.05 * (1 + Math.Sin(seconds * 1.1)), 0);
    }
}

/// <summary>Orthographic camera: model turned by yaw then pitch, <see cref="Scale" /> screen px per skin px.</summary>
public readonly record struct SkinCamera(double Yaw, double Pitch, double Scale, double CenterX, double CenterY);
