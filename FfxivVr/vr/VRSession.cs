using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
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
    FramePrediction framePrediction,
    InputManager inputManager,
    VRUI vrUI,
    GameClock gameClock,
    Debugging debugging
)
{
    public readonly VRState State = State;
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


    // Stores the hands data from the last frame so we keep the hands in the same spot of we lose tracking
    private TrackingData? lastTrackingData;
    private CameraPhase? cameraPhase;

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
            logger.Trace("Wait for VR frame sync");
            var frameState = leftRenderPhase.WaitFrame();
            framePrediction.MarkPredictedFrameTime(frameState.PredictedDisplayTime);
            renderer.StartFrame();
            // Skip presenting the left view to avoid flicker when displaying the right view
            shouldPresent = false;
            if (frameState.ShouldRender == 1)
            {
                logger.Trace("Render left eye");
                var leftLayer = renderer.RenderEye(context, leftRenderPhase.CreateEyeRender());
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
            var rightLayer = renderer.RenderEye(context, rightRenderPhase.CreateEyeRender());
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
                        phase.SwitchToRightEye();
                        renderPhase = phase.StartRender();
                        break;
                    }
                case Eye.Right:
                    {
                        lastTrackingData = cameraPhase?.TrackingData;
                        cameraPhase = null;
                        break;
                    }
                default: break;
            }
        }
        else
        {
            lastTrackingData = null;
        }
        return shouldPresent;
    }

    private void OnSessionEnd()
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
        vrSpace.RecenterCamera(vrSystem.Now());
        vrCamera.ResetSavedHeadPosition();
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

                var gameCamera = phase.GetGameCamera(vrCamera.CreateGameCamera);
                if (gameCamera == null)
                {
                    return;
                }
                gameModifier.UpdateMotionControls(
                    phase.TrackingData,
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

    internal void DoCopyRenderTexture(ID3D11DeviceContext* context, Eye eye)
    {
        if (State.SessionRunning)
        {
            renderer.CopyTexture(context, eye);
        }
    }


    internal void PrepareVRRender()
    {
        var ticks = gameClock.GetTicks();
        if (State.SessionRunning)
        {
            logger.Trace("Starting cycle");
            var predictedTime = framePrediction.GetPredictedFrameTime();

            var views = renderer.LocateView(predictedTime);
            var localSpaceHeight = configuration.MatchFloorPosition ? vrSpace.GetLocalSpaceHeight(predictedTime) : null;
            Task<FrameState> waitFrameTask = Task.Run(() =>
            {
                var frameState = waitFrameService.WaitFrame();
                return frameState;
            });
            var trackingData = GetTrackingData(predictedTime);
            VRCameraMode cameraType = vrCamera.GetVRCameraType(localSpaceHeight, trackingData.HasBodyData());
            cameraPhase = new CameraPhase(Eye.Left, views, waitFrameTask, trackingData, cameraType, vrUI.GetRotation(views[0], ticks));

            if (Conditions.IsInFlight || Conditions.IsDiving)
            {
                if (gameState.IsFirstPerson() && (configuration.DisableCameraDirectionFlying || cameraType.ShouldLockCameraVerticalRotation))
                {
                    gameModifier.ResetVerticalCameraRotation(0);
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

    private TrackingData GetTrackingData(long predictedTime)
    {
        if (!gameState.IsFirstPerson() && !debugging.ForceTracking)
        {
            return TrackingData.Disabled();
        }
        var hands = configuration.HandTracking ? GetHandTrackingData(predictedTime) : null;
        var controllers = configuration.ControllerTracking ? vrInput.GetControllerPose() : null;

        var bodyData = configuration.BodyTracking ? vrSystem.BodyTracker?.GetData(vrSpace.LocalSpace, predictedTime) : null;

        if (lastTrackingData is TrackingData last)
        {
            return last.Update(configuration.HandTracking, hands, configuration.ControllerTracking, controllers, bodyData);
        }
        else
        {
            return TrackingData.CreateNew(hands, controllers, bodyData);
        }
    }

    private HandTracking.HandPose? GetHandTrackingData(long predictedTime)
    {
        if (!configuration.HandTracking)
        {
            return null;
        }
        if (!gameState.IsFirstPerson())
        {
            return null;
        }
        return vrSystem.HandTracker?.GetHandTrackingData(vrSpace.LocalSpace, predictedTime);
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
        inputManager.UpdateGamepad(gamepadInput);
    }

    internal Ray? GetTargetRay(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* sceneCamera)
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
            return new Ray(
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