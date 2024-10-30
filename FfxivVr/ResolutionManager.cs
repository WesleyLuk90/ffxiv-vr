using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Silk.NET.Maths;
using System;
using System.Runtime.InteropServices;

namespace FfxivVR;
unsafe public class ResolutionManager(Logger logger)
{
    private Rect? originalWindow = null;
    private Vector2D<uint>? original = null;
    private readonly Logger logger = logger;

    public void ChangeResolution(Vector2D<uint> resolution)
    {
        var framework = Framework.Instance();
        var handle = framework->GameWindow->WindowHandle;
        if (handle != 0)
        {
            Rect clientRect;
            if (!GetClientRect(handle, out clientRect))
            {
                throw new Exception("Failed to GetClientRect");
            }
            Rect windowRect;
            if (!GetWindowRect(handle, out windowRect))
            {
                throw new Exception("Failed to GetWindowRect");
            }
            var xMargin = windowRect.Right - clientRect.Right + clientRect.Left - windowRect.Left;
            var yMargin = windowRect.Bottom - clientRect.Bottom + clientRect.Top - windowRect.Top;
            if (!MoveWindow(handle, 0, 0, (int)resolution.X + xMargin, (int)resolution.Y + yMargin, false))
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

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    [DllImport("user32.dll")]
    static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool GetWindowRect(IntPtr hwnd, out Rect lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public void RevertResolution()
    {
        var framework = Framework.Instance();
        var handle = framework->GameWindow->WindowHandle;
        if (handle != IntPtr.Zero)
        {
            if (originalWindow is Rect rect)
            {
                MoveWindow(handle, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, false);
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
