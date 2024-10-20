﻿using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Runtime.InteropServices;

namespace FfxivVR;

unsafe public class Resources : IDisposable
{
    public struct CameraConstants
    {
        Matrix4X4<float> modelViewProjection;

        public CameraConstants(Matrix4X4<float> modelViewProjection)
        {
            this.modelViewProjection = modelViewProjection;
        }
    }

    private D3DBuffer? cameraBuffer;
    private Vertex[]? vertices;
    private D3DBuffer? vertexBuffer;
    private readonly ID3D11Device* device;
    private readonly Logger logger;
    private ID3D11DepthStencilState* depthStencilStateOn = null;
    private ID3D11DepthStencilState* depthStencilStateOff = null;
    private ID3D11BlendState* blendState = null;
    private ID3D11RasterizerState* rasterizerState = null;
    private ID3D11SamplerState* samplerState = null;

    public Resources(ID3D11Device* device, Logger logger)
    {
        this.device = device;
        this.logger = logger;
    }

    public struct Vertex
    {
        Vector3f position;
        Vector2f uv;
        public Vertex(Vector3f position, Vector2f uv)
        {
            this.position = position;
            this.uv = uv;
        }
    }

    public void Initialize()
    {
        CreateBuffers();
        CreateSampler();
        CreateStencilState();
        CreateBlendState();
        CreateRasterizerState();
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

        device->CreateSamplerState(ref samplerDesc, ref samplerState).D3D11Check("CreateSamplerState"); ;
    }

    private void CreateBuffers()
    {
        this.cameraBuffer = CreateBuffer(new Span<byte>(new byte[sizeof(CameraConstants)]), BindFlag.ConstantBuffer);
        var tr = new Vertex(new Vector3f(1, 1, 0), new Vector2f(1, 0));
        var tl = new Vertex(new Vector3f(-1, 1, 0), new Vector2f(0, 0));
        var bl = new Vertex(new Vector3f(-1, -1, 0), new Vector2f(0, 1));
        var br = new Vertex(new Vector3f(1, -1, 0), new Vector2f(1, 1));
        this.vertices = [
            tr,
            tl,
            bl,
            tr,
            bl,
            br,
        ];
        this.vertexBuffer = CreateBuffer(MemoryMarshal.AsBytes(new Span<Vertex>(this.vertices)), BindFlag.VertexBuffer);
    }

    private void CreateStencilState()
    {
        var depthStencilOn = new DepthStencilDesc(
            depthEnable: true,
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
        fixed (ID3D11DepthStencilState** ptr = &depthStencilStateOn)
        {
            device->CreateDepthStencilState(ref depthStencilOn, ptr).D3D11Check("CreateDepthStencilState");
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
            device->CreateDepthStencilState(ref depthStencilOff, ptr).D3D11Check("CreateDepthStencilState");
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
            device->CreateRasterizerState(ref rasterizerDesc, ptr).D3D11Check("CreateRasterizerState");
        }
    }

    private void CreateBlendState()
    {
        var blendStateDesc = new BlendDesc(
            alphaToCoverageEnable: false,
            independentBlendEnable: false
        );
        blendStateDesc.RenderTarget[0].BlendEnable = true;
        blendStateDesc.RenderTarget[0].SrcBlend = Blend.SrcColor;
        blendStateDesc.RenderTarget[0].DestBlend = Blend.InvSrcAlpha;
        blendStateDesc.RenderTarget[0].BlendOp = BlendOp.Add;
        blendStateDesc.RenderTarget[0].SrcBlendAlpha = Blend.One;
        blendStateDesc.RenderTarget[0].DestBlendAlpha = Blend.Zero;
        blendStateDesc.RenderTarget[0].BlendOpAlpha = BlendOp.Add;
        blendStateDesc.RenderTarget[0].RenderTargetWriteMask = (byte)ColorWriteEnable.All;
        fixed (ID3D11BlendState** ptr = &blendState)
        {
            device->CreateBlendState(ref blendStateDesc, ptr).D3D11Check("CreateBlendState");
        }
    }

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
            device->CreateBuffer(ref description, ref subresourceData, ref buffer).D3D11Check("CreateBuffer");
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
        SetBufferData(context, MemoryMarshal.AsBytes(cameraSpan), this.cameraBuffer);

        context->VSSetConstantBuffers(0, 1, ref cameraBuffer.Handle);

        context->RSSetState(rasterizerState);
    }

    public void SetSampler(ID3D11DeviceContext* context, Texture* texture)
    {
        var resource = (ID3D11ShaderResourceView*)texture->D3D11ShaderResourceView;
        ID3D11ShaderResourceView** ptr = &resource;
        context->PSSetShaderResources(0, 1, ptr);
        context->PSSetSamplers(0, 1, ref samplerState);
    }

    public void Draw(ID3D11DeviceContext* context)
    {
        fixed (ID3D11Buffer** pHandle = &vertexBuffer.Handle)
        {
            uint stride = (uint)sizeof(Vertex);
            uint offsets = 0;
            context->IASetVertexBuffers(0, 1, pHandle, &stride, &offsets);
            context->IASetPrimitiveTopology(Silk.NET.Core.Native.D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);
            context->Draw((uint)this.vertices.Length, 0);
        }
    }

    public void Dispose()
    {
        this.cameraBuffer?.Dispose();
        this.vertexBuffer?.Dispose();
    }

    internal void SetDepthStencilState(ID3D11DeviceContext* context)
    {
        context->OMSetDepthStencilState(depthStencilStateOff, 0);
    }

    internal void SetBlendState(ID3D11DeviceContext* context)
    {
        fixed (float* ptr = new Span<float>([0, 0, 0, 0]))
        {
            context->OMSetBlendState(blendState, ptr, 1);
        }
    }
}