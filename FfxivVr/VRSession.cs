using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Numerics;

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
        resources = new Resources(device, logger);
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
        public record Rendering(FrameState frameState, View[] views) : RenderState;

        public record Skipped() : RenderState;
    }

    private RenderState renderState = new RenderState.Skipped();

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
                var views = renderer.LocateView(frameState);
                renderState = new RenderState.Rendering(frameState, views);
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

    public void EndFrame(ID3D11DeviceContext* context, Texture* gameRenderTexture)
    {
        switch (renderState)
        {
            case RenderState.SkipRender skip:
                renderer.SkipFrame(skip.frameState);
                break;
            case RenderState.Rendering rendering:
                renderer.EndFrame(context, rendering.frameState, gameRenderTexture, rendering.views);
                break;
            case RenderState.Skipped:
                break;
            default:
                logger.Error($"Unexpected frame state at end {renderState}");
                break;
        }
        renderState = new RenderState.Ready();
    }

    internal bool SecondRender(ID3D11DeviceContext* context)
    {
        return false;
    }

    internal void UpdateViewMatrix(Matrix4x4* viewMatrix)
    {
        if (renderState is RenderState.Rendering rendering)
        {
            //*viewMatrix = renderer.ComputeViewMatrix(rendering.views).ToMatrix4x4();
        }
    }

    internal void UpdateCamera(FFXIVClientStructs.FFXIV.Client.Graphics.Render.Camera* camera)
    {
        if (renderState is RenderState.Rendering rendering)
        {
            var view = rendering.views[0];
            var near = 0.1f;
            var left = MathF.Tan(view.Fov.AngleLeft) * near;
            var right = MathF.Tan(view.Fov.AngleRight) * near;
            var down = MathF.Tan(view.Fov.AngleDown) * near;
            var up = MathF.Tan(view.Fov.AngleUp) * near;

            var proj = Matrix4X4.CreatePerspectiveOffCenter<float>(left, right, down, up, nearPlaneDistance: near, farPlaneDistance: 100f);

            // Overwrite these for FFXIV
            proj.M33 = 0;
            proj.M43 = near;
            logger.Debug($"Matrix is {proj}");

            camera->ProjectionMatrix = proj.ToMatrix4x4();
            camera->ProjectionMatrix2 = proj.ToMatrix4x4();
        }
    }
}
