namespace FfxivVR;

public class VRInputService(
    VRActionService vrActionService,
    BodyTracking bodyTracking,
    VRSpace vrSpace,
    HandTracking handTracking
)
{
    public VRInputData PollInput(long predictedTime)
    {
        var (actionState, palmPose, aimPose) = vrActionService.PollActions(predictedTime);
        return new VRInputData(
            handPose: handTracking.GetHandTrackingData(vrSpace.LocalSpace, predictedTime),
            palmPose: palmPose,
            aimPose: aimPose,
            bodyJoints: bodyTracking?.GetData(vrSpace.LocalSpace, predictedTime),
            vrActionsState: actionState
        );
    }
}