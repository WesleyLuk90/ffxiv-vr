using Dalamud.Plugin.Services;

namespace FfxivVR;
public class Logger(
    IPluginLog log,
    IChatGui chatGui,
    Debugging debugging
)
{
    private readonly IChatGui chatGui = chatGui;

    private readonly IPluginLog log = log;

    internal void Debug(string message)
    {
        var logMessage = $"[Debug] {message}";
        log.Info(logMessage);
    }
    internal void Trace(string message)
    {
        if (!debugging.Trace)
        {
            return;
        }
        var logMessage = $"[Trace] {message}";
        log.Info(logMessage);
    }
    internal void Info(string message)
    {
        var logMessage = $"[Info] {message}";
        log.Info(logMessage);
        chatGui.Print(logMessage);
    }
    internal void Error(string message)
    {
        var logMessage = $"[Error] {message}";
        log.Info(logMessage);
        chatGui.Print(logMessage);
    }
}