using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;

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
        var pipelineInjector = new RenderPipelineInjector(SigScanner, logger);
        vrLifecycle = new VRLifecycle(logger, dllPath, settings, gameState, pipelineInjector);
        GameHookService.InitializeFromAttributes(pipelineInjector);
        gameHooks = new GameHooks(vrLifecycle, exceptionHandler, logger, pipelineInjector);

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
            vrLifecycle.FrameworkUpdate();
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
                    //var depthTexture = renderTargetManager->RenderTargets[10];
                    //var renderTexture = renderTargetManager->RenderTargets2[33];
                    for (int i = 0; i < renderTargetManager->RenderTargets2.Length; i++)
                    {
                        var tex = renderTargetManager->RenderTargets2[i].Value;
                        if (tex != null)
                        {
                            logger.Info($"Render target {i}:{tex->ActualWidth}x{tex->ActualHeight} format ${tex->TextureFormat}");
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
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
