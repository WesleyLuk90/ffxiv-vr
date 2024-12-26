using Silk.NET.OpenXR;
using System;
using System.Threading.Tasks;

namespace FfxivVR;

public class CameraPhase(
    Eye eye,
    View[] views,
    Task<FrameState> waitFrameTask,
    TrackingData trackingData,
    VRCameraMode caneraMode,
    float uiRotation)
{
    public Eye Eye { get; private set; } = eye;
    public View[] Views = views;
    public VRCameraMode CameraMode = caneraMode;
    private readonly float uiRotation = uiRotation;

    private Task<FrameState> WaitFrameTask { get; } = waitFrameTask;
    private GameCamera? GameCamera = null;

    public TrackingData TrackingData = trackingData;
    public View CurrentView(bool includeHeadMovement)
    {
        if (includeHeadMovement)
        {
            return Views[Eye.ToIndex()];
        }
        else
        {
            var view = Views[Eye.ToIndex()];
            var center = (Views[0].Pose.Position.ToVector3D() + Views[0].Pose.Position.ToVector3D()) / 2;
            view.Pose.Position = (view.Pose.Position.ToVector3D() - center).ToVector3f();
            return view;
        }
    }
    public void SwitchToRightEye()
    {
        GameCamera = null;
        Eye = Eye.Right;
    }

    internal RenderPhase StartRender()
    {
        return new LeftRenderPhase(Views, WaitFrameTask, uiRotation, TrackingData);
    }

    // Create it once per eye so we have consistent data    
    public GameCamera? GetGameCamera(Func<GameCamera?> factory)
    {
        if (GameCamera is GameCamera g)
        {
            return g;
        }
        GameCamera = factory();
        return GameCamera;
    }
}