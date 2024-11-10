using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.Havok.Common.Base.Math.Quaternion;
using FFXIVClientStructs.Havok.Common.Base.Math.Vector;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;

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
    public static hkVector4f ToHkVector4(this Vector3D<float> vec)
    {
        var hk = new hkVector4f();
        hk.X = vec.X;
        hk.Y = vec.Y;
        hk.Z = vec.Z;
        hk.W = 0;
        return hk;
    }

    public static Quaternion<float> ToQuaternion(this Quaternionf quat)
    {
        return new Quaternion<float>(quat.X, quat.Y, quat.Z, quat.W);
    }
    public static Quaternionf ToQuaternionf(this Quaternion<float> quat)
    {
        return new Quaternionf(quat.X, quat.Y, quat.Z, quat.W);
    }

    public static Quaternion<float> ToQuaternion(this Quaternion quat)
    {
        return new Quaternion<float>(quat.X, quat.Y, quat.Z, quat.W);
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

    public static Matrix4X4<float> ToMatrix4X4(this Posef pose)
    {
        return Matrix4X4.CreateFromQuaternion(pose.Orientation.ToQuaternion()) * Matrix4X4.CreateTranslation(pose.Position.ToVector3D());
    }

    public static Quaternion<float> ToQuaternion(this hkQuaternionf quat)
    {
        return new Quaternion<float>(quat.X, quat.Y, quat.Z, quat.W);
    }
    public static hkQuaternionf ToQuaternion(this Quaternion<float> quat)
    {
        var outQuat = new hkQuaternionf();
        outQuat.X = quat.X;
        outQuat.Y = quat.Y;
        outQuat.Z = quat.Z;
        outQuat.W = quat.W;
        return outQuat;

    }

    public static Vector3D<float> ToVector3D(this hkVector4f vec)
    {
        return new Vector3D<float>(vec.X, vec.Y, vec.Z);
    }

    public static Vector3D<float> ToYawPitchRoll(this Quaternion<float> quat)
    {
        var x = quat.X;
        var y = quat.Y;
        var z = quat.Z;
        var w = quat.W;
        return new Vector3D<float>(
            MathF.Atan2(2 * (w * x + y * z), 1 - 2 * (x * x + y * y)),
            MathF.Asin(2 * (w * y - x * z)),
            MathF.Atan2(2 * (w * z - x * y), 1 - 2 * (y * y + z * z))
        );
    }
    public static Quaternion<float> Inverse(this Quaternion<float> quat)
    {
        return Quaternion<float>.Inverse(quat);
    }
}
