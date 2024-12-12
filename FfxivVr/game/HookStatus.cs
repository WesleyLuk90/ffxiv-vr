using Dalamud.Plugin;
using System.Collections.Generic;

namespace FfxivVR;
public class HookStatus(IDalamudPluginInterface pluginInterface)
{
    public void MarkHookAdded()
    {
        var shared = GetSessionState();
        shared.Add("startup-hook");
    }

    public bool IsHookAdded()
    {
        return GetSessionState().Contains("startup-hook");
    }

    private HashSet<string> GetSessionState()
    {
        return pluginInterface.GetOrCreateData("ffxivvr.session-state", () => new HashSet<string>());
    }
}