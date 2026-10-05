using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FfxivVR;

public unsafe class ResourceFactory(
    DxDevice device
)
{
    public DepthTarget CreateDepthTarget(Vector2D<uint> size)
    {
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
        device.Device->CreateTexture2D(ref textureDescription, null, ref texture).D3D11Check("CreateTexture2D");
        var depthStencilViewDesc = new DepthStencilViewDesc(
            format: Silk.NET.DXGI.Format.FormatD24UnormS8Uint,
            viewDimension: DsvDimension.Texture2D,
            texture2D: new Tex2DDsv(
                mipSlice: 0
            )
        );
        ID3D11DepthStencilView* depthStencilView = null;
        device.Device->CreateDepthStencilView((ID3D11Resource*)texture, ref depthStencilViewDesc, ref depthStencilView).D3D11Check("CreateDepthStencilView");
        var shaderResourceViewDescription = new ShaderResourceViewDesc(
            format: Silk.NET.DXGI.Format.FormatR24UnormX8Typeless,
            viewDimension: Silk.NET.Core.Native.D3DSrvDimension.D3DSrvDimensionTexture2D,
            texture2D: new Tex2DSrv(
                mostDetailedMip: 0,
                mipLevels: 1
            )
        );
        ID3D11ShaderResourceView* shaderResourceView = null;
        device.Device->CreateShaderResourceView((ID3D11Resource*)texture, ref shaderResourceViewDescription, ref shaderResourceView).D3D11Check("CreateShaderResourceView");

        return new DepthTarget(
            texture,
            depthStencilView,
            shaderResourceView,
            size
        );
    }

    public RenderTarget CreateRenderTarget(Vector2D<uint> size, Silk.NET.DXGI.Format format = Silk.NET.DXGI.Format.FormatB8G8R8A8Unorm)
    {
        var texture = CreateMirrorTexture(size, format);
        var renderTargetViewDescription = new RenderTargetViewDesc(
            format: format,
            viewDimension: RtvDimension.Texture2D,
            texture2D: new Tex2DRtv(
                mipSlice: 0
            )
        );
        ID3D11RenderTargetView* renderTargetView = null;
        device.Device->CreateRenderTargetView((ID3D11Resource*)texture, ref renderTargetViewDescription, ref renderTargetView).D3D11Check("CreateRenderTargetView");
        var shaderResourceViewDescription = new ShaderResourceViewDesc(
            format: format,
            viewDimension: Silk.NET.Core.Native.D3DSrvDimension.D3DSrvDimensionTexture2D,
            texture2D: new Tex2DSrv(
                mostDetailedMip: 0,
                mipLevels: 1
            )
        );
        ID3D11ShaderResourceView* shaderResourceView = null;
        device.Device->CreateShaderResourceView((ID3D11Resource*)texture, ref shaderResourceViewDescription, ref shaderResourceView).D3D11Check("CreateShaderResourceView");
        return new RenderTarget(
            texture,
            renderTargetView,
            shaderResourceView,
            size
        );
    }

    // Tries to allocate the mirror texture with RenderTarget|ShaderResource|UnorderedAccess so the
    // same texture can later serve as a compute UAV target too, without a second, separate
    // allocation - falls back to RenderTarget|ShaderResource alone for formats/hardware that
    // reject the combined bind flags (e.g. some UAV-incapable typed formats), matching
    // CreateRenderTarget's previous, UAV-less behavior for those.
    private ID3D11Texture2D* CreateMirrorTexture(Vector2D<uint> size, Silk.NET.DXGI.Format format)
    {
        var withUav = BuildTexture2DDesc(size, format, (uint)(BindFlag.ShaderResource | BindFlag.RenderTarget | BindFlag.UnorderedAccess));
        ID3D11Texture2D* texture = null;
        if (device.Device->CreateTexture2D(ref withUav, null, ref texture) == 0)
        {
            return texture;
        }
        var rtvOnly = BuildTexture2DDesc(size, format, (uint)(BindFlag.ShaderResource | BindFlag.RenderTarget));
        device.Device->CreateTexture2D(ref rtvOnly, null, ref texture).D3D11Check("CreateTexture2D");
        return texture;
    }

    private static Texture2DDesc BuildTexture2DDesc(Vector2D<uint> size, Silk.NET.DXGI.Format format, uint bindFlags)
    {
        return new Texture2DDesc(
            format: format,
            width: size.X,
            height: size.Y,
            mipLevels: 1,
            sampleDesc: new Silk.NET.DXGI.SampleDesc(count: 1, quality: 0),
            usage: Usage.Default,
            cPUAccessFlags: 0,
            arraySize: 1,
            bindFlags: bindFlags,
            miscFlags: (uint)ResourceMiscFlag.Shared
        );
    }

    public ID3D11SamplerState* CreateSampler()
    {
        var samplerDesc = new SamplerDesc(
            filter: Filter.MinMagMipLinear,
            addressU: TextureAddressMode.Wrap,
            addressV: TextureAddressMode.Wrap,
            addressW: TextureAddressMode.Wrap,
            comparisonFunc: ComparisonFunc.Never,
            minLOD: 0,
            maxLOD: float.MaxValue
        );

        ID3D11SamplerState* samplerState = null;
        device.Device->CreateSamplerState(ref samplerDesc, ref samplerState).D3D11Check("CreateSamplerState");
        return samplerState;
    }

    internal D3DBuffer CreateCameraBuffer()
    {
        return CreateBuffer(new Span<byte>(new byte[sizeof(CameraConstants)]), BindFlag.ConstantBuffer);
    }

    internal D3DBuffer CreatePixelShaderConstantsBuffer()
    {
        return CreateBuffer(new Span<byte>(new byte[sizeof(PixelShaderConstants)]), BindFlag.ConstantBuffer);
    }

    internal D3DBuffer CreateConstantBuffer<T>() where T : unmanaged
    {
        return CreateBuffer(new Span<byte>(new byte[sizeof(T)]), BindFlag.ConstantBuffer);
    }
    internal VertexBuffer CreateSquareBuffer()
    {
        return CreateVertexBuffer(GeometryFactory.Plane());
    }

    internal VertexBuffer CreateCylinderBuffer()
    {
        return CreateVertexBuffer(GeometryFactory.Cylinder(sides: 16));
    }

    internal VertexBuffer CreateUIBuffer()
    {
        return CreateVertexBuffer(GeometryFactory.SegmentedPlane(segments: 32));
    }

    public ID3D11DepthStencilState* CreateDepthStencilStateOn()
    {
        var depthStencilOn = new DepthStencilDesc(
            depthEnable: true,
            depthWriteMask: DepthWriteMask.Zero,
            depthFunc: ComparisonFunc.GreaterEqual,
            stencilEnable: false,
            stencilReadMask: 0xff,
            stencilWriteMask: 0xff,
            frontFace: new DepthStencilopDesc(
                stencilFailOp: StencilOp.Keep,
                stencilDepthFailOp: StencilOp.Incr,
                stencilPassOp: StencilOp.Keep,
                stencilFunc: ComparisonFunc.Always
                ),
            backFace: new DepthStencilopDesc(
                stencilFailOp: StencilOp.Keep,
                stencilDepthFailOp: StencilOp.Decr,
                stencilPassOp: StencilOp.Keep,
                stencilFunc: ComparisonFunc.Always
                )
            );
        ID3D11DepthStencilState* depthStencilStateOn = null;
        device.Device->CreateDepthStencilState(ref depthStencilOn, ref depthStencilStateOn).D3D11Check("CreateDepthStencilState");
        return depthStencilStateOn;
    }

    public ID3D11DepthStencilState* CreateDepthStencilStateAlwaysPass()
    {
        var depthStencilAlwaysPass = new DepthStencilDesc(
            depthEnable: true,
            depthWriteMask: DepthWriteMask.All,
            depthFunc: ComparisonFunc.Always,
            stencilEnable: false,
            stencilReadMask: 0xff,
            stencilWriteMask: 0xff,
            frontFace: new DepthStencilopDesc(
                stencilFailOp: StencilOp.Keep,
                stencilDepthFailOp: StencilOp.Keep,
                stencilPassOp: StencilOp.Keep,
                stencilFunc: ComparisonFunc.Always
                ),
            backFace: new DepthStencilopDesc(
                stencilFailOp: StencilOp.Keep,
                stencilDepthFailOp: StencilOp.Keep,
                stencilPassOp: StencilOp.Keep,
                stencilFunc: ComparisonFunc.Always
                )
            );
        ID3D11DepthStencilState* depthStencilStateAlwaysPass = null;
        device.Device->CreateDepthStencilState(ref depthStencilAlwaysPass, ref depthStencilStateAlwaysPass).D3D11Check("CreateDepthStencilState");
        return depthStencilStateAlwaysPass;
    }

    public ID3D11DepthStencilState* CreateDepthStencilStateOff()
    {
        var depthStencilOff = new DepthStencilDesc(
            depthEnable: false,
            depthWriteMask: DepthWriteMask.All,
            depthFunc: ComparisonFunc.LessEqual,
            stencilEnable: true,
            stencilReadMask: 0xff,
            stencilWriteMask: 0xff,
            frontFace: new DepthStencilopDesc(
                stencilFailOp: StencilOp.Keep,
                stencilDepthFailOp: StencilOp.Keep,
                stencilPassOp: StencilOp.Keep,
                stencilFunc: ComparisonFunc.Always
                ),
            backFace: new DepthStencilopDesc(
                stencilFailOp: StencilOp.Keep,
                stencilDepthFailOp: StencilOp.Keep,
                stencilPassOp: StencilOp.Keep,
                stencilFunc: ComparisonFunc.Always
                )
            );
        ID3D11DepthStencilState* depthStencilStateOff = null;
        device.Device->CreateDepthStencilState(ref depthStencilOff, ref depthStencilStateOff).D3D11Check("CreateDepthStencilState");
        return depthStencilStateOff;
    }

    public ID3D11RasterizerState* CreateRasterizerState()
    {
        var rasterizerDesc = new RasterizerDesc(
            fillMode: FillMode.Solid,
            cullMode: CullMode.None,
            frontCounterClockwise: true,
            depthBias: 0,
            depthBiasClamp: 100,
            slopeScaledDepthBias: 0,
            depthClipEnable: false,
            scissorEnable: false,
            multisampleEnable: false,
            antialiasedLineEnable: false
        );
        ID3D11RasterizerState* rasterizerState = null;
        device.Device->CreateRasterizerState(ref rasterizerDesc, ref rasterizerState).D3D11Check("CreateRasterizerState");
        return rasterizerState;
    }

    private ID3D11BlendState* CreateBlendState(RenderTargetBlendDesc renderTargetBlendDesc)
    {
        var description = new BlendDesc(
            alphaToCoverageEnable: false,
            independentBlendEnable: false
        );
        description.RenderTarget[0] = renderTargetBlendDesc;
        ID3D11BlendState* state = null;
        device.Device->CreateBlendState(ref description, &state).D3D11Check("CreateBlendState");
        return state;
    }

    public ID3D11BlendState* CreateUIBlendState()
    {
        return CreateBlendState(new RenderTargetBlendDesc(
            blendEnable: true,
            srcBlend: Blend.One,
            destBlend: Blend.InvSrcAlpha,
            blendOp: BlendOp.Add,
            srcBlendAlpha: Blend.One,
            destBlendAlpha: Blend.One,
            blendOpAlpha: BlendOp.Max,
            renderTargetWriteMask: (byte)ColorWriteEnable.All
        ));
    }

    public ID3D11BlendState* CreateSceneBlendState()
    {
        return CreateBlendState(new RenderTargetBlendDesc(
            blendEnable: true,
            srcBlend: Blend.One,
            destBlend: Blend.One,
            blendOp: BlendOp.Add,
            srcBlendAlpha: Blend.One,
            destBlendAlpha: Blend.One,
            blendOpAlpha: BlendOp.Add,
            renderTargetWriteMask: (byte)ColorWriteEnable.All
        ));
    }

    public ID3D11BlendState* CreateCompositingBlendState()
    {
        return CreateBlendState(new RenderTargetBlendDesc(
            blendEnable: true,
            srcBlend: Blend.One,
            destBlend: Blend.InvSrcAlpha,
            blendOp: BlendOp.Add,
            srcBlendAlpha: Blend.One,
            destBlendAlpha: Blend.One,
            blendOpAlpha: BlendOp.Add,
            renderTargetWriteMask: (byte)ColorWriteEnable.All
        ));
    }

    public ID3D11BlendState* CreateStandardBlendState()
    {
        return CreateBlendState(new RenderTargetBlendDesc(
            blendEnable: true,
            srcBlend: Blend.SrcAlpha,
            destBlend: Blend.One,
            blendOp: BlendOp.Add,
            srcBlendAlpha: Blend.One,
            destBlendAlpha: Blend.One,
            blendOpAlpha: BlendOp.Add,
            renderTargetWriteMask: (byte)ColorWriteEnable.All
        ));
    }

    private D3DBuffer CreateBuffer(Span<byte> bytes, BindFlag bindFlag)
    {
        fixed (byte* p = bytes)
        {
            if (p == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            SubresourceData subresourceData = new SubresourceData(
                pSysMem: p,
                sysMemPitch: 0,
                sysMemSlicePitch: 0
            );
            BufferDesc description = new BufferDesc(
                byteWidth: (uint)bytes.Length,
                usage: Usage.Dynamic,
                bindFlags: (uint)bindFlag,
                cPUAccessFlags: (uint)CpuAccessFlag.Write,
                miscFlags: 0,
                structureByteStride: 0
            );
            ID3D11Buffer* buffer = null;
            device.Device->CreateBuffer(ref description, ref subresourceData, ref buffer).D3D11Check("CreateBuffer");
            return new D3DBuffer(buffer, (uint)bytes.Length);
        }
    }

    private VertexBuffer CreateVertexBuffer(List<Vertex> vertices)
    {
        var array = vertices.ToArray();
        var buffer = CreateBuffer(MemoryMarshal.AsBytes(new Span<Vertex>(array)), BindFlag.VertexBuffer);
        return new VertexBuffer(array, buffer);
    }
}