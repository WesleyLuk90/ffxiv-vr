using Silk.NET.Direct3D11;
using System;

namespace FfxivVR;

public unsafe partial class Resources
{
    class D3DBuffer : IDisposable
    {
        public ID3D11Buffer* Handle;
        public uint Length;

        internal D3DBuffer(ID3D11Buffer* buffer, uint length)
        {
            this.Handle = buffer;
            this.Length = length;
        }

        public void Dispose()
        {
            Handle->Release();
        }
    }
}