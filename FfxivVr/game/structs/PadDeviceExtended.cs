using FFXIVClientStructs.FFXIV.Client.System.Input;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct PadDeviceExtended
{
    public static PadDeviceExtended* FromPadDevice(PadDevice* gamepadInput) => (PadDeviceExtended*)gamepadInput;

    [FieldOffset(0x48)] public ulong Value1;
    [FieldOffset(0x50)] public ulong Value2;

    public bool IsActive => Value1 != Value2;
}