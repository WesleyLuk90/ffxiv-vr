using Dalamud;
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
    public RECT OriginalWindow;
    public Rectangle<int> ClientArea;
    public Vector2D<uint> RenderResolution;

    public ResizeState(RECT originalWindow, Rectangle<int> clientArea, Vector2D<uint> renderResolution)
    {
        OriginalWindow = originalWindow;
        ClientArea = clientArea;
        RenderResolution = renderResolution;
    }
}
unsafe public class ResolutionManager : IDisposable
{
    private const SET_WINDOW_POS_FLAGS SetWindowPositionFlags = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED;
    private const uint ExitSizeMove = 0x0232;

    public readonly Logger logger;
    private Configuration configuration;
    private ResizeState? resizeState = null;
    private UInt64 DisableSetCursorPosAddr = 0;
    private UInt64 DisableSetCursorPosOrig = 0;
    private UInt64 DisableSetCursorPosOverride = 0x05C6909090909090;

    private const string g_DisableSetCursorPosAddr = "FF ?? ?? ?? ?? 00 C6 05 ?? ?? ?? ?? 00 0F B6 43 38";
    public ResolutionManager(Logger logger, Configuration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
        DisableSetCursorPosAddr = (UInt64)Plugin.SigScanner!.ScanText(g_DisableSetCursorPosAddr);
        logger.Debug($"Found disable address {DisableSetCursorPosAddr}");
    }
    public void ChangeResolution(Vector2D<uint> resolution)
    {
        HWND handle = GetGameHWND();
        if (handle != 0)
        {
            var screenArea = GetScreenArea(handle);
            var margins = GetMargins(handle);
            var clientArea = ComputeClientRect(screenArea, resolution, margins);

            if (!PInvoke.GetWindowRect(handle, out RECT windowRect))
            {
                throw new Exception("Failed to GetWindowRect");
            }

            if (!PInvoke.SetWindowPos(handle, HWND.Null, 0, 0, clientArea.Size.X + margins.Item1.X + margins.Item2.X, clientArea.Size.Y + margins.Item1.Y + margins.Item2.Y, SetWindowPositionFlags))
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

            this.resizeState = new ResizeState(
                originalWindow: windowRect,
                clientArea: clientArea,
                renderResolution: resolution
            );
        }
        else
        {
            logger.Error("Failed to resize game window");
        }
    }

    private void DisableSetCursor()
    {
        if (DisableSetCursorPosAddr != 0)
        {
            if (SafeMemory.Read<UInt64>((nint)DisableSetCursorPosAddr, out DisableSetCursorPosOrig))
            {
                SafeMemory.Write<UInt64>((nint)DisableSetCursorPosAddr, DisableSetCursorPosOverride);
            }
        }
    }
    private void EnableSetCursor()
    {
        if (DisableSetCursorPosAddr != 0 && DisableSetCursorPosOrig != 0)
        {
            SafeMemory.Write<UInt64>((nint)DisableSetCursorPosAddr, DisableSetCursorPosOrig);
        }
    }

    private Rectangle<int> ComputeClientRect(Rectangle<int> screenArea, Vector2D<uint> resolution, Tuple<Vector2D<int>, Vector2D<int>> margins)
    {
        var availableScreenWidth = screenArea.Size.X - margins.Item1.X - margins.Item2.X;
        var availableScreenHeight = screenArea.Size.Y - margins.Item1.Y - margins.Item2.Y;
        var screenAspectRatio = (float)availableScreenWidth / availableScreenHeight;
        var gameAspectRatio = (float)resolution.X / resolution.Y;
        if (configuration.FitWindowOnScreen)
        {
            if (screenAspectRatio > gameAspectRatio)
            {
                var height = availableScreenHeight;
                var width = height * gameAspectRatio;
                return new Rectangle<int>(
                     originX: margins.Item1.X,
                     originY: margins.Item1.Y,
                     sizeX: (int)width,
                     sizeY: height
                 );
            }
            else
            {
                var width = availableScreenWidth;
                var height = width / gameAspectRatio;
                return new Rectangle<int>(
                     originX: margins.Item1.X,
                     originY: margins.Item1.Y,
                     sizeX: width,
                     sizeY: (int)height
                 );
            }
        }
        else
        {
            return new Rectangle<int>(
                originX: margins.Item1.X,
                originY: margins.Item1.Y,
                sizeX: (int)resolution.X,
                sizeY: (int)resolution.Y
            );
        }
    }

    private Rectangle<int> GetScreenArea(HWND handle)
    {
        HMONITOR monitor = PInvoke.MonitorFromWindow(handle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        MONITORINFO monitorInfo = new MONITORINFO();
        monitorInfo.cbSize = (uint)sizeof(MONITORINFO);
        if (!PInvoke.GetMonitorInfo(monitor, ref monitorInfo))
        {
            throw new Exception("Failed to GetMonitorInfo");
        }
        if (!PInvoke.GetWindowRect(handle, out RECT windowRect))
        {
            throw new Exception("Failed to GetWindowRect");
        }
        return new Rectangle<int>(
            originX: windowRect.left,
            originY: windowRect.top,
            sizeX: windowRect.Height,
            sizeY: windowRect.Width
        );
    }

    private Tuple<Vector2D<int>, Vector2D<int>> GetMargins(HWND handle)
    {
        var style = PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var exstyle = PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var desiredRect = new RECT();
        desiredRect.top = 0;
        desiredRect.left = 0;
        desiredRect.right = 100;
        desiredRect.bottom = 100;
        if (!PInvoke.AdjustWindowRectEx(ref desiredRect, (WINDOW_STYLE)style, false, (WINDOW_EX_STYLE)exstyle))
        {
            throw new Exception("Failed to AdjustWindowRect");
        }
        return Tuple.Create(new Vector2D<int>(-desiredRect.left, -desiredRect.top), new Vector2D<int>(desiredRect.right - 100, desiredRect.bottom - 100));
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
            if (resizeState is ResizeState state)
            {
                PInvoke.SetWindowPos(handle, HWND.Null, state.OriginalWindow.left, state.OriginalWindow.top, state.OriginalWindow.Width, state.OriginalWindow.Height, SetWindowPositionFlags);
            }
        }
        EnableSetCursor();
    }

    internal Point? ComputeMousePosition(Point point)
    {
        if (resizeState is ResizeState state)
        {
            var xPosition = (float)(point.X) / state.ClientArea.Size.X * state.RenderResolution.X;
            var yPosition = (float)(point.Y) / state.ClientArea.Size.Y * state.RenderResolution.Y;
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
            return Matrix4X4.CreateTranslation<float>(1f, -1f, 0) * Matrix4X4.CreateScale<float>(xScale, yScale, 1) * Matrix4X4.CreateTranslation<float>(-1, 1, 0);
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
}