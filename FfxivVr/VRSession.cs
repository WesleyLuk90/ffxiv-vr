using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Threading.Tasks;

namespace FfxivVR;

public unsafe class VRSession : IDisposable
{
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
    private readonly DalamudRenderer dalamudRenderer;
    public readonly FreeCamera FreeCamera;
    private readonly VRCamera vrCamera;
    private readonly RenderPipelineInjector renderPipelineInjector;
    private readonly Configuration configuration;
    private readonly ResolutionManager resolutionManager;
    private readonly WaitFrameService waitFrameService;
    private readonly FramePrediction framePrediction;

    public VRSession(
        XR xr,
        Logger logger,
        ID3D11Device* device,
        Configuration configuration,
        GameState gameState,
        RenderPipelineInjector renderPipelineInjector,
        IGameGui gameGui,
        IClientState clientState,
        Dalamud.Game.ClientState.Objects.ITargetManager targetManager,
        HookStatus hookStatus,
        VRInstance vrInstance
    )
    {
        vrSystem = new VRSystem(xr, device, logger, hookStatus, vrInstance);
        this.logger = logger;
        State = new VRState();
        swapchains = new VRSwapchains(xr, vrSystem, logger, device, vrInstance);
        resources = new Resources(device, logger);
        vrShaders = new VRShaders(device, logger);
        vrSpace = new VRSpace(xr, logger, vrSystem);
        this.gameState = gameState;
        this.dalamudRenderer = new DalamudRenderer(logger);
        FreeCamera = new FreeCamera();
        vrCamera = new VRCamera(configuration, FreeCamera);
        renderer = new Renderer(xr, vrSystem, State, logger, swapchains, resources, vrShaders, vrSpace, configuration, dalamudRenderer, vrCamera);
        gameVisibility = new GameVisibility(logger, gameState, gameGui, targetManager, clientState);
        waitFrameService = new WaitFrameService(vrSystem, xr);
        eventHandler = new EventHandler(xr, vrSystem, logger, State, vrSpace, waitFrameService, vrInstance);
        this.renderPipelineInjector = renderPipelineInjector;
        this.configuration = configuration;
        resolutionManager = new ResolutionManager(logger);
        framePrediction = new FramePrediction(vrSystem);
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
        cameraPhase = null;
        renderPhase = null;
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
        public Task<FrameState> WaitFrameTask { get; }

        public CameraPhase(Eye eye, View[] views, Task<FrameState> waitFrameTask)
        {
            Eye = eye;
            Views = views;
            WaitFrameTask = waitFrameTask;
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
        public Task<FrameState> WaitFrameTask { get; }

        public LeftRenderPhase(View[] views, Task<FrameState> waitFrameTask)
        {
            Views = views;
            WaitFrameTask = waitFrameTask;
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

    public bool PrePresent(ID3D11DeviceContext* context)
    {
        var shouldPresent = true;
        eventHandler.PollEvents(() =>
        {
            OnSessionEnd(context);
        });
        if (!State.SessionRunning)
        {
            if (renderPhase != null || cameraPhase != null)
            {
                logger.Debug("Session not running, discarding phases");
                renderPhase = null;
                cameraPhase = null;
            }
        }
        if (renderPhase is LeftRenderPhase leftRenderPhase)
        {
            var task = leftRenderPhase.WaitFrameTask;
            logger.Trace("Starting to wait for frame");
            task.Wait();
            var frameState = task.Result;
            framePrediction.MarkPredictedFrameTime(frameState.PredictedDisplayTime);
            logger.Trace("Start frame");
            renderer.StartFrame(context);
            if (frameState.ShouldRender == 1)
            {
                var leftLayer = renderer.RenderEye(context, frameState, leftRenderPhase.Views, Eye.Left);
                renderPhase = new RightRenderPhase(frameState, leftLayer, leftRenderPhase.Views);
                // Skip presenting the left view to avoid flicker when displaying the right view
                shouldPresent = false;
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
                        renderPhase = new LeftRenderPhase(phase.Views, cameraPhase.WaitFrameTask);
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
        return shouldPresent;
    }

    private void OnSessionEnd(ID3D11DeviceContext* context)
    {
        // Ensure we end the frame if we need to end the session
        if (renderPhase is LeftRenderPhase left)
        {
            renderer.StartFrame(context);
            renderer.SkipFrame(left.WaitFrameTask.Result);
        }
        if (renderPhase is RightRenderPhase right)
        {
            renderer.SkipFrame(right.FrameState);
        }
        renderPhase = null;
        framePrediction.Reset();
    }

    internal bool ShouldSecondRender()
    {
        return cameraPhase?.Eye == Eye.Right;
    }

    // Test Cases
    // * Dungeon start cutscene
    // * Inn login/logout
    internal void UpdateCamera(Camera* camera)
    {
        if (State.SessionRunning && cameraPhase is CameraPhase phase)
        {
            logger.Trace($"Set {phase.Eye} camera matrix");
            View view = view = phase.CurrentView();

            camera->RenderCamera->ProjectionMatrix = vrCamera.ComputeGameProjectionMatrix(view);
            camera->RenderCamera->ProjectionMatrix2 = camera->RenderCamera->ProjectionMatrix;

            Vector3D<float>? headPos = null;
            if (gameState.IsFirstPerson() && configuration.FollowCharacter)
            {
                headPos = gameVisibility.GetHeadPosition();
            }

            camera->RenderCamera->ViewMatrix = vrCamera.ComputeGameViewMatrix(view, camera->Position.ToVector3D(), camera->LookAtVector.ToVector3D(), headPos).ToMatrix4x4();
            camera->ViewMatrix = camera->RenderCamera->ViewMatrix;

            camera->RenderCamera->FoV = view.Fov.AngleRight - view.Fov.AngleLeft;
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
            // Only clear the left view to get a clean render for copying
            // The right view we skip clearing which lets it display the VR view
            if (phase.Eye == Eye.Left)
            {
                renderPipelineInjector.QueueClearCommand();
            }
        }
    }

    internal void DoCopyRenderTexture(ID3D11DeviceContext* context, Eye eye)
    {
        if (State.SessionRunning)
        {
            renderer.CopyTexture(context, eye);
        }
    }

    internal void PrepareVRRender()
    {
        if (State.SessionRunning)
        {
            logger.Trace("Starting cycle");
            var views = renderer.LocateView(framePrediction.GetPredictedFrameTime());
            Task<FrameState> waitFrameTask = Task.Run(() =>
            {
                var frameState = waitFrameService.WaitFrame();
                return frameState;
            });
            cameraPhase = new CameraPhase(Eye.Left, views, waitFrameTask);
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
