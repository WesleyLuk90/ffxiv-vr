using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace FfxivVR;

public unsafe partial class Resources(
    DxDevice device
) : IDisposable
{

    private D3DBuffer? cameraBuffer;
    private D3DBuffer? pixelShaderConstantsBuffer;
    private Vertex[]? squareVertices;
    private Vertex[]? cylinderVertices;
    private D3DBuffer? squareVertexBuffer;
    private D3DBuffer? cylinderVertexBuffer;
    private ID3D11DepthStencilState* depthStencilStateOn = null;
    private ID3D11DepthStencilState* depthStencilStateOff = null;
    private ID3D11BlendState* uiBlendState = null;
    private ID3D11BlendState* sceneBlendState = null;
    private ID3D11BlendState* compositingBlendState = null;
    private ID3D11BlendState* standardBlendState;
    private ID3D11RasterizerState* rasterizerState = null;
    private ID3D11SamplerState* samplerState = null;
    public RenderTarget UIRenderTarget = null!;
    public RenderTarget DalamudRenderTarget = null!;
    public RenderTarget CursorRenderTarget = null!;
    public RenderTarget[] SceneRenderTargets = [];
    public DepthTarget[] SceneDepthTargets = [];

    public void Initialize(Vector2D<uint> size)
    {
        CreateBuffers();
        CreateSampler();
        CreateStencilState();
        CreateBlendState();
        CreateRasterizerState();
        UIRenderTarget = CreateRenderTarget(size);
        DalamudRenderTarget = CreateRenderTarget(size);
        CursorRenderTarget = CreateRenderTarget(size);
        SceneRenderTargets = [CreateRenderTarget(size), CreateRenderTarget(size)];
        SceneDepthTargets = [CreateDepthTarget(size), CreateDepthTarget(size)];
    }

    private DepthTarget CreateDepthTarget(Vector2D<uint> size)
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

        return new DepthTarget(
            texture,
            depthStencilView,
            size
        );
    }

    private RenderTarget CreateRenderTarget(Vector2D<uint> size)
    {
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
        device.Device->CreateTexture2D(ref textureDescription, null, ref texture).D3D11Check("CreateTexture2D");
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

    private void CreateSampler()
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

        device.Device->CreateSamplerState(ref samplerDesc, ref samplerState).D3D11Check("CreateSamplerState"); ;
    }

    private void CreateBuffers()
    {
        cameraBuffer = CreateBuffer(new Span<byte>(new byte[sizeof(CameraConstants)]), BindFlag.ConstantBuffer);
        pixelShaderConstantsBuffer = CreateBuffer(new Span<byte>(new byte[sizeof(PixelShaderConstants)]), BindFlag.ConstantBuffer);
        var tr = new Vertex(new Vector3f(1, 1, 0), new Vector2f(1, 0));
        var tl = new Vertex(new Vector3f(-1, 1, 0), new Vector2f(0, 0));
        var bl = new Vertex(new Vector3f(-1, -1, 0), new Vector2f(0, 1));
        var br = new Vertex(new Vector3f(1, -1, 0), new Vector2f(1, 1));
        squareVertices = [
            tr,
            tl,
            bl,
            tr,
            bl,
            br,
        ];
        squareVertexBuffer = CreateBuffer(MemoryMarshal.AsBytes(new Span<Vertex>(squareVertices)), BindFlag.VertexBuffer);
        var sides = 16;
        var vertexCount = squareVertices.Count();
        var cylinderVertices = new Vertex[vertexCount * sides];
        for (int i = 0; i < sides; i++)
        {
            var transform = Matrix4X4.CreateScale(new Vector3D<float>(MathF.Tan(2 * MathF.PI / sides / 2), 1, 1)) * Matrix4X4.CreateTranslation(Vector3D<float>.UnitZ) * Matrix4X4.CreateRotationY<float>(2 * MathF.PI * i / sides);
            for (int j = 0; j < vertexCount; j++)
            {
                cylinderVertices[i * vertexCount + j].Position = Vector3D.Transform(squareVertices[j].Position.ToVector3D(), transform).ToVector3f();
                cylinderVertices[i * vertexCount + j].UV = squareVertices[j].UV;
            }
        }
        this.cylinderVertices = cylinderVertices;
        cylinderVertexBuffer = CreateBuffer(MemoryMarshal.AsBytes(new Span<Vertex>(cylinderVertices)), BindFlag.VertexBuffer);
    }

    private void CreateStencilState()
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
        fixed (ID3D11DepthStencilState** ptr = &depthStencilStateOn)
        {
            device.Device->CreateDepthStencilState(ref depthStencilOn, ptr).D3D11Check("CreateDepthStencilState");
        }
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
        fixed (ID3D11DepthStencilState** ptr = &depthStencilStateOff)
        {
            device.Device->CreateDepthStencilState(ref depthStencilOff, ptr).D3D11Check("CreateDepthStencilState");
        }
    }

    private void CreateRasterizerState()
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
        fixed (ID3D11RasterizerState** ptr = &rasterizerState)
        {
            device.Device->CreateRasterizerState(ref rasterizerDesc, ptr).D3D11Check("CreateRasterizerState");
        }
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
    private void CreateBlendState()
    {
        uiBlendState = CreateBlendState(new RenderTargetBlendDesc(
            blendEnable: true,
            srcBlend: Blend.One,
            destBlend: Blend.InvSrcAlpha,
            blendOp: BlendOp.Add,
            srcBlendAlpha: Blend.One,
            destBlendAlpha: Blend.One,
            blendOpAlpha: BlendOp.Max,
            renderTargetWriteMask: (byte)ColorWriteEnable.All
        ));
        sceneBlendState = CreateBlendState(new RenderTargetBlendDesc(
            blendEnable: true,
            srcBlend: Blend.One,
            destBlend: Blend.One,
            blendOp: BlendOp.Add,
            srcBlendAlpha: Blend.One,
            destBlendAlpha: Blend.One,
            blendOpAlpha: BlendOp.Add,
            renderTargetWriteMask: (byte)ColorWriteEnable.All
        ));
        compositingBlendState = CreateBlendState(new RenderTargetBlendDesc(
            blendEnable: true,
            srcBlend: Blend.One,
            destBlend: Blend.InvSrcAlpha,
            blendOp: BlendOp.Add,
            srcBlendAlpha: Blend.One,
            destBlendAlpha: Blend.One,
            blendOpAlpha: BlendOp.Add,
            renderTargetWriteMask: (byte)ColorWriteEnable.All
        ));
        standardBlendState = CreateBlendState(new RenderTargetBlendDesc(
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

    private void SetBufferData(ID3D11DeviceContext* context, Span<byte> bytes, D3DBuffer buffer)
    {
        if (bytes.Length != buffer.Length)
        {
            throw new Exception($"Invalid buffer data {bytes.Length}, expected {buffer.Length}");
        }
        MappedSubresource mappedSubresource = new MappedSubresource();
        context->Map((ID3D11Resource*)buffer.Handle, 0, Map.WriteDiscard, 0, ref mappedSubresource).D3D11Check("Map");
        fixed (byte* p = bytes)
        {
            Buffer.MemoryCopy(source: p, destination: mappedSubresource.PData, buffer.Length, bytes.Length);
        }
        context->Unmap((ID3D11Resource*)buffer.Handle, 0);
    }

    public void UpdateCamera(ID3D11DeviceContext* context, CameraConstants camera)
    {
        var cameraSpan = new Span<CameraConstants>(ref camera);
        SetBufferData(context, MemoryMarshal.AsBytes(cameraSpan), this.cameraBuffer!);

        context->VSSetConstantBuffers(0, 1, ref cameraBuffer!.Handle);

        context->RSSetState(rasterizerState);
    }

    public void SetPixelShaderConstants(ID3D11DeviceContext* context, PixelShaderConstants pixelShaderConstants)
    {
        var cameraSpan = new Span<PixelShaderConstants>(ref pixelShaderConstants);
        SetBufferData(context, MemoryMarshal.AsBytes(cameraSpan), this.pixelShaderConstantsBuffer!);

        context->PSSetConstantBuffers(0, 1, ref pixelShaderConstantsBuffer!.Handle);
    }

    public void SetSampler(ID3D11DeviceContext* context, ID3D11ShaderResourceView* shaderResourceView)
    {
        ID3D11ShaderResourceView** ptr = &shaderResourceView;
        context->PSSetShaderResources(0, 1, ptr);
        context->PSSetSamplers(0, 1, ref samplerState);
    }

    public void DrawSquare(ID3D11DeviceContext* context)
    {
        fixed (ID3D11Buffer** pHandle = &squareVertexBuffer!.Handle)
        {
            uint stride = (uint)sizeof(Vertex);
            uint offsets = 0;
            context->IASetVertexBuffers(0, 1, pHandle, &stride, &offsets);
            context->IASetPrimitiveTopology(Silk.NET.Core.Native.D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);
            context->Draw((uint)this.squareVertices!.Length, 0);
        }
    }

    public void DrawCylinder(ID3D11DeviceContext* context)
    {
        fixed (ID3D11Buffer** pHandle = &cylinderVertexBuffer!.Handle)
        {
            uint stride = (uint)sizeof(Vertex);
            uint offsets = 0;
            context->IASetVertexBuffers(0, 1, pHandle, &stride, &offsets);
            context->IASetPrimitiveTopology(Silk.NET.Core.Native.D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);
            context->Draw((uint)this.cylinderVertices!.Length, 0);
        }
    }

    public void Dispose()
    {
        cameraBuffer?.Dispose();
        squareVertexBuffer?.Dispose();
        cylinderVertexBuffer?.Dispose();
        UIRenderTarget?.Dispose();
        DalamudRenderTarget?.Dispose();
        CursorRenderTarget?.Dispose();
        foreach (var rt in SceneRenderTargets)
        {
            rt.Dispose();
        }
        if (sceneBlendState != null)
        {
            sceneBlendState->Release();
        }
        if (uiBlendState != null)
        {
            uiBlendState->Release();
        }
        if (compositingBlendState != null)
        {
            compositingBlendState->Release();
        }
        if (SceneRenderTargets != null)
        {
            foreach (var target in SceneRenderTargets)
            {
                target.Dispose();
            }
        }
    }

    internal void DisableDepthStencil(ID3D11DeviceContext* context)
    {
        context->OMSetDepthStencilState(depthStencilStateOff, 0);
    }

    internal void EnableDepthStencil(ID3D11DeviceContext* context)
    {
        context->OMSetDepthStencilState(depthStencilStateOn, 0);
    }

    internal void SetUIBlendState(ID3D11DeviceContext* context)
    {
        context->OMSetBlendState(uiBlendState, null, 0xffffffff);
    }
    internal void SetSceneBlendState(ID3D11DeviceContext* context)
    {
        context->OMSetBlendState(sceneBlendState, null, 0xffffffff);
    }
    internal void SetCompositingBlendState(ID3D11DeviceContext* context)
    {
        context->OMSetBlendState(compositingBlendState, null, 0xffffffff);
    }
    internal void SetStandardBlendState(ID3D11DeviceContext* context)
    {
        context->OMSetBlendState(standardBlendState, null, 0xffffffff);
    }
}