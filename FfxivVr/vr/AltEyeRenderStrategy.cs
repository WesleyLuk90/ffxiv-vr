using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System.Threading.Tasks;

namespace FfxivVR;

public unsafe class AltEyeRenderStrategy(
    Renderer renderer,
    FramePrediction framePrediction,
    Logger logger,
    InputManager inputManager,
    VRState state,
    RenderPipelineInjector renderPipelineInjector,
    FirstPersonManager firstPersonManager,
    VRCamera vrCamera,
    WaitFrameService waitFrameService
) : IRenderStrategy
{
    public VRSessionData? VRSessionData { get; private set; }
    private Eye eye;
    private Task<FrameState>? waitFrameTask;
    private RenderPhase? renderPhase = null;

    public void Initialize(Vector2D<uint> size)
    {
    }

    public void UpdateCamera(Camera* camera)
    {
        if (state.SessionRunning && VRSessionData is VRSessionData phase)
        {
            logger.Trace($"Set {eye} camera matrix");
            var view = CurrentView(phase, phase.CameraMode.UseHeadMovement);
            firstPersonManager.UpdateRotation(MathFactory.GetYaw(view.Pose.Orientation.ToQuaternion()));
            vrCamera.UpdateCamera(camera, phase.GetGameCamera(vrCamera.CreateGameCamera), phase.CameraMode, view);
        }
    }

    private View CurrentView(VRSessionData phase, bool includeHeadMovement)
    {
        if (includeHeadMovement)
        {
            return phase.Views[eye.ToIndex()];
        }
        else
        {
            var view = phase.Views[eye.ToIndex()];
            var center = (phase.Views[0].Pose.Position.ToVector3D() + phase.Views[1].Pose.Position.ToVector3D()) / 2;
            view.Pose.Position = (view.Pose.Position.ToVector3D() - center).ToVector3f();
            return view;
        }
    }

    public void PrepareCameraPhase(View[] views, VRInputData inputData, VRCameraMode cameraType)
    {
        eye = Eye.Left;
        waitFrameTask = waitFrameService.WaitFrameTask();
        VRSessionData = new VRSessionData(views, inputData, cameraType);
    }

    public void OnStartUI()
    {
        if (VRSessionData != null)
        {
            logger.Trace($"Queue {eye} render");
            renderPipelineInjector.QueueRenderTargetCommand(eye);
            // Only clear the left view to get a clean render for copying
            // The right view we skip clearing which lets it display the VR view
            if (eye == Eye.Left)
            {
                renderPipelineInjector.QueueClearCommand();
            }
        }
    }

    public void OnStartUIRender(Eye? eye)
    {
        if (eye is { } e)
        {
            var texture = GameTextures.GetCompositingTexture();
            var depth = GameTextures.GetGameDepthTexture();
            renderer.CopyTexture(e, (ID3D11Texture2D*)texture->D3D11Texture2D, (ID3D11Texture2D*)depth->D3D11Texture2D);
        }
    }

    public bool ShouldSecondRender()
    {
        return VRSessionData != null && eye == Eye.Right;
    }

    public void ExecuteCommands(ImmediateContext* context, int commandListId, System.Action executeOriginal)
    {
        executeOriginal();
    }

    public bool OnPresentFrame()
    {
        if (!state.SessionRunning)
        {
            if (VRSessionData != null)
            {
                logger.Debug("Session not running, discarding phases");
                VRSessionData = null;
            }
            OnSessionEnd();
        }
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
                var leftLayer = renderer.RenderEye(leftRenderPhase.CreateEyeRender(), inputManager.GetAimLine(AimType.Head, leftRenderPhase.VRInputData));
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
            var rightLayer = renderer.RenderEye(rightRenderPhase.CreateEyeRender(), inputManager.GetAimLine(AimType.Head, rightRenderPhase.VRInputData));
            renderer.EndFrame(rightRenderPhase.FrameState, rightRenderPhase.Views, [rightRenderPhase.LeftLayer, rightLayer]);
            logger.Trace("End frame");
            renderPhase = null;
        }
        if (VRSessionData is VRSessionData phase)
        {
            switch (eye)
            {
                case Eye.Left:
                    {
                        logger.Trace("Switching camera phase to right eye");
                        phase.ResetGameCamera();
                        eye = Eye.Right;
                        renderPhase = new LeftRenderPhase(phase.Views, waitFrameTask!, phase.VRInputData);
                        break;
                    }
                case Eye.Right:
                    {
                        VRSessionData = null;
                        break;
                    }
                default: break;
            }
        }
        return shouldPresent;
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
}