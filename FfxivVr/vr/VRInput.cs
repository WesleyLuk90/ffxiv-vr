using Silk.NET.OpenXR;
using static FfxivVR.HandTracking;

namespace FfxivVR;

public class VRInputData(
    HandPose handPose,
    PalmPose palmPose,
    BodyJointLocationFB[]? bodyJoints,
    AimPose aimPose,
    VRActionsState vrActionsState
)
{
    public HandPose HandPose { get; } = handPose;
    public PalmPose PalmPose { get; } = palmPose;
    public AimPose AimPose { get; } = aimPose;
    public BodyJointLocationFB[]? BodyJoints { get; } = bodyJoints;
    public VRActionsState VrActionsState { get; } = vrActionsState;

    public VRActionsState GetPhysicalActionsState()
    {
        if (HandPose.LeftHand != null || HandPose.RightHand != null)
        {
            return new VRActionsState();
        }
        return VrActionsState;
    }

    public bool HasBodyData()
    {
        return BodyJoints != null;
    }
}