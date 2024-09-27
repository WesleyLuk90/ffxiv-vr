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
        [PluginService] public static IPluginLog? Log { get; private set; } = null;

        internal void Info(string message)
        {
            Log!.Info(message);
        }
        internal void Error(string message)
        {
            Log!.Error(message);
        }
    }
}
