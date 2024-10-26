using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Silk.NET.Maths;

namespace FfxivVR;
unsafe public class ResolutionManager
{
    private Vector2D<uint>? original = null;
    public void ChangeResolution(Vector2D<uint> resolution)
    {
        var dx11DeviceInstance = Device.Instance();

        original = new Vector2D<uint>(dx11DeviceInstance->Width, dx11DeviceInstance->Height);
        dx11DeviceInstance->NewWidth = resolution.X;
        dx11DeviceInstance->NewHeight = resolution.Y;
        dx11DeviceInstance->RequestResolutionChange = 1;

        //var windowHandle = Framework.Instance()->GameWindow->WindowHandle;
        //if (windowHandle != IntPtr.Zero)
        //{
        //    //ScreenSettings* screenSettings = *(ScreenSettings**)((UInt64)frameworkInstance + 0x7A8);
        //    //if (screenSettings != null && screenSettings->hWnd != 0)
        //    //    Imports.ResizeWindow((IntPtr)screenSettings->hWnd, width, height);
        //}
    }
    public void RevertResolution()
    {
        if (original is Vector2D<uint> size)
        {
            var dx11DeviceInstance = Device.Instance();
            dx11DeviceInstance->NewWidth = size.X;
            dx11DeviceInstance->NewHeight = size.Y;
            dx11DeviceInstance->RequestResolutionChange = 1;
        }
        original = null;
    }
}
