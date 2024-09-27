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
    private VRSystem system;
    private Logger logger;
    private Renderer renderer;
    private EventHandler eventHandler;

    internal VRSession(String openXRLoaderDllPath, Logger logger, Device* device)
    {
        xr = new XR(XR.CreateDefaultContext(openXRLoaderDllPath));
        system = new VRSystem(xr, device, logger);
        this.logger = logger;
        this.renderer = new Renderer(xr, system);
        this.eventHandler = new EventHandler(xr);
    }

    internal void Initialize()
    {
        system.Initialize();
    }


    public void Dispose()
    {
        system.Dispose();
    }

    internal void Update()
    {
        this.eventHandler.PollEvents();
        this.renderer.Render();
    }
}
