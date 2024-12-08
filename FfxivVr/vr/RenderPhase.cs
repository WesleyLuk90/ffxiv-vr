using Silk.NET.OpenXR;
using System.Threading.Tasks;

namespace FfxivVR;
abstract class RenderPhase;
class LeftRenderPhase(View[] views, Task<FrameState> waitFrameTask, float uiRotation) : RenderPhase
{
    public View[] Views = views;
    private readonly float uiRotation = uiRotation;

    public Task<FrameState> WaitFrameTask { get; } = waitFrameTask;

    public FrameState WaitFrame()
    {
        return WaitFrameTask.Result;
    }

    public EyeRender CreateEyeRender()
    {
        return new EyeRender(Eye.Left, Views[Eye.Left.ToIndex()], uiRotation);
    }

    public RightRenderPhase Next(FrameState frameState, CompositionLayerProjectionView leftLayer)
    {
        return new RightRenderPhase(frameState, leftLayer, Views, uiRotation);
    }
}
class RightRenderPhase(FrameState frameState, CompositionLayerProjectionView leftLayer, View[] views, float uiRotation) : RenderPhase
{
    public FrameState FrameState = frameState;
    public CompositionLayerProjectionView LeftLayer = leftLayer;

    public View[] Views = views;
    private readonly float uiRotation = uiRotation;

    public EyeRender CreateEyeRender()
    {
        return new EyeRender(Eye.Right, Views[Eye.Right.ToIndex()], uiRotation);
    }

}