using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Silk.NET.Maths;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct AddonFade
{
    [FieldOffset(0)] public AtkUnitBase* AtkUnitBase;
    [FieldOffset(0x328)] public ByteColor Color;

    public static Vector4D<float> GetPremultipliedColor(AddonFade* addonFade)
    {
        var baseAddon = (AtkUnitBase*)addonFade;
        if (!baseAddon->IsVisible)
        {
            return Vector4D<float>.Zero;
        }
        var color = addonFade->Color;
        var alpha = (float)color.A / 255;
        return new Vector4D<float>(color.R / 255f * alpha, color.G / 255f * alpha, color.B / 255f * alpha, alpha);
    }
}