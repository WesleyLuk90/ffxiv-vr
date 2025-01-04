using Silk.NET.OpenXR;

namespace FfxivVR;

public class PalmPose(
    Posef? leftPalm,
    Posef? rightPalm
)
{
    public Posef? LeftPalm { get; } = leftPalm;
    public Posef? RightPalm { get; } = rightPalm;

    public bool HasData()
    {
        return LeftPalm != null || RightPalm != null;
    }
}