using Dalamud;
using Silk.NET.Direct3D11;
using System;
using Dalamud.Interface.Internal;
using Dalamud.Interface.ImGuiBackend;
using Dalamud.Interface.ImGuiBackend.Renderers;
using Dalamud.Bindings.ImGui;
using System.Reflection;

namespace FfxivVR;

public unsafe class DalamudRenderer
{
     private Logger logger;
     // private Dx11Renderer renderer;

     public DalamudRenderer(
          Logger logger
     )
     {
          this.logger = logger;
          var interfaceManager = Service<InterfaceManager>.GetNullable() ?? throw new Exception("Failed to get InterfaceManager");
          logger.Info($"Interface manager {interfaceManager}");
          var backend = interfaceManager.Backend as Dx11Win32Backend ?? throw new Exception("Failed to get Dx11Win32Backend"); ;
          logger.Info($"Backend {backend}");
          // renderer = backend.Renderer as Dx11Renderer ?? throw new Exception("Failed to get Dx11Renderer");

          // var assembly = Assembly.GetAssembly(typeof(Dx11Renderer));
     }

     internal void Render(ID3D11RenderTargetView* renderTargetView)
     {
          // logger.Info($"renderer {renderer}");
          // renderer.RenderDrawData(ImGui.GetDrawData());
     }

     // private RenderTargetView? SwapRenderTargetView(RenderTargetView? renderTargetView)
     // {
     // var original = (RenderTargetView?)renderTargetViewProperty?.GetValue(scene);

     // renderTargetViewProperty?.SetValue(scene, renderTargetView);
     // return original;
     // }
}