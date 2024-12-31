using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using static FfxivVR.Resources;

namespace FfxivVR;

public unsafe class Renderer(
    XR xr,
    VRSystem system,
    Logger logger,
    VRSwapchains swapchains,
    Resources resources,
    VRShaders shaders,
    VRSpace vrSpace,
    Configuration configuration,
    DalamudRenderer dalamudRenderer,
    VRCamera vrCamera,
    ResolutionManager resolutionManager,
    GameState gameState,
    VRUI vrUI)
{
    private void RenderViewport(ID3D11DeviceContext* context, ID3D11ShaderResourceView* shaderResourceView, Matrix4X4<float> modelViewProjection, bool invertAlpha = false, float fade = 0)
    {
        resources.UpdateCamera(context, new CameraConstants(
            modelViewProjection: modelViewProjection
        ));
        resources.SetPixelShaderConstants(context, new PixelShaderConstants(
            mode: invertAlpha ? ShaderMode.InvertedAlpha : ShaderMode.Texture,
            gamma: configuration.Gamma,
            color: new Vector4D<float>(1 - fade, 1 - fade, 1 - fade, 1)));
        resources.SetSampler(context, shaderResourceView);
        resources.DrawSquare(context);
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

    internal CompositionLayerProjectionView RenderEye(ID3D11DeviceContext* context, EyeRender vrView, Line? aim)
    {
        var swapchainView = swapchains.Views[vrView.Eye.ToIndex()];
        CheckResolution(swapchainView);
        var view = vrView.View;
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
        resources.DisableDepthStencil(context);

        if (vrView.Eye == Eye.Left)
        {
            logger.Trace("Rendering UI");
            RenderUITexture(context, width, height);
        }

        logger.Trace("Rendering Game");
        resources.SetSceneBlendState(context);
        var depthTarget = resources.SceneDepthTargets[vrView.Eye.ToIndex()];
        context->OMSetRenderTargets(1, ref currentColorSwapchainImage, depthTarget.DepthStencilView);

        var currentEyeRenderTarget = resources.SceneRenderTargets[vrView.Eye.ToIndex()];
        RenderViewport(context, currentEyeRenderTarget.ShaderResourceView, Matrix4X4<float>.Identity, fade: gameState.GetFade());
        resources.SetCompositingBlendState(context);
        if (ShouldUseDepthTexture())
        {
            resources.EnableDepthStencil(context);
        }

        RenderUI(context, vrViewProjectionMatrix);
        if (aim != null)
        {
            RenderRay(context, 0.001f, aim.Start, aim.End, vrViewProjectionMatrix);
        }

        xr.ReleaseSwapchainImage(swapchainView.ColorSwapchainInfo.Swapchain, null).CheckResult("ReleaseSwapchainImage");
        xr.ReleaseSwapchainImage(swapchainView.DepthSwapchainInfo.Swapchain, null).CheckResult("ReleaseSwapchainImage");
        return compositionLayerProjectionView;
    }

    private bool resolutionErrorChecked = false;
    private void CheckResolution(SwapchainView swapchainView)
    {
        if (resolutionErrorChecked)
        {
            return;
        }
        var width = swapchainView.ViewConfigurationView.RecommendedImageRectWidth;
        var height = swapchainView.ViewConfigurationView.RecommendedImageRectHeight;

        var render = GameTextures.GetGameRenderTexture();
        if (render->ActualWidth != width || render->ActualHeight != height)
        {
            logger.Error($"Unexpected window size, expected {width}x{height} but got {render->ActualWidth}x{render->ActualHeight}");
            logger.Error($"If you resized the window, please restart VR");
            resolutionErrorChecked = true;
        }
    }
    private void RenderUI(ID3D11DeviceContext* context, Matrix4X4<float> viewProj)
    {
        var matrix = vrUI.GetTransformationMatrix() * viewProj;
        resources.SetCompositingBlendState(context);
        RenderViewport(context, resources.UIRenderTarget.ShaderResourceView, matrix, false);
        RenderViewport(context, resources.DalamudRenderTarget.ShaderResourceView, resolutionManager.GetDalamudScale() * matrix, true);
        RenderViewport(context, resources.CursorRenderTarget.ShaderResourceView, matrix, false);
    }

    private void RenderUITexture(ID3D11DeviceContext* context, int width, int height)
    {
        // The render texture only has the UI at this point so make a copy
        var gameRenderTexture = GameTextures.GetGameRenderTexture();
        Box box = ComputeCopyBox(gameRenderTexture, resources.UIRenderTarget);
        context->CopySubresourceRegion((ID3D11Resource*)resources.UIRenderTarget.Texture, 0, 0, 0, 0, (ID3D11Resource*)gameRenderTexture->D3D11Texture2D, 0, ref box);

        resources.SetUIBlendState(context);
        var color = new float[] { 0f, 0f, 0f, 1f };
        context->ClearRenderTargetView(resources.DalamudRenderTarget.RenderTargetView, ref color[0]);
        dalamudRenderer.Render(resources.DalamudRenderTarget.RenderTargetView);

        RenderCursor(context, new Vector2D<float>(width, height));
    }

    private Box ComputeCopyBox(FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Texture* gameRenderTexture, RenderTarget renderTarget)
    {
        return new Box(0, 0, 0, Math.Min(gameRenderTexture->ActualWidth, renderTarget.Size.X), Math.Min(gameRenderTexture->ActualHeight, renderTarget.Size.Y), 1);
    }

    private void RenderCursor(ID3D11DeviceContext* context, Vector2D<float> windowSize)
    {
        var target = resources.CursorRenderTarget.RenderTargetView;
        var color = new float[] { 0f, 0f, 0f, 0f };
        context->ClearRenderTargetView(target, ref color[0]);
        context->OMSetRenderTargets(1, ref target, null);

        var maybeCursor = GameTextures.GetCursorTexture();
        if (maybeCursor is Cursor cursor && cursor.Visible)
        {
            var inputData = UIInputData.Instance();
            var mouseX = inputData->CursorInputs.PositionX;
            var mouseY = inputData->CursorInputs.PositionY;
            var position = new Vector3D<float>(
                mouseX / windowSize.X * 2 - 1,
                // All the cursors are offset about 10 pixels
                (1 - (mouseY + 10) / windowSize.Y) * 2 - 1, 0
            );
            var scale = Matrix4X4.CreateScale(new Vector3D<float>(maybeCursor.Width / windowSize.X, maybeCursor.Height / windowSize.Y, 0f)) *
                Matrix4X4.CreateTranslation(position);
            resources.UpdateCamera(context, new CameraConstants(
                modelViewProjection: scale
            ));
            resources.SetStandardBlendState(context);
            resources.SetSampler(context, maybeCursor.ShaderResourceView);
            resources.SetPixelShaderConstants(context, new PixelShaderConstants(
                mode: ShaderMode.Texture,
                gamma: 1f,
                color: new Vector4D<float>(1, 1, 1, 1f),
                uvOffset: maybeCursor.UvOffset,
                uvScale: maybeCursor.UvScale));
            resources.DrawSquare(context);
        }
    }

    private void RenderPoint(ID3D11DeviceContext* context, float size, Vector3D<float> position, Matrix4X4<float> viewProjectionMatrix)
    {
        var scale = Matrix4X4.CreateScale(size) * Matrix4X4.CreateTranslation(position) * viewProjectionMatrix;
        resources.UpdateCamera(context, new CameraConstants(
            modelViewProjection: scale

        ));
        resources.SetPixelShaderConstants(context, new PixelShaderConstants(
            mode: ShaderMode.Circle,
            gamma: 1f,
            color: new Vector4D<float>(0, 1, 0, 1f)));
        resources.DrawSquare(context);
    }
    private void RenderRay(ID3D11DeviceContext* context, float size, Vector3D<float> start, Vector3D<float> end, Matrix4X4<float> viewProjectionMatrix)
    {
        var rotation = MathFactory.RotateOnto(
            Vector3D<float>.UnitY,
            end - start
        );
        var scale = Matrix4X4.CreateTranslation(0, 1f, 0)
        * Matrix4X4.CreateScale(size, (end - start).Length / 2, size)
        * Matrix4X4.CreateFromQuaternion(rotation)
        * Matrix4X4.CreateTranslation(start)
        * viewProjectionMatrix;
        resources.UpdateCamera(context, new CameraConstants(
            modelViewProjection: scale
        ));
        resources.SetPixelShaderConstants(context, new PixelShaderConstants(
            mode: ShaderMode.Fill,
            gamma: 1f,
            color: new Vector4D<float>(1, 1, 1, 0.7f)));
        resources.DrawCylinder(context);
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
    internal void StartFrame()
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
        var renderTarget = resources.SceneRenderTargets[eye.ToIndex()];
        var box = ComputeCopyBox(renderTexture, renderTarget);
        context->CopySubresourceRegion((ID3D11Resource*)renderTarget.Texture, 0, 0, 0, 0, (ID3D11Resource*)renderTexture->D3D11Texture2D, 0, ref box);

        if (ShouldUseDepthTexture())
        {
            var depthTexture = GameTextures.GetGameDepthTexture();
            var depthTarget = resources.SceneDepthTargets[eye.ToIndex()];
            context->CopyResource((ID3D11Resource*)depthTarget.Texture, (ID3D11Resource*)depthTexture->D3D11Texture2D);
        }

    }

    private bool ShouldUseDepthTexture()
    {
        return !configuration.KeepUIInFront && !Conditions.IsOccupiedInCutSceneEvent;
    }
}