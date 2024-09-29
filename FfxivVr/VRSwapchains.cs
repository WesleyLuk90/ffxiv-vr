using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Linq;
namespace FfxivVR;
unsafe internal class VRSwapchains : IDisposable
{
    private static List<Format> ColorFormats = new List<Format>() {
        Format.FormatR8G8B8A8Unorm,
        Format.FormatB8G8R8A8Unorm,
        Format.FormatR8G8B8A8UnormSrgb,
        Format.FormatB8G8R8A8UnormSrgb,
    };
    private static List<Format> DepthFormats = new List<Format>() {
        Format.FormatD32Float,
        Format.FormatD16Unorm,
    };
    private readonly XR xr;
    private readonly VRSystem system;
    private readonly Logger logger;
    private readonly ID3D11Device* d11Device;
    public const ViewConfigurationType ViewConfigType = ViewConfigurationType.PrimaryStereo;
    public List<SwapchainView> Views = null!;
    public VRSwapchains(XR xr, VRSystem system, Logger logger, ID3D11Device* d11Device)
    {
        this.xr = xr;
        this.system = system;
        this.logger = logger;
        this.d11Device = d11Device;
    }
    public void Initialize()
    {
        var viewConfigurationViews = xr.GetViewConfigurationViews(system.Instance, system.SystemId, ViewConfigType);
        logger.Debug($"Got {viewConfigurationViews.Count} views");

        var formats = xr.GetSwapchainFormats(system.Session);
        var colorFormat = formats.Where(f => ColorFormats.Contains((Format)f)).First();
        var depthFormat = formats.Where(f => DepthFormats.Contains((Format)f)).First();

        Views = viewConfigurationViews.ConvertAll(viewConfigurationView =>
        {
            var colorSwapchain = CreateSwapchain(
                viewConfigurationView,
                colorFormat,
                SwapchainUsageFlags.SampledBit | SwapchainUsageFlags.ColorAttachmentBit
               );

            var colorImages = xr.GetSwapchainImages(colorSwapchain);

            var renderTargetViews = new ID3D11RenderTargetView*[colorImages.Count];
            for (int i = 0; i < colorImages.Count; i++)
            {
                var image = colorImages[i];
                ID3D11RenderTargetView* rtv = null;
                var rtvd = new RenderTargetViewDesc(
                    viewDimension: RtvDimension.Texture2D,
                    texture2D: new Tex2DRtv(
                        mipSlice: 0
                    )
                );
                d11Device->CreateRenderTargetView((ID3D11Resource*)image.Texture, ref rtvd, ref rtv);
                renderTargetViews[i] = rtv;
            }
            var colorSwapchainInfo = new SwapchainInfo<ID3D11RenderTargetView>(swapchain: colorSwapchain, swapchainFormat: colorFormat, views: renderTargetViews);

            var depthSwapchain = CreateSwapchain(
                viewConfigurationView,
                depthFormat,
                SwapchainUsageFlags.SampledBit | SwapchainUsageFlags.DepthStencilAttachmentBit);

            var depthImages = xr.GetSwapchainImages(depthSwapchain);

            var depthStencilViews = new ID3D11DepthStencilView*[depthImages.Count];
            for (int i = 0; i < depthImages.Count; i++)
            {
                var image = depthImages[i];
                var dsvd = new DepthStencilViewDesc(
                    viewDimension: DsvDimension.Texture2D,
                    texture2D: new Tex2DDsv(
                        mipSlice: 0
                    )
                );
                ID3D11DepthStencilView* dsv = null;
                d11Device->CreateDepthStencilView((ID3D11Resource*)image.Texture, ref dsvd, ref dsv);
                depthStencilViews[i] = dsv;
            }
            var depthSwapchainInfo = new SwapchainInfo<ID3D11DepthStencilView>(swapchain: depthSwapchain, swapchainFormat: depthFormat, views: depthStencilViews);

            return new SwapchainView(
                viewConfigurationView: viewConfigurationView,
                colorSwapchainInfo: colorSwapchainInfo,
                depthSwapchainInfo: depthSwapchainInfo
            );
        });
    }

    private Swapchain CreateSwapchain(
        ViewConfigurationView viewConfigurationView,
        long format,
        SwapchainUsageFlags usageFlags)
    {
        var swapchain = new Swapchain();
        var swapchainCreateInfo = new SwapchainCreateInfo(
            createFlags: 0,
            usageFlags: usageFlags,
            format: format,
            sampleCount: viewConfigurationView.RecommendedSwapchainSampleCount,
            width: viewConfigurationView.RecommendedImageRectWidth,
            height: viewConfigurationView.RecommendedImageRectHeight,
            faceCount: 1,
            arraySize: 1,
            mipCount: 1
        );
        xr.CreateSwapchain(system.Session, ref swapchainCreateInfo, ref swapchain).CheckResult("CreateSwapchain");
        return swapchain;
    }
    public void Dispose()
    {
        Views?.ForEach(view =>
        {
            foreach (var v in view.ColorSwapchainInfo.Views)
            {
                if (v != null)
                {
                    v->Release();
                }
            }
            foreach (var v in view.DepthSwapchainInfo.Views)
            {
                if (v != null)
                {
                    v->Release();
                }
            }
            xr.DestroySwapchain(view.ColorSwapchainInfo.Swapchain).LogResult("DestroySwapchain", logger);
            xr.DestroySwapchain(view.DepthSwapchainInfo.Swapchain).LogResult("DestroySwapchain", logger);
        });
    }
}

unsafe class SwapchainInfo<T>
{
    public Swapchain Swapchain;
    public long SwapchainFormat;
#pragma warning disable 8500
    public T*[] Views; // ID3D11RenderTargetView or ID3D11DepthStencilView

    internal SwapchainInfo(long swapchainFormat, T*[] views, Swapchain swapchain)
    {
        this.Swapchain = swapchain;
        this.SwapchainFormat = swapchainFormat;
        this.Views = views;
    }
#pragma warning restore 8500

}
class SwapchainView
{
    public ViewConfigurationView ViewConfigurationView;
    public SwapchainInfo<ID3D11RenderTargetView> ColorSwapchainInfo;
    public SwapchainInfo<ID3D11DepthStencilView> DepthSwapchainInfo;

    internal SwapchainView(ViewConfigurationView viewConfigurationView, SwapchainInfo<ID3D11RenderTargetView> colorSwapchainInfo, SwapchainInfo<ID3D11DepthStencilView> depthSwapchainInfo)
    {
        this.ViewConfigurationView = viewConfigurationView;
        this.ColorSwapchainInfo = colorSwapchainInfo;
        this.DepthSwapchainInfo = depthSwapchainInfo;
    }
}
