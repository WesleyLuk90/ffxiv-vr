using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;

namespace FfxivVR;
unsafe public static class ResolutionManager
{
    public static void ChangeResolution(uint width, uint height)
    {
        var dx11DeviceInstance = Device.Instance();

        dx11DeviceInstance->NewWidth = (uint)width;
        dx11DeviceInstance->NewHeight = (uint)height;
        dx11DeviceInstance->RequestResolutionChange = 1;

        var windowHandle = Framework.Instance()->GameWindow->WindowHandle;
        if (windowHandle != IntPtr.Zero)
        {
            //ScreenSettings* screenSettings = *(ScreenSettings**)((UInt64)frameworkInstance + 0x7A8);
            //if (screenSettings != null && screenSettings->hWnd != 0)
            //    Imports.ResizeWindow((IntPtr)screenSettings->hWnd, width, height);
        }
    }
}
