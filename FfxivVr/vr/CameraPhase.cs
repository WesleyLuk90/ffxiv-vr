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
    public View CurrentView()
    {
        return Views[Eye.ToIndex()];
    }
    public void SwitchToRightEye()
    {
        GameCamera = null;
        Eye = Eye.Right;
    }

    internal RenderPhase StartRender()
    {
        return new LeftRenderPhase(Views, WaitFrameTask, uiRotation);
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