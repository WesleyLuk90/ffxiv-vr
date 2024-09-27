using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FfxivVR.Windows;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace FfxivVR;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/vr";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SamplePlugin");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private Logger logger;

    public Plugin()
    {
        logger = PluginInterface.Create<Logger>();
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

        ChatGui.Print("hello world");
    }

    public void Dispose()
    {
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
        ChatGui.Print("Starting VR");
        logger.Info("Starting VR");
        vRSession?.Dispose();
        vRSession = new VRSession(
            Path.Combine(PluginInterface.AssemblyLocation.Directory.ToString(), "openxr_loader.dll"),
            logger,
            Device.Instance()
        );
        vRSession.Initialize();
    }
    private unsafe void StopVR()
    {
        ChatGui.Print("Stopping VR");
        logger.Info("Stopping VR");
        vRSession?.Dispose();
        vRSession = null;
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
