using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.Game;
using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace FfxivVR;

public unsafe class VRSession(
    Logger logger,
    Configuration configuration,
    GameState gameState,
    RenderPipelineInjector renderPipelineInjector,
    GameModifier gameModifier,
    FreeCamera freeCamera,
    VRSystem vrSystem,
    VRState State,
    VRSwapchains swapchains,
    Resources resources,
    VRShaders vrShaders,
    VRSpace vrSpace,
    VRCamera vrCamera,
    ResolutionManager resolutionManager,
    Renderer renderer,
    WaitFrameService waitFrameService,
    VRInput vrInput,
    EventHandler eventHandler,
    FramePrediction framePrediction
)
{
    private readonly VRSystem vrSystem = vrSystem;
    private readonly Logger logger = logger;
    public readonly VRState State = State;
    private readonly Renderer renderer = renderer;
    private readonly GameModifier gameModifier = gameModifier;
    private readonly VRSpace vrSpace = vrSpace;
    private readonly EventHandler eventHandler = eventHandler;
    private readonly VRShaders vrShaders = vrShaders;
    private readonly Resources resources = resources;
    private readonly VRSwapchains swapchains = swapchains;
    private readonly GameState gameState = gameState;
    private readonly FreeCamera freeCamera = freeCamera;
    private readonly VRCamera vrCamera = vrCamera;
    private readonly RenderPipelineInjector renderPipelineInjector = renderPipelineInjector;
    private readonly Configuration configuration = configuration;
    private readonly ResolutionManager resolutionManager = resolutionManager;
    private readonly WaitFrameService waitFrameService = waitFrameService;
    private readonly FramePrediction framePrediction = framePrediction;

    private readonly VRInput vrInput = vrInput;

    public void Initialize()
    {
        vrSystem.Initialize();
        vrShaders.Initialize();
        var size = swapchains.Initialize();
        resolutionManager.ChangeResolution(size);
        resources.Initialize(size);
        vrSpace.Initialize();
        vrInput.Initialize();
    }

    public class CameraPhase
    {
        public Eye Eye;
        public View[] Views;
        public VRCameraMode CameraType;

        public Task<FrameState> WaitFrameTask { get; }
        public HandTrackerExtension.HandData? Hands { get; }

        public CameraPhase(Eye eye, View[] views, Task<FrameState> waitFrameTask, HandTrackerExtension.HandData? hands, VRCameraMode cameraType)
        {
            Eye = eye;
            Views = views;
            WaitFrameTask = waitFrameTask;
            Hands = hands;
            CameraType = cameraType;
        }

        public View CurrentView()
        {
            return Views[Eye.ToIndex()];
        }
    }

    // Stores the hands data from the last frame so we keep the hands in the same spot of we lose tracking
    private HandTrackerExtension.HandData? lastHands;
    private CameraPhase? cameraPhase;

    abstract class RenderPhase;
    class LeftRenderPhase : RenderPhase
    {
        public View[] Views;
        public HandTrackerExtension.HandData? Hands;

        public Task<FrameState> WaitFrameTask { get; }

        public LeftRenderPhase(View[] views, Task<FrameState> waitFrameTask, HandTrackerExtension.HandData? hands)
        {
            Views = views;
            WaitFrameTask = waitFrameTask;
            this.Hands = hands;
        }
    }
    class RightRenderPhase : RenderPhase
    {
        public FrameState FrameState;
        public CompositionLayerProjectionView LeftLayer;

        public HandTrackerExtension.HandData? Hands { get; }

        public View[] Views;

        public RightRenderPhase(FrameState frameState, CompositionLayerProjectionView leftLayer, View[] views, HandTrackerExtension.HandData? hands)
        {
            FrameState = frameState;
            Views = views;
            LeftLayer = leftLayer;
            Hands = hands;
        }

    }

    private RenderPhase? renderPhase;

    public bool PrePresent(ID3D11DeviceContext* context)
    {
        var shouldPresent = true;
        eventHandler.PollEvents(() =>
        {
            OnSessionEnd();
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
            logger.Trace("Wait for VR frame sync");
            task.Wait();
            var frameState = task.Result;
            framePrediction.MarkPredictedFrameTime(frameState.PredictedDisplayTime);
            renderer.StartFrame();
            // Skip presenting the left view to avoid flicker when displaying the right view
            shouldPresent = false;
            if (frameState.ShouldRender == 1)
            {
                logger.Trace("Render left eye");
                var leftLayer = renderer.RenderEye(context, frameState, leftRenderPhase.Views, Eye.Left, leftRenderPhase.Hands);
                renderPhase = new RightRenderPhase(frameState, leftLayer, leftRenderPhase.Views, leftRenderPhase.Hands);
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
            var rightLayer = renderer.RenderEye(context, rightRenderPhase.FrameState, rightRenderPhase.Views, Eye.Right, rightRenderPhase.Hands);
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
                        logger.Trace("Switching camera phase to right eye");
                        phase.Eye = Eye.Right;
                        renderPhase = new LeftRenderPhase(phase.Views, cameraPhase.WaitFrameTask, phase.Hands);
                        break;
                    }
                case Eye.Right:
                    {
                        lastHands = cameraPhase?.Hands;
                        cameraPhase = null;
                        break;
                    }
                default: break;
            }
        }
        else
        {
            lastHands = null;
        }
        return shouldPresent;
    }

    private void OnSessionEnd()
    {
        // Ensure we end the frame if we need to end the session
        if (renderPhase is LeftRenderPhase left)
        {
            renderer.StartFrame();
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

    internal void UpdateCamera(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* camera)
    {
        if (State.SessionRunning && cameraPhase is CameraPhase phase)
        {
            logger.Trace($"Set {phase.Eye} camera matrix");
            View view = view = phase.CurrentView();

            camera->RenderCamera->ProjectionMatrix = vrCamera.ComputeGameProjectionMatrix(view);
            camera->RenderCamera->ProjectionMatrix2 = camera->RenderCamera->ProjectionMatrix;

            var position = camera->Position.ToVector3D();
            var lookAt = camera->LookAtVector.ToVector3D();

            var gameCamera = new GameCamera(position, lookAt, gameModifier.GetHeadPosition());
            camera->RenderCamera->ViewMatrix = vrCamera.ComputeGameViewMatrix(view, phase.CameraType, gameCamera).ToMatrix4x4();
            camera->ViewMatrix = camera->RenderCamera->ViewMatrix;

            camera->RenderCamera->FoV = view.Fov.AngleRight - view.Fov.AngleLeft;
        }
    }

    private VRCameraMode GetVRCameraType(float? localSpaceHeight)
    {
        var characterBase = gameModifier.GetCharacterBase();
        var distance = gameState.GetGameCameraDistance();
        if (freeCamera.Enabled)
        {
            return freeCamera;
        }
        else if (gameState.IsFirstPerson() && configuration.FollowCharacter)
        {
            return new FollowingFirstPersonCamera();
        }
        else if (gameState.IsFirstPerson())
        {
            return new FirstPersonCamera();
        }
        else if (localSpaceHeight is float height && characterBase != null && distance is float d)
        {
            return new LockedFloorCamera(
                groundPosition: characterBase->Position.Y,
                height: height,
                distance: d,
                worldScale: configuration.WorldScale);
        }
        else if (!configuration.KeepCameraHorizontal)
        {
            return new OrbitCamera();
        }
        else
        {
            return new LevelOrbitCamera();
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
            gameModifier.UpdateCharacterVisibility(configuration.ShowBodyInFirstPerson);

            if (configuration.ShowBodyInFirstPerson)
            {
                gameModifier.HideHeadMesh();
            }

            if (cameraPhase is CameraPhase phase)
            {
                var camera = gameState.GetCurrentCamera();
                var position = camera->Position.ToVector3D();
                var lookAt = camera->LookAtVector.ToVector3D();

                var gameCamera = new GameCamera(position, lookAt, null);

                gameModifier.UpdateMotionControls(
                    phase.Hands,
                    configuration.ControllerTracking ? vrInput.GetControllerPose() : null,
                    vrSystem.RuntimeAdjustments,
                    phase.CameraType.GetYRotation(gameCamera));
            }
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
            var predictedTime = framePrediction.GetPredictedFrameTime();

            var views = renderer.LocateView(predictedTime);
            var localSpaceHeight = configuration.MatchFloorPosition ? vrSpace.GetLocalSpaceHeight(predictedTime) : null;
            var hands = MaybeGetHandTrackingData(predictedTime, lastHands);
            Task<FrameState> waitFrameTask = Task.Run(() =>
            {
                var frameState = waitFrameService.WaitFrame();
                return frameState;
            });

            VRCameraMode cameraType = GetVRCameraType(localSpaceHeight);
            cameraPhase = new CameraPhase(Eye.Left, views, waitFrameTask, hands, cameraType);

            if (Conditions.IsInFlight && (configuration.DisableCameraDirectionFlying || cameraType.ShouldLockCameraVerticalRotation()))
            {
                if (gameState.IsFirstPerson())
                {
                    gameModifier.ResetVerticalCameraRotation(0);
                }
                else
                {
                    gameModifier.ResetVerticalCameraRotation(float.DegreesToRadians(15));
                }
            }
            else if (cameraType.ShouldLockCameraVerticalRotation())
            {
                gameModifier.ResetVerticalCameraRotation(0);
            }
        }
    }

    private HandTrackerExtension.HandData? MaybeGetHandTrackingData(long predictedTime, HandTrackerExtension.HandData? lastData)
    {
        if (!configuration.HandTracking)
        {
            return null;
        }
        if (!gameState.IsFirstPerson())
        {
            return null;
        }
        var data = vrSystem.HandTrackerExtension?.GetHandTrackingData(vrSpace.LocalSpace, predictedTime);
        if (data == null)
        {
            return null;
        }
        var left = data.LeftHandTracked() ? data.LeftHand : lastData?.LeftHand;
        var right = data.RightHandTracked() ? data.RightHand : lastData?.RightHand;
        if (left == null || right == null)
        {
            return null;
        }
        return new HandTrackerExtension.HandData(left, right);
    }

    internal Point? ComputeMousePosition(Point point)
    {
        return resolutionManager.ComputeMousePosition(point);
    }

    internal void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        gameModifier.OnNamePlateUpdate(context, handlers);
    }

    internal void UpdateGamepad(GamepadInput* gamepadInput)
    {
        vrInput.UpdateGamepad(gamepadInput);
    }
}