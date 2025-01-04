namespace FfxivVR;

public class VRInputService(
    VRActionService vrActionService,
    VRSystem vrSystem,
    VRSpace vrSpace
)
{

    public VRInputData PollInput(long predictedTime)
    {
        var (actionState, palmPose, aimPose) = vrActionService.PollActions(predictedTime);
        return new VRInputData(
            handPose: vrSystem.HandTracker?.GetHandTrackingData(vrSpace.LocalSpace, predictedTime) ?? new HandTracking.HandPose(null, null, false),
            palmPose: palmPose,
            aimPose: aimPose,
            bodyJoints: vrSystem.BodyTracker?.GetData(vrSpace.LocalSpace, predictedTime),
            vrActionsState: actionState
        );
    }
}