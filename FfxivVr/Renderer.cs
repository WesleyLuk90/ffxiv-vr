using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Lumina.Data.Parsing.Layer.LayerCommon;

namespace FfxivVR
{
    unsafe internal class Renderer : IDisposable
    {
        private XR xr;
        private VRSystem system;
        private VRState vrState;
        private Logger logger;
        private List<ViewConfigurationView> viewConfigurationViews;
        private Space localSpace = new Space();
        private const ViewConfigurationType ViewConfigType = ViewConfigurationType.PrimaryStereo;

        internal Renderer(XR xr, VRSystem system, VRState vrState, Logger logger)
        {
            this.xr = xr;
            this.system = system;
            this.vrState = vrState;
            this.logger = logger;
        }

        internal void Initialize()
        {
            uint count = 0;
            xr.EnumerateViewConfigurationView(system.Instance, system.SystemId, 0, ref count, null).CheckResult("EnumerateViewConfigurationView");
            var views = Native.CreateArray(new ViewConfigurationView(next: null), count);
            xr.EnumerateViewConfigurationView(system.Instance, system.SystemId, ViewConfigType, count, ref count, ref views[0]).CheckResult("EnumerateViewConfigurationView");
            this.viewConfigurationViews = views.ToList();
            logger.Debug($"Got {views.Length} views");

            var referenceSpace = new ReferenceSpaceCreateInfo(referenceSpaceType: ReferenceSpaceType.Local, poseInReferenceSpace: new Posef(orientation: new Quaternionf(0, 0, 0, 1), position: new Vector3f(0, 0, 0)));
            xr.CreateReferenceSpace(system.Session, ref referenceSpace, ref localSpace).CheckResult("CreateReferenceSpace");
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
            var views = Native.CreateArray(new View(), (uint)viewConfigurationViews.Count());
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
        }
    }

}
