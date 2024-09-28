using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FfxivVR;

unsafe class VRSession : IDisposable
{

    private XR xr;
    private VRSystem vrSystem;
    private Logger logger;
    private VRState vrState;
    private Renderer renderer;
    private EventHandler eventHandler;

    internal VRSession(String openXRLoaderDllPath, Logger logger, Device* device)
    {
        xr = new XR(XR.CreateDefaultContext(new string[] { openXRLoaderDllPath }));
        vrSystem = new VRSystem(xr, device, logger);
        this.logger = logger;
        vrState = new VRState();
        renderer = new Renderer(xr, vrSystem, vrState, logger);
        eventHandler = new EventHandler(xr, vrSystem, logger, vrState);
    }

    internal void Initialize()
    {
        vrSystem.Initialize();
        renderer.Initialize();
    }


    public void Dispose()
    {
        renderer.Dispose();
        vrSystem.Dispose();
    }

    internal void Update()
    {
        this.eventHandler.PollEvents();
        this.renderer.Render();
    }
}
