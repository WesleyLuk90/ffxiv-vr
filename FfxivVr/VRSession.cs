using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;

namespace FfxivVR;

public unsafe class VRSession : IDisposable
{

    private readonly XR xr;
    private readonly VRSystem vrSystem;
    private readonly Logger logger;
    public VRState State;
    private readonly Renderer renderer;
    private readonly GameVisibility gameVisibility;
    private readonly VRSpace vrSpace;
    private readonly EventHandler eventHandler;
    private readonly VRShaders vrShaders;
    private readonly Resources resources;
    private readonly VRSwapchains swapchains;

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
        vrSpace = new VRSpace(xr, logger, vrSystem);
        renderer = new Renderer(xr, vrSystem, State, logger, swapchains, resources, vrShaders, vrSpace);
        gameVisibility = new GameVisibility();
        eventHandler = new EventHandler(xr, vrSystem, logger, State, vrSpace);
    }

    public void Initialize()
    {
        vrSystem.Initialize();
        vrShaders.Initialize();
        swapchains.Initialize();
        resources.Initialize();
        vrSpace.Initialize();
    }

    public void Dispose()
    {
        vrSpace.Dispose();
        vrShaders.Dispose();
        swapchains.Dispose();
        resources.Dispose();
        vrSystem.Dispose();
    }


    public abstract record RenderState
    {
        public record Ready() : RenderState;
        public record SkipRender(FrameState frameState) : RenderState;
        public record RenderingLeft(FrameState frameState, View[] views) : RenderState;
        public record RenderingRight(FrameState frameState, View[] views, CompositionLayerProjectionView leftLayer) : RenderState;

        public record Skipped() : RenderState;
    }

    private RenderState renderState = new RenderState.Skipped();

    public void PostPresent(ID3D11DeviceContext* context)
    {
        eventHandler.PollEvents();

        if (renderState is RenderState.Ready)
        {
            if (State.SessionRunning)
            {
                var frameState = renderer.StartFrame(context);
                if (State.IsActive() && frameState.ShouldRender != 0)
                {
                    var views = renderer.LocateView(frameState);
                    renderState = new RenderState.RenderingLeft(frameState, views);
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
    }

    public void PrePresent(ID3D11DeviceContext* context, Texture* gameRenderTexture)
    {
        switch (renderState)
        {
            case RenderState.SkipRender skip:
                renderer.SkipFrame(skip.frameState);
                renderState = new RenderState.Ready();
                break;
            case RenderState.RenderingLeft rendering:
                var leftLayer = renderer.RenderEye(context, rendering.frameState, gameRenderTexture, rendering.views, Eye.Left);
                renderState = new RenderState.RenderingRight(rendering.frameState, rendering.views, leftLayer);
                break;
            case RenderState.RenderingRight rendering:
                var rightLayer = renderer.RenderEye(context, rendering.frameState, gameRenderTexture, rendering.views, Eye.Right);
                renderer.EndFrame(context, rendering.frameState, gameRenderTexture, rendering.views, [rendering.leftLayer, rightLayer]);
                renderState = new RenderState.Ready();
                break;
            case RenderState.Skipped:
                renderState = new RenderState.Ready();
                break;
            default:
                logger.Error($"Unexpected frame state at end {renderState}");
                break;
        }
    }

    internal bool SecondRender(ID3D11DeviceContext* context)
    {
        return renderState is RenderState.RenderingRight;
    }

    internal void UpdateCamera(Camera* camera)
    {
        View view;
        // These seem to be swapped because the render step is out of sync with the update camera call
        if (renderState is RenderState.RenderingLeft renderingLeft)
        {
            view = renderingLeft.views[Eye.Right.ToIndex()];
        }
        else if (renderState is RenderState.RenderingRight renderingRight)
        {
            view = renderingRight.views[Eye.Left.ToIndex()];
        }
        else
        {
            return;
        }
        var near = 0.1f;
        var left = MathF.Tan(view.Fov.AngleLeft) * near;
        var right = MathF.Tan(view.Fov.AngleRight) * near;
        var down = MathF.Tan(view.Fov.AngleDown) * near;
        var up = MathF.Tan(view.Fov.AngleUp) * near;

        var proj = Matrix4X4.CreatePerspectiveOffCenter<float>(left, right, down, up, nearPlaneDistance: near, farPlaneDistance: 100f);

        // Overwrite these for FFXIV's weird projection matrix
        proj.M33 = 0;
        proj.M43 = near;

        camera->RenderCamera->ProjectionMatrix = proj.ToMatrix4x4();
        camera->RenderCamera->ProjectionMatrix2 = proj.ToMatrix4x4();

        camera->RenderCamera->ViewMatrix = renderer.ComputeViewMatrix(view, camera->RenderCamera->Origin.ToVector3D(), camera->LookAtVector.ToVector3D()).ToMatrix4x4();
    }

    internal void RecenterCamera()
    {
        vrSpace.RecenterCamera(renderer.LastTime);
    }

    internal void UpdateModelVisibility()
    {
        gameVisibility.UpdateVisibility();
    }
}
