using Dalamud.Hooking;

namespace FfxivVR;

public class OriginalFunctions
{
    public OriginalFunctions(
        Hook<DXHooks.OMSetRenderTargets> omSetRenderTargetsHook,
        Hook<DXHooks.ClearRenderTargetView> clearRenderTargetViewHook,
        Hook<DXHooks.ClearDepthStencilView> clearDepthStencilViewHook,
        Hook<DXHooks.VSSetConstantBuffers> vsSetConstantBuffersHook,
        Hook<DXHooks.Draw> drawHook,
        Hook<DXHooks.DrawIndexed> drawIndexedHook,
        Hook<DXHooks.Map> mapHook,
        Hook<DXHooks.Unmap> unmapHook
    )
    {
        OMSetRenderTargets = omSetRenderTargetsHook.Original;
        ClearRenderTargetView = clearRenderTargetViewHook.Original;
        ClearDepthStencilView = clearDepthStencilViewHook.Original;
        VSSetConstantBuffers = vsSetConstantBuffersHook.Original;
        Draw = drawHook.Original;
        DrawIndexed = drawIndexedHook.Original;
        Map = mapHook.Original;
        Unmap = unmapHook.Original;
    }

    public DXHooks.OMSetRenderTargets OMSetRenderTargets { get; }
    public DXHooks.ClearRenderTargetView ClearRenderTargetView { get; }
    public DXHooks.ClearDepthStencilView ClearDepthStencilView { get; }
    public DXHooks.VSSetConstantBuffers VSSetConstantBuffers { get; }
    public DXHooks.Draw Draw { get; }
    public DXHooks.DrawIndexed DrawIndexed { get; }
    public DXHooks.Map Map { get; }
    public DXHooks.Unmap Unmap { get; }
}