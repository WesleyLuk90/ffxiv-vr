using Dalamud.IoC;
using Dalamud.Logging.Internal;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FfxivVR
{
    public class Logger
    {
        [PluginService] public static IPluginLog Log { get; private set; } = null!;
        [PluginService] public static IChatGui ChatGui { get; private set; } = null!;

        internal void Debug(string message)
        {
            Log.Info($"[Debug] {message}");
        }
        internal void Info(string message)
        {
            var log = $"[Info] {message}";
            Log.Info(log);
            ChatGui.Print(log);
        }
        internal void Error(string message)
        {
            var log = $"[Error] {message}";
            Log.Info(log);
            ChatGui.Print(log);
        }
    }
}
