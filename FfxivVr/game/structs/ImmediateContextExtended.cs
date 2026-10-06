using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct ImmediateContextExtended
{
    public static ImmediateContextExtended* FromImmediateContext(ImmediateContext* context) => (ImmediateContextExtended*)context;

    [FieldOffset(0xD8)] public ConstantBufferBindings VertexShaderConstantBuffers;
    [FieldOffset(0x1C0)] public ConstantBufferBindings PixelShaderConstantBuffers;
    [FieldOffset(0x738)] public ConstantBufferBindings GeometryShaderConstantBuffers;
    [FieldOffset(0xB28)] public ConstantBufferBindings HullShaderConstantBuffers;
    [FieldOffset(0xF18)] public ConstantBufferBindings DomainShaderConstantBuffers;
    [FieldOffset(0x1308)] public ConstantBufferBindings ComputeShaderConstantBuffers;
    [FieldOffset(0x17C0)] public ConstantBufferCache* ConstantBufferCache;

    public void InvalidateConstantBuffers()
    {
        InvalidateConstantBufferBindings();
        if (ConstantBufferCache != null)
        {
            ConstantBufferCache->Reset();
        }
    }

    private void InvalidateConstantBufferBindings()
    {
        VertexShaderConstantBuffers.Invalidate();
        PixelShaderConstantBuffers.Invalidate();
        GeometryShaderConstantBuffers.Invalidate();
        HullShaderConstantBuffers.Invalidate();
        DomainShaderConstantBuffers.Invalidate();
        ComputeShaderConstantBuffers.Invalidate();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 0x10)]
public struct ConstantBufferBinding
{
    public const nint InvalidSource = 1;

    [FieldOffset(0x0)] public nint Source;
    [FieldOffset(0x8)] public uint VectorCount;
}

[InlineArray(14)]
public struct ConstantBufferBindings
{
    private ConstantBufferBinding element;

    public void Invalidate()
    {
        foreach (ref var binding in this)
        {
            binding.Source = ConstantBufferBinding.InvalidSource;
            binding.VectorCount = 0;
        }
    }
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct ConstantBufferCache
{
    private const string ResetSignature = "48 8D 41 40 41 B8 0C 00 00 00 45 33 C9";
    private static delegate* unmanaged<ConstantBufferCache*, void> resetFunction;

    public static void Initialize(ISigScanner sigScanner)
    {
        resetFunction = (delegate* unmanaged<ConstantBufferCache*, void>)sigScanner.ScanText(ResetSignature);
    }

    public void Reset()
    {
        fixed (ConstantBufferCache* self = &this)
        {
            resetFunction(self);
        }
    }
}