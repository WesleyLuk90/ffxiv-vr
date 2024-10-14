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


    public abstract record RenderState
    {
        public record Ready() : RenderState;
        public record SkipRender(FrameState frameState) : RenderState;
        public record Rendering(FrameState frameState) : RenderState;

        public record Skipped() : RenderState;
    }

    private RenderState renderState = new RenderState.Ready();

    public void StartFrame(ID3D11DeviceContext* context)
    {
        eventHandler.PollEvents();
        if (renderState is not RenderState.Ready)
        {
            logger.Error($"Frame state was not Ready but was {renderState}");
        }

        if (State.SessionRunning)
        {
            var frameState = renderer.StartFrame(context);
            if (State.IsActive() && frameState.ShouldRender != 0)
            {
                //var matrices = renderer.GetProjectionMatrixes(frameState.PredictedDisplayPeriod);
                renderState = new RenderState.Rendering(frameState);
            }
            else
            {
                renderState = new RenderState.SkipRender(frameState);
            }
        }
        else
        {
            renderState = new RenderState.Skipped();
        }
    }

    public void EndFrame(ID3D11DeviceContext* context)
    {
        switch (renderState)
        {
            case RenderState.SkipRender skip:
                renderer.SkipFrame(skip.frameState);
                break;
            case RenderState.Rendering rendering:
                renderer.EndFrame(context, rendering.frameState);
                break;
            case RenderState.Skipped:
                break;
            default:
                //logger.Error($"Unexpected frame state at end {renderState}");
                break;
        }
        renderState = new RenderState.Ready();
    }
}
