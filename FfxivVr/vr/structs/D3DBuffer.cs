using Silk.NET.Direct3D11;
using System;

namespace FfxivVR;

unsafe class D3DBuffer(ID3D11Buffer* buffer, uint length) : IDisposable
{
    public ID3D11Buffer* Handle = buffer;
    public uint Length = length;

    public void Dispose()
    {
        Handle->Release();
    }
}