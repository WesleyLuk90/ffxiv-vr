using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using Silk.NET.DXGI;
using System;
using System.Drawing;
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
    private readonly HookStatus hookStatus;
    private readonly GameState gameState;

    public GameHooks(VRLifecycle vrLifecycle, ExceptionHandler exceptionHandler, Logger logger, HookStatus hookStatus, GameState gameState)
    {
        this.vrLifecycle = vrLifecycle;
        this.exceptionHandler = exceptionHandler;
        this.logger = logger;
        this.hookStatus = hookStatus;
        this.gameState = gameState;
    }
    public void Dispose()
    {
        DisposeHook(FrameworkTickHook);
        DisposeHook(DXGIPresentHook);
        DisposeHook(SetMatricesHook);
        DisposeHook(RenderThreadSetRenderTargetHook);
        DisposeHook(RenderSkeletonListHook);
        DisposeHook(PushbackUIHook);
        DisposeHook(NamePlateDrawHook);
        DisposeHook(CreateDXGIFactoryHook);
        DisposeHook(MousePointScreenToClientHook);
    }

    private void DisposeHook<T>(Hook<T>? hook) where T : Delegate
    {
        hook?.Disable();
        hook?.Dispose();
    }

    public void Initialize()
    {
        InitializeHook(FrameworkTickHook, nameof(FrameworkTickHook));
        InitializeHook(DXGIPresentHook, nameof(DXGIPresentHook));
        InitializeHook(SetMatricesHook, nameof(SetMatricesHook));
        InitializeHook(RenderThreadSetRenderTargetHook, nameof(RenderThreadSetRenderTargetHook));
        InitializeHook(RenderSkeletonListHook, nameof(RenderSkeletonListHook));
        InitializeHook(PushbackUIHook, nameof(PushbackUIHook));
        InitializeHook(NamePlateDrawHook, nameof(NamePlateDrawHook));
        InitializeHook(CreateDXGIFactoryHook, nameof(CreateDXGIFactoryHook));
        InitializeHook(MousePointScreenToClientHook, nameof(MousePointScreenToClientHook));
    }
    private void InitializeHook<T>(Hook<T>? hook, string name) where T : Delegate
    {
        if (hook == null)
        {
            logger.Error($"Failed to initialize hook {name}, signature not found");
        }
        hook?.Enable();
    }

    public delegate UInt64 FrameworkTickDg(Framework* FrameworkInstance);
    [Signature(Signatures.FrameworkTick, DetourName = nameof(FrameworkTickFn))]
    public Hook<FrameworkTickDg>? FrameworkTickHook = null;

    private UInt64 FrameworkTickFn(Framework* FrameworkInstance)
    {
        //logger.Trace("FrameworkTickFn start");
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.PrepareVRRender();
        });
        var returnValue = FrameworkTickHook!.Original(FrameworkInstance);
        var shouldSecondRender = false;
        exceptionHandler.FaultBarrier(() =>
        {
            shouldSecondRender = vrLifecycle.ShouldSecondRender();
        });
        if (shouldSecondRender)
        {
            // This can cause crashes if the plugin is unloaded during while running the second tick
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
            var currentCamera = gameState.GetCurrentCamera();
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

    private delegate void MousePointScreenToClientDg(UInt64 frameworkInstance, Point* mousePos);
    [Signature(Signatures.MousePointScreenToClient, DetourName = nameof(MousePointScreenToClientFn))]
    private Hook<MousePointScreenToClientDg>? MousePointScreenToClientHook = null;
    private void MousePointScreenToClientFn(UInt64 frameworkInstance, Point* mousePos)
    {
        MousePointScreenToClientHook!.Original(frameworkInstance, mousePos);
        exceptionHandler.FaultBarrier(() =>
        {
            var newPosition = vrLifecycle.ComputeMousePosition(*mousePos);
            if (newPosition is Point point)
            {
                *mousePos = point;
            }
        });
    }
}