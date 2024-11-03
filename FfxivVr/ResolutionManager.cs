using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Silk.NET.Maths;
using System;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace FfxivVR;
unsafe public class ResolutionManager(Logger logger)
{
    private RECT? originalWindow = null;
    private Vector2D<uint>? original = null;
    private readonly Logger logger = logger;

    public void ChangeResolution(Vector2D<uint> resolution)
    {
        HWND handle = GetGameHWND();
        if (handle != 0)
        {
            if (!PInvoke.GetClientRect(handle, out RECT clientRect))
            {
                throw new Exception("Failed to GetClientRect");
            }
            if (!PInvoke.GetWindowRect(handle, out RECT windowRect))
            {
                throw new Exception("Failed to GetWindowRect");
            }
            var xMargin = windowRect.right - clientRect.right + clientRect.left - windowRect.left;
            var yMargin = windowRect.bottom - clientRect.bottom + clientRect.top - windowRect.top;
            if (!PInvoke.MoveWindow(handle, 0, 0, (int)resolution.X + xMargin, (int)resolution.Y + yMargin, false))
            {
                throw new Exception("Failed to MoveWindow");
            }
            originalWindow = windowRect;
        }

        var dx11DeviceInstance = Device.Instance();

        original = new Vector2D<uint>(dx11DeviceInstance->Width, dx11DeviceInstance->Height);
        dx11DeviceInstance->NewWidth = resolution.X;
        dx11DeviceInstance->NewHeight = resolution.Y;
        dx11DeviceInstance->RequestResolutionChange = 1;
    }

    private static HWND GetGameHWND()
    {
        var framework = Framework.Instance();
        var handle = (HWND)framework->GameWindow->WindowHandle;
        return handle;
    }

    public void RevertResolution()
    {
        HWND handle = GetGameHWND();
        if (handle != IntPtr.Zero)
        {
            if (originalWindow is RECT rect)
            {
                PInvoke.MoveWindow(handle, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, false);
            }
        }
        if (original is Vector2D<uint> size)
        {
            var dx11DeviceInstance = Device.Instance();
            dx11DeviceInstance->NewWidth = size.X;
            dx11DeviceInstance->NewHeight = size.Y;
            dx11DeviceInstance->RequestResolutionChange = 1;
        }
        originalWindow = null;
        original = null;
    }
}
