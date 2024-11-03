using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using Silk.NET.DXGI;
using System;
using static FfxivVR.RenderPipelineInjector;

namespace FfxivVR;
unsafe public class GameHooks : IDisposable
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
    private readonly HookStatus hookStatus;

    public GameHooks(VRLifecycle vrLifecycle, ExceptionHandler exceptionHandler, Logger logger, RenderPipelineInjector renderPipelineInjector, HookStatus hookStatus)
    {
        this.vrLifecycle = vrLifecycle;
        this.exceptionHandler = exceptionHandler;
        this.logger = logger;
        this.renderPipelineInjector = renderPipelineInjector;
        this.hookStatus = hookStatus;
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
        CreateDXGIFactoryHook?.Disable();
        CreateDXGIFactoryHook?.Dispose();
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
        CreateDXGIFactoryHook?.Enable();
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
        var shouldPresent = true;
        exceptionHandler.FaultBarrier(() =>
        {
            shouldPresent = vrLifecycle.PrePresent();
        });
        if (shouldPresent)
        {
            DXGIPresentHook!.Original(a, b);
        }
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
            var manager = CameraManager.Instance();
            if (manager == null)
            {
                return;
            }
            var currentCamera = manager->CurrentCamera;
            if (currentCamera == null)
            {
                return;
            }
            if (currentCamera->RenderCamera == camera)
            {
                vrLifecycle.UpdateCamera(currentCamera);
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

    private delegate int CreateDXGIFactoryDg(IntPtr guid, void** ppFactory);
    [Signature(Signatures.CreateDXGIFactory, DetourName = nameof(CreateDXGIFactoryFn))]
    private Hook<CreateDXGIFactoryDg>? CreateDXGIFactoryHook = null;

    private unsafe int CreateDXGIFactoryFn(IntPtr guid, void** ppFactory)
    {
        hookStatus.DXGICreateHooked = true;
        // SteamVR requires using CreateDXGIFactory1
        logger.Debug("Redirecting to CreateDXGIFactory1");
        var api = DXGI.GetApi(null);
        fixed (Guid* guidPtr = &IDXGIFactory1.Guid)
        {
            return api.CreateDXGIFactory1(guidPtr, ppFactory);
        }
    }
}