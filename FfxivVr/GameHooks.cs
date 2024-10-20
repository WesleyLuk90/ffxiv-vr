﻿using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
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
    private readonly bool debugHooks = false;
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
        RunGameTasksHook?.Disable();
        RunGameTasksHook?.Dispose();
    }

    public void Initialize()
    {
        FrameworkTickHook!.Enable();
        DXGIPresentHook!.Enable();
        SetMatricesHook!.Enable();
        RunGameTasksHook!.Enable();
    }
    public delegate UInt64 FrameworkTickDg(Framework* FrameworkInstance);
    [Signature(Signatures.FrameworkTick, DetourName = nameof(FrameworkTickFn))]
    public Hook<FrameworkTickDg>? FrameworkTickHook = null;

    private UInt64 FrameworkTickFn(Framework* FrameworkInstance)
    {
        if (debugHooks)
        {
            logger.Debug("FrameworkTickFn start");
        }
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
        if (debugHooks)
        {
            logger.Debug("FrameworkTickFn end");
        }
        return returnValue;
    }

    private delegate void DXGIPresentDg(UInt64 a, UInt64 b);
    [Signature(Signatures.DXGIPresent, DetourName = nameof(DXGIPresentFn))]
    private Hook<DXGIPresentDg>? DXGIPresentHook = null;
    private void DXGIPresentFn(UInt64 a, UInt64 b)
    {
        if (debugHooks)
        {
            logger.Debug("DXGIPresentFn start");
        }
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.PrePresent();
        });
        DXGIPresentHook!.Original(a, b);
        exceptionHandler.FaultBarrier(() =>
        {
            vrLifecycle.PostPresent();
        });
        if (debugHooks)
        {
            logger.Debug("DXGIPresentFn start");
        }
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
                if (debugHooks)
                {
                    logger.Debug("SetMatricesFn start");
                }
                vrLifecycle.UpdateCamera(&FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance()->GetActiveCamera()->SceneCamera);
                if (debugHooks)
                {
                    logger.Debug("SetMatricesFn start");
                }
            }
        });
    }

    delegate void RunGameTasksDg(TaskManager* taskManager, float* frameTiming);
    [Signature(Signatures.RunGameTasks, DetourName = nameof(RunGameTasksFn))]
    private Hook<RunGameTasksDg>? RunGameTasksHook = null;

    public void RunGameTasksFn(TaskManager* taskManager, float* frameTiming)
    {
        if (debugHooks)
        {
            logger.Debug("RunGameTasksFn start");
        }
        RunGameTasksHook!.Original(taskManager, frameTiming);
        if (debugHooks)
        {
            logger.Debug("RunGameTasksFn end");
        }
    }

}