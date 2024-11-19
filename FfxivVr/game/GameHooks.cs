using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Silk.NET.DXGI;
using System;
using System.Collections.Generic;
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

    private List<Action> DisposeActions = new List<Action>();
    public void Dispose()
    {
        DisposeActions.ForEach(dispose => dispose());
        DisposeActions.Clear();
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
        InitializeHook(CreateDXGIFactoryHook, nameof(CreateDXGIFactoryHook));
        InitializeHook(MousePointScreenToClientHook, nameof(MousePointScreenToClientHook));
        InitializeHook(UpdateLetterboxingHook, nameof(UpdateLetterboxingHook));
    }
    private void InitializeHook<T>(Hook<T>? hook, string name) where T : Delegate
    {
        if (hook == null)
        {
            logger.Error($"Failed to initialize hook {name}, signature not found");
        }
        else
        {
            hook.Enable();
            DisposeActions.Add(() => DisposeHook(hook));
        }
    }

    public delegate ulong FrameworkTickDelegate(Framework* FrameworkInstance);
    [Signature(Signatures.FrameworkTick, DetourName = nameof(FrameworkTickDetour))]
    public Hook<FrameworkTickDelegate>? FrameworkTickHook = null;

    private ulong FrameworkTickDetour(Framework* FrameworkInstance)
    {
        //logger.Trace("FrameworkTickDetour start");
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
        //logger.Trace("FrameworkTickDetour end");
        return returnValue;
    }

    private delegate void DXGIPresentDelegate(long a, long b);
    [Signature(Signatures.DXGIPresent, DetourName = nameof(DXGIPresentDetour))]
    private Hook<DXGIPresentDelegate>? DXGIPresentHook = null;
    private void DXGIPresentDetour(long a, long b)
    {
        //logger.Trace("DXGIPresentDetour start");
        var shouldPresent = true;
        exceptionHandler.FaultBarrier(() =>
        {
            shouldPresent = vrLifecycle.PrePresent();
        });
        if (shouldPresent)
        {
            DXGIPresentHook!.Original(a, b);
        }
        //logger.Trace("DXGIPresentDetour end");
    }


    private delegate void SetMatricesDelegate(FFXIVClientStructs.FFXIV.Client.Game.Camera* camera, IntPtr ptr);
    [Signature(Signatures.SetMatrices, DetourName = nameof(SetMatricesDetour))]
    private Hook<SetMatricesDelegate>? SetMatricesHook = null;

    private void SetMatricesDetour(FFXIVClientStructs.FFXIV.Client.Game.Camera* camera, IntPtr ptr)
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

    private delegate void RenderThreadSetRenderTargetDelegate(Device* deviceInstance, SetRenderTargetCommand* command);
    [Signature(Signatures.RenderThreadSetRenderTarget, DetourName = nameof(RenderThreadSetRenderTargetDetour))]
    private Hook<RenderThreadSetRenderTargetDelegate>? RenderThreadSetRenderTargetHook = null;

    private void RenderThreadSetRenderTargetDetour(Device* deviceInstance, SetRenderTargetCommand* command)
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

    private delegate void RenderSkeletonListDelegate(long RenderSkeletonLinkedList, float frameTiming);
    [Signature(Signatures.RenderSkeletonList, DetourName = nameof(RenderSkeletonListDetour))]
    private Hook<RenderSkeletonListDelegate>? RenderSkeletonListHook = null;

    private unsafe void RenderSkeletonListDetour(long RenderSkeletonLinkedList, float frameTiming)
    {
        RenderSkeletonListHook!.Original(RenderSkeletonLinkedList, frameTiming);
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.UpdateVisibility();
        });
    }

    private delegate void PushbackUIDelegate(ulong a, long b);
    [Signature(Signatures.PushbackUI, DetourName = nameof(PushbackUIDetour))]
    private Hook<PushbackUIDelegate>? PushbackUIHook = null;

    private void PushbackUIDetour(ulong a, long b)
    {

        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.PreUIRender();
        });
        PushbackUIHook!.Original(a, b);
    }

    private delegate int CreateDXGIFactoryDelegate(IntPtr guid, void** ppFactory);
    [Signature(Signatures.CreateDXGIFactory, DetourName = nameof(CreateDXGIFactoryDetour))]
    private Hook<CreateDXGIFactoryDelegate>? CreateDXGIFactoryHook = null;

    private unsafe int CreateDXGIFactoryDetour(IntPtr guid, void** ppFactory)
    {
        hookStatus.MarkHookAdded();
        // SteamVR requires using CreateDXGIFactory1
        logger.Debug("Redirecting to CreateDXGIFactory1");
        var api = DXGI.GetApi(null);
        fixed (Guid* guidPtr = &IDXGIFactory1.Guid)
        {
            return api.CreateDXGIFactory1(guidPtr, ppFactory);
        }
    }

    private delegate void MousePointScreenToClientDelegate(long frameworkInstance, Point* mousePos);
    [Signature(Signatures.MousePointScreenToClient, DetourName = nameof(MousePointScreenToClientDetour))]
    private Hook<MousePointScreenToClientDelegate>? MousePointScreenToClientHook = null;
    private void MousePointScreenToClientDetour(long frameworkInstance, Point* mousePos)
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

    // https://github.com/goaaats/Dalamud.FullscreenCutscenes/blob/main/Dalamud.FullscreenCutscenes/Plugin.cs
    private delegate nint UpdateLetterboxingDelegate(InternalLetterboxing* thisptr);
    [Signature(Signatures.UpdateLetterboxing, DetourName = nameof(UpdateLetterboxingDetour))]
    private Hook<UpdateLetterboxingDelegate>? UpdateLetterboxingHook = null;
    private nint UpdateLetterboxingDetour(InternalLetterboxing* internalLetterbox)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.UpdateLetterboxing(internalLetterbox);
        });
        return UpdateLetterboxingHook!.Original(internalLetterbox);
    }
}