﻿using Silk.NET.Maths;
using System;

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

    public static float GetYaw(this Quaternion<float> quat)
    {
        return 2 * MathF.Atan2(quat.Y, quat.W);
    }

    public static float AcuteAngleBetween(float from, float to)
    {
        var angle = to - from;
        var zeroTo360 = angle - float.DegreesToRadians(360) * MathF.Floor(angle / float.DegreesToRadians(360));
        if (zeroTo360 > float.DegreesToRadians(180))
        {
            return zeroTo360 - float.DegreesToRadians(360);
        }
        return zeroTo360;
    }

    // Create a quaternion that rotates from onto to using the shorted arc
    public static Quaternion<float> RotateOnto(Vector3D<float> from, Vector3D<float> to)
    {
        var a = Vector3D.Normalize(from);
        var b = Vector3D.Normalize(to);
        var dot = Vector3D.Dot(a, b);
        if (dot < -0.999999f)
        {
            var axis = Vector3D.Cross(Vector3D<float>.UnitX, a);
            if (axis.Length < 0.000001f)
            {
                axis = Vector3D.Cross(Vector3D<float>.UnitY, a);
            }
            axis = Vector3D.Normalize(axis);
            return Quaternion<float>.CreateFromAxisAngle(axis, MathF.PI);
        }
        else if (dot > 0.999999f)
        {
            return Quaternion<float>.Identity;
        }
        else
        {
            var axis = Vector3D.Cross(a, b);
            return Quaternion<float>.Normalize(new Quaternion<float>(axis.X, axis.Y, axis.Z, 1 + dot));
        }
    }

}