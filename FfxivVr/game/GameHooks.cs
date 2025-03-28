using Dalamud.Game.ClientState.GamePad;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Common.Math;
using Silk.NET.DXGI;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Drawing;
using static FfxivVR.RenderPipelineInjector;
using CSRay = FFXIVClientStructs.FFXIV.Client.Graphics.Ray;

namespace FfxivVR;
public unsafe class GameHooks(
    VRLifecycle vrLifecycle,
    ExceptionHandler exceptionHandler,
    Logger logger,
    HookStatus hookStatus,
    IGameInteropProvider gameInteropProvider,
    GameState gameState
) : IDisposable
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
        gameInteropProvider.InitializeFromAttributes(this);
        InitializeHook(FrameworkTickHook, nameof(FrameworkTickHook));
        InitializeHook(DXGIPresentHook, nameof(DXGIPresentHook));
        InitializeHook(SetMatricesHook, nameof(SetMatricesHook));
        InitializeHook(RenderThreadSetRenderTargetHook, nameof(RenderThreadSetRenderTargetHook));
        InitializeHook(RenderSkeletonListHook, nameof(RenderSkeletonListHook));
        InitializeHook(PushbackUIHook, nameof(PushbackUIHook));
        InitializeHook(CreateDXGIFactoryHook, nameof(CreateDXGIFactoryHook));
        InitializeHook(MousePointScreenToClientHook, nameof(MousePointScreenToClientHook));
        InitializeHook(UpdateLetterboxingHook, nameof(UpdateLetterboxingHook));
        InitializeHook(GamepadPollHook, nameof(GamepadPollHook));
        InitializeHook(MousePointToRayHook, nameof(MousePointToRayHook));
        InitializeHook(shouldDrawGameObjectHook, nameof(shouldDrawGameObjectHook));
        InitializeHook(RMIFlyHook, nameof(RMIFlyHook));
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
    [Signature("40 53 48 83 EC 20 FF 81 D0 16 00 00 48 8B D9 48 8D 4C 24 30", DetourName = nameof(FrameworkTickDetour))]
    public Hook<FrameworkTickDelegate>? FrameworkTickHook = null;

    private ulong FrameworkTickDetour(Framework* FrameworkInstance)
    {
        logger.Trace("FrameworkTickDetour start");
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
        logger.Trace("FrameworkTickDetour end");
        return returnValue;
    }

    private delegate void DXGIPresentDelegate(long a, long b);
    [Signature("E8 ?? ?? ?? ?? C6 43 79 00", DetourName = nameof(DXGIPresentDetour))]
    private Hook<DXGIPresentDelegate>? DXGIPresentHook = null;
    private void DXGIPresentDetour(long a, long b)
    {
        logger.Trace("DXGIPresentDetour");
        var shouldPresent = true;
        exceptionHandler.FaultBarrier(() =>
        {
            shouldPresent = vrLifecycle.PrePresent();
        });
        if (shouldPresent)
        {
            DXGIPresentHook!.Original(a, b);
        }
    }


    private delegate void SetMatricesDelegate(FFXIVClientStructs.FFXIV.Client.Game.Camera* camera, IntPtr ptr);
    [Signature("E8 ?? ?? ?? ?? 0F 10 43 ?? C6 83", DetourName = nameof(SetMatricesDetour))]
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
    [Signature("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? F3 0F 10 5F 18", DetourName = nameof(RenderThreadSetRenderTargetDetour))]
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
    [Signature("E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? 48 8B 6C 24 ?? 48 8B 5C 24", DetourName = nameof(RenderSkeletonListDetour))]
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
    [Signature("E8 ?? ?? ?? ?? EB ?? E8 ?? ?? ?? ?? 4C 8D 5C 24 50", DetourName = nameof(PushbackUIDetour))]
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
    [Signature("E8 ?? ?? ?? ?? 85 C0 0F ?? ?? ?? ?? ?? 48 8B 8F ?? ?? 00 00 4C 8D 44 24", DetourName = nameof(CreateDXGIFactoryDetour))]
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
    [Signature("E8 ?? ?? ?? ?? 48 8B 4B ?? 48 8D 54 24 ?? FF 15", DetourName = nameof(MousePointScreenToClientDetour))]
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
    [Signature("E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ??", DetourName = nameof(UpdateLetterboxingDetour))]
    private Hook<UpdateLetterboxingDelegate>? UpdateLetterboxingHook = null;
    private nint UpdateLetterboxingDetour(InternalLetterboxing* internalLetterbox)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.UpdateLetterboxing(internalLetterbox);
        });
        return UpdateLetterboxingHook!.Original(internalLetterbox);
    }

    // https://github.com/goatcorp/Dalamud/blob/4c9b2a1577f8cd8c8b99e828d174b7122730e808/Dalamud/Game/ClientState/ClientStateAddressResolver.cs#L47
    private delegate int GamepadPollDelegate(PadDevice* thisptr);
    [Signature("40 55 53 57 41 54 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 44 0F 29 B4 24", DetourName = nameof(GamepadPollDetour))]
    private Hook<GamepadPollDelegate>? GamepadPollHook = null;
    private int GamepadPollDetour(PadDevice* gamepadInput)
    {
        logger.Trace("GamepadPollDetour");
        var returnVaue = GamepadPollHook!.Original(gamepadInput);
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.UpdateGamepad(gamepadInput);
        });
        return returnVaue;
    }
    private delegate CSRay* MousePointToRay(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* gameCamera, CSRay* ray, int mousePosX, int mousePosY);
    // https://github.com/ProjectMimer/xivr-Ex/blob/main/xivr-Ex/xivr_hooks.cs#L2191
    [Signature("E8 ?? ?? ?? ?? 4C 8B E0 48 8B EB", DetourName = nameof(MousePointToRayDetour))]
    private Hook<MousePointToRay>? MousePointToRayHook = null;

    private CSRay* MousePointToRayDetour(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* gameCamera, CSRay* ray, int mousePosX, int mousePosY)
    {
        var value = MousePointToRayHook!.Original(gameCamera, ray, mousePosX, mousePosY);
        exceptionHandler.FaultBarrier(() =>
        {
            if (vrLifecycle.GetTargetRay(gameCamera) is CSRay ray)
            {
                *value = ray;
            }
        });
        return value;
    }

    [Signature("E8 ?? ?? ?? ?? 84 C0 75 18 48 8D 0D ?? ?? ?? ?? B3 01 ?? ?? ?? ?? ?? ??", DetourName = nameof(ShouldDrawGameObjectDetour))]
    private Hook<CameraBase.Delegates.ShouldDrawGameObject> shouldDrawGameObjectHook = null!;

    public bool ShouldDrawGameObjectDetour(CameraBase* thisPtr, GameObject* gameObject, Vector3* sceneCameraPos, Vector3* lookAtVector)
    {
        var shouldDraw = shouldDrawGameObjectHook!.Original(thisPtr, gameObject, sceneCameraPos, lookAtVector);
        exceptionHandler.FaultBarrier(() =>
        {
            shouldDraw = vrLifecycle.ShouldDrawGameObject(shouldDraw, gameObject, new Vector3D<float>(sceneCameraPos->X, sceneCameraPos->Y, sceneCameraPos->Z), new Vector3D<float>(lookAtVector->X, lookAtVector->Y, lookAtVector->Z));
        });
        return shouldDraw;
    }

    // https://github.com/awgil/ffxiv_navmesh/blob/master/vnavmesh/Movement/OverrideMovement.cs#L61
    private delegate void RMIFlyDelegate(void* self, PlayerMoveControllerFlyInput* result);
    [Signature("E8 ?? ?? ?? ?? 0F B6 0D ?? ?? ?? ?? B8", DetourName = nameof(RMIFlyDetour))]
    private Hook<RMIFlyDelegate> RMIFlyHook = null!;
    private void RMIFlyDetour(void* self, PlayerMoveControllerFlyInput* result)
    {
        RMIFlyHook.Original(self, result);
        exceptionHandler.FaultBarrier(() =>
        {
            if (result->Up == 0 && result->Forward != 0 && vrLifecycle.ShouldDisableCameraVerticalFly())
            {
                // If this is non-zero then it overwrites the vertial movement so set it to a small value
                result->Up = 0.0001f;
            }
        });
    }
}