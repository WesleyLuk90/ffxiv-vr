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
}
