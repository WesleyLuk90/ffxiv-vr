using Silk.NET.OpenXR;
using System.Threading.Tasks;

namespace FfxivVR;
abstract class RenderPhase;
class LeftRenderPhase(View[] views, Task<FrameState> waitFrameTask, TrackingData trackingData) : RenderPhase
{
    public View[] Views = views;
    public Task<FrameState> WaitFrameTask { get; } = waitFrameTask;
    public TrackingData TrackingData { get; } = trackingData;

    public FrameState WaitFrame()
    {
        return WaitFrameTask.Result;
    }

    public EyeRender CreateEyeRender()
    {
        return new EyeRender(Eye.Left, Views[Eye.Left.ToIndex()]);
    }

    public RightRenderPhase Next(FrameState frameState, CompositionLayerProjectionView leftLayer)
    {
        return new RightRenderPhase(frameState, leftLayer, Views, TrackingData);
    }
}
class RightRenderPhase(FrameState frameState, CompositionLayerProjectionView leftLayer, View[] views, TrackingData trackingData) : RenderPhase
{
    public FrameState FrameState = frameState;
    public CompositionLayerProjectionView LeftLayer = leftLayer;

    public View[] Views = views;
    public TrackingData TrackingData = trackingData;

    public EyeRender CreateEyeRender()
    {
        return new EyeRender(Eye.Right, Views[Eye.Right.ToIndex()]);
    }

}