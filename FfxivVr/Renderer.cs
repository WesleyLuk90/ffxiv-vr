using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FfxivVR
{
    internal class Renderer
    {
        private XR xR;
        private VRSystem system;
        private VRState vrState;

        internal Renderer(XR xr, VRSystem system, VRState vrState)
        {
            this.xR = xr;
            this.system = system;
            this.vrState = vrState;
        }
        internal unsafe void Render()
        {
            var frameWaitInfo = new FrameWaitInfo();
            var frameState = new FrameState();
            xR.WaitFrame(system.Session, ref frameWaitInfo, ref frameState).CheckResult("WaitFrame");

            var beginFrameInfo = new FrameBeginInfo(next: null);
            xR.BeginFrame(system.Session, ref beginFrameInfo).CheckResult("BeginFrame");
            RenderLayerInfo renderLayerInfo = new RenderLayerInfo();
            if (vrState.IsActive() && frameState.ShouldRender != 0)
            {
                InnerRender(renderLayerInfo);
            }
            var layers = renderLayerInfo.Layers.ToArray();
            var endFrameInfo = new FrameEndInfo(
                displayTime: frameState.PredictedDisplayTime,
                environmentBlendMode: EnvironmentBlendMode.Opaque,
                layerCount: (uint)layers.Length,
                layers: (CompositionLayerBaseHeader**)&layers
            );
            xR.EndFrame(system.Session, ref endFrameInfo).CheckResult("EndFrame");
        }

        private void InnerRender(RenderLayerInfo renderLayerInfo)
        {
            throw new NotImplementedException();
        }
    }

    class RenderLayerInfo
    {
        internal List<CompositionLayerBaseHeader> Layers;
    }
}
