using Silk.NET.Direct3D11;
using Silk.NET.Maths;

namespace FfxivVR;

public unsafe class DepthTarget : IShaderResource
{
    public DepthTarget(
        ID3D11Texture2D* texture,
        ID3D11DepthStencilView* depthStencilView,
        ID3D11ShaderResourceView* shaderResourceView,
        Vector2D<uint> size
    )
    {
        Texture = texture;
        DepthStencilView = depthStencilView;
        ShaderResourceView = shaderResourceView;
        Size = size;
    }

    public ID3D11Texture2D* Texture { get; }
    public ID3D11DepthStencilView* DepthStencilView { get; }
    public ID3D11ShaderResourceView* ShaderResourceView { get; }
    public Vector2D<uint> Size { get; }

    public Matrix4X4<float> Scale()
    {
        return Matrix4X4.CreateScale(1, (float)Size.Y / Size.X, 1);
    }
    public void Dispose()
    {
        Texture->Release();
        DepthStencilView->Release();
        ShaderResourceView->Release();
    }
}