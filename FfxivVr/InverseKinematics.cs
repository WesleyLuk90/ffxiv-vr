using Silk.NET.Maths;
using System;

namespace FfxivVR;
public class InverseKinematics
{
    private float Epsilon = 1E-3f;
    public Tuple<Quaternion<float>, Quaternion<float>> Calculate2Bone(
        Vector3D<float> a,
        Vector3D<float> b,
        Vector3D<float> c,
        Vector3D<float> target,
        Vector3D<float> bendDirection)
    {
        var lengthAB = (b - a).Length;
        var lengthCB = (b - c).Length;
        var lengthAT = float.Clamp((target - a).Length, Epsilon, lengthAB + lengthCB - Epsilon);

        var angle1ACAB = AngleBetween(c - a, b - a);
        var angle1BABC = AngleBetween(a - b, c - b);
        var angle1ACAT = AngleBetween(c - a, target - a);

        var angle2ACAB = CalculateTriangleAngle(lengthAB, lengthAT, lengthCB);
        var angle2BABC = CalculateTriangleAngle(lengthAB, lengthCB, lengthAT);

        Vector3D<float> axis0;
        if (angle1ACAB < Epsilon || false)
        {
            axis0 = PerpendicularAxis(c - a, bendDirection);
        }
        else
        {
            axis0 = PerpendicularAxis(c - a, b - a);
        }
        var axis1 = PerpendicularAxis(c - a, target - a);

        var deltaRotation0 = angle2ACAB - angle1ACAB;
        var deltaRotation1 = angle2BABC - angle1BABC;

        var rotation0 = CreateFromAxisAngle(axis0, deltaRotation0);
        var rotation1 = CreateFromAxisAngle(axis0, deltaRotation1);
        var rotation2 = CreateFromAxisAngle(axis1, angle1ACAT);

        return Tuple.Create(rotation2 * rotation0, rotation1);
    }

    private Quaternion<float> CreateFromAxisAngle(Vector3D<float> axis, float angle)
    {
        if (MathF.Abs(angle) < Epsilon || float.IsNaN(axis.X) || float.IsNaN(axis.Y) || float.IsNaN(axis.Z))
        {
            return Quaternion<float>.Identity;
        }
        return Quaternion<float>.CreateFromAxisAngle(axis, angle);
    }

    private float AngleBetween(Vector3D<float> a, Vector3D<float> b)
    {
        return ClampedAcos(Vector3D.Dot(Vector3D.Normalize(a), Vector3D.Normalize(b)));
    }

    private Vector3D<float> PerpendicularAxis(Vector3D<float> a, Vector3D<float> b)
    {
        return Vector3D.Normalize(Vector3D.Cross(a, b));
    }

    // Calculates the angle opposite c given sides a, b and c
    private float CalculateTriangleAngle(float a, float b, float c)
    {
        return ClampedAcos((a * a + b * b - c * c) / (2 * a * b));
    }

    private float ClampedAcos(float value)
    {
        return MathF.Acos(float.Clamp(value, -1, 1));
    }
}
