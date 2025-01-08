using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.Game;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using CSRay = FFXIVClientStructs.FFXIV.Client.Graphics.Ray;

namespace FfxivVR;

public unsafe class VRSession(
    Logger logger,
    Configuration configuration,
    GameState gameState,
    RenderPipelineInjector renderPipelineInjector,
    GameModifier gameModifier,
    VRSystem vrSystem,
    VRState State,
    VRSwapchains swapchains,
    Resources resources,
    VRShaders vrShaders,
    VRSpace vrSpace,
    VRCamera vrCamera,
    ResolutionManager resolutionManager,
    RenderManager renderManager,
    WaitFrameService waitFrameService,
    VRActionService vrInput,
    EventHandler eventHandler,
    FramePrediction framePrediction,
    InputManager inputManager,
    VRUI vrUI,
    GameClock gameClock,
    VRInputService vrInputService,
    DalamudRenderer dalamudRenderer,
    FirstPersonManager firstPersonManager
)
{
    public VRState State = State;
    public void Initialize()
    {
        dalamudRenderer.Initialize();
        vrSystem.Initialize();
        vrShaders.Initialize();
        var size = swapchains.Initialize();
        resolutionManager.ChangeResolution(size);
        resources.Initialize(size);
        vrSpace.Initialize();
        vrInput.Initialize();
    }


    private CameraPhase? cameraPhase;
    public bool PrePresent()
    {
        eventHandler.PollEvents(() =>
        {
            renderManager.OnSessionEnd();
        });
        if (!State.SessionRunning)
        {
            if (cameraPhase != null)
            {
                logger.Debug("Session not running, discarding phases");
                cameraPhase = null;
            }
            renderManager.OnSessionEnd();
        }
        var shouldPresent = renderManager.RunRenderPhase();
        if (cameraPhase is CameraPhase phase)
        {
            switch (phase.Eye)
            {
                case Eye.Left:
                    {
                        logger.Trace("Switching camera phase to right eye");
                        phase.SwitchToRightEye();
                        renderManager.StartRender(phase);
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
            View view = phase.CurrentView(phase.CameraMode.UseHeadMovement);
            vrCamera.UpdateCamera(camera, phase.GetGameCamera(vrCamera.CreateGameCamera), phase.CameraMode, view);
        }
    }

    internal void RecenterCamera()
    {
        vrSpace.RecenterCamera();
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

            if (cameraPhase is CameraPhase phase && firstPersonManager.IsFirstPerson)
            {
                var camera = gameState.GetCurrentCamera();
                var position = camera->Position.ToVector3D();
                var lookAt = camera->LookAtVector.ToVector3D();

                var gameCamera = phase.GetGameCamera(vrCamera.CreateGameCamera);
                if (gameCamera == null)
                {
                    return;
                }
                gameModifier.UpdateMotionControls(
                    phase.VRInputData,
                    vrSystem.RuntimeAdjustments,
                    gameCamera.GetYRotation());
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

    internal void DoCopyRenderTexture(Eye eye)
    {
        if (State.SessionRunning)
        {
            renderManager.CopyGameRenderTexture(eye);
        }
    }


    internal void PrepareVRRender()
    {
        var ticks = gameClock.GetTicks();
        firstPersonManager.Update();
        if (State.SessionRunning)
        {
            logger.Trace("Starting cycle");
            var predictedTime = framePrediction.GetPredictedFrameTime();

            var views = vrSpace.LocateView(predictedTime);
            var localSpaceHeight = configuration.MatchFloorPosition ? vrSpace.GetLocalSpaceHeight(predictedTime) : null;
            Task<FrameState> waitFrameTask = Task.Run(() =>
            {
                var frameState = waitFrameService.WaitFrame();
                return frameState;
            });
            var inputData = vrInputService.PollInput(predictedTime);
            VRCameraMode cameraType = vrCamera.GetVRCameraType(localSpaceHeight, configuration.BodyTracking && inputData.HasBodyData());
            vrUI.Update(views[0], ticks);
            cameraPhase = new CameraPhase(Eye.Left, views, waitFrameTask, inputData, cameraType);

            if (Conditions.IsInFlight || Conditions.IsDiving)
            {
                if (firstPersonManager.IsFirstPerson && (configuration.DisableCameraDirectionFlying || cameraType.ShouldLockCameraVerticalRotation))
                {
                    gameModifier.ResetVerticalCameraRotation(float.DegreesToRadians(15));
                }
                else if (configuration.DisableCameraDirectionFlyingThirdPerson || cameraType.ShouldLockCameraVerticalRotation)
                {
                    // Vertical rotation is offset by about 15 degress for some reason
                    gameModifier.ResetVerticalCameraRotation(float.DegreesToRadians(15));
                }
            }
            else if (cameraType.ShouldLockCameraVerticalRotation)
            {
                gameModifier.ResetVerticalCameraRotation(0);
            }
        }
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
        if (cameraPhase is CameraPhase phase)
        {
            inputManager.UpdateGamepad(gamepadInput, phase.VRInputData);
        }
    }

    internal CSRay? GetTargetRay(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* sceneCamera)
    {
        logger.Trace("GetTargetRay");
        if (cameraPhase is CameraPhase phase)
        {
            var camera = gameState.GetCurrentCamera();
            if (camera == null || camera != sceneCamera)
            {
                return null;
            }

            if (phase.GetGameCamera(vrCamera.CreateGameCamera) is not GameCamera gameCamera)
            {
                return null;
            }

            var rotationMatrix = phase.CameraMode.GetRotationMatrix(gameCamera);
            var direction = Vector3D.Transform(new Vector3D<float>(0, 0, -1), Matrix4X4.CreateFromQuaternion(phase.Views[0].Pose.Orientation.ToQuaternion()) * rotationMatrix);
            return new CSRay(
                camera->Position,
                direction.ToVector3()
            );
        }
        else
        {
            return null;
        }
    }
}