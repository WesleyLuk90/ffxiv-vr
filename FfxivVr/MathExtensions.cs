using FFXIVClientStructs.FFXIV.Common.Math;
using Silk.NET.Maths;
using Silk.NET.OpenXR;

namespace FfxivVR;

public static class MathExtensions
{
    public static Vector3D<float> ToVector3D(this Vector3f vec)
    {
        return new Vector3D<float>(vec.X, vec.Y, vec.Z);
    }

    public static Vector3f ToVector3f(this Vector3D<float> vec)
    {
        return new Vector3f(vec.X, vec.Y, vec.Z);
    }

    public static Vector3D<float> ToVector3D(this Vector3 vec)
    {
        return new Vector3D<float>(vec.X, vec.Y, vec.Z);
    }

    public static Quaternion<float> ToQuaternion(this Quaternionf quat)
    {
        return new Quaternion<float>(quat.X, quat.Y, quat.Z, quat.W);
    }
    public static Quaternionf ToQuaternionf(this Quaternion<float> quat)
    {
        return new Quaternionf(quat.X, quat.Y, quat.Z, quat.W);
    }

    public static Matrix4x4 ToMatrix4x4(this Matrix4X4<float> m)
    {
        var matrix = new Matrix4x4();
        matrix.M11 = m.M11;
        matrix.M12 = m.M12;
        matrix.M13 = m.M13;
        matrix.M14 = m.M14;
        matrix.M21 = m.M21;
        matrix.M22 = m.M22;
        matrix.M23 = m.M23;
        matrix.M24 = m.M24;
        matrix.M31 = m.M31;
        matrix.M32 = m.M32;
        matrix.M33 = m.M33;
        matrix.M34 = m.M34;
        matrix.M41 = m.M41;
        matrix.M42 = m.M42;
        matrix.M43 = m.M43;
        matrix.M44 = m.M44;

        return matrix;
    }
    public static Matrix4X4<float> ToMatrix4X4(this Matrix4x4 m)
    {
        return new Matrix4X4<float>(
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44);
    }
}
