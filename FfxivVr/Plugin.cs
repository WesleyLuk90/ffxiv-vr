using Dalamud.Game.Command;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FfxivVR;

public unsafe sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameHookService { get; private set; } = null!;
    [PluginService] internal static IGamepadState GamepadState { get; private set; } = null!;
    [PluginService] internal static INamePlateGui NamePlateGui { get; private set; } = null!;

    private const string CommandName = "/vr";
    private readonly Logger logger;

    private readonly ExceptionHandler exceptionHandler;
    private readonly VRLifecycle vrLifecycle;
    private readonly VRStartStop vrStartStop;
    private readonly GamepadManager gamepadManager;
    private readonly Configuration configuration;
    private readonly GameState gameState;
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem WindowSystem = new("FFXIV VR");
    private readonly FreeCamera freeCamera;

    private readonly HudLayoutManager hudLayoutManager;
    private readonly ConfigManager configManager;
    private readonly Transitions transitions;

    private IHost AppHost;
    public Plugin()
    {
        var appFactory = PluginInterface.Create<AppFactory>() ?? throw new NullReferenceException("Failed to create logger");
        AppHost = appFactory.CreateSession();

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Run /vr start and /vr stop to toggle VR. Run /vr to open settings."
        });

        ChatGui.Print("Loaded VR Plugin");

        GameHookService.InitializeFromAttributes(AppHost.Services.GetRequiredService<RenderPipelineInjector>());
        var gameHooks = AppHost.Services.GetRequiredService<GameHooks>();
        GameHookService.InitializeFromAttributes(AppHost.Services.GetRequiredService<GameHooks>());
        gameHooks.Initialize();
        Framework.Update += FrameworkUpdate;

        WindowSystem.AddWindow(AppHost.Services.GetRequiredService<ConfigWindow>());
        WindowSystem.AddWindow(AppHost.Services.GetRequiredService<DebugWindow>());

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        NamePlateGui.OnDataUpdate += OnNamePlateUpdate;
        ClientState.Login += Login;
        ClientState.Logout += Logout;

        logger = AppHost.Services.GetRequiredService<Logger>();
        exceptionHandler = AppHost.Services.GetRequiredService<ExceptionHandler>();
        vrLifecycle = AppHost.Services.GetRequiredService<VRLifecycle>();
        vrStartStop = AppHost.Services.GetRequiredService<VRStartStop>();
        gamepadManager = AppHost.Services.GetRequiredService<GamepadManager>();
        configuration = AppHost.Services.GetRequiredService<Configuration>();
        gameState = AppHost.Services.GetRequiredService<GameState>();
        configWindow = AppHost.Services.GetRequiredService<ConfigWindow>();
        freeCamera = AppHost.Services.GetRequiredService<FreeCamera>();
        hudLayoutManager = AppHost.Services.GetRequiredService<HudLayoutManager>();
        configManager = AppHost.Services.GetRequiredService<ConfigManager>();
        transitions = AppHost.Services.GetRequiredService<Transitions>();
        debugWindow = AppHost.Services.GetRequiredService<DebugWindow>();
        hookStatus = AppHost.Services.GetRequiredService<HookStatus>();
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

    public void Dispose()
    {
        Framework.Update -= FrameworkUpdate;
        ClientState.Login -= Login;
        ClientState.Logout -= Logout;
        NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
        AppHost.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void ToggleConfigUI()
    {
        configWindow.Toggle();
    }

    private void DrawUI()
    {
        Debugging.DrawLocation();
        WindowSystem.Draw();
        // We require dalamud ui to be ready so wait for the draw call
        exceptionHandler.FaultBarrier(() =>
        {
            MaybeOnBootStartVR();
        });
    }

    private bool? isFirstPerson = null;

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
    private bool LaunchAtStartChecked = false;
    private DebugWindow debugWindow;
    private HookStatus hookStatus;

    private void MaybeOnBootStartVR()
    {
        var shouldLaunchOnStart = !LaunchAtStartChecked &&
            configuration.StartVRAtBoot &&
            PluginInterface.Reason == PluginLoadReason.Boot;
        LaunchAtStartChecked = true;
        if (shouldLaunchOnStart)
        {
            try
            {
                vrStartStop.StartVR();
            }
            catch (VRSystem.FormFactorUnavailableException)
            {
                logger.Debug("No vr headset connected, skipping start at boot");
            }
        }
    }

    private void UpdateFreeCam(IFramework framework)
    {
        gamepadManager.Update();
        var speed = 0.05f;
        var rotationSpeed = 2 * MathF.PI / 200;
        var timeDelta = (float)framework.UpdateDelta.TotalSeconds;
        freeCamera.UpdatePosition(
            walkDelta: new Vector2D<float>(GamepadState.LeftStick.X, GamepadState.LeftStick.Y) * timeDelta * speed,
            heightDelta: GamepadState.RightStick.Y * timeDelta * speed,
            rotationDelta: -GamepadState.RightStick.X * timeDelta * rotationSpeed
        );
    }

    private unsafe void OnCommand(string command, string args)
    {
        var arguments = args.Split(" ");
        if (command == CommandName)
        {
            switch (arguments.FirstOrDefault())
            {
                case "":
                    configWindow.Toggle();
                    break;
                case "start":
                case "on":
                    vrStartStop.StartVR();
                    break;
                case "stop":
                case "off":
                    vrStartStop.StopVR();
                    break;
                case "recenter":
                    vrLifecycle.RecenterCamera();
                    break;
                case "config":
                    if (arguments.ElementAtOrDefault(1) is not string name || arguments.ElementAtOrDefault(2) is not string value)
                    {
                        logger.Error("Invalid syntax, expected \"/vr config <name> <value>\"");
                        break;
                    }
                    configManager.SetConfig(name, value);
                    break;
                case "freecam":
                    if (freeCamera.Enabled)
                    {
                        freeCamera.Enabled = false;
                        logger.Info("Disabled free cam");
                    }
                    else
                    {
                        var active = gameState.GetCurrentCamera();
                        var gameCamera = new GameCamera(active->Position.ToVector3D(), active->LookAtVector.ToVector3D(), null);
                        freeCamera.Reset(gameCamera.Position, gameCamera.GetYRotation());
                        freeCamera.Enabled = true;
                        logger.Info("Enabled free cam");
                    }
                    break;
                case "debug":
                    debugWindow.Toggle();
                    break;
                default:
                    logger.Error($"Unknown command {arguments.FirstOrDefault()}");
                    break;
            }
        }
    }
}