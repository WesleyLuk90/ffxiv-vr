using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using System;

namespace FfxivVR;

public unsafe class SinglePassRenderer(
        Logger logger,
        RunOnce runOnce
        ) : IDisposable
{

    public DepthTarget? Depth { get; private set; }
    public RenderTarget? Texture { get; private set; }


    public void Initialize()
    {
        Depth = CreateDepthTarget(new Vector2D<uint>(1000, 1000));
        Texture = CreateRenderTarget(new Vector2D<uint>(1000, 1000));
    }

    public void Dispose()
    {

        Depth?.Dispose();
        Texture?.Dispose();
    }
    private DepthTarget CreateDepthTarget(Vector2D<uint> size)
    {
        ID3D11Device* device = (ID3D11Device*)Device.Instance()->D3D11Forwarder;
        var textureDescription = new Texture2DDesc(
            format: Silk.NET.DXGI.Format.FormatR24G8Typeless,
            width: size.X,
            height: size.Y,
            mipLevels: 1,
            sampleDesc: new Silk.NET.DXGI.SampleDesc(count: 1, quality: 0),
            usage: Usage.Default,
            cPUAccessFlags: 0,
            arraySize: 1,
            bindFlags: (uint)(BindFlag.DepthStencil | BindFlag.ShaderResource),
            miscFlags: (uint)ResourceMiscFlag.Shared
        );
        ID3D11Texture2D* texture = null;
        device->CreateTexture2D(ref textureDescription, null, ref texture).D3D11Check("CreateTexture2D");
        var depthStencilViewDesc = new DepthStencilViewDesc(
            format: Silk.NET.DXGI.Format.FormatD24UnormS8Uint,
            viewDimension: DsvDimension.Texture2D,
            texture2D: new Tex2DDsv(
                mipSlice: 0
            )
        );
        ID3D11DepthStencilView* depthStencilView = null;
        device->CreateDepthStencilView((ID3D11Resource*)texture, ref depthStencilViewDesc, ref depthStencilView).D3D11Check("CreateDepthStencilView");

        return new DepthTarget(
            texture,
            depthStencilView,
            size
        );
    }

    private RenderTarget CreateRenderTarget(Vector2D<uint> size)
    {
        ID3D11Device* device = (ID3D11Device*)Device.Instance()->D3D11Forwarder;
        var format = Silk.NET.DXGI.Format.FormatB8G8R8A8Unorm;
        var textureDescription = new Texture2DDesc(
            format: format,
            width: size.X,
            height: size.Y,
            mipLevels: 1,
            sampleDesc: new Silk.NET.DXGI.SampleDesc(count: 1, quality: 0),
            usage: Usage.Default,
            cPUAccessFlags: 0,
            arraySize: 1,
            bindFlags: (uint)(BindFlag.ShaderResource | BindFlag.RenderTarget),
            miscFlags: (uint)ResourceMiscFlag.Shared
        );
        ID3D11Texture2D* texture = null;
        device->CreateTexture2D(ref textureDescription, null, ref texture).D3D11Check("CreateTexture2D");
        var renderTargetViewDescription = new RenderTargetViewDesc(
            format: format,
            viewDimension: RtvDimension.Texture2D,
            texture2D: new Tex2DRtv(
                mipSlice: 0
            )
        );
        ID3D11RenderTargetView* renderTargetView = null;
        device->CreateRenderTargetView((ID3D11Resource*)texture, ref renderTargetViewDescription, ref renderTargetView).D3D11Check("CreateRenderTargetView");
        var shaderResourceViewDescription = new ShaderResourceViewDesc(
            format: format,
            viewDimension: Silk.NET.Core.Native.D3DSrvDimension.D3DSrvDimensionTexture2D,
            texture2D: new Tex2DSrv(
                mostDetailedMip: 0,
                mipLevels: 1
            )
        );
        ID3D11ShaderResourceView* shaderResourceView = null;
        device->CreateShaderResourceView((ID3D11Resource*)texture, ref shaderResourceViewDescription, ref shaderResourceView).D3D11Check("CreateShaderResourceView");
        return new RenderTarget(
            texture,
            renderTargetView,
            shaderResourceView,
            size
        );
    }

    internal void ClearDepthStencilView(OriginalFunctions originalFunctions, ID3D11DeviceContext* context, ID3D11DepthStencilView* pDepthStencilView, uint clearFlags, float depth, byte stencil)
    {
    }

    internal void ClearRenderTargetViewDetour(OriginalFunctions originalFunctions, ID3D11DeviceContext* context, ID3D11RenderTargetView* pRenderTargetView, float* colorRGBA)
    {
    }

    internal void DrawDetour(OriginalFunctions originalFunctions, ID3D11DeviceContext* context, uint vertexCount, uint startVertexLocation)
    {
        // originalFunctions.OMSetRenderTargets(context, 1, &texture, lastDSV != null ? Depth?.DepthStencilView : null);
        // originalFunctions.Draw(context, vertexCount, startVertexLocation);
    }

    internal void DrawIndexedDetour(OriginalFunctions originalFunctions, ID3D11DeviceContext* context, uint indexCount, uint startIndexLocation, int baseVertexLocation)
    {
        // var texture = Texture?.RenderTargetView;
        // OMSetRenderTargetsHook?.Original(context, 1, &texture, lastDSV != null ? Depth?.DepthStencilView : null);
        // DrawIndexedHook?.Original(context, indexCount, startIndexLocation, baseVertexLocation);
    }
}