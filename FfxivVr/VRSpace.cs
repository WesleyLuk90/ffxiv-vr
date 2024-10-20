using Silk.NET.Maths;
using Silk.NET.OpenXR;

namespace FfxivVR;
unsafe public class VRSpace
{
    private readonly XR xr;
    private readonly Logger logger;
    private readonly VRSystem system;
    public Space LocalSpace = new Space();
    private Space viewSpace = new Space();
    public VRSpace(XR xr, Logger logger, VRSystem system)
    {
        this.xr = xr;
        this.logger = logger;
        this.system = system;
    }


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
        xr.CreateReferenceSpace(system.Session, ref viewSpaceCreateInfo, ref viewSpace).CheckResult("CreateReferenceSpace");
    }

    public void Dispose()
    {
        xr.DestroySpace(LocalSpace).LogResult("DestroySpace", logger);
        xr.DestroySpace(viewSpace).LogResult("DestroySpace", logger);
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
    }

    private Posef GetCurrentPose()
    {
        Vector3D<float> scale;
        Vector3D<float> translation;
        Quaternion<float> rotation;
        Matrix4X4.Decompose(currentSpaceTransform, out scale, out rotation, out translation);

        var pose = new Posef(
            orientation: rotation.ToQuaternionf(),
            position: translation.ToVector3f()
        );
        return pose;
    }

    internal void RecenterCamera(long xrTime)
    {
        logger.Info("Recentering camera");
        var spaceLocation = new SpaceLocation(next: null);
        xr.LocateSpace(viewSpace, LocalSpace, xrTime, ref spaceLocation).CheckResult("LocateSpace");
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
    }
}
