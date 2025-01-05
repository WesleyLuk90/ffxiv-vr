using Dalamud.Game.ClientState.GamePad;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct InternalGamepadInput
{
    public static InternalGamepadInput* FromGamepadInput(GamepadInput* gamepadInput) => (InternalGamepadInput*)gamepadInput;

    [FieldOffset(0x48)] public ulong Value1;
    [FieldOffset(0x50)] public ulong Value2;

    public bool IsActive => Value1 != Value2;
}