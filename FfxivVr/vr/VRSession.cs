using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
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
    FirstPersonManager firstPersonManager,
    Debugging debugging,
    ITargetManager targetManager
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
            var view = phase.CurrentView(phase.CameraMode.UseHeadMovement);
            firstPersonManager.UpdateRotation(MathFactory.GetYaw(view.Pose.Orientation.ToQuaternion()));
            vrCamera.UpdateCamera(camera, phase.GetGameCamera(vrCamera.CreateGameCamera), phase.CameraMode, view);
        }
    }

    internal void RecenterCamera()
    {
        vrSpace.RecenterCamera();
    }

    internal void UpdateVisibility()
    {
        if (Conditions.Instance()->OccupiedInCutSceneEvent)
        {
            return;
        }
        if (State.SessionRunning)
        {
            if (firstPersonManager.IsFirstPerson)
            {
                gameModifier.HideHeadMesh();
            }

            if (cameraPhase is CameraPhase phase && EnableMotionTracking())
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

    private bool EnableMotionTracking()
    {
        if (debugging.AlwaysMotionControls)
        {
            return true;
        }
        return firstPersonManager.IsFirstPerson && (!configuration.DisableMotionTrackingInCombat || !Conditions.Instance()->InCombat);
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
        var ticks = gameClock.MarkFrame();
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

            if (cameraType.ShouldLockCameraVerticalRotation)
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

    internal void UpdateGamepad(PadDevice* padDevice)
    {
        if (cameraPhase is CameraPhase phase)
        {
            var padDeviceExtended = PadDeviceExtended.FromPadDevice(padDevice);
            inputManager.UpdateGamepad(&padDevice->GamepadInputData, phase.VRInputData, padDeviceExtended->IsActive);
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

    internal bool ShouldDrawGameObject(bool shouldDraw, GameObject* gameObject, Vector3D<float> cameraPosition, Vector3D<float> lookAtPosition)
    {
        if (gameState.IsInCutscene() || gameState.IsBetweenAreas())
        {
            return shouldDraw;
        }
        if (firstPersonManager.IsFirstPerson && configuration.HideBodyInFirstPerson)
        {
            if (gameState.IsPlayer(gameObject->EntityId))
            {
                return false;
            }
        }
        if (shouldDraw)
        {
            return true;
        }
        if ((IntPtr)gameObject == targetManager.Target?.Address || gameState.IsPlayer(gameObject->EntityId))
        {
            return true;
        }
        var radius = Math.Max(gameObject->GetRadius() + 1, 2);
        var cameraDistance = (gameObject->Position.ToVector3D() - cameraPosition).Length;
        var targetDistance = (gameObject->Position.ToVector3D() - lookAtPosition).Length;
        return cameraDistance < radius || cameraDistance < radius;
    }

    internal bool ShouldDisableCameraVerticalFly()
    {
        if (firstPersonManager.IsFirstPerson)
        {
            return configuration.DisableCameraDirectionFlying;
        }
        else
        {
            return configuration.DisableCameraDirectionFlyingThirdPerson;
        }
    }
}