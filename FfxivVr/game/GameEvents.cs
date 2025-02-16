using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;

namespace FfxivVR;

public class GameEvents(
    INamePlateGui namePlateGui,
    IClientState clientState,
    Transitions transitions,
    IFramework framework,
    ExceptionHandler exceptionHandler,
    VRLifecycle vrLifecycle,
    HudLayoutManager hudLayoutManager,
    GamepadManager gamepadManager,
    FreeCamera freeCamera,
    IGamepadState gamepadState,
    GameState gameState,
    Debugging debugging
) : IDisposable
{

    public void Initialize()
    {
        namePlateGui.OnDataUpdate += OnNamePlateUpdate;
        clientState.Login += Login;
        clientState.Logout += Logout;
        framework.Update += FrameworkUpdate;
    }

    private void Logout(int type, int code)
    {
        transitions.OnLogout();
    }

    private void Login()
    {
        transitions.OnLogin();
    }

    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        vrLifecycle.OnNamePlateUpdate(context, handlers);
    }


    private Vector3D<float> fpPosition = new Vector3D<float>();
    private Vector3D<float> thirdPersonTarget = new Vector3D<float>();
    private unsafe void FrameworkUpdate(IFramework framework)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            UpdateFreeCam(framework);


            var internalCamera = gameState.GetInternalGameCamera();
            var currentCamera = gameState.GetCurrentCamera();
            if (internalCamera->CameraMode == CameraView.FirstPerson)
            {
                fpPosition = currentCamera->Position.ToVector3D();
            }
            else
            {
                thirdPersonTarget = currentCamera->LookAtVector.ToVector3D();
            }
            var gamePos = clientState.LocalPlayer?.Position ?? new System.Numerics.Vector3();
            var playerPos = new Vector3D<float>(gamePos.X, gamePos.Y, gamePos.Z);
            debugging.DebugShow("FP Position", fpPosition);
            debugging.DebugShow("Target", thirdPersonTarget);
            debugging.DebugShow("Delta", fpPosition - thirdPersonTarget);
            debugging.DebugShow("Player", playerPos);
            debugging.DebugShow("FP to Player", fpPosition - playerPos);
            var fixedHead = gameState.GetFixedHeadPosition();

            debugging.DebugShow("Head Pos", fixedHead);

            hudLayoutManager.Update();
        });
    }

    private void UpdateFreeCam(IFramework framework)
    {
        gamepadManager.Update();
        var speed = 0.05f;
        var rotationSpeed = 2 * MathF.PI / 200;
        var timeDelta = (float)framework.UpdateDelta.TotalSeconds;
        freeCamera.UpdatePosition(
            walkDelta: new Vector2D<float>(gamepadState.LeftStick.X, gamepadState.LeftStick.Y) * timeDelta * speed,
            heightDelta: gamepadState.RightStick.Y * timeDelta * speed,
            rotationDelta: -gamepadState.RightStick.X * timeDelta * rotationSpeed
        );
    }


    public void Dispose()
    {
        framework.Update -= FrameworkUpdate;
        clientState.Login -= Login;
        clientState.Logout -= Logout;
        namePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
    }
}