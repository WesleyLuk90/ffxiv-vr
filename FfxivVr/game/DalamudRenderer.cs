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

public unsafe class DalamudRenderer(IInterfaceManager interfaceManager, Resources resources, ResolutionManager resolutionManager) : IDisposable
{
    private readonly IInterfaceManager _interfaceManager = interfaceManager;
    private Dx11Win32Backend? backend;
    private Dx11Renderer? renderer;
    private bool disposed = false;
    private int scaledMouseEvents = 0;

    public void Initialize()
    {
        backend = _interfaceManager.InterfaceManager.Backend as Dx11Win32Backend ?? throw new Exception("Failed to get Dx11Win32Backend");
        renderer = backend.Renderer as Dx11Renderer ?? throw new Exception("Failed to get Dx11Renderer");
        backend.NewInputFrame += OnNewInputFrame;
        _interfaceManager.InterfaceManager.Draw += OnDraw;
    }

    private void OnNewInputFrame()
    {
        var queue = ImGui.GetCurrentContext().InputEventsQueue.AsSpan();
        if (disposed || resolutionManager.GetMouseScale() is not { } scale)
        {
            scaledMouseEvents = queue.Length;
            return;
        }
        var origin = ImGui.GetMainViewport().Pos;
        for (var i = Math.Min(scaledMouseEvents, queue.Length); i < queue.Length; i++)
        {
            ref var inputEvent = ref queue[i];
            if (inputEvent.Type != ImGuiInputEventType.MousePos)
            {
                continue;
            }
            ref var position = ref inputEvent.Union.MousePos;
            if (position.PosX <= -float.MaxValue || position.PosY <= -float.MaxValue)
            {
                continue;
            }
            position.PosX = MathF.Floor(origin.X + (position.PosX - origin.X) * scale.X);
            position.PosY = MathF.Floor(origin.Y + (position.PosY - origin.Y) * scale.Y);
        }
        scaledMouseEvents = queue.Length;
    }

    private void OnDraw()
    {
        scaledMouseEvents = ImGui.GetCurrentContext().InputEventsQueue.Size;
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
        if (backend != null)
        {
            backend.NewInputFrame -= OnNewInputFrame;
        }
        if (renderer != null)
        {
            _interfaceManager.InterfaceManager.Draw -= OnDraw;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RenderDrawDataInternal")]
    static extern void RenderDrawDataInternal(
        Dx11Renderer instance, TerraFX.Interop.DirectX.ID3D11Texture2D* renderTargetTexture, TerraFX.Interop.DirectX.ID3D11RenderTargetView* renderTargetView, ImDrawDataPtr drawData, bool clearRenderTarget);
}