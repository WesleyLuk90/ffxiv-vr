using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.UI;
using Silk.NET.Direct3D11;
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

    private const string CommandName = "/vr";
    private Logger logger { get; init; }

    private readonly ExceptionHandler exceptionHandler;
    private readonly VRLifecycle vrLifecycle;
    private readonly GameHooks gameHooks;
    private readonly Configuration configuration;
    private readonly GameState gameState = new GameState(ClientState);
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
        });
    }

    public void Dispose()
    {
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
                        configuration.WorldScale = scale;
                    }
                    else
                    {
                        logger.Info($"Invalid scale {scaleString}, must be between 0.1 and 10");
                    }
                    break;
                case "ui-distance":
                    var distanceString = arguments.ElementAtOrDefault(1);
                    float distance;
                    if (float.TryParse(distanceString, out distance) && distance < 10 && distance > 0.1)
                    {
                        logger.Info($"Setting UI distance to {distance}");
                        configuration.UIDistance = distance;
                    }
                    else
                    {
                        logger.Info($"Invalid distance {distanceString}, must be between 0.1 and 10");
                    }
                    break;
                case "debug-cursor":
                    var data = UIInputData.Instance();
                    logger.Info($"Cursor {data->CursorXPosition} {data->CursorYPosition}");
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
        logger.Debug($"Saving settings {configuration}");
    }
}
