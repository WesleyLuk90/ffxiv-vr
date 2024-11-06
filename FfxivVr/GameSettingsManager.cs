using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace FfxivVR;
public unsafe class GameSettingsManager(
    Logger logger)
{
    private readonly Logger logger = logger;

    public void SetUIBooleanSetting(ConfigOption option, bool value)
    {
        var framework = Framework.Instance();
        var optionIndex = (int)option;
        framework->SystemConfig.UiControlGamepadConfig.ConfigEntry[optionIndex].SetValue(value ? 1 : 0);
    }

    public uint GetIntSystemSetting(ConfigOption option)
    {
        var framework = Framework.Instance();
        var optionIndex = (int)option;
        return framework->SystemConfig.ConfigEntry[optionIndex].Value.UInt;
    }
    public void SetIntSystemSetting(ConfigOption option, uint value)
    {
        var framework = Framework.Instance();
        var optionIndex = (int)option;
        framework->SystemConfig.ConfigEntry[optionIndex].SetValue(value);
    }
}
