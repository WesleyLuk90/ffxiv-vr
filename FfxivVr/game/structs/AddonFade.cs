using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct AddonFade
{
    [FieldOffset(0)] public AtkUnitBase* AtkUnitBase;
    [FieldOffset(0x280 + 3)] public byte Alpha;

    public static float GetAlpha(AddonFade* addonFade)
    {
        var baseAddon = (AtkUnitBase*)addonFade;
        if (!baseAddon->IsVisible)
        {
            return 0;
        }
        return (float)addonFade->Alpha / 255;
    }
}