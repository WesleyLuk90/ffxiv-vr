using Silk.NET.OpenXR;
using System.Linq;

namespace FfxivVR;

public class TrackingData(
    HandTracking.HandPose? handPose,
    HandTracking.HandPose? lastValidHandPose,
    VRInput.ControllerPose? controllerPose,
    VRInput.ControllerPose? lastValidControllerPose,
    BodyJointLocationFB[]? bodyData)
{
    public HandTracking.HandPose? HandPose { get; } = handPose;
    public HandTracking.HandPose? LastValidHandPose { get; } = lastValidHandPose;
    public VRInput.ControllerPose? ControllerPose { get; } = controllerPose;
    public VRInput.ControllerPose? LastValidControllerPose { get; } = lastValidControllerPose;
    public BodyJointLocationFB[]? BodyData { get; } = bodyData;
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

    internal static TrackingData CreateNew(HandTracking.HandPose? hands, VRInput.ControllerPose? controllers, BodyJointLocationFB[]? bodyData)
    {
        return new TrackingData(hands, null, controllers, null, bodyData);
    }

    internal TrackingData Update(bool handTracking, HandTracking.HandPose? hands, bool controllerTracking, VRInput.ControllerPose? controllers, BodyJointLocationFB[]? bodyData)
    {
        return new TrackingData(
            handTracking ? hands : null,
            handTracking ? new HandTracking.HandPose(GetLeftHand(), GetRightHand()) : null,
            controllerTracking ? controllers : null,
            controllerTracking ? new VRInput.ControllerPose(GetLeftController(), GetRightController()) : null,
            bodyData: MergeBodyData(bodyData)
        );
    }

    private BodyJointLocationFB[]? MergeBodyData(BodyJointLocationFB[]? newBodyData)
    {
        if (newBodyData is not { } newData)
        {
            return null;
        }
        if (BodyData is not { } lastData)
        {
            return newBodyData;
        }
        return newData.Zip(lastData)
        .Select(pair =>
        {
            if (pair.First.LocationFlags.IsValidOrientation())
            {

                return pair.First;
            }
            else
            {
                return pair.Second;
            }
        })
        .ToArray();
    }

    internal static TrackingData Disabled()
    {
        return new TrackingData(null, null, null, null, null);
    }

    internal bool HasData()
    {
        return (GetLeftHand() != null || GetLeftController() != null)
            && (GetRightHand() != null || GetRightController() != null)
            || BodyData != null;
    }

    internal bool HasBodyData()
    {
        if (BodyData is not { } data)
        {
            return false;
        }
        return data.Any(d => d.LocationFlags.IsValidOrientation());
    }
}