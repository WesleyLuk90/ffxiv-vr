using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;

namespace FfxivVR;

public class VRUI(
    Configuration configuration,
    Resources resources,
    Debugging debugging
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
    public Matrix4X4<float> GetTransformationMatrix()
    {
        var translationMatrix = Matrix4X4.CreateTranslation(PlaneCenter) * Matrix4X4.CreateFromQuaternion(MathFactory.YRotation(currentAngle));
        var uiScale = resources.UIRenderTarget.AspectRatioTransform() * Matrix4X4.CreateScale(configuration.UISize);
        return uiScale * translationMatrix;
    }

    private Vector3D<float> PlaneCenter => new Vector3D<float>(0.0f, 0.0f, -configuration.UIDistance);
    private Vector3D<float> Normal => Vector3D.Transform(new Vector3D<float>(0, 0, -1), MathFactory.YRotation(currentAngle));

    internal void ResetAngle()
    {
        target = 0;
        transition = false;
        currentAngle = 0;
    }

    internal Line Intersect(Ray ray)
    {
        var normal = Normal;
        var d = Vector3D.Dot(PlaneCenter - ray.Origin, normal) / Vector3D.Dot(normal, ray.Direction);
        return ray.ToLine(d);
    }

    internal Vector2D<float>? GetViewportPosition(Line line)
    {
        var size = new Vector2D<float>(2 * configuration.UISize, 2 * configuration.UISize * resources.UIRenderTarget.AspectRatio);
        var bottomLeft = -size / 2;
        var v = new Vector2D<float>(line.End.X - bottomLeft.X, line.End.Y - bottomLeft.Y) / size;
        v.Y = 1 - v.Y;
        if (v.X < 0 || v.X > 1 || v.Y < 0 || v.Y > 1)
        {
            return null;
        }
        return v;
    }
}