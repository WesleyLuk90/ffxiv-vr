using Silk.NET.OpenXR;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FFXIVClientStructs.STD.Helper.IStaticEncoding;

namespace FfxivVR
{
    internal unsafe static class XRExtensions
    {
        internal static List<long> GetSwapchainFormats(this XR xr, Session session)
        {
            var formats = EnumerateListValue<long>((capacity, countOut, outArray) =>
                xr.EnumerateSwapchainFormats(session, capacity, countOut, outArray), 0, "EnumerateSwapchainFormats");
            return formats.ToList();
        }
        internal static List<ViewConfigurationView> GetViewConfigurationViews(this XR xr, Instance instance, ulong systemID, ViewConfigurationType viewConfigurationType)
        {
            var views = EnumerateListValue((capacity, countOut, outArray) =>
                xr.EnumerateViewConfigurationView(instance, systemID, viewConfigurationType, capacity, countOut, outArray),
                new ViewConfigurationView(next: null), "EnumerateViewConfigurationView");
            return views.ToList();
        }

        internal static List<ExtensionProperties> GetInstanceExtensionProperties(this XR xr, string? layerName)
        {
            return EnumerateListValue((capacity, countOut, outArray) =>
                xr.EnumerateInstanceExtensionProperties(layerName, capacity, countOut, outArray),
                new ExtensionProperties(next: null), "EnumerateInstanceExtensionProperties").ToList();
        }

        internal static List<SwapchainImageD3D11KHR> GetSwapchainImages(this XR xr, Swapchain swapchain)
        {
            return EnumerateListValue((capacity, countOut, outArray) =>
            {
                fixed (SwapchainImageD3D11KHR* firstImage = &outArray[0])
                {
                    return xr.EnumerateSwapchainImages(swapchain, capacity, countOut, (SwapchainImageBaseHeader*)firstImage);
                }
            }, new SwapchainImageD3D11KHR(next: null), "EnumerateSwapchainImages").ToList();
        }

        internal delegate Result EnumerateDelegate<T>(uint capacity, uint* countOut, Span<T> outArray);
        internal static T[] EnumerateListValue<T>(EnumerateDelegate<T> enumerate, T initialValue, string action)
        {
            uint count = 0;
            enumerate(0, &count, null).CheckResult(action);
            var result = new T[count];
            enumerate(count, &count, result).CheckResult(action);
            return result;
        }
    }
}
