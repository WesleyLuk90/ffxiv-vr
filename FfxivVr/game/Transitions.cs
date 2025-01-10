using Dalamud.Game.Config;
using Dalamud.Plugin.Services;

namespace FfxivVR;

public class Transitions(
    VRLifecycle vrLifecycle,
    Configuration configuration,
    Logger logger,
    HudLayoutManager hudLayoutManager,
    GameConfigManager gameConfigManager,
    IGameConfig gameConfig,
    GameState gameState
)
{
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
        UpdateAutoFaceTarget();
    }

    private void UpdateAutoFaceTarget()
    {
        if (configuration.DisableAutoFaceTargetInFirstPerson && vrLifecycle.IsEnabled() && gameState.IsFirstPerson())
        {
            gameConfigManager.SetSetting(UiControlOption.AutoFaceTargetOnAction.ToString(), 0);
        }
        else
        {
            gameConfigManager.Revert(UiControlOption.AutoFaceTargetOnAction.ToString());
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
        UpdateAutoFaceTarget();
    }

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
        UpdateAutoFaceTarget();
        gameConfigManager.Revert();
    }

    internal void OnLogin()
    {
        hudLayoutManager.RequestHudLayoutUpdate();
        if (vrLifecycle.IsEnabled())
        {
            gameConfigManager.ApplyVRSettings();
            UpdateAutoFaceTarget();
        }
    }

    internal void OnLogout()
    {
        gameConfigManager.Revert();
    }
}