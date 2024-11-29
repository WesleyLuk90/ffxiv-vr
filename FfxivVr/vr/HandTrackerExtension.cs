using Silk.NET.Core;
using Silk.NET.OpenXR;
using System;
using System.Linq;

namespace FfxivVR;
public unsafe class HandTrackerExtension(
    PfnVoidFunction xrCreateHandTrackerEXT,
    PfnVoidFunction xrDestroyHandTrackerEXT,
    PfnVoidFunction xrLocateHandJointsEXT) : IDisposable
{
    delegate* unmanaged[Cdecl]<Session, HandTrackerCreateInfoEXT*, HandTrackerEXT*, Result> xrCreateHandTrackerEXT =
        (delegate* unmanaged[Cdecl]<Session, HandTrackerCreateInfoEXT*, HandTrackerEXT*, Result>)xrCreateHandTrackerEXT.Handle;
    delegate* unmanaged[Cdecl]<HandTrackerEXT, HandJointsLocateInfoEXT*, HandJointLocationsEXT*, Result> xrLocateHandJointsEXT =
        (delegate* unmanaged[Cdecl]<HandTrackerEXT, HandJointsLocateInfoEXT*, HandJointLocationsEXT*, Result>)xrLocateHandJointsEXT.Handle;
    delegate* unmanaged[Cdecl]<HandTrackerEXT, Result> xrDestroyHandTrackerEXT =
        (delegate* unmanaged[Cdecl]<HandTrackerEXT, Result>)xrDestroyHandTrackerEXT.Handle;

    private HandTrackerEXT leftHandTracker = new HandTrackerEXT();
    private HandTrackerEXT rightHandTracker = new HandTrackerEXT();

    public void Dispose()
    {
        xrDestroyHandTrackerEXT(leftHandTracker).CheckResult("DestroyHandTrackerEXT");
        xrDestroyHandTrackerEXT(rightHandTracker).CheckResult("DestroyHandTrackerEXT");
    }

    public void Initialize(Session session)
    {
        HandTrackerCreateInfoEXT leftCreateInfo = new HandTrackerCreateInfoEXT(hand: HandEXT.LeftExt, handJointSet: HandJointSetEXT.DefaultExt);
        fixed (HandTrackerEXT* ptr = &leftHandTracker)
        {
            xrCreateHandTrackerEXT(session, &leftCreateInfo, ptr).CheckResult("CreateHandTrackerEXT");
        }
        HandTrackerCreateInfoEXT rightCreateInfo = new HandTrackerCreateInfoEXT(hand: HandEXT.RightExt, handJointSet: HandJointSetEXT.DefaultExt);
        fixed (HandTrackerEXT* ptr = &rightHandTracker)
        {
            xrCreateHandTrackerEXT(session, &rightCreateInfo, ptr).CheckResult("CreateHandTrackerEXT");
        }
    }

    private uint JointCount = 26; // Number of values in HandJointEXT enum
    public HandPose GetHandTrackingData(Space space, long predictedTime)
    {
        HandJointsMotionRangeInfoEXT motionRangeInfo = new HandJointsMotionRangeInfoEXT(handJointsMotionRange: HandJointsMotionRangeEXT.UnobstructedExt);
        var locateInfo = new HandJointsLocateInfoEXT(
            baseSpace: space,
            time: predictedTime
        );
        var leftHand = new HandJointLocationEXT[JointCount];
        var leftSpan = new Span<HandJointLocationEXT>(leftHand);
        fixed (HandJointLocationEXT* ptr = leftSpan)
        {
            var locations = new HandJointLocationsEXT(
                jointCount: JointCount,
                jointLocations: ptr
            );
            xrLocateHandJointsEXT(leftHandTracker, &locateInfo, &locations).CheckResult("LocateHandJointsEXT");
        }
        var rightHand = new HandJointLocationEXT[JointCount];
        var rightSpan = new Span<HandJointLocationEXT>(rightHand);
        fixed (HandJointLocationEXT* ptr = rightSpan)
        {
            var locations = new HandJointLocationsEXT(
                jointCount: JointCount,
                jointLocations: ptr
            );
            xrLocateHandJointsEXT(rightHandTracker, &locateInfo, &locations).CheckResult("LocateHandJointsEXT");
        }
        var leftValid = leftHand.Any(j => j.LocationFlags != 0);
        var rightValid = rightHand.Any(j => j.LocationFlags != 0);
        return new HandPose(leftValid ? leftHand : null, rightValid ? rightHand : null);
    }

    public class HandPose(HandJointLocationEXT[]? LeftHand, HandJointLocationEXT[]? RightHand)
    {
        public HandJointLocationEXT[]? LeftHand { get; } = LeftHand;
        public HandJointLocationEXT[]? RightHand { get; } = RightHand;
    }
}