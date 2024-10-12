using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;

namespace FfxivVR;
unsafe internal class GameHooks : IDisposable
{
    public GameHooks(VRLifecycle vrLifecycle, ExceptionHandler exceptionHandler)
    {
        this.vrLifecycle = vrLifecycle;
        this.exceptionHandler = exceptionHandler;
    }
    public void Dispose()
    {
        FrameworkTickHook?.Disable();
        FrameworkTickHook?.Dispose();
        DXGIPresentHook?.Disable();
        DXGIPresentHook?.Dispose();
    }

    public void Initialize()
    {
        FrameworkTickHook!.Enable();
        DXGIPresentHook!.Enable();
    }
    public delegate UInt64 FrameworkTickDg(Framework* FrameworkInstance);
    [Signature(Signatures.FrameworkTick, DetourName = nameof(FrameworkTickFn))]
    public Hook<FrameworkTickDg>? FrameworkTickHook = null;
    private UInt64 FrameworkTickFn(Framework* FrameworkInstance)
    {
        return FrameworkTickHook!.Original(FrameworkInstance);
    }

    private delegate void DXGIPresentDg(UInt64 a, UInt64 b);
    [Signature(Signatures.DXGIPresent, DetourName = nameof(DXGIPresentFn))]
    private Hook<DXGIPresentDg>? DXGIPresentHook = null;
    private readonly VRLifecycle vrLifecycle;
    private readonly ExceptionHandler exceptionHandler;

    private void DXGIPresentFn(UInt64 a, UInt64 b)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.StartFrame();
        });
        DXGIPresentHook!.Original(a, b);
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.EndFrame();
        });
    }
}