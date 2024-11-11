using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SharpDX.Win32;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

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

    private const string CommandName = "/vr";
    private Logger logger { get; init; }

    private readonly ExceptionHandler exceptionHandler;
    private readonly VRDiagnostics diagnostics;
    private readonly VRLifecycle vrLifecycle;
    private readonly GamepadManager gamepadManager;
    private readonly GameHooks gameHooks;
    private readonly Configuration configuration;
    private readonly GameState gameState = new GameState(ClientState);
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem WindowSystem = new("FFXIV VR");
    private readonly CompanionPlugins companionPlugins = new CompanionPlugins();

    private readonly GameSettingsManager gameSettingsManager;

    private readonly GameModifier gameModifier;

    private readonly FreeCamera freeCamera = new FreeCamera();
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
        var hookStatus = new HookStatus();
        var xr = new XR(XR.CreateDefaultContext([dllPath]));
        diagnostics = new VRDiagnostics(logger);
        gameSettingsManager = new GameSettingsManager(logger);
        gameModifier = new GameModifier(logger, gameState, GameGui, TargetManager, ClientState);
        vrLifecycle = new VRLifecycle(logger, xr, configuration, gameState, pipelineInjector, hookStatus, diagnostics, gameModifier, freeCamera);
        gamepadManager = new GamepadManager(GamepadState, freeCamera);
        GameHookService.InitializeFromAttributes(pipelineInjector);
        gameHooks = new GameHooks(vrLifecycle, exceptionHandler, logger, hookStatus, gameState);
        GameHookService.InitializeFromAttributes(gameHooks);
        gameHooks.Initialize();
        Framework.Update += FrameworkUpdate;


        configWindow = new ConfigWindow(configuration, vrLifecycle, ToggleVR);
        WindowSystem.AddWindow(configWindow);
        debugWindow = new DebugWindow();
        WindowSystem.AddWindow(debugWindow);

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
    }

    private void ToggleConfigUI()
    {
        configWindow.Toggle();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    private bool? isFirstPerson = null;

    private void FrameworkUpdate(IFramework framework)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            MaybeOnBootStartVR();
            if (!Conditions.IsOccupiedInCutSceneEvent)
            {
                var nextFirstPerson = gameState.IsFirstPerson();
                if (nextFirstPerson && isFirstPerson == false)
                {
                    ThirdToFirstPerson();
                }
                if (!nextFirstPerson && isFirstPerson == true)
                {
                    FirstToThirdPerson();
                }
                isFirstPerson = nextFirstPerson;
            }

            UpdateFreeCam(framework);
        });
    }


    private bool LaunchAtStartChecked = false;
    private DebugWindow debugWindow;
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

    private void FirstToThirdPerson()
    {
        if (!vrLifecycle.IsEnabled())
        {
            return;
        }
        if (configuration.RecenterOnViewChange)
        {
            vrLifecycle.RecenterCamera();
        }
        if (configuration.DisableAutoFaceTargetInFirstPerson)
        {
            gameSettingsManager.SetUIBooleanSetting(ConfigOption.AutoFaceTargetOnAction, true);
        }
    }

    private void ThirdToFirstPerson()
    {
        if (!vrLifecycle.IsEnabled())
        {
            return;
        }
        if (configuration.RecenterOnViewChange)
        {
            vrLifecycle.RecenterCamera();
        }
        if (configuration.DisableAutoFaceTargetInFirstPerson)
        {
            gameSettingsManager.SetUIBooleanSetting(ConfigOption.AutoFaceTargetOnAction, false);
        }
    }

    public void Dispose()
    {
        configuration.Save();
        companionPlugins.OnUnload();
        Framework.Update -= FrameworkUpdate;
        gameHooks.Dispose();

        CommandManager.RemoveHandler(CommandName);

        vrLifecycle.Dispose();
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
                case "info":
                    diagnostics.Print();
                    break;
                case "recenter":
                    vrLifecycle.RecenterCamera();
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
                        var gameCamera = new GameCamera(active->Position.ToVector3D(), active->LookAtVector.ToVector3D());
                        freeCamera.Reset(gameCamera.Position, gameCamera.GetYRotation());
                        freeCamera.Enabled = true;
                        logger.Info("Enabled free cam");
                    }
                    break;
                // Development commands
                case "debugmode":
                    Debugging.DebugMode = !Debugging.DebugMode;
                    logger.Info($"DebugMode enabled: {Debugging.DebugMode}");
                    break;
                case "debug":
                    debugWindow.Toggle();
                    break;
                case "resetcamera":
                    var currentCamera = gameState.GetRawCamera();
                    if (currentCamera == null)
                    {
                        return;
                    }
                    logger.Info($"Hrotation {currentCamera->CurrentHRotation}");
                    logger.Info($"CurrentVRotation {currentCamera->CurrentVRotation}");
                    currentCamera->CurrentVRotation = 0;
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
        if (gameSettingsManager.GetIntSystemSetting(ConfigOption.ScreenMode) == 1)
        {
            logger.Error("VR does not work in full screen. Please switch to windowed or borderless window.");
            return;
        }
        if (gameSettingsManager.GetIntSystemSetting(ConfigOption.Gamma) == 50) // Gamma of 50 breaks the render, adjust it slightly
        {
            gameSettingsManager.SetIntSystemSetting(ConfigOption.Gamma, 51);
        }
        diagnostics.OnStart();
        vrLifecycle.EnableVR();
        companionPlugins.OnActivate();
    }
    private void StopVR()
    {
        diagnostics.OnStop();
        vrLifecycle.DisableVR();
        configuration.Save();
        companionPlugins.OnDeactivate();
    }
}
