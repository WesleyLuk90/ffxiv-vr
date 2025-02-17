
using Silk.NET.Direct3D11;
using System;

namespace FfxivVR;

unsafe class VertexBuffer(Vertex[] vertices, D3DBuffer buffer) : IDisposable
{
    public Vertex[] Vertices { get; } = vertices;
    public D3DBuffer Buffer { get; } = buffer;

    public ID3D11Buffer* Handle = buffer.Handle;
    public uint VertexCount = (uint)vertices.Length;

    public void Dispose()
    {
        Buffer.Dispose();
    }
}