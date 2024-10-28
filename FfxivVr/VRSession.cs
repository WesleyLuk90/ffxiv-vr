using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI;
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
    private readonly GameState gameState;
    private readonly RenderPipelineInjector renderPipelineInjector;
    private readonly IGameGui gameGui;
    private readonly ResolutionManager resolutionManager = new ResolutionManager();
    public VRSession(string openXRLoaderDllPath, Logger logger, ID3D11Device* device, Configuration configuration, GameState gameState, RenderPipelineInjector renderPipelineInjector, IGameGui gameGui, IClientState clientState, Dalamud.Game.ClientState.Objects.ITargetManager targetManager)
    {
        this.xr = new XR(XR.CreateDefaultContext(new string[] { openXRLoaderDllPath }));
        vrSystem = new VRSystem(xr, device, logger);
        this.logger = logger;
        State = new VRState();
        swapchains = new VRSwapchains(xr, vrSystem, logger, device);
        resources = new Resources(device, logger);
        vrShaders = new VRShaders(device, logger);
        vrSpace = new VRSpace(xr, logger, vrSystem);
        this.gameState = gameState;
        renderer = new Renderer(xr, vrSystem, State, logger, swapchains, resources, vrShaders, vrSpace, configuration);
        gameVisibility = new GameVisibility(logger, gameState, gameGui, targetManager, clientState);
        eventHandler = new EventHandler(xr, vrSystem, logger, State, vrSpace);
        this.renderPipelineInjector = renderPipelineInjector;
        this.gameGui = gameGui;
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

    public class CameraPhase
    {
        public Eye Eye;
        public View[] Views;

        public CameraPhase(Eye eye, View[] views)
        {
            Eye = eye;
            Views = views;
        }

        public View CurrentView()
        {
            return Views[Eye.ToIndex()];
        }
    }

    private CameraPhase? cameraPhase;

    abstract class RenderPhase;
    class LeftRenderPhase : RenderPhase
    {
        public View[] Views;

        public LeftRenderPhase(View[] views)
        {
            Views = views;
        }
    }
    class RightRenderPhase : RenderPhase
    {
        public FrameState FrameState;
        public CompositionLayerProjectionView LeftLayer;
        public View[] Views;

        public RightRenderPhase(FrameState frameState, CompositionLayerProjectionView leftLayer, View[] views)
        {
            FrameState = frameState;
            Views = views;
            LeftLayer = leftLayer;
        }

    }

    private RenderPhase? renderPhase;

    public void PrePresent(ID3D11DeviceContext* context)
    {
        eventHandler.PollEvents();
        if (!State.SessionRunning)
        {
            renderPhase = null;
        }
        if (renderPhase is LeftRenderPhase leftRenderPhase)
        {
            var maybeFrameState = renderer.StartFrame(context);
            if (maybeFrameState is FrameState frameState && frameState.ShouldRender == 1)
            {
                var leftLayer = renderer.RenderEye(context, frameState, leftRenderPhase.Views, Eye.Left);
                renderPhase = new RightRenderPhase(frameState, leftLayer, leftRenderPhase.Views);
            }
            else
            {
                if (maybeFrameState is FrameState skipFrameState)
                {
                    renderer.SkipFrame(skipFrameState);
                }
                renderPhase = null;
            }
        }
        else if (renderPhase is RightRenderPhase rightRenderPhase)
        {
            var rightLayer = renderer.RenderEye(context, rightRenderPhase.FrameState, rightRenderPhase.Views, Eye.Right);
            renderer.EndFrame(context, rightRenderPhase.FrameState, rightRenderPhase.Views, [rightRenderPhase.LeftLayer, rightLayer]);
            logger.Trace("End frame");
            renderPhase = null;
        }
        if (cameraPhase is CameraPhase phase)
        {
            switch (phase.Eye)
            {
                case Eye.Left:
                    {
                        phase.Eye = Eye.Right;
                        renderPhase = new LeftRenderPhase(phase.Views);
                        break;
                    }
                case Eye.Right:
                    {
                        cameraPhase = null;
                        break;
                    }
                default: break;
            }
        }
    }

    internal bool SecondRender(ID3D11DeviceContext* context)
    {
        return cameraPhase?.Eye == Eye.Right;
    }

    internal void UpdateCamera(Camera* camera)
    {
        if (State.SessionRunning && cameraPhase is CameraPhase phase)
        {
            logger.Trace($"Set {phase.Eye} camera matrix");
            View view = view = phase.CurrentView();

            camera->RenderCamera->ProjectionMatrix = renderer.ComputeProjectionMatrix(view);
            camera->RenderCamera->ProjectionMatrix2 = camera->RenderCamera->ProjectionMatrix;

            camera->RenderCamera->ViewMatrix = renderer.ComputeViewMatrix(view, camera->RenderCamera->Origin.ToVector3D(), camera->LookAtVector.ToVector3D()).ToMatrix4x4();
            camera->ViewMatrix = camera->RenderCamera->ViewMatrix;
        }
    }

    internal void RecenterCamera()
    {
        vrSpace.RecenterCamera(vrSystem.Now());
    }

    internal void UpdateVisibility()
    {
        if (State.SessionRunning)
        {
            gameVisibility.ForceFirstPersonBodyVisible();

            gameVisibility.HideHeadMesh();
        }
    }

    internal void PreUIRender()
    {
        if (State.SessionRunning && cameraPhase is CameraPhase phase)
        {
            logger.Trace($"Queue {phase.Eye} render");

            renderPipelineInjector.QueueRenderTargetCommand(phase.Eye);
            renderPipelineInjector.QueueClearCommand();
        }
    }

    internal void DoCopyRenderTexture(ID3D11DeviceContext* context, Eye eye)
    {
        if (State.SessionRunning)
        {
            renderer.CopyTexture(context, eye);
        }
    }

    internal void StartCycle()
    {
        if (State.SessionRunning)
        {
            var views = renderer.LocateView(vrSystem.Now());
            cameraPhase = new CameraPhase(Eye.Left, views);
        }
    }

    internal void UpdateNamePlates(AddonNamePlate* namePlate)
    {
        if (!State.SessionRunning)
        {
            return;
        }
        gameVisibility.UpdateNamePlates(namePlate);
    }
}
