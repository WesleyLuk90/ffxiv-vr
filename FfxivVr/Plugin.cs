using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Silk.NET.Maths;
using System;
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

    private const string CommandName = "/vr";
    private Logger logger { get; init; }

    private readonly ExceptionHandler exceptionHandler;
    private readonly VRLifecycle vrLifecycle;
    private readonly GamepadManager gamepadManager;
    private readonly GameHooks gameHooks;
    private readonly Configuration configuration;
    private readonly GameState gameState = new GameState(ClientState);
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem WindowSystem = new("FFXIV VR");

    private readonly GameSettingsManager gameSettingsManager;
    public Plugin()
    {
        logger = PluginInterface.Create<Logger>() ?? throw new NullReferenceException("Failed to create logger");
        configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        ChatGui.Print("Loaded VR Plugin");

        var dir = PluginInterface.AssemblyLocation.Directory ?? throw new NullReferenceException("Assembly Location missing");
        var dllPath = Path.Combine(dir.ToString(), "openxr_loader.dll");

        exceptionHandler = new ExceptionHandler(logger);
        var pipelineInjector = new RenderPipelineInjector(SigScanner, logger);
        var hookStatus = new HookStatus();
        vrLifecycle = new VRLifecycle(logger, dllPath, configuration, gameState, pipelineInjector, GameGui, ClientState, TargetManager, hookStatus);
        gamepadManager = new GamepadManager(GamepadState, vrLifecycle);
        GameHookService.InitializeFromAttributes(pipelineInjector);
        gameHooks = new GameHooks(vrLifecycle, exceptionHandler, logger, pipelineInjector, hookStatus);
        GameHookService.InitializeFromAttributes(gameHooks);
        gameHooks.Initialize();
        Framework.Update += FrameworkUpdate;

        gameSettingsManager = new GameSettingsManager(logger);

        configWindow = new ConfigWindow(configuration);
        WindowSystem.AddWindow(configWindow);

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

    }

    private void ToggleMainUI()
    {
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

            UpdateFreeCam(framework);
        });
    }

    private void UpdateFreeCam(IFramework framework)
    {
        gamepadManager.Update();
        var speed = 0.05f;
        var rotationSpeed = 2 * MathF.PI / 200;
        var timeDelta = (float)framework.UpdateDelta.TotalSeconds;
        vrLifecycle.GetFreeCamera()?.UpdatePosition(
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
            gameSettingsManager.SetBooleanSetting(ConfigOption.AutoFaceTargetOnAction, true);
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
            gameSettingsManager.SetBooleanSetting(ConfigOption.AutoFaceTargetOnAction, false);
        }
    }

    public void Dispose()
    {
        configWindow.Dispose();
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
                    StartVR();
                    break;
                case "stop":
                    StopVR();
                    break;
                case "recenter":
                    vrLifecycle.RecenterCamera();
                    break;
                case "freecam":
                    var freeCam = vrLifecycle.GetFreeCamera();
                    if (freeCam == null)
                    {
                        logger.Info("Free cam can only be enabled after VR has started");
                    }
                    else
                    {
                        if (freeCam.Enabled)
                        {
                            freeCam.Enabled = false;
                            logger.Info("Disabled free cam");
                        }
                        else
                        {
                            freeCam.Enabled = true;
                            logger.Info("Enabled free cam");
                        }
                    }
                    break;
                default:
                    logger.Error($"Unknown command {arguments.FirstOrDefault()}");
                    break;
            }
        }
    }
    public unsafe void StartVR()
    {
        vrLifecycle.EnableVR();
    }
    private unsafe void StopVR()
    {
        vrLifecycle.DisableVR();
        configuration.Save();
    }
}
