using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Lumina;
using Silk.NET.DXGI;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.STD.Helper.IStaticEncoding;
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
    private const ViewConfigurationType ViewConfigType = ViewConfigurationType.PrimaryStereo;
    private List<View>? views;
    public VRSwapchains(XR xr, VRSystem system, Logger logger)
    {
        this.xr = xr;
        this.system = system;
        this.logger = logger;
    }
    public void Initialize()
    {
        var viewConfigurationViews = xr.GetViewConfigurationViews(system.Instance, system.SystemId, ViewConfigType);
        logger.Debug($"Got {viewConfigurationViews.Count} views");

        var formats = xr.GetSwapchainFormats(system.Session);
        var colorFormat = formats.Where(f => ColorFormats.Contains((Format)f)).First();
        var depthFormat = formats.Where(f => DepthFormats.Contains((Format)f)).First();

        views = viewConfigurationViews.ConvertAll(viewConfigurationView =>
        {
            var colorSwapchainInfo = CreateSwapchainInfo(
                viewConfigurationView,
                colorFormat,
                SwapchainUsageFlags.SampledBit | SwapchainUsageFlags.ColorAttachmentBit,
                ImageViewCreateInfo.ImageType.RTV,
                ImageViewCreateInfo.Aspect.ColorBit
                );
            var depthSwapchainInfo = CreateSwapchainInfo(
                viewConfigurationView,
                depthFormat,
                SwapchainUsageFlags.SampledBit | SwapchainUsageFlags.DepthStencilAttachmentBit,
                ImageViewCreateInfo.ImageType.DSV,
                ImageViewCreateInfo.Aspect.DepthBit
                );

            return new View(
                viewConfigurationView: viewConfigurationView,
                colorSwapchainInfo: colorSwapchainInfo,
                depthSwapchainInfo: depthSwapchainInfo
            );
        });
    }

    private SwapchainInfo CreateSwapchainInfo(
        ViewConfigurationView viewConfigurationView,
        long format,
        SwapchainUsageFlags usageFlags,
        ImageViewCreateInfo.ImageType type,
        ImageViewCreateInfo.Aspect aspect)
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

        var images = xr.GetSwapchainImages(swapchain);

        var imageCreateInfos = images.ConvertAll(image =>
            new ImageViewCreateInfo(
                image: (IntPtr)image.Texture,
                type: type,
                view: ImageViewCreateInfo.View.Type2D,
                format: format,
                aspect: aspect,
                baseMipLevel: 0,
                levelCount: 1,
                baseArrayLayer: 0,
                layerCount: 1
            )
        );

        return new SwapchainInfo(swapchain: swapchain, swapchainFormat: format, imageCreateInfos: imageCreateInfos);
    }
    public void Dispose()
    {
    }
}

class SwapchainInfo
{
    internal Swapchain swapchain;
    internal long swapchainFormat;
    internal List<ImageViewCreateInfo> imageCreateInfos;

    internal SwapchainInfo(long swapchainFormat, List<ImageViewCreateInfo> imageCreateInfos, Swapchain swapchain)
    {
        this.swapchain = swapchain;
        this.swapchainFormat = swapchainFormat;
        this.imageCreateInfos = imageCreateInfos;
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

public class ImageViewCreateInfo
{
    IntPtr image;
    ImageType type;
    View view;
    long format;
    Aspect aspect;
    int baseMipLevel;
    int levelCount;
    int baseArrayLayer;
    int layerCount;

    public ImageViewCreateInfo(IntPtr image, ImageType type, View view, long format, Aspect aspect, int baseMipLevel, int levelCount, int baseArrayLayer, int layerCount)
    {
        this.image = image;
        this.type = type;
        this.view = view;
        this.format = format;
        this.aspect = aspect;
        this.baseMipLevel = baseMipLevel;
        this.levelCount = levelCount;
        this.baseArrayLayer = baseArrayLayer;
        this.layerCount = layerCount;
    }

    public enum ImageType
    {
        RTV,
        DSV,
        SRV,
        UAV,
    }

    public enum View
    {
        Type1D,
        Type2D,
        Type3D,
        TypeCube,
        Type1DArray,
        Type2DArray,
        TypeCubeArray,
    }

    public enum Aspect
    {
        ColorBit = 0x01,
        DepthBit = 0x02,
        StencilBit = 0x04,
    }
}
