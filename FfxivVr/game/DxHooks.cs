using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Silk.NET.Direct3D11;
using System;
using System.Collections.Generic;

namespace FfxivVR;

public unsafe class DXHooks(
    Logger logger,
    IGameInteropProvider gameInteropProvider,
    RenderTargetLookup renderTargetLookup,
    ExceptionHandler exceptionHandler,
    SinglePassRenderer singlePassRenderer
) : IDisposable
{
    bool didInit = false;
    private OriginalFunctions? _originalFunctions = null;
    public void Initialize()
    {
        if (didInit)
        {
            return;
        }
        var ic = (ID3D11DeviceContext*)Device.Instance()->D3D11DeviceContext;
        OMSetRenderTargetsHook = HookVTable<OMSetRenderTargets>(ic->LpVtbl, OMSetRenderTargetsDetour, 33);
        ClearRenderTargetViewHook = HookVTable<ClearRenderTargetView>(ic->LpVtbl, ClearRenderTargetViewDetour, 50);
        ClearDepthStencilViewHook = HookVTable<ClearDepthStencilView>(ic->LpVtbl, ClearDepthStencilViewDetour, 53);
        VSSetConstantBuffersHook = HookVTable<VSSetConstantBuffers>(ic->LpVtbl, VSSetConstantBuffersDetour, 7);
        DrawHook = HookVTable<Draw>(ic->LpVtbl, DrawDetour, 13);
        DrawIndexedHook = HookVTable<DrawIndexed>(ic->LpVtbl, DrawIndexedDetour, 12);
        MapHook = HookVTable<Map>(ic->LpVtbl, MapDetour, 14);
        UnmapHook = HookVTable<Unmap>(ic->LpVtbl, UnmapDetour, 15);

        _originalFunctions = new OriginalFunctions(
            OMSetRenderTargetsHook,
            ClearRenderTargetViewHook,
            ClearDepthStencilViewHook,
            VSSetConstantBuffersHook,
            DrawHook,
            DrawIndexedHook,
            MapHook,
            UnmapHook
        );

        didInit = true;
    }
    Hook<T> HookVTable<T>(void** vtable, T del, int index) where T : Delegate
    {
        var hook = gameInteropProvider.HookFromAddress(vtable[index], del);
        hook.Enable();
        DisposeActions.Add(() => DisposeHook(hook));
        return hook;
    }
    private List<Action> DisposeActions = new();
    private void DisposeHook<T>(Hook<T>? hook) where T : Delegate
    {
        hook?.Disable();
        hook?.Dispose();
    }
    public void Dispose()
    {
        DisposeActions.ForEach(a => a.Invoke());
        DisposeActions.Clear();
    }

    public delegate void OMSetRenderTargets(ID3D11DeviceContext* context, uint numViews, ID3D11RenderTargetView** ppRenderTargetViews, ID3D11DepthStencilView* pDepthStencilView);
    private Hook<OMSetRenderTargets>? OMSetRenderTargetsHook = null;
    private ID3D11RenderTargetView* lastRTV;
    private ID3D11DepthStencilView* lastDSV;
    private void OMSetRenderTargetsDetour(ID3D11DeviceContext* context, uint numViews, ID3D11RenderTargetView** ppRenderTargetViews, ID3D11DepthStencilView* pDepthStencilView)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            if (numViews > 0 && ppRenderTargetViews[0] != null && renderTargetLookup.IsRenderTexture(ppRenderTargetViews[0]))
            {
                logger.Trace($"OMSetRenderTargetsDetour Count:{numViews} DSV:{(nint)pDepthStencilView:X}");
                lastRTV = ppRenderTargetViews[0];
                lastDSV = pDepthStencilView;
            }
            else
            {
                lastRTV = null;
                lastDSV = null;
            }
        });
        OMSetRenderTargetsHook?.Original(context, numViews, ppRenderTargetViews, pDepthStencilView);
    }

    public delegate void ClearRenderTargetView(ID3D11DeviceContext* context, ID3D11RenderTargetView* pRenderTargetView, float* colorRGBA);
    private Hook<ClearRenderTargetView>? ClearRenderTargetViewHook = null;
    private void ClearRenderTargetViewDetour(ID3D11DeviceContext* context, ID3D11RenderTargetView* pRenderTargetView, float* colorRGBA)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            if (pRenderTargetView != null && renderTargetLookup.IsRenderTexture(pRenderTargetView))
            {
                logger.Debug("ClearRenderTargetViewDetour");
                singlePassRenderer.ClearRenderTargetViewDetour(_originalFunctions!, context, pRenderTargetView, colorRGBA);
            }
        });
        ClearRenderTargetViewHook?.Original(context, pRenderTargetView, colorRGBA);
    }

    public delegate void ClearDepthStencilView(ID3D11DeviceContext* context, ID3D11DepthStencilView* pDepthStencilView, uint clearFlags, float depth, byte stencil);
    private Hook<ClearDepthStencilView>? ClearDepthStencilViewHook = null;
    private void ClearDepthStencilViewDetour(ID3D11DeviceContext* context, ID3D11DepthStencilView* pDepthStencilView, uint clearFlags, float depth, byte stencil)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            if (pDepthStencilView != null && renderTargetLookup.IsDepthStencil(pDepthStencilView))
            {
                logger.Trace("ClearDepthStencilView");
                singlePassRenderer.ClearDepthStencilView(_originalFunctions!, context, pDepthStencilView, clearFlags, depth, stencil);
            }
        });
        ClearDepthStencilViewHook?.Original(context, pDepthStencilView, clearFlags, depth, stencil);
    }

    public delegate void Draw(ID3D11DeviceContext* context, uint vertexCount, uint startVertexLocation);
    private Hook<Draw>? DrawHook = null;
    private void DrawDetour(ID3D11DeviceContext* context, uint vertexCount, uint startVertexLocation)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            if (lastRTV != null)
            {
                logger.Trace("DrawDetour");
                singlePassRenderer.DrawDetour(_originalFunctions!, context, vertexCount, startVertexLocation);
                var rtv = lastRTV;
                OMSetRenderTargetsHook?.Original(context, 1, &rtv, lastDSV);
            }
        });
        DrawHook?.Original(context, vertexCount, startVertexLocation);
    }


    public delegate void DrawIndexed(ID3D11DeviceContext* context, uint indexCount, uint startIndexLocation, int baseVertexLocation);
    private Hook<DrawIndexed>? DrawIndexedHook = null;

    private void DrawIndexedDetour(ID3D11DeviceContext* context, uint indexCount, uint startIndexLocation, int baseVertexLocation)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            if (lastRTV != null)
            {
                logger.Trace("DrawIndexedDetour");
                singlePassRenderer.DrawIndexedDetour(_originalFunctions!, context, indexCount, startIndexLocation, baseVertexLocation);
                var rtv = lastRTV;
                OMSetRenderTargetsHook?.Original(context, 1, &rtv, lastDSV);
            }
        });
        DrawIndexedHook?.Original(context, indexCount, startIndexLocation, baseVertexLocation);
    }

    public delegate void VSSetConstantBuffers(ID3D11DeviceContext* context, uint startSlot, uint numBuffers, ID3D11Buffer** ppConstantBuffers);
    private Hook<VSSetConstantBuffers>? VSSetConstantBuffersHook = null;
    private void VSSetConstantBuffersDetour(ID3D11DeviceContext* context, uint startSlot, uint numBuffers, ID3D11Buffer** ppConstantBuffers)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            logger.Trace($"VSSetConstantBuffersDetour {startSlot} {numBuffers}");
            singlePassRenderer.VSSetConstantBuffersDetour(_originalFunctions!, context, startSlot, numBuffers, ppConstantBuffers);
        });
        VSSetConstantBuffersHook?.Original(context, startSlot, numBuffers, ppConstantBuffers);
    }

    public delegate int Map(ID3D11DeviceContext* context, ID3D11Resource* pResource, uint subresource, Silk.NET.Direct3D11.Map mapType, uint mapFlags, MappedSubresource* pMappedResource);
    private Hook<Map>? MapHook = null;
    private int MapDetour(ID3D11DeviceContext* context, ID3D11Resource* pResource, uint subresource, Silk.NET.Direct3D11.Map mapType, uint mapFlags, MappedSubresource* pMappedResource)
    {
        var value = MapHook!.Original(context, pResource, subresource, mapType, mapFlags, pMappedResource);
        exceptionHandler.FaultBarrier(() =>
        {
            singlePassRenderer.Map(_originalFunctions!, context, pResource, subresource, mapType, mapFlags, pMappedResource);
        });
        return value;
    }

    public delegate void Unmap(ID3D11DeviceContext* context, ID3D11Resource* pResource, uint subresource);
    private Hook<Unmap>? UnmapHook = null;
    private void UnmapDetour(ID3D11DeviceContext* context, ID3D11Resource* pResource, uint subresource)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            singlePassRenderer.Unmap(_originalFunctions!, context, pResource, subresource);
        });
        UnmapHook!.Original(context, pResource, subresource);
    }
}