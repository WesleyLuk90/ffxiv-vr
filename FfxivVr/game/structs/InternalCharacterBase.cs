using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct InternalCharacterBase
{
    public static InternalCharacterBase* FromCharacterBase(CharacterBase* characterBase) => (InternalCharacterBase*)characterBase;
    [FieldOffset(0x2A4)] public float Height;
}