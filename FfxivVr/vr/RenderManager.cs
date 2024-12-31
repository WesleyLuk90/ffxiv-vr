using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Silk.NET.Direct3D11;

namespace FfxivVR;

public unsafe class RenderManager(
    Renderer renderer,
    FramePrediction framePrediction,
    Logger logger,
    InputManager inputManager
)
{
    private RenderPhase? renderPhase = null;
    public bool RunRenderPhase()
    {
        var context = GetContext();
        var shouldPresent = true;
        if (renderPhase is LeftRenderPhase leftRenderPhase)
        {
            logger.Trace("Wait for VR frame sync");
            var frameState = leftRenderPhase.WaitFrame();
            framePrediction.MarkPredictedFrameTime(frameState.PredictedDisplayTime);
            renderer.StartFrame();
            // Skip presenting the left view to avoid flicker when displaying the right view
            shouldPresent = false;
            if (frameState.ShouldRender == 1)
            {
                logger.Trace("Render left eye");
                var leftLayer = renderer.RenderEye(context, leftRenderPhase.CreateEyeRender(), inputManager.GetAimLine());
                renderPhase = leftRenderPhase.Next(frameState, leftLayer);
            }
            else
            {
                logger.Trace("Frame skipped");
                renderer.SkipFrame(frameState);
                renderPhase = null;
            }
        }
        else if (renderPhase is RightRenderPhase rightRenderPhase)
        {
            logger.Trace("Render right eye");
            var rightLayer = renderer.RenderEye(context, rightRenderPhase.CreateEyeRender(), inputManager.GetAimLine());
            renderer.EndFrame(context, rightRenderPhase.FrameState, rightRenderPhase.Views, [rightRenderPhase.LeftLayer, rightLayer]);
            logger.Trace("End frame");
            renderPhase = null;
        }
        return shouldPresent;
    }

    internal void CopyGameRenderTexture(Eye eye)
    {
        renderer.CopyTexture(GetContext(), eye);
    }

    public void OnSessionEnd()
    {
        // Ensure we end the frame if we need to end the session
        if (renderPhase is LeftRenderPhase left)
        {
            renderer.StartFrame();
            renderer.SkipFrame(left.WaitFrame());
        }
        if (renderPhase is RightRenderPhase right)
        {
            renderer.SkipFrame(right.FrameState);
        }
        renderPhase = null;
        framePrediction.Reset();
    }

    internal void StartRender(CameraPhase phase)
    {
        renderPhase = phase.StartRender();
    }
    private static ID3D11DeviceContext* GetContext()
    {
        return (ID3D11DeviceContext*)Device.Instance()->D3D11DeviceContext;
    }
}