using Silk.NET.OpenXR;
using Silk.NET.OpenXR.Extensions.FB;
using System;

namespace FfxivVR;

public unsafe class BodyTracking(
    FBBodyTracking fBBodyTracking,
    Logger logger
)
{

    private BodyTrackerFB bodyTracker;
    public void Initialize(Session session)
    {
        var info = new BodyTrackerCreateInfoFB(bodyJointSet: BodyJointSetFB.DefaultFB);
        fBBodyTracking.CreateBodyTrackerFB(session, ref info, ref bodyTracker).CheckResult("CreateBodyTrackerFB");
        var skeleton = new BodySkeletonFB(
            jointCount: (uint?)BodyJointFB.CountFB
        );
        var array = new BodySkeletonJointFB[(int)BodyJointFB.CountFB];
        fixed (BodySkeletonJointFB* ptr = new Span<BodySkeletonJointFB>(array))
        {
            skeleton.Joints = ptr;
            fBBodyTracking.GetBodySkeletonFB(bodyTracker, ref skeleton).CheckResult("GetBodySkeletonFB");
        }
    }

    public BodyJointLocationFB[] GetData(Space space, long time)
    {
        var locateInfo = new BodyJointsLocateInfoFB(next: null, baseSpace: space, time: time);
        var locationsArray = new BodyJointLocationFB[(int)BodyJointFB.CountFB];
        var locations = new BodyJointLocationsFB(next: null, jointCount: (uint?)BodyJointFB.CountFB);
        fixed (BodyJointLocationFB* ptr = new Span<BodyJointLocationFB>(locationsArray))
        {
            locations.JointLocations = ptr;
            fBBodyTracking.LocateBodyJointsFB(bodyTracker, ref locateInfo, ref locations).CheckResult("LocateBodyJointsFB");
        }
        return locationsArray;
    }

    public void Dispose()
    {
        fBBodyTracking.DestroyBodyTrackerFB(bodyTracker).LogResult("DestroyBodyTrackerFB", logger);
    }

    internal BodySkeletonJointFB[] GetSkeleton()
    {
        var joints = new BodySkeletonJointFB[(int)BodyJointFB.CountFB];
        fixed (BodySkeletonJointFB* ptr = new Span<BodySkeletonJointFB>(joints))
        {
            var skele = new BodySkeletonFB(jointCount: (uint?)BodyJointFB.CountFB, joints: ptr);
            fBBodyTracking.GetBodySkeletonFB(bodyTracker, ref skele).CheckResult("GetBodySkeletonFB");
        }
        return joints;
    }
}