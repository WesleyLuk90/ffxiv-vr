using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System;

namespace FfxivVR;

public unsafe class VRSession : IDisposable
{

    private XR xr;
    private VRSystem vrSystem;
    private Logger logger;
    public VRState State;
    private Renderer renderer;
    private EventHandler eventHandler;
    private VRSwapchains swapchains;

    public VRSession(String openXRLoaderDllPath, Logger logger, ID3D11Device* device, ID3D11DeviceContext* deviceContext)
        : this(new XR(XR.CreateDefaultContext(new string[] { openXRLoaderDllPath })), logger, device, deviceContext) { }
    public VRSession(XR xr, Logger logger, ID3D11Device* device, ID3D11DeviceContext* deviceContext)
    {
        this.xr = xr;
        vrSystem = new VRSystem(xr, device, logger);
        this.logger = logger;
        State = new VRState();
        swapchains = new VRSwapchains(xr, vrSystem, logger, device);
        renderer = new Renderer(xr, vrSystem, State, logger, swapchains, deviceContext);
        eventHandler = new EventHandler(xr, vrSystem, logger, State);
    }

    public void Initialize()
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

    public void Update()
    {
        this.eventHandler.PollEvents();
        if (State.SessionRunning)
        {
            this.renderer.Render();
        }
    }
}
