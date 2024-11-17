using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct ClearCommand
{
    [FieldOffset(0x00)] public int SwitchType;
    [FieldOffset(0x04)] public int clearType;
    [FieldOffset(0x08)] public float colorB;
    [FieldOffset(0x0C)] public float colorG;
    [FieldOffset(0x10)] public float colorR;
    [FieldOffset(0x14)] public float colorA;
    [FieldOffset(0x18)] public float clearDepth;
    [FieldOffset(0x1C)] public int clearStencil;
    [FieldOffset(0x20)] public int clearCheck;
    [FieldOffset(0x24)] public float Top;
    [FieldOffset(0x28)] public float Left;
    [FieldOffset(0x2C)] public float Width;
    [FieldOffset(0x30)] public float Height;
    [FieldOffset(0x34)] public float MinZ;
    [FieldOffset(0x38)] public float MaxZ;
};