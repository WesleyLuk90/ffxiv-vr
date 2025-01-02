using Silk.NET.Maths;
using Silk.NET.OpenXR;

namespace FfxivVR;

public class AimPose(
    Posef? leftAim,
    Posef? rightAim,
    Posef? headAim
)
{
    public Posef? LeftAim { get; } = leftAim;
    public Posef? RightAim { get; } = rightAim;
    public Posef? HeadAim { get; } = headAim;
    public Ray? GetAimRay(AimType aimType)
    {
        Posef? maybeAim = null;
        switch (aimType)
        {
            case AimType.LeftHand:
                {
                    maybeAim = LeftAim;
                    break;
                }
            case AimType.RightHand:
                {
                    maybeAim = RightAim;
                    break;
                }
            case AimType.Head:
                {
                    maybeAim = HeadAim;
                    break;
                }
        }
        if (maybeAim is not { } aim)
        {
            return null;
        }
        var start = aim.Position.ToVector3D();
        var rotation = aim.Orientation.ToQuaternion();
        var direction = Vector3D.Transform(new Vector3D<float>(0, 0, -1), rotation);
        return new Ray(start, direction);
    }
}