using FFXIVClientStructs.FFXIV.Common.Math;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;

namespace FfxivVR;
internal class VRCamera(Configuration configuration)
{
    private readonly Configuration configuration = configuration;

    internal Matrix4x4 ComputeGameProjectionMatrix(View view)
    {
        var near = 0.1f;
        var left = MathF.Tan(view.Fov.AngleLeft) * near;
        var right = MathF.Tan(view.Fov.AngleRight) * near;
        var down = MathF.Tan(view.Fov.AngleDown) * near;
        var up = MathF.Tan(view.Fov.AngleUp) * near;

        var proj = Matrix4X4.CreatePerspectiveOffCenter(left, right, down, up, nearPlaneDistance: near, farPlaneDistance: 100f);

        // FFXIV uses reverse z matrixes, update the matrix to handle this
        proj.M33 = 0;
        proj.M43 = near;
        return proj.ToMatrix4x4();
    }
    internal Matrix4X4<float> ComputeGameViewMatrix(View view, Vector3D<float> position, Vector3D<float> lookAt, Vector3D<float>? headPosition)
    {
        var forwardVector = lookAt - position;
        var yAngle = -MathF.PI / 2 - MathF.Atan2(forwardVector.Z, forwardVector.X);

        var gameViewMatrix = Matrix4X4.CreateScale(1f / configuration.WorldScale) * Matrix4X4.CreateRotationY(yAngle) * Matrix4X4.CreateTranslation(headPosition ?? position);
        var vrViewMatrix = Matrix4X4.CreateFromQuaternion(view.Pose.Orientation.ToQuaternion()) * Matrix4X4.CreateTranslation(view.Pose.Position.ToVector3D());

        var viewMatrix = vrViewMatrix * gameViewMatrix;
        Matrix4X4.Invert(viewMatrix, out Matrix4X4<float> invertedViewMatrix);
        return invertedViewMatrix;
    }

    internal Matrix4X4<float> ComputeVRViewProjectionMatrix(View view)
    {
        var rotation = Matrix4X4.CreateFromQuaternion(view.Pose.Orientation.ToQuaternion());
        var translation = Matrix4X4.CreateTranslation(view.Pose.Position.ToVector3D());
        var toView = Matrix4X4.Multiply(rotation, translation);
        Matrix4X4.Invert(toView, out Matrix4X4<float> viewInverted);

        var near = 0.05f;
        var left = MathF.Tan(view.Fov.AngleLeft) * near;
        var right = MathF.Tan(view.Fov.AngleRight) * near;
        var down = MathF.Tan(view.Fov.AngleDown) * near;
        var up = MathF.Tan(view.Fov.AngleUp) * near;

        var proj = Matrix4X4.CreatePerspectiveOffCenter(left, right, down, up, nearPlaneDistance: near, farPlaneDistance: 100f);
        return Matrix4X4.Multiply(viewInverted, proj);
    }
}
