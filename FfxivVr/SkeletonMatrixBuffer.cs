using Silk.NET.Maths;
using System;
using System.Runtime.InteropServices;

namespace FfxivVR;
public unsafe class SkeletonMatrixBuffer : IDisposable
{
    private IntPtr Buffer;
    private Matrix4X4<float>* matrix;
    SkeletonMatrixBuffer()
    {
        // https://github.com/ktisis-tools/Ktisis/blob/6befd90cd057636e1ca15c1ceef101dc7131e622/Ktisis/Interop/Alloc.cs
        Buffer = Marshal.AllocHGlobal(sizeof(Matrix4X4<float>) + 16);
        var aligned = (Buffer + 15) / 16 * 16;
        matrix = (Matrix4X4<float>*)aligned;
    }

    public void Dispose()
    {
        Marshal.FreeHGlobal(Buffer);
    }
}
