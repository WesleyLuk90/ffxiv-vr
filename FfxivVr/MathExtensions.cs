using Silk.NET.Maths;
using Silk.NET.OpenXR;

namespace FfxivVR;

public static class MathExtensions
{
    public static Vector3D<float> ToVector3D(this Vector3f vec)
    {
        return new Vector3D<float>(vec.X, vec.Y, vec.Z);
    }

    public static Quaternion<float> ToQuaternion(this Quaternionf quat)
    {
        return new Quaternion<float>(quat.X, quat.Y, quat.Z, quat.W);
    }
}
