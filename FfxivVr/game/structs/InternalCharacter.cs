using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct InternalCharacter
{
    public static InternalCharacter* FromCharacter(Character* character) => (InternalCharacter*)character;
    [FieldOffset(308)] public float FixHeadPosition;
}