using Silk.NET.Maths;
using Silk.NET.OpenXR;

namespace FfxivVR;
unsafe public class VRSpace(
    XR xr,
    Logger logger,
    VRSystem system,
    VRUI vrUI)
{

    public Space LocalSpace = new Space();
    private Space ViewSpace = new Space();
    private Space? StageReferenceSpace = null;

    internal void Initialize()
    {
        CreateReferenceSpace();
    }

    private Matrix4X4<float> currentSpaceTransform = Matrix4X4<float>.Identity;

    private void CreateReferenceSpace()
    {
        var localSpaceCreateInfo = new ReferenceSpaceCreateInfo(
            referenceSpaceType: ReferenceSpaceType.Local,
            poseInReferenceSpace: GetCurrentPose()
        );
        xr.CreateReferenceSpace(system.Session, ref localSpaceCreateInfo, ref LocalSpace).CheckResult("CreateReferenceSpace");
        var viewSpaceCreateInfo = new ReferenceSpaceCreateInfo(
            referenceSpaceType: ReferenceSpaceType.View,
            poseInReferenceSpace: new Posef(orientation: new Quaternionf(0, 0, 0, 1), position: new Vector3f(0, 0, 0))
        );
        xr.CreateReferenceSpace(system.Session, ref viewSpaceCreateInfo, ref ViewSpace).CheckResult("CreateReferenceSpace");
        var referenceSpaceTypes = xr.GetAvailableReferenceSpaceTypes(system.Session);
        if (referenceSpaceTypes.Contains(ReferenceSpaceType.Stage))
        {
            var stageCreateInfo = new ReferenceSpaceCreateInfo(
                referenceSpaceType: ReferenceSpaceType.Stage,
                poseInReferenceSpace: GetCurrentPose()
            );
            var stage = new Space();
            xr.CreateReferenceSpace(system.Session, ref stageCreateInfo, ref stage).CheckResult("CreateReferenceSpace");
            StageReferenceSpace = stage;
            logger.Debug($"Stage reference space available");
        }
    }

    public void Dispose()
    {
        xr.DestroySpace(LocalSpace).LogResult("DestroySpace", logger);
        xr.DestroySpace(ViewSpace).LogResult("DestroySpace", logger);
        if (StageReferenceSpace is Space stage)
        {
            xr.DestroySpace(stage).LogResult("DestroySpace", logger);
        }
    }
    internal void ResetCamera()
    {
        logger.Info("Resetting camera");
        var oldSpace = LocalSpace;
        currentSpaceTransform = Matrix4X4<float>.Identity;
        Posef pose = GetCurrentPose();
        LocalSpace = new Space();
        var localSpaceCreateInfo = new ReferenceSpaceCreateInfo(
        referenceSpaceType: ReferenceSpaceType.Local,
            poseInReferenceSpace: pose
        );
        xr.CreateReferenceSpace(system.Session, ref localSpaceCreateInfo, ref LocalSpace).CheckResult("CreateReferenceSpace");
        xr.DestroySpace(oldSpace).CheckResult("DestroySpace");
        vrUI.ResetAngle();
    }

    private Posef GetCurrentPose()
    {
        Matrix4X4.Decompose(currentSpaceTransform, out _, out Quaternion<float> rotation, out Vector3D<float> translation);

        var pose = new Posef(
            orientation: rotation.ToQuaternionf(),
            position: translation.ToVector3f()
        );
        return pose;
    }

    internal void RecenterCamera(long xrTime)
    {
        logger.Info($"Recentering camera");
        var spaceLocation = new SpaceLocation(next: null);
        xr.LocateSpace(ViewSpace, LocalSpace, xrTime, ref spaceLocation).CheckResult("LocateSpace");
        var oldSpace = LocalSpace;
        var pose = spaceLocation.Pose;
        var positionMatrix = Matrix4X4.CreateTranslation(pose.Position.ToVector3D());
        var newRotation = pose.Orientation.ToQuaternion();
        newRotation.X = 0;
        newRotation.Z = 0;
        newRotation = Quaternion<float>.Normalize(newRotation);
        var rotationMatrix = Matrix4X4.CreateFromQuaternion(newRotation);

        currentSpaceTransform = rotationMatrix * positionMatrix * currentSpaceTransform;

        LocalSpace = new Space();
        var localSpaceCreateInfo = new ReferenceSpaceCreateInfo(
        referenceSpaceType: ReferenceSpaceType.Local,
            poseInReferenceSpace: GetCurrentPose()
        );
        xr.CreateReferenceSpace(system.Session, ref localSpaceCreateInfo, ref LocalSpace).CheckResult("CreateReferenceSpace");
        xr.DestroySpace(oldSpace).CheckResult("DestroySpace");
        vrUI.ResetAngle();
    }

    public float? GetLocalSpaceHeight(long predictedDisplayTime)
    {
        if (StageReferenceSpace is Space stage)
        {
            var location = new SpaceLocation(next: null);
            xr.LocateSpace(LocalSpace, stage, predictedDisplayTime, ref location).CheckResult("LocateSpace");
            return location.Pose.Position.Y;
        }
        return null;
    }
}