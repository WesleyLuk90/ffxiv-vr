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
        private readonly Resources resources;
        private readonly VRShaders shaders;
        private Space localSpace = new Space();

        internal Renderer(XR xr, VRSystem system, VRState vrState, Logger logger, VRSwapchains swapchains, Resources resources, VRShaders shaders)
        {
            this.xr = xr;
            this.system = system;
            this.vrState = vrState;
            this.logger = logger;
            this.swapchains = swapchains;
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

        internal void Render(ID3D11DeviceContext* context)
        {
            var frameWaitInfo = new FrameWaitInfo(next: null);
            var frameState = new FrameState(next: null);
            xr.WaitFrame(system.Session, ref frameWaitInfo, ref frameState).CheckResult("WaitFrame");

            var beginFrameInfo = new FrameBeginInfo(next: null);
            xr.BeginFrame(system.Session, ref beginFrameInfo).CheckResult("BeginFrame");
            CompositionLayerProjectionView[] compositionLayers = Array.Empty<CompositionLayerProjectionView>();
            if (vrState.IsActive() && frameState.ShouldRender != 0)
            {
                compositionLayers = InnerRender(context, frameState.PredictedDisplayTime);
            }
            if (compositionLayers.Length > 0)
            {
                var compositionLayerSpan = new Span<CompositionLayerProjectionView>(compositionLayers);
                fixed (CompositionLayerProjectionView* ptr = compositionLayerSpan)
                {
                    var layerProjection = new CompositionLayerProjection(
                        layerFlags: CompositionLayerFlags.BlendTextureSourceAlphaBit | CompositionLayerFlags.CorrectChromaticAberrationBit,
                        space: localSpace,
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

        private CompositionLayerProjectionView[] InnerRender(ID3D11DeviceContext* context, long predictedDisplayTime)
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
                RenderCube(context, viewProj);

                var releaseInfo = new SwapchainImageReleaseInfo(next: null);
                xr.ReleaseSwapchainImage(swapchainView.ColorSwapchainInfo.Swapchain, ref releaseInfo).CheckResult("ReleaseSwapchainImage");
                xr.ReleaseSwapchainImage(swapchainView.DepthSwapchainInfo.Swapchain, ref releaseInfo).CheckResult("ReleaseSwapchainImage");
            }
            return layers;
        }
        private void RenderCube(ID3D11DeviceContext* context, Matrix4X4<float> viewProjection)
        {
            var translation = Matrix4X4.CreateTranslation(new Vector3D<float>(0.3f, 0.3f, -0.5f));
            var modelViewProjection = Matrix4X4.Multiply(translation, viewProjection);
            resources.UpdateCamera(context, new CameraConstants(
                modelViewProjection: modelViewProjection
            ));
            resources.Draw(context);
            translation = Matrix4X4.CreateTranslation(new Vector3D<float>(-0.3f, -0.3f, -1f));
            modelViewProjection = Matrix4X4.Multiply(translation, viewProjection);
            resources.UpdateCamera(context, new CameraConstants(
                modelViewProjection: modelViewProjection
            ));
            resources.Draw(context);
        }


        public void Dispose()
        {
            xr.DestroySpace(localSpace).LogResult("DestroySpace", logger);
        }
    }

}
