using Silk.NET.OpenXR;
using System.Linq;

namespace FfxivVR;

public class TrackingData(
    HandTracking.HandPose? handPose,
    HandTracking.HandPose? lastValidHandPose,
    PalmPose? controllerPose,
    PalmPose? lastValidControllerPose,
    BodyJointLocationFB[]? bodyData)
{
    public HandTracking.HandPose? HandPose { get; } = handPose;
    public HandTracking.HandPose? LastValidHandPose { get; } = lastValidHandPose;
    public PalmPose? ControllerPose { get; } = controllerPose;
    public PalmPose? LastValidControllerPose { get; } = lastValidControllerPose;
    public BodyJointLocationFB[]? BodyData { get; } = bodyData;
    public HandJointLocationEXT[]? GetLeftHand()
    {
        return HandPose?.LeftHand ?? LastValidHandPose?.LeftHand;
    }
    public HandJointLocationEXT[]? GetRightHand()
    {
        return HandPose?.RightHand ?? LastValidHandPose?.RightHand;
    }

    public Posef? GetLeftPalm()
    {
        return ControllerPose?.LeftPalm ?? LastValidControllerPose?.LeftPalm;
    }

    public Posef? GetRightPalm()
    {
        return ControllerPose?.RightPalm ?? LastValidControllerPose?.RightPalm;
    }

    internal static TrackingData CreateNew(HandTracking.HandPose? hands, PalmPose? controllers, BodyJointLocationFB[]? bodyData)
    {
        return new TrackingData(hands, null, controllers, null, bodyData);
    }

    internal TrackingData Update(bool handTracking, HandTracking.HandPose? hands, bool controllerTracking, PalmPose? controllers, BodyJointLocationFB[]? bodyData)
    {
        return new TrackingData(
            handTracking ? hands : null,
            handTracking ? new HandTracking.HandPose(GetLeftHand(), GetRightHand()) : null,
            controllerTracking ? controllers : null,
            controllerTracking ? new PalmPose(GetLeftPalm(), GetRightPalm()) : null,
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
        return (GetLeftHand() != null || GetLeftPalm() != null)
            && (GetRightHand() != null || GetRightPalm() != null)
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