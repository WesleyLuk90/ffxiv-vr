using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;

namespace FfxivVR
{
    unsafe internal class Renderer : IDisposable
    {
        private XR xr;
        private VRSystem system;
        private VRState vrState;
        private Logger logger;
        private readonly VRSwapchains swapchains;
        private readonly ID3D11DeviceContext* deviceContext;
        private Space localSpace = new Space();

        internal Renderer(XR xr, VRSystem system, VRState vrState, Logger logger, VRSwapchains swapchains, ID3D11DeviceContext* deviceContext)
        {
            this.xr = xr;
            this.system = system;
            this.vrState = vrState;
            this.logger = logger;
            this.swapchains = swapchains;
            this.deviceContext = deviceContext;
        }

        internal void Initialize()
        {
            CreateReferenceSpace();
        }


        private void CreateReferenceSpace()
        {
            var referenceSpace = new ReferenceSpaceCreateInfo(referenceSpaceType: ReferenceSpaceType.Local, poseInReferenceSpace: new Posef(orientation: new Quaternionf(0, 0, 0, 1), position: new Vector3f(0, 0, 0)));
            xr.CreateReferenceSpace(system.Session, ref referenceSpace, ref localSpace).CheckResult("CreateReferenceSpace");
        }

        internal void Render()
        {
            var frameWaitInfo = new FrameWaitInfo(next: null);
            var frameState = new FrameState(next: null);
            xr.WaitFrame(system.Session, ref frameWaitInfo, ref frameState).CheckResult("WaitFrame");

            var beginFrameInfo = new FrameBeginInfo(next: null);
            xr.BeginFrame(system.Session, ref beginFrameInfo).CheckResult("BeginFrame");

            CompositionLayerProjectionView[] compositionLayers = Array.Empty<CompositionLayerProjectionView>();
            if (vrState.IsActive() && frameState.ShouldRender != 0)
            {
                compositionLayers = InnerRender(frameState.PredictedDisplayTime);
            }
            if (compositionLayers.Length > 0)
            {
                fixed (CompositionLayerProjectionView* firstLayer = &compositionLayers[0])
                {
                    var layerProjection = new CompositionLayerProjection(
                        layerFlags: CompositionLayerFlags.BlendTextureSourceAlphaBit | CompositionLayerFlags.CorrectChromaticAberrationBit,
                        space: localSpace,
                        viewCount: (uint)compositionLayers.Length,
                        views: firstLayer
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
            else
            {
                var endFrameInfo = new FrameEndInfo(
                    displayTime: frameState.PredictedDisplayTime,
                    environmentBlendMode: EnvironmentBlendMode.Opaque,
                    layerCount: 0,
                    layers: null
                );
                xr.EndFrame(system.Session, ref endFrameInfo).CheckResult("EndFrame");
            }
        }

        private CompositionLayerProjectionView[] InnerRender(long predictedDisplayTime)
        {
            var views = Native.CreateArray(new View(next: null), (uint)swapchains.Views.Count);
            var viewState = new ViewState(next: null);
            var viewLocateInfo = new ViewLocateInfo(next: null, viewConfigurationType: VRSwapchains.ViewConfigType, displayTime: predictedDisplayTime, space: localSpace);
            uint viewCount = 0;
            xr.LocateView(system.Session, ref viewLocateInfo, ref viewState, ref viewCount, views).CheckResult("LocateView");

            if (viewCount != swapchains.Views.Count)
            {
                throw new Exception($"Unexpected view count, got {viewCount} but expected {swapchains.Views.Count}");
            }

            var layers = new CompositionLayerProjectionView[viewCount];
            for (int viewIndex = 0; viewIndex < viewCount; viewIndex++)
            {
                var swapchainView = swapchains.Views[viewIndex];
                var imageAcquireInfo = new SwapchainImageAcquireInfo(next: null);
                uint colorImageIndex = 0;
                xr.AcquireSwapchainImage(swapchainView.ColorSwapchainInfo.Swapchain, ref imageAcquireInfo, ref colorImageIndex).CheckResult("AcquireSwapchainImage");
                uint depthSwapchainInfo = 0;
                xr.AcquireSwapchainImage(swapchainView.DepthSwapchainInfo.Swapchain, ref imageAcquireInfo, ref depthSwapchainInfo).CheckResult("AcquireSwapchainImage");
                var waitInfo = new SwapchainImageWaitInfo(next: null);
                waitInfo.Timeout = 1000000000L;
                xr.WaitSwapchainImage(swapchainView.ColorSwapchainInfo.Swapchain, ref waitInfo).CheckResult("WaitSwapchainImage");
                xr.WaitSwapchainImage(swapchainView.DepthSwapchainInfo.Swapchain, ref waitInfo).CheckResult("WaitSwapchainImage");

                var width = (int)swapchainView.ViewConfigurationView.RecommendedImageRectWidth;
                var height = (int)swapchainView.ViewConfigurationView.RecommendedImageRectHeight;

                layers[viewIndex] = new CompositionLayerProjectionView(
                    pose: views[viewIndex].Pose,
                    fov: views[viewIndex].Fov,
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
                var color = new float[4] { 0.17f, 0.17f, 0.17f, 1 };
                fixed (float* p = &color[0])
                {
                    deviceContext->ClearRenderTargetView(swapchainView.ColorSwapchainInfo.Views[0], p);
                }
                deviceContext->ClearDepthStencilView(swapchainView.DepthSwapchainInfo.Views[0], (uint)ClearFlag.Depth, 1.0f, 0);

                deviceContext->OMSetRenderTargets(1, in swapchainView.ColorSwapchainInfo.Views[0], swapchainView.DepthSwapchainInfo.Views[0]);
                Viewport viewport = new Viewport(
                    topLeftX: 0f,
                    topLeftY: 0f,
                    width: width,
                    height: height,
                    minDepth: 0f,
                    maxDepth: 1f);
                var scissor = new Box2D<int>(
                    minX: 0, minY: 0, maxX: width, maxY: height);
                deviceContext->RSSetViewports(1, &viewport);
                deviceContext->RSSetScissorRects(1, &scissor);

                // Compute the view-projection transform.
                // All matrices (including OpenXR's) are column-major, right-handed.
                //XrMatrix4x4f proj;
                //XrMatrix4x4f_CreateProjectionFov(&proj, m_apiType, views[i].fov, nearZ, farZ);
                //XrMatrix4x4f toView;
                //XrVector3f scale1m{ 1.0f, 1.0f, 1.0f};
                //XrMatrix4x4f_CreateTranslationRotationScale(&toView, &views[i].pose.position, &views[i].pose.orientation, &scale1m);
                //XrMatrix4x4f view;
                //XrMatrix4x4f_InvertRigidBody(&view, &toView);
                //XrMatrix4x4f_Multiply(&cameraConstants.viewProj, &proj, &view);
                // XR_DOCS_TAG_END_SetupFrameRendering

                // XR_DOCS_TAG_BEGIN_CallRenderCuboid
                //renderCuboidIndex = 0;
                // Draw a floor. Scale it by 2 in the X and Z, and 0.1 in the Y,
                //RenderCuboid({ { 0.0f, 0.0f, 0.0f, 1.0f}, { 0.0f, -m_viewHeightM, 0.0f} }, { 2.0f, 0.1f, 2.0f}, { 0.4f, 0.5f, 0.5f});
                // Draw a "table".
                //RenderCuboid({ { 0.0f, 0.0f, 0.0f, 1.0f}, { 0.0f, -m_viewHeightM + 0.9f, -0.7f} }, { 1.0f, 0.2f, 1.0f}, { 0.6f, 0.6f, 0.4f});
                // XR_DOCS_TAG_END_CallRenderCuboid

                var releaseInfo = new SwapchainImageReleaseInfo(next: null);
                xr.ReleaseSwapchainImage(swapchainView.ColorSwapchainInfo.Swapchain, ref releaseInfo).CheckResult("ReleaseSwapchainImage");
                xr.ReleaseSwapchainImage(swapchainView.DepthSwapchainInfo.Swapchain, ref releaseInfo).CheckResult("ReleaseSwapchainImage");
            }
            return layers;
        }

        public void Dispose()
        {
            xr.DestroySpace(localSpace).LogResult("DestroySpace", logger);
        }
    }

}
