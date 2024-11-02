using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace FfxivVR
{
    public class Logger
    {
        [PluginService] public static IPluginLog? Log { get; private set; } = null;
        [PluginService] public static IChatGui? ChatGui { get; private set; } = null;


        internal void Debug(string message)
        {
            var log = $"[Debug] {message}";
            Log?.Info(log);
        }

        public bool TraceEnabled = true;
        internal void Trace(string message)
        {
            if (!TraceEnabled)
            {
                return;
            }
            var log = $"[Trace] {message}";
            Log?.Info(log);
        }
        internal void Info(string message)
        {
            var log = $"[Info] {message}";
            Log?.Info(log);
            ChatGui?.Print(log);
        }
        internal void Error(string message)
        {
            var log = $"[Error] {message}";
            Log?.Info(log);
            ChatGui?.Print(log);
        }
    }
}
