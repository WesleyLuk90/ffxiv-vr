using Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiBackend;
using Dalamud.Interface.ImGuiBackend.Renderers;
using Dalamud.Interface.Internal;
using Silk.NET.Direct3D11;
using System;
using System.Runtime.CompilerServices;

namespace FfxivVR;

public unsafe class DalamudRenderer
{
    private Dx11Renderer? renderer;
    public void Initialize()
    {
        var interfaceManager = Service<InterfaceManager>.GetNullable() ?? throw new Exception("Failed to get InterfaceManager");
        var backend = interfaceManager.Backend as Dx11Win32Backend ?? throw new Exception("Failed to get Dx11Win32Backend");
        renderer = backend.Renderer as Dx11Renderer ?? throw new Exception("Failed to get Dx11Renderer");
    }

    internal void Render(ID3D11Texture2D* renderTargetTexture, ID3D11RenderTargetView* renderTargetView)
    {
        RenderDrawDataInternal(renderer!, (TerraFX.Interop.DirectX.ID3D11Texture2D*)renderTargetTexture, (TerraFX.Interop.DirectX.ID3D11RenderTargetView*)renderTargetView, ImGui.GetDrawData(), false);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RenderDrawDataInternal")]
    static extern void RenderDrawDataInternal(
        Dx11Renderer instance, TerraFX.Interop.DirectX.ID3D11Texture2D* renderTargetTexture, TerraFX.Interop.DirectX.ID3D11RenderTargetView* renderTargetView, ImDrawDataPtr drawData, bool clearRenderTarget);
}