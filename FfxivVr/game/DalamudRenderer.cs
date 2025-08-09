using Dalamud;
using Silk.NET.Direct3D11;
using System;
using Dalamud.Interface.Internal;
using Dalamud.Interface.ImGuiBackend;
using Dalamud.Interface.ImGuiBackend.Renderers;
using Dalamud.Bindings.ImGui;
using System.Reflection;
using System.Linq;

namespace FfxivVR;

public unsafe class DalamudRenderer
{
     private Logger logger;
     private Dx11Renderer renderer;
     private MethodInfo renderDrawDataInternalMethod;

     public DalamudRenderer(
          Logger logger
     )
     {
          this.logger = logger;
          var interfaceManager = Service<InterfaceManager>.GetNullable() ?? throw new Exception("Failed to get InterfaceManager");
          logger.Info($"Interface manager {interfaceManager}");
          var backend = interfaceManager.Backend as Dx11Win32Backend ?? throw new Exception("Failed to get Dx11Win32Backend"); ;
          logger.Info($"Backend {backend}");
          renderer = backend.Renderer as Dx11Renderer ?? throw new Exception("Failed to get Dx11Renderer");

          var assembly = Assembly.GetAssembly(typeof(Dx11Renderer)) ?? throw new Exception("Failed to get Assembly");
          var type = assembly.GetType("Dalamud.Interface.ImGuiBackend.Renderers.Dx11Renderer") ?? throw new Exception("Failed to get Dx11Renderer type");
          renderDrawDataInternalMethod = type.GetRuntimeMethods().Where(m => m.Name == "RenderDrawDataInternal").First();
          logger.Info($"Got methd {renderDrawDataInternalMethod}");
     }

     internal void Render(ID3D11RenderTargetView* renderTargetView)
     {
          RenderDrawDataInternal(renderTargetView, ImGui.GetDrawData(), false);
     }

     private void RenderDrawDataInternal(
        ID3D11RenderTargetView* renderTargetView,
        ImDrawDataPtr drawData,
        bool clearRenderTarget)
     {
          renderDrawDataInternalMethod.Invoke(renderer, [(int)renderTargetView, drawData, clearRenderTarget]);
     }

}