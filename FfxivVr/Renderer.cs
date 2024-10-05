using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using static FfxivVR.Resources;

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
        private readonly Resources resources;
        private readonly VRShaders shaders;
        private Space localSpace = new Space();

        internal Renderer(XR xr, VRSystem system, VRState vrState, Logger logger, VRSwapchains swapchains, ID3D11DeviceContext* deviceContext, Resources resources, VRShaders shaders)
        {
            this.xr = xr;
            this.system = system;
            this.vrState = vrState;
            this.logger = logger;
            this.swapchains = swapchains;
            this.deviceContext = deviceContext;
            this.resources = resources;
            this.shaders = shaders;
        }

        internal void Initialize()
        {
            CreateReferenceSpace();
        }


        private void CreateReferenceSpace()
        {
            var referenceSpace = new ReferenceSpaceCreateInfo(
                referenceSpaceType: ReferenceSpaceType.Local,
                poseInReferenceSpace: new Posef(orientation: new Quaternionf(0, 0, 0, 1), position: new Vector3f(0, 0, 0)));
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

        bool runOnce = true;
        private CompositionLayerProjectionView[] InnerRender(long predictedDisplayTime)
        {
            var views = Native.CreateArray(new View(next: null), (uint)swapchains.Views.Count);
            var viewState = new ViewState(next: null);
            var viewLocateInfo = new ViewLocateInfo(
                viewConfigurationType: ViewConfigurationType.PrimaryStereo,
                displayTime: predictedDisplayTime,
                space: localSpace
            );
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
                var view = views[viewIndex];
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

                layers[viewIndex] = new CompositionLayerProjectionView(
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
                var color = new float[4] { 0.17f, 0.17f, 0.17f, 1 };
                fixed (float* p = &color[0])
                {
                    deviceContext->ClearRenderTargetView(currentColorSwapchainImage, p);
                }
                deviceContext->ClearDepthStencilView(currentDepthSwapchainImage, (uint)ClearFlag.Depth, 1.0f, 0);

                deviceContext->OMSetRenderTargets(1, in currentColorSwapchainImage, currentDepthSwapchainImage);
                Viewport viewport = new Viewport(
                    topLeftX: 0f,
                    topLeftY: 0f,
                    width: width,
                    height: height,
                    minDepth: 0f,
                    maxDepth: 1f);
                deviceContext->RSSetViewports(1, &viewport);

                var rotation = Matrix4X4.CreateFromQuaternion<float>(view.Pose.Orientation.ToQuaternion());
                var translation = Matrix4X4.CreateTranslation<float>(view.Pose.Position.ToVector3D());
                var toView = Matrix4X4.Multiply(rotation, translation);
                var viewInverted = new Matrix4X4<float>();
                Matrix4X4.Invert(toView, out viewInverted);

                var horizontal = view.Fov.AngleRight - view.Fov.AngleLeft;
                //var vertical = view.Fov.AngleUp - view.Fov.AngleDown;
                var proj = Matrix4X4.CreatePerspectiveFieldOfView<float>(horizontal, ((float)width) / height, nearPlaneDistance: 0.05f, farPlaneDistance: 100f);
                var viewProj = Matrix4X4.Multiply(viewInverted, proj);

                RenderCube(viewProj);

                var releaseInfo = new SwapchainImageReleaseInfo(next: null);
                xr.ReleaseSwapchainImage(swapchainView.ColorSwapchainInfo.Swapchain, ref releaseInfo).CheckResult("ReleaseSwapchainImage");
                xr.ReleaseSwapchainImage(swapchainView.DepthSwapchainInfo.Swapchain, ref releaseInfo).CheckResult("ReleaseSwapchainImage");
            }
            return layers;
        }
        private void RenderCube(Matrix4X4<float> viewProjection)
        {
            var translation = Matrix4X4.CreateTranslation(new Vector3D<float>(0.3f, 0.3f, -0.5f));
            var modelViewProjection = Matrix4X4.Multiply(translation, viewProjection);
            //var temp = new Matrix4X4<float>();
            //Matrix4X4.Invert(translation, out temp);
            resources.UpdateCamera(new CameraConstants(
                modelViewProjection: modelViewProjection
            ));
            resources.Draw();
            translation = Matrix4X4.CreateTranslation(new Vector3D<float>(-0.3f, -0.3f, -1f));
            modelViewProjection = Matrix4X4.Multiply(translation, viewProjection);
            resources.UpdateCamera(new CameraConstants(
                modelViewProjection: modelViewProjection
            ));
            resources.Draw();
        }


        public void Dispose()
        {
            xr.DestroySpace(localSpace).LogResult("DestroySpace", logger);
        }
    }

}
