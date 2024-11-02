using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using static FfxivVR.RenderPipelineInjector;

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
    private readonly RenderPipelineInjector renderPipelineInjector;
    public GameHooks(VRLifecycle vrLifecycle, ExceptionHandler exceptionHandler, Logger logger, RenderPipelineInjector renderPipelineInjector)
    {
        this.vrLifecycle = vrLifecycle;
        this.exceptionHandler = exceptionHandler;
        this.logger = logger;
        this.renderPipelineInjector = renderPipelineInjector;
    }
    public void Dispose()
    {
        FrameworkTickHook?.Disable();
        FrameworkTickHook?.Dispose();
        DXGIPresentHook?.Disable();
        DXGIPresentHook?.Dispose();
        SetMatricesHook?.Disable();
        SetMatricesHook?.Dispose();
        RenderThreadSetRenderTargetHook?.Disable();
        RenderThreadSetRenderTargetHook?.Dispose();
        RenderSkeletonListHook?.Disable();
        RenderSkeletonListHook?.Dispose();
        PushbackUIHook?.Disable();
        PushbackUIHook?.Dispose();
        NamePlateDrawHook?.Disable();
        NamePlateDrawHook?.Dispose();
    }

    public void Initialize()
    {
        FrameworkTickHook!.Enable();
        DXGIPresentHook!.Enable();
        SetMatricesHook!.Enable();
        RenderThreadSetRenderTargetHook!.Enable();
        RenderSkeletonListHook?.Enable();
        PushbackUIHook?.Enable();
        NamePlateDrawHook?.Enable();
    }
    public delegate UInt64 FrameworkTickDg(Framework* FrameworkInstance);
    [Signature(Signatures.FrameworkTick, DetourName = nameof(FrameworkTickFn))]
    public Hook<FrameworkTickDg>? FrameworkTickHook = null;

    private UInt64 FrameworkTickFn(Framework* FrameworkInstance)
    {
        //logger.Trace("FrameworkTickFn start");
        vrLifecycle.PrepareVRRender();
        var returnValue = FrameworkTickHook!.Original(FrameworkInstance);
        var shouldSecondRender = false;
        exceptionHandler.FaultBarrier(() =>
        {
            shouldSecondRender = vrLifecycle.ShouldSecondRender();
        });
        if (shouldSecondRender)
        {
            returnValue = FrameworkTickHook!.Original(FrameworkInstance);
        }
        //logger.Trace("FrameworkTickFn end");
        return returnValue;
    }

    private delegate void DXGIPresentDg(UInt64 a, UInt64 b);
    [Signature(Signatures.DXGIPresent, DetourName = nameof(DXGIPresentFn))]
    private Hook<DXGIPresentDg>? DXGIPresentHook = null;
    private void DXGIPresentFn(UInt64 a, UInt64 b)
    {
        //logger.Trace("DXGIPresentFn start");
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.PrePresent();
        });
        DXGIPresentHook!.Original(a, b);
        //logger.Trace("DXGIPresentFn end");
    }


    private delegate void SetMatricesDelegate(FFXIVClientStructs.FFXIV.Client.Game.Camera* camera, IntPtr ptr);
    [Signature(Signatures.SetMatrices, DetourName = nameof(SetMatricesFn))]
    private Hook<SetMatricesDelegate>? SetMatricesHook = null;

    private void SetMatricesFn(FFXIVClientStructs.FFXIV.Client.Game.Camera* camera, IntPtr ptr)
    {
        SetMatricesHook!.Original(camera, ptr);
        exceptionHandler.FaultBarrier(() =>
        {
            if (camera == FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance()->GetActiveCamera()->SceneCamera.RenderCamera)
            {
                vrLifecycle.UpdateCamera(&FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance()->GetActiveCamera()->SceneCamera);
            }
        });
    }

    private delegate void RenderThreadSetRenderTargetDg(Device* deviceInstance, SetRenderTargetCommand* command);
    [Signature(Signatures.RenderThreadSetRenderTarget, DetourName = nameof(RenderThreadSetRenderTargetFn))]
    private Hook<RenderThreadSetRenderTargetDg>? RenderThreadSetRenderTargetHook = null;

    private void RenderThreadSetRenderTargetFn(Device* deviceInstance, SetRenderTargetCommand* command)
    {
        var renderTargets = command->numRenderTargets;
        if (renderTargets == LeftEyeRenderTargetNumber || renderTargets == RightEyeRenderTargetNumber)
        {
            exceptionHandler.FaultBarrier(() =>
            {
                vrLifecycle.DoCopyRenderTexture(renderTargets == LeftEyeRenderTargetNumber ? Eye.Left : Eye.Right);
            });
        }
        else
        {
            RenderThreadSetRenderTargetHook!.Original(deviceInstance, command);
        }
    }

    private delegate void RenderSkeletonListDg(UInt64 RenderSkeletonLinkedList, float frameTiming);
    [Signature(Signatures.RenderSkeletonList, DetourName = nameof(RenderSkeletonListFn))]
    private Hook<RenderSkeletonListDg>? RenderSkeletonListHook = null;

    private unsafe void RenderSkeletonListFn(UInt64 RenderSkeletonLinkedList, float frameTiming)
    {
        RenderSkeletonListHook!.Original(RenderSkeletonLinkedList, frameTiming);
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.UpdateVisibility();
        });
    }

    private delegate void PushbackUIDg(UInt64 a, UInt64 b);
    [Signature(Signatures.PushbackUI, DetourName = nameof(PushbackUIFn))]
    private Hook<PushbackUIDg>? PushbackUIHook = null;

    private void PushbackUIFn(UInt64 a, UInt64 b)
    {

        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.PreUIRender();
        });
        PushbackUIHook!.Original(a, b);
    }
    private delegate void NamePlateDrawDg(AddonNamePlate* a);
    [Signature(Signatures.NamePlateDraw, DetourName = nameof(NamePlateDrawFn))]
    private Hook<NamePlateDrawDg>? NamePlateDrawHook = null;

    private void NamePlateDrawFn(AddonNamePlate* a)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.UpdateNamePlates(a);
        });
        NamePlateDrawHook!.Original(a);
    }
}