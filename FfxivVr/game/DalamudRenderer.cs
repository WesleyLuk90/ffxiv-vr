using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiBackend;
using Dalamud.Interface.ImGuiBackend.Renderers;
using Dalamud.Interface.Internal;
using System;
using System.Runtime.CompilerServices;

namespace FfxivVR;

public interface IInterfaceManager
{
    InterfaceManager InterfaceManager { get; }
}

public class DalamudInterfaceManager : IInterfaceManager
{
    private readonly InterfaceManager _interfaceManager;

    public DalamudInterfaceManager(InterfaceManager interfaceManager)
    {
        _interfaceManager = interfaceManager;
    }

    public InterfaceManager InterfaceManager => _interfaceManager;
}

public unsafe class DalamudRenderer(IInterfaceManager interfaceManager, Resources resources) : IDisposable
{
    private readonly IInterfaceManager _interfaceManager = interfaceManager;
    private Dx11Renderer? renderer;
    private bool disposed = false;

    public void Initialize()
    {
        var backend = _interfaceManager.InterfaceManager.Backend as Dx11Win32Backend ?? throw new Exception("Failed to get Dx11Win32Backend");
        renderer = backend.Renderer as Dx11Renderer ?? throw new Exception("Failed to get Dx11Renderer");
        _interfaceManager.InterfaceManager.Draw += OnDraw;
    }

    private void OnDraw()
    {
        // Render before Dalamud disposes textures, otherwise the draw data may reference disposed textures
        _interfaceManager.InterfaceManager.RunAfterImGuiRender(Render);
    }

    private void Render()
    {
        var renderTarget = resources.DalamudRenderTarget;
        if (disposed || renderTarget == null)
        {
            return;
        }
        RenderDrawDataInternal(renderer!, (TerraFX.Interop.DirectX.ID3D11Texture2D*)renderTarget.Texture, (TerraFX.Interop.DirectX.ID3D11RenderTargetView*)renderTarget.RenderTargetView, ImGui.GetDrawData(), true);
    }

    public void Dispose()
    {
        disposed = true;
        if (renderer != null)
        {
            _interfaceManager.InterfaceManager.Draw -= OnDraw;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RenderDrawDataInternal")]
    static extern void RenderDrawDataInternal(
        Dx11Renderer instance, TerraFX.Interop.DirectX.ID3D11Texture2D* renderTargetTexture, TerraFX.Interop.DirectX.ID3D11RenderTargetView* renderTargetView, ImDrawDataPtr drawData, bool clearRenderTarget);
}