using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct InternalCharacter
{
    public static InternalCharacter* FromCharacter(Character* character) => (InternalCharacter*)character;
    [FieldOffset((int)(0x1C9A33DB174 - 0x1c9a33db050))] public float FixHeadPosition;
}