using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System;

namespace FfxivVR;

public unsafe class VRSession : IDisposable
{

    private readonly XR xr;
    private readonly VRSystem vrSystem;
    private readonly Logger logger;
    public readonly VRState State;
    private readonly Renderer renderer;
    private readonly GameVisibility gameVisibility;
    private readonly VRSpace vrSpace;
    private readonly EventHandler eventHandler;
    private readonly VRShaders vrShaders;
    private readonly Resources resources;
    private readonly VRSwapchains swapchains;
    private readonly VRSettings settings;
    private readonly GameState gameState;
    private readonly RenderPipelineInjector renderPipelineInjector;
    private readonly ResolutionManager resolutionManager = new ResolutionManager();
    public VRSession(string openXRLoaderDllPath, Logger logger, ID3D11Device* device, VRSettings settings, GameState gameState, RenderPipelineInjector renderPipelineInjector)
        : this(new XR(XR.CreateDefaultContext(new string[] { openXRLoaderDllPath })), logger, device, settings, gameState, renderPipelineInjector)
    {
    }
    public VRSession(XR xr, Logger logger, ID3D11Device* device, VRSettings settings, GameState gameState, RenderPipelineInjector renderPipelineInjector)
    {
        this.xr = xr;
        vrSystem = new VRSystem(xr, device, logger);
        this.logger = logger;
        State = new VRState();
        swapchains = new VRSwapchains(xr, vrSystem, logger, device);
        resources = new Resources(device, logger);
        vrShaders = new VRShaders(device, logger);
        vrSpace = new VRSpace(xr, logger, vrSystem);
        this.settings = settings;
        this.gameState = gameState;
        renderer = new Renderer(xr, vrSystem, State, logger, swapchains, resources, vrShaders, vrSpace, settings);
        gameVisibility = new GameVisibility(logger);
        eventHandler = new EventHandler(xr, vrSystem, logger, State, vrSpace);
        this.renderPipelineInjector = renderPipelineInjector;
    }

    public void Initialize()
    {
        vrSystem.Initialize();
        vrShaders.Initialize();
        var size = swapchains.Initialize();
        resolutionManager.ChangeResolution(size);
        resources.Initialize(size);
        vrSpace.Initialize();
    }

    public void Dispose()
    {
        resolutionManager.RevertResolution();
        vrSpace.Dispose();
        vrShaders.Dispose();
        swapchains.Dispose();
        resources.Dispose();
        vrSystem.Dispose();
    }


    public abstract class RenderState
    {
        public class Ready() : RenderState;
        public class SkipRender(FrameState frameState) : RenderState
        {
            public FrameState FrameState { get; } = frameState;
        }

        public class RenderingLeft(FrameState frameState, View[] views) : RenderState
        {
            public FrameState FrameState { get; } = frameState;
            public View[] Views { get; } = views;
        }

        public class RenderingRight(FrameState frameState, View[] views, CompositionLayerProjectionView leftLayer) : RenderState
        {
            public FrameState FrameState { get; } = frameState;
            public View[] Views { get; } = views;
            public CompositionLayerProjectionView LeftLayer { get; } = leftLayer;
        }

        public class Skipped() : RenderState;
    }

    private RenderState renderState = new RenderState.Skipped();

    public void PostPresent(ID3D11DeviceContext* context)
    {
        eventHandler.PollEvents();

        if (renderState is RenderState.Ready)
        {
            if (State.SessionRunning)
            {
                var maybeFrameState = renderer.StartFrame(context);
                if (maybeFrameState is FrameState frameState)
                {
                    if (State.IsActive() && frameState.ShouldRender != 0)
                    {
                        var views = renderer.LocateView(frameState);
                        renderState = new RenderState.RenderingLeft(frameState, views);
                    }
                    else
                    {
                        renderState = new RenderState.SkipRender(frameState);
                    }
                }
                else
                {
                    renderState = new RenderState.Skipped();
                }
            }
            else
            {
                renderState = new RenderState.Skipped();
            }
        }
    }

    public void PrePresent(ID3D11DeviceContext* context, Texture* gameRenderTexture)
    {
        switch (renderState)
        {
            case RenderState.SkipRender skip:
                renderer.SkipFrame(skip.FrameState);
                renderState = new RenderState.Ready();
                break;
            case RenderState.RenderingLeft rendering:
                var leftLayer = renderer.RenderEye(context, rendering.FrameState, gameRenderTexture, rendering.Views, Eye.Left);
                renderState = new RenderState.RenderingRight(rendering.FrameState, rendering.Views, leftLayer);
                break;
            case RenderState.RenderingRight rendering:
                var rightLayer = renderer.RenderEye(context, rendering.FrameState, gameRenderTexture, rendering.Views, Eye.Right);
                renderer.EndFrame(context, rendering.FrameState, gameRenderTexture, rendering.Views, [rendering.LeftLayer, rightLayer]);
                renderState = new RenderState.Ready();
                break;
            case RenderState.Skipped:
                renderState = new RenderState.Ready();
                break;
            default:
                logger.Error($"Unexpected frame state at end {renderState}");
                break;
        }
    }

    internal bool SecondRender(ID3D11DeviceContext* context)
    {
        return renderState is RenderState.RenderingRight;
    }

    internal void UpdateCamera(Camera* camera)
    {
        View view;
        // These seem to be swapped because the render step is out of sync with the update camera call
        if (renderState is RenderState.RenderingLeft renderingLeft)
        {
            view = renderingLeft.Views[Eye.Right.ToIndex()];
        }
        else if (renderState is RenderState.RenderingRight renderingRight)
        {
            view = renderingRight.Views[Eye.Left.ToIndex()];
        }
        else
        {
            return;
        }

        camera->RenderCamera->ProjectionMatrix = renderer.ComputeProjectionMatrix(view);
        camera->RenderCamera->ProjectionMatrix2 = camera->RenderCamera->ProjectionMatrix;

        camera->RenderCamera->ViewMatrix = renderer.ComputeViewMatrix(view, camera->RenderCamera->Origin.ToVector3D(), camera->LookAtVector.ToVector3D()).ToMatrix4x4();
        camera->ViewMatrix = camera->RenderCamera->ViewMatrix;
    }

    internal void RecenterCamera()
    {
        vrSpace.RecenterCamera(renderer.LastTime);
    }

    internal void UpdateVisibility()
    {
        if (State.SessionRunning && gameState.IsFirstPerson())
        {
            gameVisibility.ForceFirstPersonBodyVisible();
            gameVisibility.HideHeadMesh();
        }
    }

    internal void PreUIRender()
    {
        if (State.SessionRunning)
        {
            if (renderState is RenderState.RenderingLeft)
            {
                renderPipelineInjector.RedirectUIRender(true);
            }
            else if (renderState is RenderState.RenderingRight)
            {
                renderPipelineInjector.RedirectUIRender(false);
            }
            else if (renderState is RenderState.Skipped || renderState is RenderState.SkipRender) { }
            else
            {
                logger.Debug($"Invalid render state ${renderState}");
            }
        }
    }

    internal void DoCopyRenderTexture(ID3D11DeviceContext* context, bool isLeft)
    {
        if (State.SessionRunning)
        {
            renderer.CopyTexture(context, isLeft);
        }
    }
}
