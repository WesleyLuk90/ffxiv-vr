using Silk.NET.Maths;
using System.Runtime.InteropServices;

namespace FfxivVR;

public unsafe partial class Resources
{
    [StructLayout(LayoutKind.Explicit)]
    public struct PixelShaderConstants(ShaderMode mode, float gamma, Vector4D<float> color, Vector2D<float>? uvOffset = null, Vector2D<float>? uvScale = null)
    {
        [FieldOffset(0)] int mode = (int)mode;
        [FieldOffset(4)] float gamma = gamma;
        [FieldOffset(8)] readonly float padding1 = 0;
        [FieldOffset(12)] readonly float padding2 = 0;
        [FieldOffset(16)] Vector4D<float> color = color;
        [FieldOffset(32)] Vector2D<float> uvScale = uvScale ?? Vector2D<float>.One;
        [FieldOffset(40)] Vector2D<float> uvOffset = uvOffset ?? Vector2D<float>.Zero;

    }
}