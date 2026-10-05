using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FfxivVR;

public unsafe class FfxivVrNative(string path, EndFrameDispatcher endFrameDispatcher) : IEndFrameListener, IDisposable
{
    private readonly string path = path;
    private readonly EndFrameDispatcher endFrameDispatcher = endFrameDispatcher;
    private nint handle;
    private delegate* unmanaged<ID3D11DeviceContext*, uint, uint, void*, void*, byte*, bool> initFn;
    private delegate* unmanaged<bool> shutdownFn;
    private delegate* unmanaged<bool, bool> setConfigFn;
    private delegate* unmanaged<uint, float*, float*, bool> setProjectionMatrixFn;
    private delegate* unmanaged<int, bool> setActiveEyeFn;
    private delegate* unmanaged<bool> onFrameEndFn;

    public bool Initialized { get; private set; } = false;
    private bool renderHooksEnabled = true;

    // compositionTexture/geometryTexture are captured once here and remembered for the rest of
    // the VR session (see ffxiv_vr_native_init's doc comment) - if the game reallocates either
    // render target while VR stays active (e.g. a resolution-scale or graphics-setting change),
    // Shutdown/Initialize must be called again to pick up the new pointer.
    public bool Initialize(DxDeviceContext deviceContext, Vector2D<uint> size, void* compositionTexture, void* geometryTexture, string? logDirectory)
    {
        if (Initialized)
        {
            return true;
        }
        Load();
        if (logDirectory != null)
        {
            var logDirectoryBytes = Encoding.UTF8.GetBytes(logDirectory + '\0');
            fixed (byte* pLogDirectory = logDirectoryBytes)
            {
                Initialized = initFn(deviceContext.Context, size.X, size.Y, compositionTexture, geometryTexture, pLogDirectory);
            }
        }
        else
        {
            Initialized = initFn(deviceContext.Context, size.X, size.Y, compositionTexture, geometryTexture, null);
        }
        if (Initialized)
        {
            endFrameDispatcher.Register(this);
            ApplyHooksEnabled();
        }
        return Initialized;
    }

    public void Shutdown()
    {
        if (Initialized)
        {
            shutdownFn();
            Initialized = false;
            endFrameDispatcher.Unregister(this);
        }
    }

    // Called once per real D3D Present via EndFrameDispatcher.
    public void EndFrame()
    {
        OnFrameEnd();
    }

    // Self-render bracket pattern: SinglePassRenderStrategy disables hooks around its own D3D calls
    // so those calls don't re-enter the hooks, then re-enables once done.
    public bool SetRenderHooksEnabled(bool enabled)
    {
        renderHooksEnabled = enabled;
        return ApplyHooksEnabled();
    }

    private bool ApplyHooksEnabled()
    {
        return Initialized && setConfigFn(renderHooksEnabled);
    }

    // view/projection must each point to 16 floats, row-major, already in the shader's M x v
    // convention. projection may be null to use the game's own current projection for that
    // frame instead.
    public bool SetProjectionMatrix(Eye eye, float* view, float* projection)
    {
        return Initialized && setProjectionMatrixFn((uint)eye.ToIndex(), view, projection);
    }

    public bool SetActiveEye(Eye? eye)
    {
        return Initialized && setActiveEyeFn(eye?.ToIndex() ?? -1);
    }

    // Call once per real D3D Present.
    public bool OnFrameEnd()
    {
        return Initialized && onFrameEndFn();
    }

    public void Dispose()
    {
        Shutdown();
        if (handle != 0)
        {
            NativeLibrary.Free(handle);
            handle = 0;
        }
    }

    private void Load()
    {
        if (handle != 0)
        {
            return;
        }
        handle = NativeLibrary.Load(path);
        initFn = (delegate* unmanaged<ID3D11DeviceContext*, uint, uint, void*, void*, byte*, bool>)NativeLibrary.GetExport(handle, "ffxiv_vr_native_init");
        shutdownFn = (delegate* unmanaged<bool>)NativeLibrary.GetExport(handle, "ffxiv_vr_native_shutdown");
        setConfigFn = (delegate* unmanaged<bool, bool>)NativeLibrary.GetExport(handle, "ffxiv_vr_native_set_config");
        setProjectionMatrixFn = (delegate* unmanaged<uint, float*, float*, bool>)NativeLibrary.GetExport(handle, "ffxiv_vr_native_set_projection_matrix");
        setActiveEyeFn = (delegate* unmanaged<int, bool>)NativeLibrary.GetExport(handle, "ffxiv_vr_native_set_active_eye");
        onFrameEndFn = (delegate* unmanaged<bool>)NativeLibrary.GetExport(handle, "ffxiv_vr_native_on_frame_end");
    }
}