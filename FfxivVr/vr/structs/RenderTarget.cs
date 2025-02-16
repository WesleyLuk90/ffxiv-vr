using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using System;

namespace FfxivVR;

public unsafe partial class Resources
{
    public class RenderTarget : IDisposable
    {
        public RenderTarget(
            ID3D11Texture2D* texture,
            ID3D11RenderTargetView* renderTargetView,
            ID3D11ShaderResourceView* shaderResourceView,
            Vector2D<uint> size
        )
        {
            Texture = texture;
            RenderTargetView = renderTargetView;
            ShaderResourceView = shaderResourceView;
            Size = size;
        }

        public ID3D11Texture2D* Texture { get; }
        public ID3D11RenderTargetView* RenderTargetView { get; }
        public ID3D11ShaderResourceView* ShaderResourceView { get; }
        public Vector2D<uint> Size { get; }

        public float AspectRatio => (float)Size.Y / Size.X;

        public Matrix4X4<float> AspectRatioTransform()
        {
            return Matrix4X4.CreateScale(1, AspectRatio, 1);
        }
        public void Dispose()
        {
            Texture->Release();
            RenderTargetView->Release();
            ShaderResourceView->Release();
        }
    }
}