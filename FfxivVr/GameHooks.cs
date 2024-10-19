using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;
using System.Runtime.InteropServices;

namespace FfxivVR;
unsafe internal class GameHooks : IDisposable
{
    /**
     * Call order
     * FrameworkTickHook {
     * StartFrame
     * 
     * OriginalFrameworkTick()
     * CalculateViewMatrix x7
     * 
     * OriginalFrameworkTick()
     * CalculateViewMatrix x7
     * 
     * EndFrame
     * }
     */
    private readonly VRLifecycle vrLifecycle;
    private readonly ExceptionHandler exceptionHandler;
    private readonly Logger logger;
    public GameHooks(VRLifecycle vrLifecycle, ExceptionHandler exceptionHandler, Logger logger)
    {
        this.vrLifecycle = vrLifecycle;
        this.exceptionHandler = exceptionHandler;
        this.logger = logger;
    }
    public void Dispose()
    {
        FrameworkTickHook?.Disable();
        FrameworkTickHook?.Dispose();
        DXGIPresentHook?.Disable();
        DXGIPresentHook?.Dispose();
        //CalculateViewMatrix2Hook?.Disable();
        //CalculateViewMatrix2Hook?.Dispose();
        SetMatricesHook?.Disable();
        SetMatricesHook?.Dispose();
    }

    public void Initialize()
    {
        FrameworkTickHook!.Enable();
        DXGIPresentHook!.Enable();
        //CalculateViewMatrix2Hook!.Enable();
        SetMatricesHook!.Enable();
    }
    public delegate UInt64 FrameworkTickDg(Framework* FrameworkInstance);
    [Signature(Signatures.FrameworkTick, DetourName = nameof(FrameworkTickFn))]
    public Hook<FrameworkTickDg>? FrameworkTickHook = null;

    private UInt64 FrameworkTickFn(Framework* FrameworkInstance)
    {
        //logger.Debug($"Framework thread {GetCurrentThreadId()}");
        //logger.Debug("Framework start");
        var returnValue = FrameworkTickHook!.Original(FrameworkInstance);
        var shouldSecondRender = false;
        exceptionHandler.FaultBarrier(() =>
        {
            shouldSecondRender = vrLifecycle.SecondRender();
        });
        if (shouldSecondRender)
        {
            returnValue = FrameworkTickHook!.Original(FrameworkInstance);
        }
        //logger.Debug("Framework end");
        return returnValue;
    }

    [DllImport("KERNEL32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint GetCurrentThreadId();

    private delegate void DXGIPresentDg(UInt64 a, UInt64 b);
    [Signature(Signatures.DXGIPresent, DetourName = nameof(DXGIPresentFn))]
    private Hook<DXGIPresentDg>? DXGIPresentHook = null;
    private void DXGIPresentFn(UInt64 a, UInt64 b)
    {
        //logger.Debug($"Present thread {GetCurrentThreadId()}");
        //logger.Debug("Present start");
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.EndFrame();
        });
        DXGIPresentHook!.Original(a, b);
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.StartFrame();
        });
        //logger.Debug("Present end");
    }


    //private delegate void CalculateViewMatrixDg(Matrix4x4* viewMatrix, Vector4 position, Vector4 lookAt, Vector4 d);
    //[Signature(Signatures.CalculateViewMatrix, DetourName = nameof(CalculateViewMatrixFn))]
    //private Hook<CalculateViewMatrixDg>? CalculateViewMatrix2Hook = null;
    //private void CalculateViewMatrixFn(Matrix4x4* viewMatrix, Vector4 position, Vector4 lookAt, Vector4 d)
    //{
    //    CalculateViewMatrix2Hook!.Original(viewMatrix, position, lookAt, d);
    //    exceptionHandler.FaultBarrier(() =>
    //    {
    //        vrLifecycle.UpdateViewMatrix(viewMatrix, position, lookAt);
    //    });
    //}

    private delegate void SetMatricesDelegate(Camera* camera, IntPtr ptr);
    [Signature(Signatures.SetMatrices, DetourName = nameof(SetMatricesFn))]
    private Hook<SetMatricesDelegate>? SetMatricesHook = null;

    /**
     * Game matrix
     * {M11:0.6842151 M12:0        M13:0   M14:0}
     * {M21:0         M22:2.432765 M23:0   M24:0}
     * {M31:0         M32:0        M33:0   M34:-1}
     * {M41:0         M42:0        M43:0.1 M44:0}
     * 
     * Standard matrix
     * {M11:1.2071067 M12:0         M13:0          M14:0}
     * {M21:0         M22:2.4142134 M23:0          M24:0}
     * {M31:0         M32:0         M33:-1.001001  M34:-1}
     * {M41:0         M42:0         M43:-0.1001001 M44:0}
     */
    private void SetMatricesFn(Camera* camera, IntPtr ptr)
    {
        SetMatricesHook!.Original(camera, ptr);
        exceptionHandler.FaultBarrier(() =>
        {
            if (camera == CameraManager.Instance()->GetActiveCamera()->SceneCamera.RenderCamera)
            {
                vrLifecycle.UpdateCamera(&CameraManager.Instance()->GetActiveCamera()->SceneCamera);
            }
        });
    }

}