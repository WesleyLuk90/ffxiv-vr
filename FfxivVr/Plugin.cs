using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.IO;
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
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IGamepadState GamepadState { get; private set; } = null!;
    [PluginService] internal static INamePlateGui NamePlateGui { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;

    private const string CommandName = "/vr";
    private Logger logger { get; init; }

    private readonly ExceptionHandler exceptionHandler;
    private readonly VRLifecycle vrLifecycle;
    private readonly GamepadManager gamepadManager;
    private readonly GameHooks gameHooks;
    private readonly Configuration configuration;
    private readonly GameState gameState = new GameState(ClientState, GameGui);
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem WindowSystem = new("FFXIV VR");
    private readonly CompanionPlugins companionPlugins = new CompanionPlugins();

    private readonly GameModifier gameModifier;

    private readonly FreeCamera freeCamera = new FreeCamera();

    private readonly HudLayoutManager hudLayoutManager;
    private readonly ConfigManager configManager;
    private readonly GameConfigManager gameConfigManager;
    private readonly Transitions transitions;

    public Plugin()
    {
        logger = PluginInterface.Create<Logger>() ?? throw new NullReferenceException("Failed to create logger");
        configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Run /vr start and /vr stop to toggle VR. Run /vr to open settings."
        });

        ChatGui.Print("Loaded VR Plugin");

        var dir = PluginInterface.AssemblyLocation.Directory ?? throw new NullReferenceException("Assembly Location missing");
        var dllPath = Path.Combine(dir.ToString(), "openxr_loader.dll");

        exceptionHandler = new ExceptionHandler(logger);
        var pipelineInjector = new RenderPipelineInjector(SigScanner, logger);
        var hookStatus = new HookStatus(PluginInterface);
        this.hookStatus = hookStatus;
        var xr = new XR(XR.CreateDefaultContext([dllPath]));
        gameModifier = new GameModifier(logger, gameState, GameGui, TargetManager, ClientState);
        vrLifecycle = new VRLifecycle(logger, xr, configuration, gameState, pipelineInjector, hookStatus, gameModifier, freeCamera);
        gamepadManager = new GamepadManager(GamepadState, freeCamera);
        GameHookService.InitializeFromAttributes(pipelineInjector);
        gameHooks = new GameHooks(vrLifecycle, exceptionHandler, logger, hookStatus, gameState);
        GameHookService.InitializeFromAttributes(gameHooks);
        gameHooks.Initialize();
        Framework.Update += FrameworkUpdate;

        gameConfigManager = new GameConfigManager(GameConfig, logger, configuration);

        configWindow = new ConfigWindow(configuration, vrLifecycle, ToggleVR, gameConfigManager);
        WindowSystem.AddWindow(configWindow);
        debugWindow = new DebugWindow();
        WindowSystem.AddWindow(debugWindow);

        hudLayoutManager = new HudLayoutManager(configuration, vrLifecycle, logger);
        configManager = new ConfigManager(configuration, logger);


        transitions = new Transitions(vrLifecycle, configuration, GameConfig, logger, companionPlugins, hudLayoutManager, gameConfigManager);

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        NamePlateGui.OnDataUpdate += OnNamePlateUpdate;
        ClientState.Login += Login;
        ClientState.Logout += Logout;
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
        companionPlugins.OnUnload();
        Framework.Update -= FrameworkUpdate;
        ClientState.Login -= Login;
        ClientState.Logout -= Logout;
        NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
        gameHooks.Dispose();

        CommandManager.RemoveHandler(CommandName);

        vrLifecycle.Dispose();
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

        exceptionHandler.FaultBarrier(() =>
        {
            gameConfigManager.Initialize();
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
                StartVR();
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
                    StartVR();
                    break;
                case "stop":
                case "off":
                    StopVR();
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
    public void ToggleVR()
    {
        if (vrLifecycle.IsEnabled())
        {
            StopVR();
        }
        else
        {
            StartVR();
        }
    }
    public void StartVR()
    {
        if (!transitions.PreStartVR())
        {
            return;
        }
        vrLifecycle.EnableVR();
        transitions.PostStartVR();
    }
    private void StopVR()
    {
        vrLifecycle.DisableVR();
        transitions.PostStopVR();
    }
}