using System.Runtime.InteropServices;

namespace FfxivVR;

enum LetterBoxingOption
{
    EnableLetterboxing = 1 << 5,
}

[StructLayout(LayoutKind.Explicit)]
struct InternalLetterboxing
{
    [FieldOffset(0x40)] public LetterBoxingOption ShouldLetterBox;
}