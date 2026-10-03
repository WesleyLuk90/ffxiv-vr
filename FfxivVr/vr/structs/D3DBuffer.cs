using Silk.NET.Direct3D11;
using System;

namespace FfxivVR;

unsafe class D3DBuffer(ID3D11Buffer* buffer, uint length) : IDisposable
{
    public ID3D11Buffer* Handle = buffer;
    public uint Length = length;

    public void SetData(ID3D11DeviceContext* context, Span<byte> bytes)
    {
        if (bytes.Length != Length)
        {
            throw new Exception($"Invalid buffer data {bytes.Length}, expected {Length}");
        }
        MappedSubresource mappedSubresource = new MappedSubresource();
        context->Map((ID3D11Resource*)Handle, 0, Map.WriteDiscard, 0, ref mappedSubresource).D3D11Check("Map");
        fixed (byte* p = bytes)
        {
            Buffer.MemoryCopy(source: p, destination: mappedSubresource.PData, Length, (uint)bytes.Length);
        }
        context->Unmap((ID3D11Resource*)Handle, 0);
    }

    public void Dispose()
    {
        Handle->Release();
    }
}