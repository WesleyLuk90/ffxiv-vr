using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Silk.NET.Direct3D11;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

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
        vrLifecycle = new VRLifecycle(logger, dllPath, configuration, gameState, pipelineInjector, GameGui, ClientState, TargetManager);
        GameHookService.InitializeFromAttributes(pipelineInjector);
        gameHooks = new GameHooks(vrLifecycle, exceptionHandler, logger, pipelineInjector);

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
        });
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
                case "debugsetting":
                    gameSettingsManager.SetBooleanSetting(ConfigOption.AutoFaceTargetOnAction, false);
                    break;
                case "toggle-gamepad":
                    var assembly = Assembly.GetAssembly(typeof(IGamepadState)) ?? throw new Exception("Could not get assembly");
                    var gamepad = assembly.GetType("Dalamud.Game.ClientState.GamePad.GamepadState") ?? throw new Exception("Could not get gamepad");
                    var property = gamepad.GetProperty("NavEnableGamepad",
                         BindingFlags.NonPublic |
                         BindingFlags.Instance) ?? throw new Exception("Could not get NavEnableGamepad");
                    var newState = !(bool)property.GetValue(GamepadState);
                    property.SetValue(GamepadState, newState);
                    logger.Info($"Set state {newState}");
                    break;
                case "printtextures":
                    var renderTargetManager = RenderTargetManager.Instance();
                    //var depthTexture = renderTargetManager->RenderTargets[10];
                    //var renderTexture = renderTargetManager->RenderTargets2[33];
                    for (int i = 0; i < renderTargetManager->RenderTargets2.Length; i++)
                    {
                        var tex = renderTargetManager->RenderTargets2[i].Value;
                        if (tex != null)
                        {
                            logger.Info($"Render target {i}:{tex->ActualWidth}x{tex->ActualHeight} format ${tex->TextureFormat}");
                            var texture = (ID3D11Texture2D*)tex->D3D11Texture2D;
                            var desc = new Texture2DDesc();
                            texture->GetDesc(ref desc);
                            logger.Info($"Type is {desc.Format}");
                        }
                        else
                        {
                            logger.Info($"Render target null{i}");
                        }
                    }
                    //logger.Info($"depth:{depthTexture.Value->ActualWidth}x{depthTexture.Value->ActualHeight} format ${depthTexture.Value->TextureFormat}");
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
