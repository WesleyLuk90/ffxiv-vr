using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using System;
using System.Runtime.InteropServices;

namespace FfxivVR;

public unsafe class Resources(
    ResourceFactory resourceFactory
) : IDisposable
{

    private D3DBuffer? cameraBuffer;
    private D3DBuffer? pixelShaderConstantsBuffer;
    private VertexBuffer? squareBuffer;
    private VertexBuffer? cylinderBuffer;
    private VertexBuffer? uiBuffer;
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
        cameraBuffer = resourceFactory.CreateCameraBuffer();
        pixelShaderConstantsBuffer = resourceFactory.CreatePixelShaderConstantsBuffer();
        squareBuffer = resourceFactory.CreateSquareBuffer();
        cylinderBuffer = resourceFactory.CreateCylinderBuffer();
        uiBuffer = resourceFactory.CreateUIBuffer();

        samplerState = resourceFactory.CreateSampler();

        depthStencilStateOn = resourceFactory.CreateDepthStencilStateOn();
        depthStencilStateOff = resourceFactory.CreateDepthStencilStateOff();

        uiBlendState = resourceFactory.CreateUIBlendState();
        sceneBlendState = resourceFactory.CreateSceneBlendState();
        compositingBlendState = resourceFactory.CreateCompositingBlendState();
        standardBlendState = resourceFactory.CreateStandardBlendState();

        rasterizerState = resourceFactory.CreateRasterizerState();

        UIRenderTarget = resourceFactory.CreateRenderTarget(size);
        DalamudRenderTarget = resourceFactory.CreateRenderTarget(size);
        CursorRenderTarget = resourceFactory.CreateRenderTarget(size);
        SceneRenderTargets = [resourceFactory.CreateRenderTarget(size), resourceFactory.CreateRenderTarget(size)];
        SceneDepthTargets = [resourceFactory.CreateDepthTarget(size), resourceFactory.CreateDepthTarget(size)];
    }

    public void UpdateCamera(ID3D11DeviceContext* context, CameraConstants camera)
    {
        var cameraSpan = new Span<CameraConstants>(ref camera);
        cameraBuffer!.SetData(context, MemoryMarshal.AsBytes(cameraSpan));

        context->VSSetConstantBuffers(0, 1, ref cameraBuffer!.Handle);

        context->RSSetState(rasterizerState);
    }

    public void SetPixelShaderConstants(ID3D11DeviceContext* context, PixelShaderConstants pixelShaderConstants)
    {
        var cameraSpan = new Span<PixelShaderConstants>(ref pixelShaderConstants);
        pixelShaderConstantsBuffer!.SetData(context, MemoryMarshal.AsBytes(cameraSpan));

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
        fixed (ID3D11Buffer** pHandle = &squareBuffer!.Handle)
        {
            uint stride = (uint)sizeof(Vertex);
            uint offsets = 0;
            context->IASetVertexBuffers(0, 1, pHandle, &stride, &offsets);
            context->IASetPrimitiveTopology(Silk.NET.Core.Native.D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);
            context->Draw(squareBuffer.VertexCount, 0);
        }
    }
    public void DrawUI(ID3D11DeviceContext* context)
    {
        fixed (ID3D11Buffer** pHandle = &uiBuffer!.Handle)
        {
            uint stride = (uint)sizeof(Vertex);
            uint offsets = 0;
            context->IASetVertexBuffers(0, 1, pHandle, &stride, &offsets);
            context->IASetPrimitiveTopology(Silk.NET.Core.Native.D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);
            context->Draw(uiBuffer.VertexCount, 0);
        }
    }

    public void DrawCylinder(ID3D11DeviceContext* context)
    {
        fixed (ID3D11Buffer** pHandle = &cylinderBuffer!.Handle)
        {
            uint stride = (uint)sizeof(Vertex);
            uint offsets = 0;
            context->IASetVertexBuffers(0, 1, pHandle, &stride, &offsets);
            context->IASetPrimitiveTopology(Silk.NET.Core.Native.D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);
            context->Draw(cylinderBuffer.VertexCount, 0);
        }
    }

    public void Dispose()
    {
        cameraBuffer?.Dispose();
        pixelShaderConstantsBuffer?.Dispose();
        cylinderBuffer?.Dispose();
        squareBuffer?.Dispose();
        uiBuffer?.Dispose();
        UIRenderTarget?.Dispose();
        DalamudRenderTarget?.Dispose();
        CursorRenderTarget?.Dispose();
        foreach (var rt in SceneRenderTargets)
        {
            rt.Dispose();
        }
        foreach (var dt in SceneDepthTargets)
        {
            dt.Dispose();
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
        if (standardBlendState != null)
        {
            standardBlendState->Release();
        }
        if (depthStencilStateOn != null)
        {
            depthStencilStateOn->Release();
        }
        if (depthStencilStateOff != null)
        {
            depthStencilStateOff->Release();
        }
        if (rasterizerState != null)
        {
            rasterizerState->Release();
        }
        if (samplerState != null)
        {
            samplerState->Release();
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