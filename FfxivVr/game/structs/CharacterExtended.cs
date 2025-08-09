using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct CharacterExtended
{
    public static CharacterExtended* FromCharacter(Character* character) => (CharacterExtended*)character;
    [FieldOffset((int)(0x1C9A33DB174 - 0x1c9a33db050))] public float FixHeadPosition;
}