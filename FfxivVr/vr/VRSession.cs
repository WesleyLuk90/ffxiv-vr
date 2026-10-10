using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Drawing;
using CSRay = FFXIVClientStructs.FFXIV.Client.Graphics.Ray;

namespace FfxivVR;

public unsafe class VRSession(
    Logger logger,
    Configuration configuration,
    GameState gameState,
    GameModifier gameModifier,
    VRSystem vrSystem,
    VRState State,
    VRSwapchains swapchains,
    Resources resources,
    VRShaders vrShaders,
    VRSpace vrSpace,
    VRCamera vrCamera,
    ResolutionManager resolutionManager,
    IRenderStrategy renderStrategy,
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
    public VRState State { get; } = State;
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
        renderStrategy.Initialize(size);
    }

    public bool OnPresentFrame()
    {
        eventHandler.PollEvents(() =>
        {
            renderStrategy.OnSessionEnd();
        });
        var shouldPresent = renderStrategy.OnPresentFrame();
        return shouldPresent;
    }

    public bool ShouldRerunGameLoop()
    {
        return renderStrategy.ShouldSecondRender();
    }

    public void ExecuteCommands(FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.ImmediateContext* context, int commandListId, Action executeOriginal)
    {
        if (!State.SessionRunning)
        {
            executeOriginal();
            return;
        }
        renderStrategy.ExecuteCommands(context, commandListId, executeOriginal);
    }

    // Test Cases
    // * Dungeon start cutscene
    // * Inn login/logout

    public void UpdateCamera(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* camera)
    {
        renderStrategy.UpdateCamera(camera);
    }

    public void RecenterCamera()
    {
        vrSpace.RecenterCamera();
    }

    public void UpdateVisibility()
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

            if (renderStrategy.VRSessionData is VRSessionData phase && EnableMotionTracking())
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

    public void OnStartUI()
    {
        if (State.SessionRunning)
        {
            renderStrategy.OnStartUI();
        }
    }

    public void OnStartUIRender(Eye? eye)
    {
        if (State.SessionRunning)
        {
            renderStrategy.OnStartUIRender(eye);
        }
    }


    public void OnStartGameLoop()
    {
        var ticks = gameClock.MarkFrame();
        firstPersonManager.Update();
        if (State.SessionRunning)
        {
            logger.Trace("Starting cycle");
            long predictedTime;
            if (!configuration.AltFramePrediction)
            {
                predictedTime = framePrediction.GetPredictedFrameTime();
            }
            else
            {
                predictedTime = framePrediction.GetAltPredictedFrameTime() ?? framePrediction.GetPredictedFrameTime();
            }

            var views = vrSpace.LocateView(predictedTime);
            var localSpaceHeight = configuration.MatchFloorPosition ? vrSpace.GetLocalSpaceHeight(predictedTime) : null;
            var inputData = vrInputService.PollInput(predictedTime);
            VRCameraMode cameraType = vrCamera.GetVRCameraType(localSpaceHeight, configuration.BodyTracking && inputData.HasBodyData());
            vrUI.Update(views[0], ticks);

            renderStrategy.PrepareCameraPhase(views, inputData, cameraType);

            if (cameraType.ShouldLockCameraVerticalRotation)
            {
                gameModifier.ResetVerticalCameraRotation(0);
            }
        }
    }


    public Point? ComputeMousePosition(Point point)
    {
        return resolutionManager.ComputeMousePosition(point);
    }

    public void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        gameModifier.OnNamePlateUpdate(context, handlers);
    }

    public void UpdateGamepad(PadDevice* padDevice)
    {
        if (renderStrategy.VRSessionData is VRSessionData phase)
        {
            var padDeviceExtended = PadDeviceExtended.FromPadDevice(padDevice);
            inputManager.UpdateGamepad(&padDevice->GamepadInputData, phase.VRInputData, padDeviceExtended->IsActive);
        }
    }

    public CSRay? GetTargetRay(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* sceneCamera)
    {
        logger.Trace("GetTargetRay");
        if (renderStrategy.VRSessionData is VRSessionData phase)
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

    public Vector3D<float>? GetPlayerHeadLookAtTarget(GameObject* gameObject, int attachBoneIndex)
    {
        if (!State.SessionRunning || !gameModifier.IsHiddenHeadAttachBone(gameObject, attachBoneIndex))
        {
            return null;
        }
        if (renderStrategy.VRSessionData is not VRSessionData phase)
        {
            return null;
        }
        if (phase.GetGameCamera(vrCamera.CreateGameCamera) is not GameCamera gameCamera)
        {
            return null;
        }
        return vrCamera.ComputeHeadWorldPosition(phase.Views[Eye.Left.ToIndex()], phase.Views[Eye.Right.ToIndex()], phase.CameraMode, gameCamera);
    }

    public bool ShouldDrawGameObject(bool shouldDraw, GameObject* gameObject, Vector3D<float> cameraPosition, Vector3D<float> lookAtPosition)
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
        var asChar = gameObject->GetAsCharacter();
        if (asChar != null)
        {
            var parent = asChar->GetParentCharacter();
            if (parent != null)
            {
                if (gameState.getCharacterOrGpose() == parent)
                {
                    return true;
                }
            }
        }
        var radius = Math.Max(gameObject->GetRadius() + 1, 2);
        var cameraDistance = (gameObject->Position.ToVector3D() - cameraPosition).Length;
        var targetDistance = (gameObject->Position.ToVector3D() - lookAtPosition).Length;
        return cameraDistance < radius || targetDistance < radius;
    }

    public bool ShouldDisableCameraVerticalFly()
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