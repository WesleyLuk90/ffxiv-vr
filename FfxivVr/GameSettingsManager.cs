using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace FfxivVR;
public unsafe class GameSettingsManager(
    Logger logger)
{
    private readonly Logger logger = logger;

    public void SetBooleanSetting(ConfigOption option, bool value)
    {
        var framework = Framework.Instance();
        var optionIndex = (int)option;
        framework->SystemConfig.UiControlGamepadConfig.ConfigEntry[optionIndex].SetValue(value ? 1 : 0);
    }
}
