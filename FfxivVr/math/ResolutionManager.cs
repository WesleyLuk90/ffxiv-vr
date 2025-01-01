using Dalamud;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Silk.NET.Maths;
using System;
using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace FfxivVR;

class ResizeState
{
    public Rectangle<int> OriginalWindow;
    public Rectangle<int> ClientArea;
    public Vector2D<uint> RenderResolution;

    public ResizeState(Rectangle<int> originalWindow, Rectangle<int> clientRectangle, Vector2D<uint> renderResolution)
    {
        OriginalWindow = originalWindow;
        ClientArea = clientRectangle;
        RenderResolution = renderResolution;
    }

    public nint? OriginalWindowStyle { get; }
}
public unsafe class ResolutionManager : IDisposable
{
    private const uint ExitSizeMove = 0x0232;

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos
    private HWND TOPMOST = new HWND(-1);
    private HWND NOTOPMOST = new HWND(-2);

    public readonly Logger logger;
    private Configuration configuration;
    private ResizeState? resizeState = null;
    private ulong DisableSetCursorPosAddr = 0;
    private ulong DisableSetCursorPosOrig = 0;
    private ulong DisableSetCursorPosOverride = 0x05C6909090909090;

    private const string g_DisableSetCursorPosAddr = "FF ?? ?? ?? ?? 00 C6 05 ?? ?? ?? ?? 00 0F B6 43 38";
    public ResolutionManager(Logger logger, Configuration configuration, ISigScanner sigScanner)
    {
        this.logger = logger;
        this.configuration = configuration;
        DisableSetCursorPosAddr = (ulong)sigScanner!.ScanText(g_DisableSetCursorPosAddr);
    }

    private Vector2D<int> ComputeClientSize(HWND handle, Vector2D<int> resolution)
    {
        var availableArea = GetMonitorArea(handle) - GetWindowMargins(handle);
        if (AspectRatio(availableArea) > AspectRatio(resolution))
        {
            return new Vector2D<int>((int)(AspectRatio(resolution) * availableArea.Y), availableArea.Y);
        }
        else
        {
            return new Vector2D<int>(availableArea.X, (int)(availableArea.X / AspectRatio(resolution)));
        }
    }
    private static float AspectRatio(Vector2D<int> size)
    {
        return ((float)size.X) / size.Y;
    }

    // Area minus the taskbar
    private Vector2D<int> GetMonitorArea(HWND handle)
    {
        HMONITOR monitor = PInvoke.MonitorFromWindow(handle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        MONITORINFO monitorInfo = new MONITORINFO();
        monitorInfo.cbSize = (uint)sizeof(MONITORINFO);
        if (!PInvoke.GetMonitorInfo(monitor, ref monitorInfo))
        {
            throw new Exception("Failed to GetMonitorInfo");
        }
        return new Vector2D<int>(monitorInfo.rcWork.Width, monitorInfo.rcWork.Height);
    }

    // Gets the total amount of margins added to each window
    private Vector2D<int> GetWindowMargins(HWND handle)
    {
        var client = GetClientRect(handle);
        var frameBounds = GetExtendedFrameBounds(handle);
        return frameBounds.Size - client.Size;
    }

    private Rectangle<int> GetClientRect(HWND handle)
    {
        if (!PInvoke.GetClientRect(handle, out RECT clientRect))
        {
            throw new Exception("Failed to GetClientRect");
        }
        var client = ToRectangle(clientRect);
        return client;
    }

    private Rectangle<int> ToRectangle(RECT rect)
    {
        return new Rectangle<int>(rect.left, rect.top, rect.Width, rect.Height);
    }

    private Rectangle<int> GetExtendedFrameBounds(HWND handle)
    {
        RECT frame = new();
        if (PInvoke.DwmGetWindowAttribute(handle, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &frame, (uint)sizeof(RECT)).Failed)
        {
            throw new Exception("Failed to DwmGetWindowAttribute");
        }
        return new Rectangle<int>(frame.left, frame.top, frame.Width, frame.Height);
    }
    public void ChangeResolution(Vector2D<uint> resolution)
    {
        HWND handle = GetGameWindowHandle();
        if (handle != 0)
        {
            var clientSize = ComputeClientSize(handle, resolution.As<int>());
            var clientRect = ComputeClientRectangle(handle, clientSize);
            var adjustRect = AdjustRect(handle, clientRect);
            var windowRect = GetWindowRect(handle);

            if (!PInvoke.SetWindowPos(
                hWnd: handle,
                hWndInsertAfter: configuration.WindowAlwaysOnTop ? TOPMOST : HWND.Null,
                X: adjustRect.Origin.X,
                Y: adjustRect.Origin.Y,
                cx: adjustRect.Size.X,
                cy: adjustRect.Size.Y,
                uFlags: 0))
            {
                throw new Exception("Failed to MoveWindow");
            }
            PInvoke.SendMessage(handle, ExitSizeMove, 0, 0);

            // This needs to happen after the window resize
            var dx11DeviceInstance = Device.Instance();

            dx11DeviceInstance->NewWidth = resolution.X;
            dx11DeviceInstance->NewHeight = resolution.Y;
            dx11DeviceInstance->RequestResolutionChange = 1;

            DisableSetCursor();

            resizeState = new ResizeState(
                originalWindow: windowRect,
                clientRectangle: clientRect,
                renderResolution: resolution
            );
        }
        else
        {
            logger.Error("Failed to resize game window");
        }
    }

    private Rectangle<int> ComputeClientRectangle(HWND handle, Vector2D<int> clientArea)
    {
        var withMargins = AdjustRect(handle, new Rectangle<int>());
        var offset = -withMargins.Origin + GetWindowRect(handle).Origin - GetExtendedFrameBounds(handle).Origin;
        return new Rectangle<int>(offset, clientArea);
    }
    private Rectangle<int> AdjustRect(HWND handle, Rectangle<int> rectangle)
    {
        var style = PInvoke.GetWindowLongPtr(handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var exstyle = PInvoke.GetWindowLongPtr(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var desiredRect = new RECT();
        desiredRect.top = rectangle.Origin.Y;
        desiredRect.left = rectangle.Origin.X;
        desiredRect.bottom = rectangle.Max.Y;
        desiredRect.right = rectangle.Max.X;
        if (!PInvoke.AdjustWindowRectEx(ref desiredRect, (WINDOW_STYLE)style, false, (WINDOW_EX_STYLE)exstyle))
        {
            throw new Exception("Failed to AdjustWindowRect");
        }
        return ToRectangle(desiredRect);
    }

    private void DisableSetCursor()
    {
        if (DisableSetCursorPosAddr != 0)
        {
            if (SafeMemory.Read((nint)DisableSetCursorPosAddr, out DisableSetCursorPosOrig))
            {
                SafeMemory.Write((nint)DisableSetCursorPosAddr, DisableSetCursorPosOverride);
            }
        }
    }
    private void EnableSetCursor()
    {
        if (DisableSetCursorPosAddr != 0 && DisableSetCursorPosOrig != 0)
        {
            SafeMemory.Write((nint)DisableSetCursorPosAddr, DisableSetCursorPosOrig);
        }
    }

    private Rectangle<int> GetWindowRect(HWND handle)
    {
        if (!PInvoke.GetWindowRect(handle, out RECT windowRect))
        {
            throw new Exception("Failed to GetWindowRect");
        }
        return ToRectangle(windowRect);
    }
    private static HWND GetGameWindowHandle()
    {
        var framework = Framework.Instance();
        var handle = (HWND)framework->GameWindow->WindowHandle;
        return handle;
    }

    public void RevertResolution()
    {
        HWND handle = GetGameWindowHandle();
        if (handle != IntPtr.Zero)
        {
            if (resizeState is ResizeState state)
            {
                PInvoke.SetWindowPos(handle, NOTOPMOST, state.OriginalWindow.Origin.X, state.OriginalWindow.Origin.Y, state.OriginalWindow.Size.X, state.OriginalWindow.Size.Y, 0);
            }
        }
        EnableSetCursor();
    }

    internal Point? ComputeMousePosition(Point point)
    {
        if (resizeState is ResizeState state)
        {
            var xPosition = (float)point.X / state.ClientArea.Size.X * state.RenderResolution.X;
            var yPosition = (float)point.Y / state.ClientArea.Size.Y * state.RenderResolution.Y;
            return new Point((int)xPosition, (int)yPosition);
        }
        return null;
    }

    public Matrix4X4<float> GetDalamudScale()
    {
        if (resizeState is ResizeState state)
        {
            var xScale = (float)state.RenderResolution.X / state.ClientArea.Size.X;
            var yScale = (float)state.RenderResolution.Y / state.ClientArea.Size.Y;
            return Matrix4X4.CreateTranslation(1f, -1f, 0) * Matrix4X4.CreateScale(xScale, yScale, 1) * Matrix4X4.CreateTranslation<float>(-1, 1, 0);
        }
        else
        {
            return Matrix4X4<float>.Identity;
        }
    }

    public void Dispose()
    {
        RevertResolution();
    }

    internal Vector2D<int>? WindowToScreen(Vector2D<float> pos)
    {

        if (resizeState is ResizeState state)
        {
            return state.ClientArea.Origin + (state.ClientArea.Size.As<float>() * pos).As<int>();
        }
        return null;
    }
}