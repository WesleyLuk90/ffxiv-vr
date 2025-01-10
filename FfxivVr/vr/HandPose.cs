using Silk.NET.OpenXR;

namespace FfxivVR;

public class HandPose(HandJointLocationEXT[]? LeftHand, HandJointLocationEXT[]? RightHand, bool isFromController)
{
    public HandJointLocationEXT[]? LeftHand { get; } = LeftHand;
    public HandJointLocationEXT[]? RightHand { get; } = RightHand;
    public bool IsFromController { get; } = isFromController;

    public bool IsHandTracking()
    {
        return (LeftHand != null || RightHand != null) && !IsFromController;
    }
    public bool HasData()
    {
        return LeftHand != null || RightHand != null;
    }
}