using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
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
    private Posef? GetLeftAim()
    {
        return ControllerPose?.LeftAim ?? LastValidControllerPose?.LeftAim;
    }

    private Posef? GetRightAim()
    {
        return ControllerPose?.RightAim ?? LastValidControllerPose?.RightAim;
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
            controllerTracking ? new VRInput.ControllerPose(GetLeftPalm(), GetRightPalm(), GetLeftAim(), GetRightAim()) : null,
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

    public Tuple<Vector3D<float>, Vector3D<float>> GetAimRay()
    {
        if ((GetLeftAim() ?? GetRightAim()) is not { } aim)
        {
            return Tuple.Create(Vector3D<float>.Zero, new Vector3D<float>(1, 1, 1));
        }
        var start = aim.Position.ToVector3D();
        var rotation = aim.Orientation.ToQuaternion();
        var target = Vector3D.Transform(new Vector3D<float>(0, 0, -3), rotation);
        return Tuple.Create(start, start + target);
    }
}