using Silk.NET.OpenXR;
using System;

namespace FfxivVR;

public class VRUI(
    Configuration configuration
)
{
    private readonly Configuration configuration = configuration;
    private float target = 0;
    private bool transition = false;
    private float currentAngle = 0;
    private float percentPerSecond = 5f;
    public float GetRotation(View view, float ticks)
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
        return currentAngle;
    }
    internal void ResetAngle()
    {
        target = 0;
        transition = false;
        currentAngle = 0;
    }
}