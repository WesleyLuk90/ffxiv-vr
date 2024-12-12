using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
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
    Configuration configuration,
    VRLifecycle vrLifecycle,
    GameState gameState,
    HudLayoutManager hudLayoutManager,
    IDalamudPluginInterface pluginInterface,
    GamepadManager gamepadManager,
    FreeCamera freeCamera,
    VRStartStop vrStartStop,
    Logger logger,
    IGamepadState gamepadState
) : IDisposable
{
    private readonly INamePlateGui namePlateGui = namePlateGui;
    private readonly IClientState clientState = clientState;
    private readonly IFramework framework = framework;
    private readonly ExceptionHandler exceptionHandler = exceptionHandler;
    private readonly Configuration configuration = configuration;
    private readonly VRLifecycle vrLifecycle = vrLifecycle;
    private readonly GameState gameState = gameState;
    private readonly HudLayoutManager hudLayoutManager = hudLayoutManager;
    private readonly IDalamudPluginInterface pluginInterface = pluginInterface;
    private readonly GamepadManager gamepadManager = gamepadManager;
    private readonly FreeCamera freeCamera = freeCamera;
    private readonly VRStartStop vrStartStop = vrStartStop;
    private readonly Logger logger = logger;
    private readonly IGamepadState gamepadState = gamepadState;

    public void Initialize()
    {
        namePlateGui.OnDataUpdate += OnNamePlateUpdate;
        clientState.Login += Login;
        clientState.Logout += Logout;
        framework.Update += FrameworkUpdate;
    }

    private bool? isFirstPerson = null;
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

    private void FrameworkUpdate(IFramework framework)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            if (!Conditions.IsOccupiedInCutSceneEvent)
            {
                var nextFirstPerson = gameState.IsFirstPerson();
                if (nextFirstPerson && isFirstPerson == false)
                {
                    transitions.ThirdToFirstPerson();
                }
                if (!nextFirstPerson && isFirstPerson == true)
                {
                    transitions.FirstToThirdPerson();
                }
                isFirstPerson = nextFirstPerson;
            }

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