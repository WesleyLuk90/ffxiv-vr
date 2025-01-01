using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;
using System.Linq;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace FfxivVR;

public class CommandHander(
    ICommandManager commandManager,
    ConfigWindow configWindow,
    VRStartStop vrStartStop,
    VRLifecycle vrLifecycle,
    Logger logger,
    ConfigManager configManager,
    FreeCamera freeCamera,
    GameState gameState,
    DebugWindow debugWindow,
    Configuration configuration
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
                case "resolution":
                    var framework = Framework.Instance();
                    var handle = (HWND)framework->GameWindow->WindowHandle;
                    HMONITOR monitor = PInvoke.MonitorFromWindow(handle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
                    MONITORINFO monitorInfo = new MONITORINFO();
                    monitorInfo.cbSize = (uint)sizeof(MONITORINFO);
                    if (!PInvoke.GetMonitorInfo(monitor, ref monitorInfo))
                    {
                        throw new Exception("Failed to GetMonitorInfo");
                    }
                    logger.Info($"Monitor size {monitorInfo.rcMonitor.Display()} work:{monitorInfo.rcWork.Display()}");
                    PInvoke.GetClientRect(handle, out RECT clientRect);
                    logger.Info($"Client rect {clientRect.Display()}");
                    PInvoke.GetWindowRect(handle, out RECT winRect);
                    logger.Info($"winRect rect {winRect.Display()}");

                    var style = PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
                    var exstyle = PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
                    var desiredRect = new RECT();
                    desiredRect.top = 0;
                    desiredRect.left = 0;
                    desiredRect.right = 0;
                    desiredRect.bottom = 0;
                    PInvoke.AdjustWindowRectEx(ref desiredRect, (WINDOW_STYLE)style, false, (WINDOW_EX_STYLE)exstyle);
                    logger.Info($"AdjustWindowRectEx {desiredRect.Display()}");
                    RECT frame = new();
                    PInvoke.DwmGetWindowAttribute(handle, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &frame, (uint)sizeof(RECT));
                    logger.Info($"frame {frame.Display()}");
                    RECT spi = new();
                    PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETWORKAREA, 0, &spi, 0);
                    logger.Info($"SPI_GETWORKAREA {spi.Display()}");
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