using Dalamud.Game.Config;
using Dalamud.Plugin.Services;

namespace FfxivVR;

public class Transitions(
    VRLifecycle vrLifecycle,
    Configuration configuration,
    IGameConfig gameConfig,
    Logger logger,
    VRDiagnostics diagnostics,
    CompanionPlugins companionPlugins,
    HudLayoutManager hudLayoutManager
)
{
    private readonly VRLifecycle vrLifecycle = vrLifecycle;
    private readonly Configuration configuration = configuration;
    private readonly IGameConfig gameConfig = gameConfig;
    private readonly Logger logger = logger;
    private readonly VRDiagnostics diagnostics = diagnostics;
    private readonly CompanionPlugins companionPlugins = companionPlugins;

    public void FirstToThirdPerson()
    {
        if (!vrLifecycle.IsEnabled())
        {
            return;
        }
        if (configuration.RecenterOnViewChange)
        {
            vrLifecycle.RecenterCamera();
        }
        if (configuration.DisableAutoFaceTargetInFirstPerson)
        {
            gameConfig.Set(UiControlOption.AutoFaceTargetOnAction, true);
        }
    }

    public void ThirdToFirstPerson()
    {
        if (!vrLifecycle.IsEnabled())
        {
            return;
        }
        if (configuration.RecenterOnViewChange)
        {
            vrLifecycle.RecenterCamera();
        }
        if (configuration.DisableAutoFaceTargetInFirstPerson)
        {
            gameConfig.Set(UiControlOption.AutoFaceTargetOnAction, false);
        }
    }

    public bool PreStartVR()
    {
        if (!gameConfig.TryGet(SystemConfigOption.ScreenMode, out uint screenMode))
        {
            logger.Error("Failed to lookup screen mode");
        }
        if (screenMode == 1)
        {
            logger.Error("VR does not work in full screen. Please switch to windowed or borderless window.");
            return false;
        }
        if (!gameConfig.TryGet(SystemConfigOption.Gamma, out uint gamma))
        {
            logger.Error("Failed to lookup screen mode");
        }
        if (gamma == 50) // Gamma of 50 breaks the render, adjust it slightly
        {
            gameConfig.Set(SystemConfigOption.Gamma, 51);
        }
        diagnostics.OnStart();
        return true;
    }

    public void PreStopVR()
    {
        diagnostics.OnStop();
    }

    internal void PostStartVR()
    {
        companionPlugins.OnActivate();
        hudLayoutManager.RequestHudLayoutUpdate();
    }

    internal void PostStopVR()
    {
        companionPlugins.OnDeactivate();
        hudLayoutManager.RequestHudLayoutUpdate();
    }

    internal void OnLogin()
    {
        hudLayoutManager.RequestHudLayoutUpdate();
    }
}