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
    private VRShaders vrShaders;
    private Resources resources;
    private VRSwapchains swapchains;

    public VRSession(String openXRLoaderDllPath, Logger logger, ID3D11Device* device)
        : this(new XR(XR.CreateDefaultContext(new string[] { openXRLoaderDllPath })), logger, device) { }
    public VRSession(XR xr, Logger logger, ID3D11Device* device)
    {
        this.xr = xr;
        vrSystem = new VRSystem(xr, device, logger);
        this.logger = logger;
        State = new VRState();
        swapchains = new VRSwapchains(xr, vrSystem, logger, device);
        resources = new Resources(device);
        vrShaders = new VRShaders(device, logger);
        renderer = new Renderer(xr, vrSystem, State, logger, swapchains, resources, vrShaders);
        eventHandler = new EventHandler(xr, vrSystem, logger, State);
    }

    public void Initialize()
    {
        vrSystem.Initialize();
        vrShaders.Initialize();
        swapchains.Initialize();
        resources.Initialize();
        renderer.Initialize();
    }

    public void Dispose()
    {
        renderer.Dispose();
        vrShaders.Dispose();
        swapchains.Dispose();
        resources.Dispose();
        vrSystem.Dispose();
    }

    public void Update(ID3D11DeviceContext* context)
    {
        if (State.SessionRunning)
        {
            this.renderer.Render(context);
        }
    }

    public abstract record FrameState
    {
        public record Ready() : FrameState;
        public record Started() : FrameState;
        public record Skipped() : FrameState;
    }

    private FrameState frameState = new FrameState.Ready();

    internal void StartFrame()
    {
        if (frameState is FrameState.Ready)
        {
            logger.Error($"Frame state was not Ready but was {frameState}");
        }

        if (State.SessionRunning)
        {
            renderer.StartFrame();
            frameState = new FrameState.Started();
        }
        else
        {
            frameState = new FrameState.Skipped();
        }
    }

    internal void EndFrame()
    {
        switch (frameState)
        {
            case FrameState.Started:
                renderer.EndFrame();
                break;

            case FrameState.Skipped:
                break;
            default:
                logger.Error($"Unexpected frame state at end {frameState}");
                break;
        }
    }
}
