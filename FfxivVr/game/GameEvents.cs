using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Common.Math;
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
    Logger logger,
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
        logger.Debug("Logout");
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

    private unsafe void FrameworkUpdate(IFramework framework)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            UpdateFreeCam(framework);

            hudLayoutManager.Update();

            var current = gameState.GetCurrentCamera();
            if (current != null)
            {
                debugging.DebugShow("Camera Address", ((ulong)current).ToString("X"));
                debugging.DebugShow("Position", current->Position.ToVector3D());
                debugging.DebugShow("Target", current->LookAtVector.ToVector3D());
            }
            var character = (Character*)(clientState.LocalPlayer?.Address ?? 0);
            if (character != null)
            {
                var pos = new Vector3();
                character->GetCenterPosition(&pos);
                debugging.DebugShow("Player Position", pos.ToVector3D());
            }
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