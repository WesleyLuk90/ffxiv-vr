using Silk.NET.Direct3D11;
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

            var endFrameInfo = new FrameEndInfo(
                displayTime: frameState.PredictedDisplayTime,
                environmentBlendMode: EnvironmentBlendMode.Opaque,
                layerCount: 0,
                layers: null
            );
            xr.EndFrame(system.Session, ref endFrameInfo).CheckResult("EndFrame");
            //CompositionLayerProjectionView[] compositionLayers = new CompositionLayerProjectionView[] { };
            //if (vrState.IsActive() && frameState.ShouldRender != 0)
            //{
            //    compositionLayers = InnerRender(frameState.PredictedDisplayTime);
            //}
            //if (compositionLayers.Length > 0 || false)
            //{
            //    fixed (CompositionLayerProjectionView* firstLayer = &compositionLayers[0])
            //    {
            //        CompositionLayerBaseHeader*[] layerPointers = new CompositionLayerBaseHeader*[compositionLayers.Length];
            //        for (int i = 0; i < compositionLayers.Length; i++)
            //        {
            //            CompositionLayerProjectionView* viewPointer = &firstLayer[i];
            //            layerPointers[i] = (CompositionLayerBaseHeader*)viewPointer;
            //        }
            //        fixed (CompositionLayerBaseHeader** layersListPointer = &layerPointers[0])
            //        {
            //            var endFrameInfo = new FrameEndInfo(
            //                displayTime: frameState.PredictedDisplayTime,
            //                environmentBlendMode: EnvironmentBlendMode.Opaque,
            //                layerCount: (uint)compositionLayers.Length,
            //                layers: layersListPointer
            //            );
            //            xr.EndFrame(system.Session, ref endFrameInfo).CheckResult("EndFrame");
            //        }
            //    }
            //}
            //else
            //{
            //    var endFrameInfo = new FrameEndInfo(
            //        displayTime: frameState.PredictedDisplayTime,
            //        environmentBlendMode: EnvironmentBlendMode.Opaque,
            //        layerCount: 0,
            //        layers: null
            //    );
            //    xr.EndFrame(system.Session, ref endFrameInfo).CheckResult("EndFrame");
            //}
        }

        private CompositionLayerProjectionView[] InnerRender(long predictedDisplayTime)
        {
            var views = Native.CreateArray(new View(next: null), (uint)swapchains.Views.Count);
            var viewState = new ViewState(next: null);
            var viewLocateInfo = new ViewLocateInfo(next: null, viewConfigurationType: VRSwapchains.ViewConfigType, displayTime: predictedDisplayTime, space: localSpace);
            uint viewCount = 0;
            xr.LocateView(system.Session, ref viewLocateInfo, ref viewState, ref viewCount, views).CheckResult("LocateView");
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
                var color = new float[] { 0.17f, 0.17f, 0.17f, 1 };
                deviceContext->ClearRenderTargetView(swapchainView.ColorSwapchainInfo.Views[0], ref color[0]);
                deviceContext->ClearDepthStencilView(swapchainView.DepthSwapchainInfo.Views[0], (uint)ClearFlag.Depth, 1.0f, 0);
            }
            return layers;
        }

        public void Dispose()
        {
            xr.DestroySpace(localSpace).LogResult("DestroySpace", logger);
        }
    }

}
