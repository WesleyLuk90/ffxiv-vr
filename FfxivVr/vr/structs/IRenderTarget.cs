using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using System;

namespace FfxivVR;

public unsafe interface IShaderResource : IDisposable
{
    ID3D11Texture2D* Texture { get; }
    ID3D11ShaderResourceView* ShaderResourceView { get; }
    Vector2D<uint> Size { get; }
}