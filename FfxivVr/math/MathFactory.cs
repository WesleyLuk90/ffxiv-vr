using Silk.NET.Maths;

namespace FfxivVR;
public static class MathFactory
{
    public static Quaternion<float> XRotation(float angle)
    {
        return Quaternion<float>.CreateFromAxisAngle(new Vector3D<float>(1, 0, 0), angle);
    }
    public static Quaternion<float> YRotation(float angle)
    {
        return Quaternion<float>.CreateFromAxisAngle(new Vector3D<float>(0, 1, 0), angle);
    }
    public static Quaternion<float> ZRotation(float angle)
    {
        return Quaternion<float>.CreateFromAxisAngle(new Vector3D<float>(0, 0, 1), angle);
    }

    public static Vector3D<float> Vector(float x, float y, float z)
    {
        return new Vector3D<float>(x, y, z);
    }
    public static Quaternion<float> AxisAngle(float x, float y, float z, float angle)
    {
        return Quaternion<float>.CreateFromAxisAngle(Vector3D.Normalize<float>(new Vector3D<float>(x, y, z)), float.DegreesToRadians(angle));
    }

    public static Matrix4X4<float> CreateScaleRotationTranslationMatrix(Vector3D<float> scale, Quaternion<float> rotation, Vector3D<float> translation)
    {
        return Matrix4X4.CreateScale(scale) * Matrix4X4.CreateFromQuaternion(rotation) * Matrix4X4.CreateTranslation(translation);
    }
}