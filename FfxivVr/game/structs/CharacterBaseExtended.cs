using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct CharacterBaseExtended
{
    public static CharacterBaseExtended* FromCharacterBase(CharacterBase* characterBase) => (CharacterBaseExtended*)characterBase;
    [FieldOffset(0x2A4)] public float Height;
}