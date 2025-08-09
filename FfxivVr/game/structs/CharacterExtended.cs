using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct CharacterExtended
{
    public static CharacterExtended* FromCharacter(Character* character) => (CharacterExtended*)character;
    [FieldOffset((int)(0x1F7A25E1184 - 0x1f7a25e1050))] public float FixHeadPosition;
}