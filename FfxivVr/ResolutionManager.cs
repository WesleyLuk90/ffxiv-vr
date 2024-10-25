using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace FfxivVR;
unsafe public class ResolutionManager
{
    private uint? originalWidth = null;
    private uint? originalHeight = null;
    public void ChangeResolution(uint width, uint height)
    {
        var dx11DeviceInstance = Device.Instance();

        originalWidth = dx11DeviceInstance->Width;
        originalHeight = dx11DeviceInstance->Height;
        dx11DeviceInstance->NewWidth = (uint)width;
        dx11DeviceInstance->NewHeight = (uint)height;
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
        if (originalWidth is uint width && originalHeight is uint hight)
        {
            var dx11DeviceInstance = Device.Instance();
            dx11DeviceInstance->NewWidth = width;
            dx11DeviceInstance->NewHeight = hight;
            dx11DeviceInstance->RequestResolutionChange = 1;
        }
        originalWidth = null;
        originalHeight = null;
    }
}
