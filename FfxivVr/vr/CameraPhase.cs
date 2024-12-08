using Silk.NET.OpenXR;
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
    public Eye Eye = eye;
    public View[] Views = views;
    public VRCameraMode CameraMode = caneraMode;
    private readonly float uiRotation = uiRotation;

    private Task<FrameState> WaitFrameTask { get; } = waitFrameTask;
    public TrackingData TrackingData = trackingData;
    public View CurrentView()
    {
        return Views[Eye.ToIndex()];
    }

    internal RenderPhase StartRender()
    {
        return new LeftRenderPhase(Views, WaitFrameTask, uiRotation);
    }
}