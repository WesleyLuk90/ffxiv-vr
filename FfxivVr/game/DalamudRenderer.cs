using Dalamud;
using Silk.NET.Direct3D11;
using System;
using System.Reflection;
using Dalamud.Interface.Internal;
using System.Runtime.InteropServices;

namespace FfxivVR;

public unsafe class DalamudRenderer
{
     //      private Dx11Renderer.ViewportHandler viewportHandler;
     //      private RawDX11Scene? scene;
     //      private FieldInfo? renderTargetViewProperty;

     // Use reflection to get access to RawDX11Scene, swap out the render target view and render
     internal void Initialize()
     {
          // var assembly = Assembly.GetAssembly(typeof(IServiceType)) ?? throw new Exception("Failed to find assembly");
          var interfaceManager = Service<InterfaceManager>.GetNullable();
          // var interfaceManagerType = assembly.GetType("Dalamud.Interface.Internal.InterfaceManager");
          //      ?? throw new Exception("Failed to find InterfaceManager type");
          // var service = assembly.GetType("Dalamud.Service`1")
          //      ?? throw new Exception("Failed to find Dalamud.Service type");
          // var genericService = service.MakeGenericType(interfaceManagerType)
          //      ?? throw new Exception("Failed to make Dalamud.Service generic");
          // var method = genericService.GetMethod("Get")
          //      ?? throw new Exception("Failed to find Service.Get method");
          // var interfaceManager = method.Invoke(service, null)
          //      ?? throw new Exception("Failed to get interfaceManager");
          // var sceneMethod = interfaceManagerType.GetProperty("Scene")
          //      ?? throw new Exception("Failed to find Scene property");
          // var scene = sceneMethod.GetValue(interfaceManager)
          //      ?? throw new Exception("Failed to get Scene property");
          // this.scene = (RawDX11Scene)(scene);
          // renderTargetViewProperty = this.scene.GetType().GetField("rtv",
          //                  BindingFlags.NonPublic |
          //                  BindingFlags.Instance)
          //      ?? throw new Exception("Failed to get renderTargetViewProperty");
     }

     internal void Render(ID3D11RenderTargetView* renderTargetView)
     {
          // var temp = new RenderTargetView((IntPtr)renderTargetView);
          // var original = SwapRenderTargetView(temp);
          // scene?.Render();
          // SwapRenderTargetView(original);
     }

     // private RenderTargetView? SwapRenderTargetView(RenderTargetView? renderTargetView)
     // {
     // var original = (RenderTargetView?)renderTargetViewProperty?.GetValue(scene);

     // renderTargetViewProperty?.SetValue(scene, renderTargetView);
     // return original;
     // }
}