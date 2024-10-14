using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System.Numerics;

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

    public static Matrix4x4 ToMatrix4x4(this Matrix4X4<float> m)
    {
        return new Matrix4x4(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);
    }
}
