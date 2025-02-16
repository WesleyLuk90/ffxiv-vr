using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;

namespace FfxivVR;

public class VRUI(
    Configuration configuration,
    Resources resources
)
{
    private float target = 0;
    private bool transition = false;
    private float currentAngle = 0;
    private float percentPerSecond = 5f;
    public void Update(View view, float ticks)
    {
        var headRotation = view.Pose.Orientation.ToQuaternion().GetYaw();
        if (MathF.Abs(MathFactory.AcuteAngleBetween(headRotation, target)) > float.DegreesToRadians(configuration.UITransitionAngle))
        {
            target = headRotation;
            transition = true;
        }
        if (transition)
        {
            currentAngle += MathFactory.AcuteAngleBetween(currentAngle, target) * ticks * percentPerSecond;
            if (MathF.Abs(MathFactory.AcuteAngleBetween(currentAngle, target)) < float.DegreesToRadians(1))
            {
                transition = false;
            }
        }
    }
    public Matrix4X4<float> GetModelMatrix()
    {
        return Matrix4X4.CreateTranslation(PlaneCenter) * Matrix4X4.CreateFromQuaternion(CurrentAngleRotation);
    }
    public Matrix4X4<float> GetDeformMatrix()
    {
        return resources.UIRenderTarget.AspectRatioTransform() * Matrix4X4.CreateScale(configuration.UISize);
    }

    private Quaternion<float> CurrentAngleRotation => MathFactory.YRotation(currentAngle);
    private Vector3D<float> PlaneCenter => new Vector3D<float>(0.0f, 0.0f, -configuration.UIDistance);
    private Vector3D<float> Normal => Vector3D.Transform(new Vector3D<float>(0, 0, -1), CurrentAngleRotation);

    internal void ResetAngle()
    {
        target = 0;
        transition = false;
        currentAngle = 0;
    }

    internal Line Intersect(Ray ray)
    {
        var normal = Normal;
        var center = Vector3D.Transform(PlaneCenter, CurrentAngleRotation);
        var d = Vector3D.Dot(center - ray.Origin, normal) / Vector3D.Dot(normal, ray.Direction);
        return ray.ToLine(d);
    }

    internal Vector2D<float>? GetViewportPosition(Line line)
    {
        var center = Vector3D.Transform(PlaneCenter, CurrentAngleRotation);
        var lookAt = Matrix4X4.CreateLookAt(Vector3D<float>.Zero, center, Vector3D<float>.UnitY);
        var size = new Vector2D<float>(2 * configuration.UISize, 2 * configuration.UISize * resources.UIRenderTarget.AspectRatio);
        var projected = Vector3D.Transform(line.End, lookAt);
        var viewport = new Vector2D<float>(projected.X, projected.Y);
        var v = (viewport + size / 2) / size;
        v.Y = 1 - v.Y;
        if (v.X < 0 || v.X > 1 || v.Y < 0 || v.Y > 1)
        {
            return null;
        }
        return v;
    }

    internal Vector2D<float>? GetViewportPosition(AimType aimType, AimPose aimPose)
    {
        if (aimPose.GetAimRay(aimType) is not { } ray)
        {
            return null;
        }
        var line = Intersect(ray);
        return GetViewportPosition(line);
    }

    internal Line? GetAimLine(AimType aimType, AimPose aimPose)
    {
        var ray = aimPose.GetAimRay(aimType);
        if (ray == null)
        {
            return null;
        }
        return Intersect(ray);
    }
}