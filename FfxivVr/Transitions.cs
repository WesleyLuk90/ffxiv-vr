using Dalamud.Game.Config;
using Dalamud.Plugin.Services;

namespace FfxivVR;

public class Transitions(
    VRLifecycle vrLifecycle,
    Configuration configuration,
    IGameConfig gameConfig,
    Logger logger,
    HudLayoutManager hudLayoutManager,
    GameConfigManager gameConfigManager
)
{
    private readonly VRLifecycle vrLifecycle = vrLifecycle;
    private readonly Configuration configuration = configuration;
    private readonly IGameConfig gameConfig = gameConfig;
    private readonly Logger logger = logger;
    private readonly GameConfigManager gameConfigManager = gameConfigManager;

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
        MaybeEnableAutoFaceTarget();
    }

    private void MaybeEnableAutoFaceTarget()
    {
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
        gameConfigManager.Apply();
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
        return true;
    }

    internal void PostStartVR()
    {
        hudLayoutManager.RequestHudLayoutUpdate();
    }

    internal void PostStopVR()
    {
        hudLayoutManager.RequestHudLayoutUpdate();
        MaybeEnableAutoFaceTarget();
        gameConfigManager.Revert();
    }

    internal void OnLogin()
    {
        hudLayoutManager.RequestHudLayoutUpdate();
        if (vrLifecycle.IsEnabled())
        {
            gameConfigManager.Apply();
        }
    }

    internal void OnLogout()
    {
        MaybeEnableAutoFaceTarget();
        gameConfigManager.Revert();
    }
}