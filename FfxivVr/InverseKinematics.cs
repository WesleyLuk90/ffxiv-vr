using FFXIVClientStructs.FFXIV.Common.Lua;
using FFXIVClientStructs.FFXIV.Component.SteamApi.Callbacks;
using Silk.NET.Maths;
using System;
using System.Diagnostics;

namespace FfxivVR;
public class InverseKinematics
{
    private double Epsilon = 1E-6f;
    public Tuple<Quaternion<float>, Quaternion<float>> Calculate2Bone(
        Vector3D<float> a,
        Vector3D<float> b,
        Vector3D<float> c,
        Vector3D<float> target,
        Vector3D<float> bendDirection)
    {
        var (rot1, rot2) = Calculate2BoneDouble(
            new Vector3D<double>(a.X, a.Y, a.Z),
            new Vector3D<double>(b.X, b.Y, b.Z),
            new Vector3D<double>(c.X, c.Y, c.Z),
            new Vector3D<double>(target.X, target.Y, target.Z),
            new Vector3D<double>(bendDirection.X, bendDirection.Y, bendDirection.Z)
        );

        return Tuple.Create(
            new Quaternion<float>((float)rot1.X, (float)rot1.Y, (float)rot1.Z, (float)rot1.W),
            new Quaternion<float>((float)rot2.X, (float)rot2.Y, (float)rot2.Z, (float)rot2.W)
        );
    }
    public Tuple<Quaternion<double>, Quaternion<double>> Calculate2BoneDouble(
        Vector3D<double> a,
        Vector3D<double> b,
        Vector3D<double> c,
        Vector3D<double> target,
        Vector3D<double> bendDirection)
    {
        var lengthAB = (b - a).Length;
        var lengthCB = (b - c).Length;
        var lengthAT = double.Clamp((target - a).Length, Epsilon, lengthAB + lengthCB - Epsilon);

        var angle1ACAB = AngleBetween(c - a, b - a);
        var angle1BABC = AngleBetween(a - b, c - b);
        var angle1ACAT = AngleBetween(c - a, target - a);

        var angle2ACAB = CalculateTriangleAngle(lengthAB, lengthAT, lengthCB);
        var angle2BABC = CalculateTriangleAngle(lengthAB, lengthCB, lengthAT);

        var axis0 = PerpendicularAxis(c - a, bendDirection);
        var axis1 = PerpendicularAxis(c - a, target - a);

        var deltaRotation0 = angle2ACAB - angle1ACAB;
        var deltaRotation1 = angle2BABC - angle1BABC;

        var rotation0 = CreateFromAxisAngle(axis0, deltaRotation0);
        var rotation1 = CreateFromAxisAngle(axis0, deltaRotation1);
        var rotation2 = CreateFromAxisAngle(axis1, angle1ACAT);

        Trace.WriteLine($"lengthAB {lengthAB}");
        Trace.WriteLine($"lengthCB {lengthCB}");
        Trace.WriteLine($"lengthAT {lengthAT}");
        Trace.WriteLine($"angle1ACAB {double.RadiansToDegrees(angle1ACAB)}");
        Trace.WriteLine($"angle1BABC {double.RadiansToDegrees(angle1BABC)}");
        Trace.WriteLine($"angle1ACAT {double.RadiansToDegrees(angle1ACAT)}");
        Trace.WriteLine($"angle2ACAB {double.RadiansToDegrees(angle2ACAB)}");
        Trace.WriteLine($"angle2BABC {double.RadiansToDegrees(angle2BABC)}");
        Trace.WriteLine($"a1 rotation {axis0} {double.RadiansToDegrees(deltaRotation0)} = {rotation0}");
        Trace.WriteLine($"a2 rotation {axis1} {double.RadiansToDegrees(angle1ACAT)} = {rotation2}");
        Trace.WriteLine($"b rotation {axis0} {double.RadiansToDegrees(deltaRotation1)} = {rotation1}");

        return Tuple.Create(rotation0 * rotation2, rotation1);
    }

    private Quaternion<double> CreateFromAxisAngle(Vector3D<double> axis, double angle)
    {
        if (Math.Abs(angle) < Epsilon || double.IsNaN(axis.X) || double.IsNaN(axis.Y) || double.IsNaN(axis.Z))
        {
            return Quaternion<double>.Identity;
        }
        return Quaternion<double>.CreateFromAxisAngle(axis, angle);
    }

    private double AngleBetween(Vector3D<double> a, Vector3D<double> b)
    {
        return ClampedAcos(Vector3D.Dot(Vector3D.Normalize(a), Vector3D.Normalize(b)));
    }

    private Vector3D<double> PerpendicularAxis(Vector3D<double> a, Vector3D<double> b)
    {
        return Vector3D.Normalize(Vector3D.Cross(a, b));
    }

    // Calculates the angle opposite c given sides a, b and c
    private double CalculateTriangleAngle(double a, double b, double c)
    {
        return ClampedAcos((a * a + b * b - c * c) / (2 * a * b));
    }

    private double ClampedAcos(double value)
    {
        return Math.Acos(double.Clamp(value, -1, 1));
    }
}
