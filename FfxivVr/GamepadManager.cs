using Dalamud.Plugin.Services;
using System;
using System.Reflection;

namespace FfxivVR;
internal class GamepadManager
{
    private readonly IGamepadState gamepadState;
    private readonly VRLifecycle lifecycle;
    private readonly PropertyInfo NavEnableGamepadProperty;

    public GamepadManager(IGamepadState gamepadState, VRLifecycle lifecycle)
    {
        var assembly = Assembly.GetAssembly(typeof(IGamepadState)) ?? throw new Exception("Could not get assembly");
        var gamepad = assembly.GetType("Dalamud.Game.ClientState.GamePad.GamepadState") ?? throw new Exception("Could not get gamepad");
        this.NavEnableGamepadProperty = gamepad.GetProperty("NavEnableGamepad",
             BindingFlags.NonPublic |
             BindingFlags.Instance) ?? throw new Exception("Could not get NavEnableGamepad");
        this.gamepadState = gamepadState;
        this.lifecycle = lifecycle;
    }

    public void Update()
    {
        if (lifecycle.GetFreeCamera()?.Enabled == true)
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
