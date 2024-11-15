using Silk.NET.Maths;

namespace FfxivVR;

public class RuntimeAdjustments
{
    public Quaternion<float> ThumbRotation = Quaternion<float>.Identity;
}

public class OculusRuntimeAdjustments : RuntimeAdjustments
{
    public OculusRuntimeAdjustments()
    {
        ThumbRotation = MathFactory.ZRotation(float.DegreesToRadians(90));
    }
}