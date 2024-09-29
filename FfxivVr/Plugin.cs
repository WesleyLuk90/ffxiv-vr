using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FfxivVR.Windows;
using Silk.NET.Direct3D11;
using System;
using System.Collections.Generic;
using System.IO;

namespace FfxivVR;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private const string CommandName = "/vr";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SamplePlugin");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }


    private Logger logger { get; init; }

    public Plugin()
    {
        logger = PluginInterface.Create<Logger>() ?? throw new NullReferenceException("Failed to create logger");
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Framework.Update += Update;

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
    }

    private Dictionary<string, int> exceptionCount = new Dictionary<string, int>();
    private void Update(IFramework framework)
    {
        try
        {
            vRSession?.Update();
        }
        catch (Exception ex)
        {
            var currentCount = exceptionCount.GetValueOrDefault(ex.Message) + 1;
            exceptionCount[ex.Message] = currentCount;
            if (currentCount == 5)
            {
                logger.Error($"Got same error 5 times ({ex.Message}), surpressing");
            }
            else if (currentCount < 5)
            {
                throw;
            }
        }
    }

    public void Dispose()
    {
        Framework.Update -= Update;
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        vRSession?.Dispose();
    }
    private VRSession? vRSession;
    private unsafe void OnCommand(string command, string args)
    {
        if (command == CommandName)
        {
            switch (args)
            {
                case "start":
                    StartVR();
                    break;
                case "stop":
                    StopVR();
                    break;
            }
        }
    }

    private unsafe void StartVR()
    {
        logger.Info("Starting VR");
        vRSession?.Dispose();
        var dir = PluginInterface.AssemblyLocation.Directory ?? throw new NullReferenceException("Assembly Location missing");
        var device = Device.Instance();
        ID3D11Device* d11Device = (ID3D11Device*)device->D3D11Forwarder;
        ID3D11DeviceContext* d11DeviceContext = (ID3D11DeviceContext*)device->D3D11DeviceContext;
        vRSession = new VRSession(
            Path.Combine(dir.ToString(), "openxr_loader.dll"),
            logger,
            device: d11Device,
            deviceContext: d11DeviceContext
        );
        try
        {
            vRSession.Initialize();
        }
        catch (Exception ex)
        {
            logger.Error("VR Session failed to load");
            try
            {
                vRSession.Dispose();
            }
            catch (Exception e)
            {
                logger.Error($"Got error when disposing session {e}");
            }
            vRSession = null;
            throw new Exception("Failed to start VR", ex);
        }
    }
    private unsafe void StopVR()
    {
        logger.Info("Stopping VR");
        vRSession?.Dispose();
        vRSession = null;
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
