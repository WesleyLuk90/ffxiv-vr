using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Windows.Win32;

namespace FfxivVR;

public unsafe class RenderManager(
    Renderer renderer,
    FramePrediction framePrediction,
    Logger logger,
    VRInput vrInput,
    Configuration configuration,
    VRUI vrUI,
    Debugging debugging,
    ResolutionManager resolutionManager
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
                var aim = GetAimLine();
                if (aim != null)
                {
                    debugging.DebugShow("Aim Position", aim);
                    var pos = vrUI.GetPosition(aim);
                    debugging.DebugShow("Pos", pos);
                    if (pos.X > 0 && pos.X < 1 && pos.Y > 0 && pos.Y < 1)
                    {
                        if (resolutionManager.WindowToScreen(pos) is { } screenCoords)
                        {
                            PInvoke.SetCursorPos(screenCoords.X, screenCoords.Y);
                        }
                    }
                }
                var leftLayer = renderer.RenderEye(context, leftRenderPhase.CreateEyeRender(), GetAimLine());
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
            var rightLayer = renderer.RenderEye(context, rightRenderPhase.CreateEyeRender(), GetAimLine());
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

    public Line? GetAimLine()
    {
        var ray = GetAimRay();
        if (ray == null)
        {
            return null;
        }
        return vrUI.Intersect(ray);
    }

    public Ray? GetAimRay()
    {
        if (!configuration.EnableMouse)
        {
            return null;
        }
        var aimPose = vrInput.GetAimPose();
        if ((aimPose?.LeftAim ?? aimPose?.RightAim) is not { } aim)
        {
            return null;
        }
        var start = aim.Position.ToVector3D();
        var rotation = aim.Orientation.ToQuaternion();
        var direction = Vector3D.Transform(new Vector3D<float>(0, 0, -1), rotation);
        return new Ray(start, direction);
    }
}