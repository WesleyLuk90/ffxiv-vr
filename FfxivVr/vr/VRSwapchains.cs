using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Maths;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Linq;
namespace FfxivVR;
unsafe public class VRSwapchains(
    XR xr,
    VRSystem system,
    Logger logger,
    DxDevice device) : IDisposable
{
    private static List<Format> ColorFormats = new List<Format>() {
        Format.FormatR8G8B8A8UnormSrgb,
        Format.FormatB8G8R8A8UnormSrgb,
    };
    private static List<Format> DepthFormats = new List<Format>() {
        Format.FormatD32Float,
        Format.FormatD16Unorm,
    };
    public const ViewConfigurationType ViewConfigType = ViewConfigurationType.PrimaryStereo;
    public List<SwapchainView> Views = null!;
    public Vector2D<uint> Initialize()
    {
        var viewConfigurationViews = xr.GetViewConfigurationViews(system.Instance, system.SystemId, ViewConfigType);
        if (viewConfigurationViews.Count != 2)
        {
            throw new Exception($"Invalid number of views, expected 2");
        }
        logger.Debug($"Got {viewConfigurationViews.Count} ViewConfigurationViews");

        var formats = xr.GetSwapchainFormats(system.Session);
        var colorFormat = formats.Where(f => ColorFormats.Contains((Format)f)).First();
        logger.Debug($"Selected color format {(Format)colorFormat}, available {string.Join(",", formats.Select(f => (Format)f))}");
        var depthFormat = formats.Where(f => DepthFormats.Contains((Format)f)).First();

        var width = viewConfigurationViews[0].RecommendedImageRectWidth;
        var height = viewConfigurationViews[0].RecommendedImageRectHeight;
        Views = viewConfigurationViews.ConvertAll(viewConfigurationView =>
        {
            logger.Debug($"View has resolution {viewConfigurationView.RecommendedImageRectWidth}x{viewConfigurationView.RecommendedImageRectHeight}");
            var colorSwapchain = CreateSwapchain(
                viewConfigurationView,
                colorFormat,
                SwapchainUsageFlags.SampledBit | SwapchainUsageFlags.ColorAttachmentBit
               );

            var colorImages = xr.GetSwapchainImages(colorSwapchain);
            if (colorImages.Count == 0)
            {
                throw new Exception($"Expected 1 color image but got {colorImages.Count}");
            }
            logger.Debug($"Created {colorImages.Count} color swapchain images");

            var renderTargetViews = new ID3D11RenderTargetView*[colorImages.Count];
            var imageTextures = new ID3D11Texture2D*[colorImages.Count];
            for (int i = 0; i < colorImages.Count; i++)
            {
                var image = colorImages[i];
                ID3D11RenderTargetView* rtv = null;
                var rtvd = new RenderTargetViewDesc(
                    format: (Format)colorFormat,
                    viewDimension: RtvDimension.Texture2D,
                    texture2D: new Tex2DRtv(
                        mipSlice: 0
                    )
                ); ;
                device.Device->CreateRenderTargetView((ID3D11Resource*)image.Texture, ref rtvd, ref rtv).D3D11Check("CreateRenderTargetView");
                renderTargetViews[i] = rtv;
                imageTextures[i] = (ID3D11Texture2D*)image.Texture;
            }
            var colorSwapchainInfo = new SwapchainInfo<ID3D11RenderTargetView>(swapchain: colorSwapchain, swapchainFormat: colorFormat, views: renderTargetViews, textures: imageTextures);

            var depthSwapchain = CreateSwapchain(
                viewConfigurationView,
                depthFormat,
                SwapchainUsageFlags.SampledBit | SwapchainUsageFlags.DepthStencilAttachmentBit);

            var depthImages = xr.GetSwapchainImages(depthSwapchain);
            logger.Debug($"Created {depthImages.Count} depth swapchain images");

            if (depthImages.Count == 0)
            {
                throw new Exception($"Expected 1 depth image but got {depthImages.Count}");
            }
            var depthStencilViews = new ID3D11DepthStencilView*[depthImages.Count];
            var depthTextures = new ID3D11Texture2D*[depthImages.Count];
            for (int i = 0; i < depthImages.Count; i++)
            {
                var image = depthImages[i];
                var dsvd = new DepthStencilViewDesc(
                    format: (Format)depthFormat,
                    viewDimension: DsvDimension.Texture2D,
                    texture2D: new Tex2DDsv(
                        mipSlice: 0
                    )
                );
                ID3D11DepthStencilView* dsv = null;
                device.Device->CreateDepthStencilView((ID3D11Resource*)image.Texture, ref dsvd, ref dsv).D3D11Check("CreateRenderTargetView");
                depthStencilViews[i] = dsv;
                depthTextures[i] = (ID3D11Texture2D*)image.Texture;
            }
            var depthSwapchainInfo = new SwapchainInfo<ID3D11DepthStencilView>(swapchain: depthSwapchain, swapchainFormat: depthFormat, views: depthStencilViews, textures: depthTextures);

            return new SwapchainView(
                viewConfigurationView: viewConfigurationView,
                colorSwapchainInfo: colorSwapchainInfo,
                depthSwapchainInfo: depthSwapchainInfo
            );
        });
        return new Vector2D<uint>(width, height);
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

public unsafe class SwapchainInfo<T>
{
    public Swapchain Swapchain;
    public long SwapchainFormat;
#pragma warning disable 8500
    public T*[] Views; // ID3D11RenderTargetView or ID3D11DepthStencilView
    public ID3D11Texture2D*[] Textures;

    internal SwapchainInfo(long swapchainFormat, T*[] views, Swapchain swapchain, ID3D11Texture2D*[] textures)
    {
        this.Swapchain = swapchain;
        this.SwapchainFormat = swapchainFormat;
        this.Views = views;
        foreach (var v in views)
        {
            if (v == null)
            {
                throw new ArgumentNullException($"Got a null view");
            }
        }
        Textures = textures;

    }
#pragma warning restore 8500

}
public class SwapchainView
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