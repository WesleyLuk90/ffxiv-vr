using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
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
    [FieldOffset(0x38)] public short unk3;
    [FieldOffset(0x38)] public short unk4;
    [FieldOffset(0x38)] public short unk5;
    [FieldOffset(0x38)] public short unk6;
};