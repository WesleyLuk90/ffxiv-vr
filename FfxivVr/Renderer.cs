using FFXIVClientStructs.FFXIV.Client.UI;
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
    private readonly Configuration configuration;

    internal Renderer(XR xr, VRSystem system, VRState vrState, Logger logger, VRSwapchains swapchains, Resources resources, VRShaders shaders, VRSpace vrSpace, Configuration configuration)
    {
        this.xr = xr;
        this.system = system;
        this.vrState = vrState;
        this.logger = logger;
        this.swapchains = swapchains;
        this.resources = resources;
        this.shaders = shaders;
        this.vrSpace = vrSpace;
        this.configuration = configuration;
    }


    private void RenderViewport(ID3D11DeviceContext* context, ID3D11ShaderResourceView* shaderResourceView, Matrix4X4<float> modelViewProjection)
    {
        resources.UpdateCamera(context, new CameraConstants(
            modelViewProjection: modelViewProjection
        ));
        resources.SetPixelShaderConstants(context, new PixelShaderConstants(
            mode: 0,
            color: new Vector4f(0, 0, 0, 1)));
        resources.SetSampler(context, shaderResourceView);
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
    internal View[] LocateView(long predictedDisplayTime)
    {
        var views = Native.CreateArray(new View(next: null), (uint)swapchains.Views.Count);
        var viewState = new ViewState(next: null);
        var viewLocateInfo = new ViewLocateInfo(
            viewConfigurationType: ViewConfigurationType.PrimaryStereo,
            displayTime: predictedDisplayTime,
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

    internal CompositionLayerProjectionView RenderEye(ID3D11DeviceContext* context, FrameState frameState, View[] views, Eye eye)
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
        fixed (float* ptr = new Span<float>([0f, 0f, 0f, 1]))
        {
            context->ClearRenderTargetView(currentColorSwapchainImage, ptr);
        }
        context->ClearDepthStencilView(currentDepthSwapchainImage, (uint)ClearFlag.Depth, 1.0f, 0);

        resources.SetDepthStencilState(context);
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

        var currentEyeRenderTarget = resources.eyeRenderTargets[eye.ToIndex()];
        resources.SetVRBlendState(context);
        if (eye == Eye.Left)
        {
            logger.Trace("Rendering left eye");
            RenderViewport(context, currentEyeRenderTarget.ShaderResourceView, Matrix4X4<float>.Identity);

            var translationMatrix = Matrix4X4.CreateTranslation(new Vector3D<float>(0.0f, 0.0f, -configuration.UIDistance));
            var modelViewProjection = Matrix4X4.Multiply(translationMatrix, viewProj);
            var gameRenderTexture = GameTextures.GetGameRenderTexture();
            resources.SetUIBlendState(context);

            context->CopyResource((ID3D11Resource*)resources.uiRenderTarget.Texture, (ID3D11Resource*)gameRenderTexture->D3D11Texture2D);

            RenderCursor(context, currentEyeRenderTarget, new Vector2D<float>(width, height));

            context->OMSetRenderTargets(1, ref currentColorSwapchainImage, currentDepthSwapchainImage);
            RenderViewport(context, resources.uiRenderTarget.ShaderResourceView, modelViewProjection);
        }
        else if (eye == Eye.Right)
        {
            logger.Trace("Rendering right eye");
            RenderViewport(context, currentEyeRenderTarget.ShaderResourceView, Matrix4X4<float>.Identity);

            var translationMatrix = Matrix4X4.CreateTranslation(new Vector3D<float>(0.0f, 0.0f, -configuration.UIDistance));
            var modelViewProjection = Matrix4X4.Multiply(translationMatrix, viewProj);
            resources.SetUIBlendState(context);
            RenderViewport(context, resources.uiRenderTarget!.ShaderResourceView, modelViewProjection);
        }

        var releaseInfo = new SwapchainImageReleaseInfo(next: null);
        xr.ReleaseSwapchainImage(swapchainView.ColorSwapchainInfo.Swapchain, ref releaseInfo).CheckResult("ReleaseSwapchainImage");
        xr.ReleaseSwapchainImage(swapchainView.DepthSwapchainInfo.Swapchain, ref releaseInfo).CheckResult("ReleaseSwapchainImage");
        return compositionLayerProjectionView;
    }

    private void RenderCursor(ID3D11DeviceContext* context, RenderTarget currentEyeRenderTarget, Vector2D<float> windowSize)
    {
        var cursorSize = 10f;
        var inputData = UIInputData.Instance();
        var position = new Vector3D<float>(
            inputData->CursorXPosition / windowSize.X * 2 - 1,
            (1 - inputData->CursorYPosition / windowSize.Y) * 2 - 1, 0
        );
        var target = resources.uiRenderTarget.RenderTargetView;
        context->OMSetRenderTargets(1, ref target, null);
        var scale = Matrix4X4.CreateScale(new Vector3D<float>(cursorSize / windowSize.X, cursorSize / windowSize.Y, 0f)) *
            Matrix4X4.CreateTranslation(position);
        resources.UpdateCamera(context, new CameraConstants(
            modelViewProjection: scale
        ));
        resources.SetPixelShaderConstants(context, new PixelShaderConstants(
            mode: 1,
            color: new Vector4f(1, 0, 0, 1f)));
        resources.Draw(context);
    }

    internal void EndFrame(ID3D11DeviceContext* context, FrameState frameState, View[] views, CompositionLayerProjectionView[] compositionLayerProjectionViews)
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
            var result = xr.EndFrame(system.Session, ref endFrameInfo);
            if (result == Result.ErrorSessionNotRunning)
            {
                return;
            }
            result.CheckResult("EndFrame");
        }
    }

    internal FrameState? StartFrame(ID3D11DeviceContext* context)
    {
        var frameWaitInfo = new FrameWaitInfo(next: null);
        var frameState = new FrameState(next: null);
        xr.WaitFrame(system.Session, ref frameWaitInfo, ref frameState).CheckResult("WaitFrame");

        var beginFrameInfo = new FrameBeginInfo(next: null);
        var result = xr.BeginFrame(system.Session, ref beginFrameInfo);
        if (result != Result.FrameDiscarded)
        {
            result.CheckResult("BeginFrame");
        }
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

        var gameViewMatrix = Matrix4X4.CreateScale(1f / configuration.WorldScale) * Matrix4X4.CreateRotationY<float>(yAngle) * Matrix4X4.CreateTranslation<float>(position);
        var vrViewMatrix = Matrix4X4.CreateFromQuaternion<float>(view.Pose.Orientation.ToQuaternion()) * Matrix4X4.CreateTranslation<float>(view.Pose.Position.ToVector3D());

        var viewMatrix = vrViewMatrix * gameViewMatrix;
        var invertedViewMatrix = Matrix4X4<float>.Identity;
        Matrix4X4.Invert(viewMatrix, out invertedViewMatrix);
        return invertedViewMatrix;
    }

    internal void CopyTexture(ID3D11DeviceContext* context, Eye eye)
    {
        var renderTexture = GameTextures.GetGameRenderTexture();

        logger.Trace($"Copy resource {eye} render target");
        var texture = resources.eyeRenderTargets[eye.ToIndex()].Texture;
        context->CopyResource((ID3D11Resource*)texture, (ID3D11Resource*)renderTexture->D3D11Texture2D);
    }
}
