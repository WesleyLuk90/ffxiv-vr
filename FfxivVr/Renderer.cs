using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Common.Math;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using static FfxivVR.Resources;

namespace FfxivVR;

unsafe internal class Renderer
{
    private readonly XR xr;
    private readonly VRSystem system;
    private readonly VRState vrState;
    private readonly Logger logger;
    private readonly VRSwapchains swapchains;
    private readonly Resources resources;
    private readonly VRShaders shaders;
    private readonly VRSpace vrSpace;
    private readonly VRSettings settings;

    internal Renderer(XR xr, VRSystem system, VRState vrState, Logger logger, VRSwapchains swapchains, Resources resources, VRShaders shaders, VRSpace vrSpace, VRSettings settings)
    {
        this.xr = xr;
        this.system = system;
        this.vrState = vrState;
        this.logger = logger;
        this.swapchains = swapchains;
        this.resources = resources;
        this.shaders = shaders;
        this.vrSpace = vrSpace;
        this.settings = settings;
    }


    private void RenderViewport(ID3D11DeviceContext* context, Texture* texture, Matrix4X4<float> viewProjection)
    {
        var translation = Matrix4X4.CreateTranslation(new Vector3D<float>(0.0f, 0.0f, 0.0f));
        var modelViewProjection = Matrix4X4.Multiply(translation, viewProjection);
        resources.UpdateCamera(context, new CameraConstants(
            modelViewProjection: translation
        ));
        resources.SetSampler(context, texture);
        resources.Draw(context);
    }


    internal void SkipFrame(FrameState frameState)
    {
        var endFrameInfo = new FrameEndInfo(
            displayTime: frameState.PredictedDisplayTime,
            environmentBlendMode: EnvironmentBlendMode.Opaque,
            layerCount: 0,
            layers: null
        );
        xr.EndFrame(system.Session, ref endFrameInfo).CheckResult("EndFrame");
    }

    // Can't be called from the FrameworkTickFn
    internal View[] LocateView(FrameState frameState)
    {
        var views = Native.CreateArray(new View(next: null), (uint)swapchains.Views.Count);
        var viewState = new ViewState(next: null);
        var viewLocateInfo = new ViewLocateInfo(
            viewConfigurationType: ViewConfigurationType.PrimaryStereo,
            displayTime: frameState.PredictedDisplayTime,
            space: vrSpace.LocalSpace
        );
        uint viewCount = 0;
        xr.LocateView(system.Session, ref viewLocateInfo, ref viewState, ref viewCount, views).CheckResult("LocateView");
        if (viewCount != 2)
        {
            throw new Exception($"LocateView returned an unexpected number of views, got {viewCount}");
        }
        return views;
    }

    internal CompositionLayerProjectionView RenderEye(ID3D11DeviceContext* context, FrameState frameState, Texture* texture, View[] views, Eye eye)
    {
        var swapchainView = swapchains.Views[eye.ToIndex()];
        var view = views[eye.ToIndex()];
        uint colorImageIndex = 0;
        xr.AcquireSwapchainImage(swapchainView.ColorSwapchainInfo.Swapchain, null, ref colorImageIndex).CheckResult("AcquireSwapchainImage");
        var currentColorSwapchainImage = swapchainView.ColorSwapchainInfo.Views[colorImageIndex];
        uint depthSwapchainIndex = 0;
        xr.AcquireSwapchainImage(swapchainView.DepthSwapchainInfo.Swapchain, null, ref depthSwapchainIndex).CheckResult("AcquireSwapchainImage");
        var currentDepthSwapchainImage = swapchainView.DepthSwapchainInfo.Views[depthSwapchainIndex];
        var waitInfo = new SwapchainImageWaitInfo(next: null);
        waitInfo.Timeout = 1000000000L;
        xr.WaitSwapchainImage(swapchainView.ColorSwapchainInfo.Swapchain, ref waitInfo).CheckResult("WaitSwapchainImage");
        xr.WaitSwapchainImage(swapchainView.DepthSwapchainInfo.Swapchain, ref waitInfo).CheckResult("WaitSwapchainImage");

        var width = (int)swapchainView.ViewConfigurationView.RecommendedImageRectWidth;
        var height = (int)swapchainView.ViewConfigurationView.RecommendedImageRectHeight;

        var compositionLayerProjectionView = new CompositionLayerProjectionView(
            pose: view.Pose,
            fov: view.Fov,
            subImage: new SwapchainSubImage(
                swapchain: swapchainView.ColorSwapchainInfo.Swapchain,
                imageRect: new Rect2Di(
                    offset: new Offset2Di(
                        x: 0,
                        y: 0
                    ),
                    extent: new Extent2Di(
                        width: width,
                        height: height
                    )
                ),
                imageArrayIndex: 0
            )
        );
        fixed (float* ptr = new Span<float>([0.17f, 0.17f, 0.17f, 1]))
        {
            context->ClearRenderTargetView(currentColorSwapchainImage, ptr);
        }
        context->ClearDepthStencilView(currentDepthSwapchainImage, (uint)ClearFlag.Depth, 1.0f, 0);

        resources.SetDepthStencilState(context);
        resources.SetBlendState(context);
        context->OMSetRenderTargets(1, ref currentColorSwapchainImage, currentDepthSwapchainImage);
        Viewport viewport = new Viewport(
            topLeftX: 0f,
            topLeftY: 0f,
            width: width,
            height: height,
            minDepth: 0f,
            maxDepth: 1f);
        context->RSSetViewports(1, &viewport);
        Box2D<int> scissor = new Box2D<int>(
            new Vector2D<int>(0, 0),
            new Vector2D<int>(width, height)
        );
        context->RSSetScissorRects(1, &scissor);

        var rotation = Matrix4X4.CreateFromQuaternion<float>(view.Pose.Orientation.ToQuaternion());
        var translation = Matrix4X4.CreateTranslation<float>(view.Pose.Position.ToVector3D());
        var toView = Matrix4X4.Multiply(rotation, translation);
        var viewInverted = new Matrix4X4<float>();
        Matrix4X4.Invert(toView, out viewInverted);

        var near = 0.05f;
        var left = MathF.Tan(view.Fov.AngleLeft) * near;
        var right = MathF.Tan(view.Fov.AngleRight) * near;
        var down = MathF.Tan(view.Fov.AngleDown) * near;
        var up = MathF.Tan(view.Fov.AngleUp) * near;

        var proj = Matrix4X4.CreatePerspectiveOffCenter<float>(left, right, down, up, nearPlaneDistance: near, farPlaneDistance: 100f);
        var viewProj = Matrix4X4.Multiply(viewInverted, proj);

        shaders.SetShaders(context);
        RenderViewport(context, texture, viewProj);

        //var swapchainTexture = swapchainView.ColorSwapchainInfo.Textures[colorImageIndex];
        //var box = new Box(0,
        //    0, 0, 500, 500, 1);
        //context->CopySubresourceRegion((ID3D11Resource*)swapchainTexture, 0, 0, 0, 0, (ID3D11Resource*)texture, 0, &box);
        var releaseInfo = new SwapchainImageReleaseInfo(next: null);
        xr.ReleaseSwapchainImage(swapchainView.ColorSwapchainInfo.Swapchain, ref releaseInfo).CheckResult("ReleaseSwapchainImage");
        xr.ReleaseSwapchainImage(swapchainView.DepthSwapchainInfo.Swapchain, ref releaseInfo).CheckResult("ReleaseSwapchainImage");
        return compositionLayerProjectionView;
    }

    internal void EndFrame(ID3D11DeviceContext* context, FrameState frameState, Texture* texture, View[] views, CompositionLayerProjectionView[] compositionLayerProjectionViews)
    {
        var compositionLayerSpan = new Span<CompositionLayerProjectionView>(compositionLayerProjectionViews);
        fixed (CompositionLayerProjectionView* ptr = compositionLayerSpan)
        {
            var layerProjection = new CompositionLayerProjection(
                layerFlags: CompositionLayerFlags.BlendTextureSourceAlphaBit | CompositionLayerFlags.CorrectChromaticAberrationBit,
                space: vrSpace.LocalSpace,
                viewCount: (uint)compositionLayerSpan.Length,
                views: ptr
            );
            CompositionLayerProjection* layerProjectionPointer = &layerProjection;
            var endFrameInfo = new FrameEndInfo(
                displayTime: frameState.PredictedDisplayTime,
                environmentBlendMode: EnvironmentBlendMode.Opaque,
                layerCount: 1,
                layers: (CompositionLayerBaseHeader**)&layerProjectionPointer
            );
            xr.EndFrame(system.Session, ref endFrameInfo).CheckResult("EndFrame");
        }
    }

    public long LastTime = 0;
    internal FrameState StartFrame(ID3D11DeviceContext* context)
    {
        var frameWaitInfo = new FrameWaitInfo(next: null);
        var frameState = new FrameState(next: null);
        xr.WaitFrame(system.Session, ref frameWaitInfo, ref frameState).CheckResult("WaitFrame");

        var beginFrameInfo = new FrameBeginInfo(next: null);
        xr.BeginFrame(system.Session, ref beginFrameInfo).CheckResult("BeginFrame");
        LastTime = frameState.PredictedDisplayTime;
        return frameState;
    }

    internal Matrix4x4 ComputeProjectionMatrix(View view)
    {
        var near = 0.1f;
        var left = MathF.Tan(view.Fov.AngleLeft) * near;
        var right = MathF.Tan(view.Fov.AngleRight) * near;
        var down = MathF.Tan(view.Fov.AngleDown) * near;
        var up = MathF.Tan(view.Fov.AngleUp) * near;

        var proj = Matrix4X4.CreatePerspectiveOffCenter<float>(left, right, down, up, nearPlaneDistance: near, farPlaneDistance: 100f);

        // Overwrite these for FFXIV's weird projection matrix
        proj.M33 = 0;
        proj.M43 = near;
        return proj.ToMatrix4x4();
    }
    internal Matrix4X4<float> ComputeViewMatrix(View view, Vector3D<float> position, Vector3D<float> lookAt)
    {
        var forwardVector = lookAt - position;
        var yAngle = -MathF.PI / 2 - MathF.Atan2(forwardVector.Z, forwardVector.X);

        var gameViewMatrix = Matrix4X4.CreateScale(1f / settings.Scale) * Matrix4X4.CreateRotationY<float>(yAngle) * Matrix4X4.CreateTranslation<float>(position);
        var vrViewMatrix = Matrix4X4.CreateFromQuaternion<float>(view.Pose.Orientation.ToQuaternion()) * Matrix4X4.CreateTranslation<float>(view.Pose.Position.ToVector3D());

        var viewMatrix = vrViewMatrix * gameViewMatrix;
        var invertedViewMatrix = Matrix4X4<float>.Identity;
        Matrix4X4.Invert(viewMatrix, out invertedViewMatrix);
        return invertedViewMatrix;
    }
}
