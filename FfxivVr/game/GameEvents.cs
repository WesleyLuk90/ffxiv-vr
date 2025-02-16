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
    IGamepadState gamepadState
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


    private unsafe void FrameworkUpdate(IFramework framework)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            UpdateFreeCam(framework);

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