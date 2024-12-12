using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace FfxivVR;

public class PluginUI(
    ConfigWindow configWindow,
    DebugWindow debugWindow,
    IDalamudPluginInterface pluginInterface,
    Debugging debugging,
    ExceptionHandler exceptionHandler,
    Configuration configuration,
    VRStartStop vrStartStop,
    Logger logger
)
{
    private readonly WindowSystem WindowSystem = new("FFXIV VR");
    public void Initialize()
    {
        WindowSystem.AddWindow(configWindow);
        WindowSystem.AddWindow(debugWindow);

        pluginInterface.UiBuilder.Draw += DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
    }

    private void ToggleConfigUI()
    {
        configWindow.Toggle();
    }
    private void DrawUI()
    {
        debugging.DrawLocation();
        WindowSystem.Draw();
        // We require dalamud ui to be ready so wait for the draw call
        exceptionHandler.FaultBarrier(() =>
        {
            MaybeOnBootStartVR();
        });
    }
    private bool LaunchAtStartChecked = false;
    private void MaybeOnBootStartVR()
    {
        var shouldLaunchOnStart = !LaunchAtStartChecked &&
            configuration.StartVRAtBoot &&
            pluginInterface.Reason == PluginLoadReason.Boot;
        LaunchAtStartChecked = true;
        if (shouldLaunchOnStart)
        {
            try
            {
                vrStartStop.StartVR();
            }
            catch (VRSystem.FormFactorUnavailableException)
            {
                logger.Debug("No vr headset connected, skipping start at boot");
            }
        }
    }
}