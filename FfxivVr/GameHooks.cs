using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;

namespace FfxivVR;
unsafe internal class GameHooks : IDisposable
{
    /**
     * Call order
     * Framework start
     * Present start
     * Present end
     * Present start
     * Present end
     * Framework end
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
        SetMatricesHook?.Disable();
        SetMatricesHook?.Dispose();
    }

    public void Initialize()
    {
        FrameworkTickHook!.Enable();
        DXGIPresentHook!.Enable();
        SetMatricesHook!.Enable();
    }
    public delegate UInt64 FrameworkTickDg(Framework* FrameworkInstance);
    [Signature(Signatures.FrameworkTick, DetourName = nameof(FrameworkTickFn))]
    public Hook<FrameworkTickDg>? FrameworkTickHook = null;

    private UInt64 FrameworkTickFn(Framework* FrameworkInstance)
    {
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

    private delegate void DXGIPresentDg(UInt64 a, UInt64 b);
    [Signature(Signatures.DXGIPresent, DetourName = nameof(DXGIPresentFn))]
    private Hook<DXGIPresentDg>? DXGIPresentHook = null;
    private void DXGIPresentFn(UInt64 a, UInt64 b)
    {
        //logger.Debug("Present start");
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.PrePresent();
        });
        DXGIPresentHook!.Original(a, b);
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.PostPresent();
        });
        //logger.Debug("Present end");
    }


    private delegate void SetMatricesDelegate(Camera* camera, IntPtr ptr);
    [Signature(Signatures.SetMatrices, DetourName = nameof(SetMatricesFn))]
    private Hook<SetMatricesDelegate>? SetMatricesHook = null;

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