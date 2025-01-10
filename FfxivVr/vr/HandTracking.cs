using Silk.NET.OpenXR;
using Silk.NET.OpenXR.Extensions.EXT;
using System;
using System.Linq;

namespace FfxivVR;

public unsafe class HandTracking(
    ExtHandTracking handTracking
) : IDisposable
{
    private HandTrackerEXT leftHandTracker = new HandTrackerEXT();
    private HandTrackerEXT rightHandTracker = new HandTrackerEXT();

    public void Dispose()
    {
        handTracking.DestroyHandTracker(leftHandTracker).CheckResult("DestroyHandTrackerEXT");
        handTracking.DestroyHandTracker(rightHandTracker).CheckResult("DestroyHandTrackerEXT");
    }

    public void Initialize(Session session)
    {
        var sources = new Span<HandTrackingDataSourceEXT>([HandTrackingDataSourceEXT.UnobstructedExt, HandTrackingDataSourceEXT.ControllerExt]);
        fixed (HandTrackingDataSourceEXT* sourcesPointer = sources)
        {
            HandTrackingDataSourceInfoEXT dataSourceInfo = new HandTrackingDataSourceInfoEXT(
                next: null,
                requestedDataSourceCount: (uint)sources.Length,
                requestedDataSources: sourcesPointer
            );
            HandTrackerCreateInfoEXT leftCreateInfo = new HandTrackerCreateInfoEXT(next: &dataSourceInfo, hand: HandEXT.LeftExt, handJointSet: HandJointSetEXT.DefaultExt);
            fixed (HandTrackerEXT* ptr = &leftHandTracker)
            {
                handTracking.CreateHandTracker(session, &leftCreateInfo, ptr).CheckResult("CreateHandTrackerEXT");
            }
            HandTrackerCreateInfoEXT rightCreateInfo = new HandTrackerCreateInfoEXT(next: &dataSourceInfo, hand: HandEXT.RightExt, handJointSet: HandJointSetEXT.DefaultExt);
            fixed (HandTrackerEXT* ptr = &rightHandTracker)
            {
                handTracking.CreateHandTracker(session, &rightCreateInfo, ptr).CheckResult("CreateHandTrackerEXT");
            }
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
        var leftDataSource = new HandTrackingDataSourceStateEXT(next: null);
        fixed (HandJointLocationEXT* ptr = leftSpan)
        {
            var locations = new HandJointLocationsEXT(
                next: &leftDataSource,
                jointCount: JointCount,
                jointLocations: ptr
            );
            handTracking.LocateHandJoints(leftHandTracker, &locateInfo, &locations).CheckResult("LocateHandJointsEXT");
        }
        var rightHand = new HandJointLocationEXT[JointCount];
        var rightSpan = new Span<HandJointLocationEXT>(rightHand);
        var rightDataSource = new HandTrackingDataSourceStateEXT(next: null);
        fixed (HandJointLocationEXT* ptr = rightSpan)
        {
            var locations = new HandJointLocationsEXT(
                next: &rightDataSource,
                jointCount: JointCount,
                jointLocations: ptr
            );
            handTracking.LocateHandJoints(rightHandTracker, &locateInfo, &locations).CheckResult("LocateHandJointsEXT");
        }
        var leftValid = leftHand.Any(j => j.LocationFlags != 0);
        var rightValid = rightHand.Any(j => j.LocationFlags != 0);
        bool isFromController = (leftDataSource.IsActive != 0 && leftDataSource.DataSource == HandTrackingDataSourceEXT.ControllerExt) ||
            (rightDataSource.IsActive != 0 && rightDataSource.DataSource == HandTrackingDataSourceEXT.ControllerExt);
        // This seems to sometimes return true for a frame after the controller is no longer being tracked so make sure this is true for at least 2 frames as a work around
        var currentIsFromController = isFromController && LastIsFromController;
        LastIsFromController = isFromController;
        return new HandPose(leftValid ? leftHand : null, rightValid ? rightHand : null, isFromController: currentIsFromController);
    }

    private bool LastIsFromController = false;

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
}