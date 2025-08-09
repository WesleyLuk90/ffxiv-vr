using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit, Size = 0x38)]
public unsafe struct SetRenderTargetCommand
{
    [FieldOffset(0x00)] public int SwitchType;
    [FieldOffset(0x04)] public int numRenderTargets;
    [FieldOffset(0x08)] public Texture* RenderTarget0;
    [FieldOffset(0x10)] public Texture* RenderTarget1;
    [FieldOffset(0x18)] public Texture* RenderTarget2;
    [FieldOffset(0x20)] public Texture* RenderTarget3;
    [FieldOffset(0x28)] public Texture* RenderTarget4;
    [FieldOffset(0x30)] public Texture* DepthBuffer;
};