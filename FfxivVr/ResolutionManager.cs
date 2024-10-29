using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Silk.NET.Maths;
using System;
using System.Runtime.InteropServices;

namespace FfxivVR;
unsafe public class ResolutionManager()
{
    private Vector2D<uint>? original = null;
    public void ChangeResolution(Vector2D<uint> resolution)
    {
        var framework = Framework.Instance();
        var handle = framework->GameWindow->WindowHandle;
        if (handle != 0)
        {
            MoveWindow(handle, 0, 0, (int)resolution.X, (int)resolution.Y, false);
        }

        var dx11DeviceInstance = Device.Instance();

        original = new Vector2D<uint>(dx11DeviceInstance->Width, dx11DeviceInstance->Height);
        dx11DeviceInstance->NewWidth = resolution.X;
        dx11DeviceInstance->NewHeight = resolution.Y;
        dx11DeviceInstance->RequestResolutionChange = 1;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    public void RevertResolution()
    {
        //if (original is Vector2D<uint> size)
        //{
        //    var dx11DeviceInstance = Device.Instance();
        //    dx11DeviceInstance->NewWidth = size.X;
        //    dx11DeviceInstance->NewHeight = size.Y;
        //    dx11DeviceInstance->RequestResolutionChange = 1;
        //}
        original = null;
    }
}
