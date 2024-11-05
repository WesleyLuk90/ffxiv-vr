using FFXIVClientStructs.FFXIV.Client.UI;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using static FfxivVR.Resources;

namespace FfxivVR;

unsafe internal class Renderer(
    XR xr,
    VRSystem system,
    VRState vrState,
    Logger logger,
    VRSwapchains swapchains,
    Resources resources,
    VRShaders shaders,
    VRSpace vrSpace,
    Configuration configuration,
    DalamudRenderer dalamudRenderer,
    VRCamera vrCamera,
    VRDiagnostics diagnostics)
{
    private readonly XR xr = xr;
    private readonly VRSystem system = system;
    private readonly VRState vrState = vrState;
    private readonly Logger logger = logger;
    private readonly VRSwapchains swapchains = swapchains;
    private readonly Resources resources = resources;
    private readonly VRShaders shaders = shaders;
    private readonly VRSpace vrSpace = vrSpace;
    private readonly Configuration configuration = configuration;
    private readonly DalamudRenderer dalamudRenderer = dalamudRenderer;
    private readonly VRCamera vrCamera = vrCamera;
    private readonly VRDiagnostics diagnostics = diagnostics;

    private void RenderViewport(ID3D11DeviceContext* context, ID3D11ShaderResourceView* shaderResourceView, Matrix4X4<float> modelViewProjection, bool invertAlpha = false)
    {
        resources.UpdateCamera(context, new CameraConstants(
            modelViewProjection: modelViewProjection
        ));
        resources.SetPixelShaderConstants(context, new PixelShaderConstants(
            mode: invertAlpha ? 2 : 0,
            gamma: configuration.Gamma,
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

    private long lastLocateDelay = 0;
    private long? lastLocateView = null;
    internal View[] LocateView(long predictedDisplayTime)
    {
        var views = Native.CreateArray(new View(next: null), (uint)swapchains.Views.Count);
        var viewState = new ViewState(next: null);
        var viewLocateInfo = new ViewLocateInfo(
            viewConfigurationType: ViewConfigurationType.PrimaryStereo,
            displayTime: predictedDisplayTime + lastLocateDelay,
            space: vrSpace.LocalSpace
        );
        lastLocateView = predictedDisplayTime;
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
        var color = new float[] { 0f, 0f, 0f, 1f };
        context->ClearRenderTargetView(currentColorSwapchainImage, ref color[0]);
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

        var vrViewProjectionMatrix = vrCamera.ComputeVRViewProjectionMatrix(view);

        shaders.SetShaders(context);

        var currentEyeRenderTarget = resources.SceneRenderTargets[eye.ToIndex()];
        if (eye == Eye.Left)
        {
            logger.Trace("Rendering left eye");
            resources.SetSceneBlendState(context);
            RenderViewport(context, currentEyeRenderTarget.ShaderResourceView, Matrix4X4<float>.Identity);

            RenderUITexture(context, width, height);

            context->OMSetRenderTargets(1, ref currentColorSwapchainImage, currentDepthSwapchainImage);

            RenderUI(context, vrViewProjectionMatrix);
        }
        else if (eye == Eye.Right)
        {
            logger.Trace("Rendering right eye");
            resources.SetSceneBlendState(context);
            RenderViewport(context, currentEyeRenderTarget.ShaderResourceView, Matrix4X4<float>.Identity);

            RenderUI(context, vrViewProjectionMatrix);
        }

        xr.ReleaseSwapchainImage(swapchainView.ColorSwapchainInfo.Swapchain, null).CheckResult("ReleaseSwapchainImage");
        xr.ReleaseSwapchainImage(swapchainView.DepthSwapchainInfo.Swapchain, null).CheckResult("ReleaseSwapchainImage");
        diagnostics.OnRender(eye);
        return compositionLayerProjectionView;
    }

    private void RenderUI(ID3D11DeviceContext* context, Matrix4X4<float> viewProj)
    {
        var translationMatrix = Matrix4X4.CreateTranslation(new Vector3D<float>(0.0f, 0.0f, -configuration.UIDistance));
        var modelViewProjection = Matrix4X4.Multiply(translationMatrix, viewProj);

        resources.SetCompositingBlendState(context);
        RenderViewport(context, resources.UIRenderTarget.ShaderResourceView, modelViewProjection, false);
        RenderViewport(context, resources.DalamudRenderTarget.ShaderResourceView, modelViewProjection, true);
        RenderViewport(context, resources.CursorRenderTarget.ShaderResourceView, modelViewProjection, false);
    }

    private void RenderUITexture(ID3D11DeviceContext* context, int width, int height)
    {
        var gameRenderTexture = GameTextures.GetGameRenderTexture();
        context->CopyResource((ID3D11Resource*)resources.UIRenderTarget.Texture, (ID3D11Resource*)gameRenderTexture->D3D11Texture2D);

        resources.SetUIBlendState(context);
        var color = new float[] { 0f, 0f, 0f, 1f };
        context->ClearRenderTargetView(resources.DalamudRenderTarget.RenderTargetView, ref color[0]);
        dalamudRenderer.Render(resources.DalamudRenderTarget.RenderTargetView);

        RenderCursor(context, new Vector2D<float>(width, height));
    }

    private void RenderCursor(ID3D11DeviceContext* context, Vector2D<float> windowSize)
    {
        var cursorSize = 10f;
        var inputData = UIInputData.Instance();
        var position = new Vector3D<float>(
            inputData->CursorXPosition / windowSize.X * 2 - 1,
            (1 - inputData->CursorYPosition / windowSize.Y) * 2 - 1, 0
        );
        var target = resources.CursorRenderTarget.RenderTargetView;
        var color = new float[] { 0f, 0f, 0f, 0f };
        context->ClearRenderTargetView(target, ref color[0]);
        context->OMSetRenderTargets(1, ref target, null);
        var scale = Matrix4X4.CreateScale(new Vector3D<float>(cursorSize / windowSize.X, cursorSize / windowSize.Y, 0f)) *
            Matrix4X4.CreateTranslation(position);
        resources.UpdateCamera(context, new CameraConstants(
            modelViewProjection: scale
        ));
        resources.SetPixelShaderConstants(context, new PixelShaderConstants(
            mode: 1,
            gamma: 1f,
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
    internal void StartFrame(ID3D11DeviceContext* context)
    {
        var beginFrameInfo = new FrameBeginInfo(next: null);
        var result = xr.BeginFrame(system.Session, ref beginFrameInfo);
        if (result != Result.FrameDiscarded)
        {
            result.CheckResult("BeginFrame");
        }
    }

    internal void CopyTexture(ID3D11DeviceContext* context, Eye eye)
    {
        var renderTexture = GameTextures.GetGameRenderTexture();

        logger.Trace($"Copy resource {eye} render target");
        var texture = resources.SceneRenderTargets[eye.ToIndex()].Texture;
        context->CopyResource((ID3D11Resource*)texture, (ID3D11Resource*)renderTexture->D3D11Texture2D);
        diagnostics.OnCopy(eye);
    }
}
