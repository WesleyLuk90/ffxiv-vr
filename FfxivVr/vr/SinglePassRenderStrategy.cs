using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Numerics;

namespace FfxivVR;

public unsafe class SinglePassRenderStrategy(
    RenderPipelineInjector renderPipelineInjector,
    Renderer renderer,
    VRState state,
    Logger logger,
    FramePrediction framePrediction,
    InputManager inputManager,
    VRCamera vrCamera,
    FfxivVrNative ffxivVrNative,
    DxDeviceContext dxDeviceContext,
    Configuration configuration,
    WaitFrameService waitFrameService
) : IRenderStrategy, IDisposable
{
    public VRSessionData? VRSessionData { get; private set; }
    public VRSessionData? RenderSessionData { get; private set; }

    private FrameState? renderFrameState = null;
    // private Task<FrameState>? waitFrameTask;
    private Vector2D<uint>? pendingNativeSize;
    private nint lastCompositingTexture;
    private nint lastGeometryTexture;

    public void Initialize(Vector2D<uint> size)
    {
        logger.Info($"Configure VR with size {size}");
        pendingNativeSize = size;
    }

    public void Dispose()
    {
        ffxivVrNative.Dispose();
    }

    private void EnsureNativeInitialized()
    {
        if (pendingNativeSize is not Vector2D<uint> size)
        {
            return;
        }
        var compositingTexture = GameTextures.GetCompositingTexture();
        var geometryTexture = GameTextures.GetGeometryTexture();
        if (compositingTexture == null || geometryTexture == null)
        {
            return;
        }
        if (compositingTexture->ActualWidth != size.X || compositingTexture->ActualHeight != size.Y)
        {
            return;
        }
        if (geometryTexture->ActualWidth != size.X || geometryTexture->ActualHeight != size.Y)
        {
            return;
        }
        var compositingPtr = (nint)compositingTexture->D3D11Texture2D;
        var geometryPtr = (nint)geometryTexture->D3D11Texture2D;
        if (ffxivVrNative.Initialized)
        {
            if (compositingPtr == lastCompositingTexture && geometryPtr == lastGeometryTexture)
            {
                return;
            }
            logger.Info("Compositing/geometry render targets were reallocated, reinitializing native render");
            ffxivVrNative.Shutdown();
        }
        logger.Info($"Compositing/geometry textures resized to {size}, initializing native render");
        if (ffxivVrNative.Initialize(dxDeviceContext, size, compositingTexture->D3D11Texture2D, geometryTexture->D3D11Texture2D, configuration.DumpDirectory))
        {
            lastCompositingTexture = compositingPtr;
            lastGeometryTexture = geometryPtr;
        }
    }

    public void PrepareCameraPhase(View[] views, VRInputData inputData, VRCameraMode cameraType)
    {
        VRSessionData = new VRSessionData(views, inputData, cameraType);
        var centerView = vrCamera.ComputeCenterView(views[Eye.Left.ToIndex()], views[Eye.Right.ToIndex()]);
        SetEyeProjection(Eye.Left, centerView, views[Eye.Left.ToIndex()]);
        SetEyeProjection(Eye.Right, centerView, views[Eye.Right.ToIndex()]);
    }

    private void SetEyeProjection(Eye eye, View centerView, View eyeView)
    {
        var deltaView = vrCamera.ComputeEyeDeltaView(centerView, eyeView);
        var projection = Matrix4x4.Transpose(vrCamera.ComputeGameProjectionMatrix(eyeView));
        ffxivVrNative.SetProjectionMatrix(eye, (float*)&deltaView, (float*)&projection);
    }
    public void UpdateCamera(Camera* camera)
    {
        if (state.SessionRunning && VRSessionData is VRSessionData phase)
        {
            var centerView = vrCamera.ComputeCenterView(phase.Views[Eye.Left.ToIndex()], phase.Views[Eye.Right.ToIndex()]);
            if (pendingNativeSize is Vector2D<uint> size && size.X > 0 && size.Y > 0)
            {
                centerView.Fov = VRCamera.WidenFovToAspect(centerView.Fov, (float)size.X / size.Y);
            }
            vrCamera.UpdateCamera(camera, phase.GetGameCamera(vrCamera.CreateGameCamera), phase.CameraMode, centerView);
        }
    }


    public void OnStartUI()
    {
        renderPipelineInjector.QueueRenderTargetCommand(Eye.Left);
        renderPipelineInjector.QueueClearCommand();
    }


    Eye eye = Eye.Left;
    public void OnStartUIRender(Eye? _eye)
    {
        if (_eye == null)
        {
        }
        else
        {
            var texture = GameTextures.GetCompositingTexture();
            var depth = GameTextures.GetGameDepthTexture();
            renderer.CopyTexture(eye, (ID3D11Texture2D*)texture->D3D11Texture2D, (ID3D11Texture2D*)depth->D3D11Texture2D);
            if (eye == Eye.Left)
            {
                eye = Eye.Right;
            }
            else
            {
                eye = Eye.Left;
            }
        }
    }

    public bool ShouldSecondRender()
    {
        return false;
    }

    public bool OnPresentFrame()
    {
        if (!state.SessionRunning)
        {
            if (VRSessionData != null)
            {
                logger.Debug("Session not running, discarding phase");
                VRSessionData = null;
            }
            OnSessionEnd();
            return true;
        }
        if (RenderSessionData is VRSessionData render && renderFrameState is FrameState frameState)
        {
            logger.Trace("Wait for VR frame sync");
            renderer.StartFrame();
            if (frameState.ShouldRender == 1)
            {
                ffxivVrNative.SetRenderHooksEnabled(false);
                // TODO, maybe render this earlier so we copy the clean ui?
                var leftLayer = renderer.RenderEye(new EyeRender(Eye.Left, render.Views[Eye.Left.ToIndex()]), inputManager.GetAimLine(AimType.Head, render.VRInputData));
                var rightLayer = renderer.RenderEye(new EyeRender(Eye.Right, render.Views[Eye.Right.ToIndex()]), inputManager.GetAimLine(AimType.Head, render.VRInputData));
                ffxivVrNative.SetRenderHooksEnabled(true);
                renderer.EndFrame(frameState, render.Views, [leftLayer, rightLayer]);
            }
            else
            {
                logger.Trace("Frame skipped");
                renderer.SkipFrame(frameState);
            }
            RenderSessionData = null;
            renderFrameState = null;
        }
        if (VRSessionData is VRSessionData phase)
        {
            var nextFrameState = waitFrameService.WaitFrame();
            if (nextFrameState.ShouldRender == 1)
            {
                framePrediction.MarkPredictedFrameTime(nextFrameState.PredictedDisplayTime);
                renderFrameState = nextFrameState;
                RenderSessionData = phase;
                VRSessionData = null;
            }
            else
            {
                renderFrameState = null;
                renderer.StartFrame();
                renderer.SkipFrame(nextFrameState);
            }
        }
        EnsureNativeInitialized();
        return true;
    }

    public void OnSessionEnd()
    {
        // Ensure we end the frame if we need to end the session
        VRSessionData = null;
        if (renderFrameState is FrameState fs)
        {
            renderer.StartFrame();
            renderer.SkipFrame(fs);
            renderFrameState = null;
        }
        // waitFrameTask = null;
        framePrediction.Reset();
    }
}