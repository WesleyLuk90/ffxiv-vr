using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace FfxivVR;

public class HudLayoutManager(Configuration configuration, VRLifecycle vRLifecycle, Logger logger)
{
    private readonly Configuration configuration = configuration;
    private readonly VRLifecycle vRLifecycle = vRLifecycle;
    private bool shouldUpdate = false;
    public void Update()
    {
        if (shouldUpdate)
        {
            shouldUpdate = false;
            if (vRLifecycle.IsEnabled() && configuration.VRHudLayout is int vrLayout && 0 <= vrLayout && vrLayout <= 3)
            {
                SwitchLayout(vrLayout);
            }
            else if (!vRLifecycle.IsEnabled() && configuration.DefaultHudLayout is int defaultLayout && 0 <= defaultLayout && defaultLayout <= 3)
            {
                SwitchLayout(defaultLayout);
            }
        }
    }

    private unsafe void SwitchLayout(int layoutIndex)
    {
        var config = AddonConfig.Instance();
        if (config == null)
        {
            return;
        }
        config->ChangeHudLayout((uint)layoutIndex);
        logger.Debug($"Switched hud layout {layoutIndex}");
    }

    public void RequestHudLayoutUpdate()
    {
        shouldUpdate = true;
    }
}