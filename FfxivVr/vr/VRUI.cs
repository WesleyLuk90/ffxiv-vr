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
        return Matrix4X4.CreateScale(configuration.UISize) * Matrix4X4.CreateTranslation(PlaneCenter) * Matrix4X4.CreateFromQuaternion(CurrentAngleRotation);
    }
    public Matrix4X4<float> GetDeformMatrix()
    {
        return resources.UIRenderTarget.AspectRatioTransform();
    }

    private Quaternion<float> CurrentAngleRotation => MathFactory.YRotation(currentAngle);
    private Vector3D<float> PlaneCenter => new Vector3D<float>(0.0f, 0.0f, -configuration.UIDistance);

    internal void ResetAngle()
    {
        target = 0;
        transition = false;
        currentAngle = 0;
    }

    private UIPanel Panel => new UIPanel(GetModelMatrix(), configuration.UICurvature, resources.UIRenderTarget.AspectRatio);

    internal Line? Intersect(Ray ray)
    {
        return Panel.Intersect(new Ray(ray.Origin / configuration.WorldScale, ray.Direction));
    }

    internal Vector3D<float> ViewportToWorld(Vector2D<float> viewportPosition)
    {
        return Panel.ViewportToWorld(viewportPosition);
    }

    internal Vector2D<float> ScreenToViewport(int x, int y)
    {
        var size = resources.UIRenderTarget.Size.As<float>();
        return new Vector2D<float>(x / size.X, y / size.Y);
    }

    internal Vector2D<float>? GetViewportPosition(Line line)
    {
        return Panel.WorldToViewport(line.End);
    }

    internal Vector2D<float>? GetViewportPosition(AimType aimType, AimPose aimPose)
    {
        if (aimPose.GetAimRay(aimType) is not { } ray)
        {
            return null;
        }
        if (Intersect(ray) is not { } line)
        {
            return null;
        }
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