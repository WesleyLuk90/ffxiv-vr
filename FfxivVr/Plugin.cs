using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FfxivVR.Windows;
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

    private const string CommandName = "/vr";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SamplePlugin");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }


    private Logger logger { get; init; }

    private readonly ExceptionHandler exceptionHandler;
    private readonly VRLifecycle vrLifecycle;
    private readonly GameHooks gameHooks;
    private readonly VRSettings settings = new VRSettings();
    private readonly GameState gameState = new GameState();
    public Plugin()
    {
        logger = PluginInterface.Create<Logger>() ?? throw new NullReferenceException("Failed to create logger");
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // you might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        ChatGui.Print("Loaded VR Plugin");

        var dir = PluginInterface.AssemblyLocation.Directory ?? throw new NullReferenceException("Assembly Location missing");
        var dllPath = Path.Combine(dir.ToString(), "openxr_loader.dll");

        exceptionHandler = new ExceptionHandler(logger);
        vrLifecycle = new VRLifecycle(logger, dllPath, settings, gameState);
        gameHooks = new GameHooks(vrLifecycle, exceptionHandler, logger);

        GameHookService.InitializeFromAttributes(gameHooks);
        gameHooks.Initialize();
        Framework.Update += FrameworkUpdate;
    }

    private bool? isFirstPerson = null;
    private void FrameworkUpdate(IFramework framework)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            var isFirstPersonNow = gameState.IsFirstPerson();
            if (isFirstPerson != null && isFirstPerson != isFirstPersonNow)
            {
                vrLifecycle.RecenterCamera();
            }
            isFirstPerson = isFirstPersonNow;
            vrLifecycle.UpdateModelVisibility();
        });
    }

    public void Dispose()
    {
        Framework.Update -= FrameworkUpdate;
        gameHooks.Dispose();
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

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
                case "start":
                    StartVR();
                    break;
                case "stop":
                    StopVR();
                    break;
                case "scale":
                    var scaleString = arguments.ElementAtOrDefault(1);
                    float scale;
                    if (float.TryParse(scaleString, out scale) && scale < 10 && scale > 0.1)
                    {
                        logger.Info($"Setting world scale to {scale}");
                        settings.Scale = scale;
                    }
                    else
                    {
                        logger.Info($"Invalid scale {scaleString}, must be between 0.1 and 10");
                    }
                    break;
                case "printtextures":
                    var renderTargetManager = RenderTargetManager.Instance();
                    var depthTexture = renderTargetManager->RenderTargets[10];
                    var renderTexture = renderTargetManager->RenderTargets2[33];
                    logger.Info($"Render target:{renderTexture.Value->ActualWidth}x{renderTexture.Value->ActualHeight} format ${renderTexture.Value->TextureFormat}");
                    logger.Info($"depth:{depthTexture.Value->ActualWidth}x{depthTexture.Value->ActualHeight} format ${depthTexture.Value->TextureFormat}");
                    break;
                case "printcamera":
                    logger.Info($"Camera mode {SceneCameraExtensions.GetCameraMode()}");
                    break;
                case "player":
                    var player = ClientState.LocalPlayer;
                    if (player == null)
                    {
                        logger.Info($"No player");
                        break;
                    }
                    var character = (Character*)player!.Address;
                    logger.Info($"flags {character->GameObject.DrawObject->Flags}");
                    character->GameObject.DrawObject->Flags = (byte)ModelCullTypes.Visible;
                    break;
                default:
                    logger.Error($"Unknown command {arguments.FirstOrDefault()}");
                    break;
            }
        }
    }

    public enum ModelCullTypes : byte
    {
        None = 0,
        InsideCamera = 0x42,
        OutsideCullCone = 0x43,
        Visible = 0x4B
    }
    public unsafe void StartVR()
    {
        vrLifecycle.EnableVR();
    }
    private unsafe void StopVR()
    {
        vrLifecycle.DisableVR();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
