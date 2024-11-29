using Silk.NET.OpenXR;
using System;

namespace FfxivVR;

public class TrackingData(
    HandTrackerExtension.HandPose? handPose,
    HandTrackerExtension.HandPose? lastValidHandPose,
    VRInput.ControllerPose? controllerPose,
    VRInput.ControllerPose? lastValidControllerPose
)
{
    public HandTrackerExtension.HandPose? HandPose { get; } = handPose;
    public HandTrackerExtension.HandPose? LastValidHandPose { get; } = lastValidHandPose;
    public VRInput.ControllerPose? ControllerPose { get; } = controllerPose;
    public VRInput.ControllerPose? LastValidControllerPose { get; } = lastValidControllerPose;

    public HandJointLocationEXT[]? GetLeftHand()
    {
        if (HandPose?.LeftHand != null)
        {
            return HandPose?.LeftHand;
        }
        if (ControllerPose?.LeftController == null)
        {
            return LastValidHandPose?.LeftHand;
        }
        return null;
    }
    public HandJointLocationEXT[]? GetRightHand()
    {
        if (HandPose?.RightHand != null)
        {
            return HandPose?.RightHand;
        }
        if (ControllerPose?.RightController == null)
        {
            return LastValidHandPose?.RightHand;
        }
        return null;
    }

    public Posef? GetLeftController()
    {
        if (ControllerPose?.LeftController != null)
        {
            return ControllerPose.LeftController;
        }
        if (HandPose?.LeftHand == null)
        {
            return LastValidControllerPose?.LeftController;
        }
        return null;
    }
    public Posef? GetRightController()
    {
        if (ControllerPose?.RightController != null)
        {
            return ControllerPose.RightController;
        }
        if (HandPose?.RightHand == null)
        {
            return LastValidControllerPose?.RightController;
        }
        return null;
    }

    internal static TrackingData CreateNew(HandTrackerExtension.HandPose? hands, VRInput.ControllerPose? controllers)
    {
        return new TrackingData(hands, null, controllers, null);
    }

    internal TrackingData Update(bool handTracking, HandTrackerExtension.HandPose? hands, bool controllerTracking, VRInput.ControllerPose? controllers)
    {
        return new TrackingData(
            handTracking ? hands : null,
            handTracking ? new HandTrackerExtension.HandPose(GetLeftHand(), GetRightHand()) : null,
            controllerTracking ? controllers : null,
            controllerTracking ? new VRInput.ControllerPose(GetLeftController(), GetRightController()) : null
        );
    }

    internal static TrackingData Disabled()
    {
        return new TrackingData(null, null, null, null);
    }

    internal bool HasData()
    {
        return (GetLeftHand() != null || GetLeftController() != null) && (GetRightHand() != null || GetRightController() != null);
    }
}
