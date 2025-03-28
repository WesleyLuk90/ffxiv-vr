using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using System;
using System.Linq;
using Windows.Win32.Foundation;

namespace FfxivVR;

public class CommandHander(
    ICommandManager commandManager,
    ConfigWindow configWindow,
    VRStartStop vrStartStop,
    VRLifecycle vrLifecycle,
    Logger logger,
    ConfigManager configManager,
    DebugWindow debugWindow
) : IDisposable
{
    private const string CommandName = "/vr";
    public void Initialize()
    {
        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Run /vr start and /vr stop to toggle VR. Run /vr to open settings."
        });
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
                case "debug":
                    debugWindow.Toggle();
                    break;
                default:
                    logger.Error($"Unknown command {arguments.FirstOrDefault()}");
                    break;
            }
        }
    }

    public void Dispose()
    {
        commandManager.RemoveHandler(CommandName);
    }
}
internal static class Ext
{
    internal static string Display(this RECT rect)
    {
        return $"l:{rect.left}  r:{rect.right} t:{rect.top} b:{rect.bottom}";
    }

}