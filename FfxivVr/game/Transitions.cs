using Dalamud.Game.Config;
using Dalamud.Plugin.Services;

namespace FfxivVR;

public class Transitions(
    VRLifecycle vrLifecycle,
    Logger logger,
    HudLayoutManager hudLayoutManager,
    GameConfigManager gameConfigManager,
    IGameConfig gameConfig
)
{

    public bool PreStartVR()
    {
        gameConfigManager.ApplyVRSettings();
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
        gameConfigManager.Revert();
    }

    internal void OnLogin()
    {
        hudLayoutManager.RequestHudLayoutUpdate();
        if (vrLifecycle.IsEnabled())
        {
            gameConfigManager.ApplyVRSettings();
        }
    }

    internal void OnLogout()
    {
        gameConfigManager.Revert();
    }
}