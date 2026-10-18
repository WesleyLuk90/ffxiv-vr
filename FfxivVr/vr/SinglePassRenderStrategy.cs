using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Collections.Concurrent;
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
    WaitFrameService waitFrameService,
    ITargetManager targetManager,
    IGameGui gameGui,
    Resources resources,
    NameplateModifier nameplateModifier
) : IRenderStrategy, IDisposable
{
    public VRSessionData? VRSessionData { get; private set; }
    public VRSessionData? RenderSessionData { get; private set; }

    private FrameState? renderFrameState = null;
    private Vector2D<uint>? pendingNativeSize;
    private nint lastCompositingTexture;
    private nint lastGeometryTexture;
    private const int MaxPendingTargetCursorDepths = 3;
    private readonly ConcurrentQueue<float?> pendingTargetCursorDepths = new();
    private Matrix4x4? centerViewProjection;

    public void Initialize(Vector2D<uint> size)
    {
        logger.Debug($"Configure VR with size {size}");
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
            logger.Debug("Compositing/geometry render targets were reallocated, reinitializing native render");
            ffxivVrNative.Shutdown();
        }
        logger.Debug($"Compositing/geometry textures resized to {size}, initializing native render");
        if (ffxivVrNative.Initialize(dxDeviceContext, size, compositingTexture->D3D11Texture2D, geometryTexture->D3D11Texture2D, configuration.DumpDirectory))
        {
            lastCompositingTexture = compositingPtr;
            lastGeometryTexture = geometryPtr;
            ffxivVrNative.SetEyeSceneTargets(
                resources.SceneRenderTargets[Eye.Left.ToIndex()].RenderTargetView,
                resources.SceneRenderTargets[Eye.Right.ToIndex()].RenderTargetView,
                resources.SceneDepthTargets[Eye.Left.ToIndex()].DepthStencilView,
                resources.SceneDepthTargets[Eye.Right.ToIndex()].DepthStencilView);
        }
    }

    public void PrepareCameraPhase(View[] views, VRInputData inputData, VRCameraMode cameraType)
    {
        VRSessionData = new VRSessionData(views, inputData, cameraType);
    }

    private void SetEyeProjections(View[] views)
    {
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
            if (!phase.CameraMode.UseHeadMovement)
            {
                var headCenter = (phase.Views[Eye.Left.ToIndex()].Pose.Position.ToVector3D() + phase.Views[Eye.Right.ToIndex()].Pose.Position.ToVector3D()) / 2;
                centerView.Pose.Position = (centerView.Pose.Position.ToVector3D() - headCenter).ToVector3f();
            }
            vrCamera.UpdateCamera(camera, phase.GetGameCamera(vrCamera.CreateGameCamera), phase.CameraMode, centerView);
            centerViewProjection = Matrix4x4.Multiply(camera->RenderCamera->ViewMatrix, camera->RenderCamera->ProjectionMatrix);
        }
    }


    public void OnStartUI()
    {
        ffxivVrNative.SetTargetCursorTexture(FindTargetCursorTexture());
        pendingTargetCursorDepths.Enqueue(ComputeTargetCursorDepth());
        while (pendingTargetCursorDepths.Count > MaxPendingTargetCursorDepths)
        {
            pendingTargetCursorDepths.TryDequeue(out _);
        }
        renderPipelineInjector.QueueRenderTargetCommand(Eye.Left);
        renderPipelineInjector.QueueClearCommand();
    }

    private ID3D11ShaderResourceView* FindTargetCursorTexture()
    {
        var cursor = (AtkUnitBase*)gameGui.GetAddonByName("_TargetCursor", 1).Address;
        if (cursor == null)
        {
            return null;
        }
        foreach (var node in cursor->UldManager.Nodes)
        {
            if (node.Value == null || node.Value->Type != NodeType.Image)
            {
                continue;
            }
            var image = (AtkImageNode*)node.Value;
            if (image->PartsList == null || image->PartId >= image->PartsList->PartCount)
            {
                continue;
            }
            var asset = image->PartsList->Parts[image->PartId].UldAsset;
            if (asset == null || asset->AtkTexture.TextureType != TextureType.Resource)
            {
                continue;
            }
            var resource = asset->AtkTexture.Resource;
            if (resource == null || resource->KernelTextureObject == null)
            {
                continue;
            }
            return (ID3D11ShaderResourceView*)resource->KernelTextureObject->D3D11ShaderResourceView;
        }
        return null;
    }

    private float? ComputeTargetCursorDepth()
    {
        var target = targetManager.Target ?? targetManager.SoftTarget;
        if (target == null)
        {
            return null;
        }
        if (nameplateModifier.TargetNameplateDepth is float nameplateDepth)
        {
            return nameplateDepth;
        }
        if (centerViewProjection is not Matrix4x4 viewProjection)
        {
            return null;
        }
        var gameObject = (GameObject*)target.Address;
        FFXIVClientStructs.FFXIV.Common.Math.Vector3 position;
        gameObject->GetNamePlateWorldPosition(&position);
        var clip = Vector4.Transform(new Vector4(position, 1), viewProjection);
        return clip.W > 0 ? MathF.Min(clip.Z / clip.W, nameplateModifier.MaxNameplateDepth) : null;
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
                if (pendingTargetCursorDepths.TryDequeue(out var targetCursorDepth) && targetCursorDepth is float cursorDepth)
                {
                    ffxivVrNative.SetTargetCursorDepth(cursorDepth, renderer.ShouldUseDepthTexture());
                }
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

    private const int RootCommandListId = 30;

    public void ExecuteCommands(ImmediateContext* context, int commandListId, System.Action executeOriginal)
    {
        if (commandListId != RootCommandListId)
        {
            executeOriginal();
            return;
        }
        if (RenderSessionData is VRSessionData render)
        {
            SetEyeProjections(render.Views);
        }
        ExecuteCommandsForEye(context, Eye.Left, executeOriginal);
        logger.Trace("ExecuteCommands replaying top-level call a second time");
        ExecuteCommandsForEye(context, Eye.Right, executeOriginal);
    }

    private void ExecuteCommandsForEye(ImmediateContext* context, Eye eye, System.Action executeOriginal)
    {
        ffxivVrNative.SetActiveEye(eye);
        ImmediateContextExtended.FromImmediateContext(context)->InvalidateConstantBuffers();
        executeOriginal();
        ffxivVrNative.SetActiveEye(null);
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
        pendingTargetCursorDepths.Clear();
        centerViewProjection = null;
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