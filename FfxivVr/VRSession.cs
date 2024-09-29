using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System;

namespace FfxivVR;

unsafe class VRSession : IDisposable
{

    private XR xr;
    private VRSystem vrSystem;
    private Logger logger;
    private VRState vrState;
    private Renderer renderer;
    private EventHandler eventHandler;
    private VRSwapchains swapchains;

    internal VRSession(String openXRLoaderDllPath, Logger logger, ID3D11Device* device, ID3D11DeviceContext* deviceContext)
    {
        xr = new XR(XR.CreateDefaultContext(new string[] { openXRLoaderDllPath }));
        vrSystem = new VRSystem(xr, device, logger);
        this.logger = logger;
        vrState = new VRState();
        swapchains = new VRSwapchains(xr, vrSystem, logger, device);
        renderer = new Renderer(xr, vrSystem, vrState, logger, swapchains, deviceContext);
        eventHandler = new EventHandler(xr, vrSystem, logger, vrState);
    }

    internal void Initialize()
    {
        vrSystem.Initialize();
        swapchains.Initialize();
        renderer.Initialize();
    }


    public void Dispose()
    {
        renderer.Dispose();
        swapchains.Dispose();
        vrSystem.Dispose();
    }

    internal void Update()
    {
        this.eventHandler.PollEvents();
        this.renderer.Render();
    }
}
