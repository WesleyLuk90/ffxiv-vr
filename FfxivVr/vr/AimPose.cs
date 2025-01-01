using Silk.NET.OpenXR;

namespace FfxivVR;

public unsafe partial class VRInput
{
    public class AimPose(

        Posef? leftAim,
        Posef? rightAim,
        Posef? headAim
    )
    {
        public Posef? LeftAim { get; } = leftAim;
        public Posef? RightAim { get; } = rightAim;
        public Posef? HeadAim { get; } = headAim;
    }
}