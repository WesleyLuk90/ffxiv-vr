using Dalamud.Plugin.Services;
using System;
using System.Reflection;

namespace FfxivVR;
internal class GamepadManager
{
    private readonly IGamepadState gamepadState;
    private readonly PropertyInfo NavEnableGamepadProperty;

    private readonly FreeCamera freeCamera;

    public GamepadManager(IGamepadState gamepadState, FreeCamera freeCamera)
    {
        var assembly = Assembly.GetAssembly(typeof(IGamepadState)) ?? throw new Exception("Could not get assembly");
        var gamepad = assembly.GetType("Dalamud.Game.ClientState.GamePad.GamepadState") ?? throw new Exception("Could not get gamepad");
        this.NavEnableGamepadProperty = gamepad.GetProperty("NavEnableGamepad",
             BindingFlags.NonPublic |
             BindingFlags.Instance) ?? throw new Exception("Could not get NavEnableGamepad");
        this.gamepadState = gamepadState;
        this.freeCamera = freeCamera;
    }

    public void Update()
    {
        if (freeCamera.Enabled)
        {
            SetEnableGamepad(false);
        }
        else
        {
            SetEnableGamepad(true);
        }
    }

    private void SetEnableGamepad(bool enable)
    {
        NavEnableGamepadProperty.SetValue(gamepadState, !enable);
    }
}
