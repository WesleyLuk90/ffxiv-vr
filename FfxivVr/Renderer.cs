using Silk.NET.DXGI;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static FfxivVR.Renderer;
using static Lumina.Data.Parsing.Layer.LayerCommon;

namespace FfxivVR
{
    unsafe internal class Renderer : IDisposable
    {
        private XR xr;
        private VRSystem system;
        private VRState vrState;
        private Logger logger;
        //private List<ViewConfigurationView>? viewConfigurationViews;
        private List<View>? views;
        private Space localSpace = new Space();
        private const ViewConfigurationType ViewConfigType = ViewConfigurationType.PrimaryStereo;

        private List<Format> ColorFormats = new List<Format>() {
            Format.FormatR8G8B8A8Unorm,
            Format.FormatB8G8R8A8Unorm,
            Format.FormatR8G8B8A8UnormSrgb,
            Format.FormatB8G8R8A8UnormSrgb,
        };
        private List<Format> DepthFormats = new List<Format>() {
            Format.FormatD32Float,
            Format.FormatD16Unorm,
        };

        internal Renderer(XR xr, VRSystem system, VRState vrState, Logger logger)
        {
            this.xr = xr;
            this.system = system;
            this.vrState = vrState;
            this.logger = logger;
        }

        internal void Initialize()
        {
            CreateViews();
            CreateReferenceSpace();
        }

        class SwapchainInfo
        {
            internal Swapchain swapchain;
            internal long swapchainFormat;
            internal List<IntPtr> imageViews;

            internal SwapchainInfo(long swapchainFormat, List<IntPtr> imageViews, Swapchain swapchain = new Swapchain())
            {
                this.swapchain = swapchain;
                this.swapchainFormat = swapchainFormat;
                this.imageViews = imageViews;
            }
        }
        class View
        {
            ViewConfigurationView viewConfigurationView;
            SwapchainInfo colorSwapchainInfo;
            SwapchainInfo depthSwapchainInfo;

            internal View(ViewConfigurationView viewConfigurationView, SwapchainInfo colorSwapchainInfo, SwapchainInfo depthSwapchainInfo)
            {
                this.viewConfigurationView = viewConfigurationView;
                this.colorSwapchainInfo = colorSwapchainInfo;
                this.depthSwapchainInfo = depthSwapchainInfo;
            }
        }

        private void CreateReferenceSpace()
        {
            var referenceSpace = new ReferenceSpaceCreateInfo(referenceSpaceType: ReferenceSpaceType.Local, poseInReferenceSpace: new Posef(orientation: new Quaternionf(0, 0, 0, 1), position: new Vector3f(0, 0, 0)));
            xr.CreateReferenceSpace(system.Session, ref referenceSpace, ref localSpace).CheckResult("CreateReferenceSpace");
        }

        private void CreateViews()
        {
            var viewConfigurationViews = xr.GetViewConfigurationViews(system.Instance, system.SystemId, ViewConfigType);
            logger.Debug($"Got {viewConfigurationViews.Count} views");

            var formats = xr.GetSwapchainFormats(system.Session);
            var colorFormat = formats.Where(f => ColorFormats.Contains((Format)f)).First();
            var depthFormat = formats.Where(f => DepthFormats.Contains((Format)f)).First();

            views = viewConfigurationViews.ConvertAll(viewConfigurationView =>
            {
                var colorSwapchainInfo = CreateSwapchainInfo(viewConfigurationView, colorFormat, SwapchainUsageFlags.SampledBit | SwapchainUsageFlags.ColorAttachmentBit);
                var depthSwapchainInfo = CreateSwapchainInfo(viewConfigurationView, depthFormat, SwapchainUsageFlags.SampledBit | SwapchainUsageFlags.DepthStencilAttachmentBit);

                return new View(
                    viewConfigurationView: viewConfigurationView,
                    colorSwapchainInfo: colorSwapchainInfo,
                    depthSwapchainInfo: depthSwapchainInfo
                );
            });
        }

        private SwapchainInfo CreateSwapchainInfo(ViewConfigurationView viewConfigurationView, long colorFormat, SwapchainUsageFlags usageFlags)
        {
            var colorSwapchainInfo = new SwapchainInfo(swapchainFormat: colorFormat, new List<IntPtr>());
            var colorSwapchainCreateInfo = new SwapchainCreateInfo(
                createFlags: 0,
                usageFlags: usageFlags,
                format: colorFormat,
                sampleCount: viewConfigurationView.RecommendedSwapchainSampleCount,
                width: viewConfigurationView.RecommendedImageRectWidth,
                height: viewConfigurationView.RecommendedImageRectHeight,
                faceCount: 1,
                arraySize: 1,
                mipCount: 1
            );
            xr.CreateSwapchain(system.Session, ref colorSwapchainCreateInfo, ref colorSwapchainInfo.swapchain);

            var images = xr.GetSwapchainImages(colorSwapchainInfo.swapchain);

            return colorSwapchainInfo;
        }

        internal void Render()
        {
            var frameWaitInfo = new FrameWaitInfo();
            var frameState = new FrameState();
            xr.WaitFrame(system.Session, ref frameWaitInfo, ref frameState).CheckResult("WaitFrame");

            var beginFrameInfo = new FrameBeginInfo(next: null);
            xr.BeginFrame(system.Session, ref beginFrameInfo).CheckResult("BeginFrame");
            CompositionLayerProjectionView[] compositionLayers = new CompositionLayerProjectionView[] { };
            if (vrState.IsActive() && frameState.ShouldRender != 0)
            {
                compositionLayers = InnerRender(frameState.PredictedDisplayTime);
            }
            fixed (CompositionLayerProjectionView* firstLayer = &compositionLayers[0])
            {
                CompositionLayerBaseHeader*[] layerPointers = new CompositionLayerBaseHeader*[compositionLayers.Length];
                for (int i = 0; i < compositionLayers.Length; i++)
                {
                    CompositionLayerProjectionView* viewPointer = &firstLayer[i];
                    layerPointers[i] = (CompositionLayerBaseHeader*)viewPointer;
                }
                fixed (CompositionLayerBaseHeader** layersListPointer = &layerPointers[0])
                {
                    var endFrameInfo = new FrameEndInfo(
                        displayTime: frameState.PredictedDisplayTime,
                    environmentBlendMode: EnvironmentBlendMode.Opaque,
                        layerCount: (uint)compositionLayers.Length,
                        layers: layersListPointer
                    );
                    xr.EndFrame(system.Session, ref endFrameInfo).CheckResult("EndFrame");
                }
            }
        }

        private CompositionLayerProjectionView[] InnerRender(long predictedDisplayTime)
        {
            var views = Native.CreateArray(new View(), (uint)viewConfigurationViews!.Count());
            var viewState = new ViewState(next: null);
            var viewLocateInfo = new ViewLocateInfo(next: null, viewConfigurationType: ViewConfigType, displayTime: predictedDisplayTime, space: localSpace);
            uint viewCount = 0;
            xr.LocateView(system.Session, ref viewLocateInfo, ref viewState, ref viewCount, views).CheckResult("LocateView");

            var layers = new CompositionLayerProjectionView[viewCount];
            for (int viewIndex = 0; viewIndex < viewCount; viewIndex++)
            {

            }
            return layers;
        }

        public void Dispose()
        {
            xr.DestroySpace(localSpace).CheckResult("DestroySpace");
            DestoryViews();
        }

        private void DestoryViews()
        {
            throw new NotImplementedException();
        }
    }

}
