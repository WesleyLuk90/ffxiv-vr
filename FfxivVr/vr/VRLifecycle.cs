using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Drawing;
using static FfxivVR.VRSystem;
using CSRay = FFXIVClientStructs.FFXIV.Client.Graphics.Ray;

namespace FfxivVR;

public unsafe class VRLifecycle(
        IServiceScopeFactory scopeFactory,
        Logger logger,
        ExceptionHandler exceptionHandler
) : IDisposable
{
    private VRStatus status = VRStatus.Off.Instance;

    private VRSession? ActiveSession => (status as VRStatus.Active)?.Session;
    private VRSession? RunningSession => (status as VRStatus.Running)?.Session;

    public void EnableVR()
    {
        exceptionHandler.ClearExceptions();
        if (status is VRStatus.Active)
        {
            logger.Error("VR is already running");
            return;
        }
        logger.Info("Starting VR");
        var scope = scopeFactory.CreateScope();
        try
        {
            var session = scope.ServiceProvider.GetRequiredService<VRSession>();
            session.Initialize();
            lock (this)
            {
                status = new VRStatus.Idle(scope, session);
            }
        }
        catch (Exception e)
        {
            if (e is FormFactorUnavailableException)
            {
                logger.Error("Failed to start VR, headset not found");
            }
            else if (e is ShaderModDetected)
            {
                logger.Error("GShade or ReShade is not supported using SteamVR. Either delete GShade/ReShade or use a different runtime.");
            }
            else if (e is MissingDXHook)
            {
                logger.Error("SteamVR requires 'Dalamud Settings > Wait for plugins before game loads' to be enabled. Please enable the setting and restart the game.");
            }
            else
            {
                logger.Error($"Failed to start VR {e}");
            }
            scope.Dispose();
        }
    }

    public bool IsEnabled()
    {
        return status is VRStatus.Active;
    }

    public void RequestStop()
    {
        lock (this)
        {
            if (status is VRStatus.RestartPending)
            {
                status = VRStatus.Off.Instance;
            }
            else
            {
                Stop(StopReason.UserRequested);
            }
        }
    }

    public void RequestRestart()
    {
        lock (this)
        {
            Stop(StopReason.Restart);
        }
    }

    public bool TakePendingRestart()
    {
        lock (this)
        {
            if (status is not VRStatus.RestartPending)
            {
                return false;
            }
            status = VRStatus.Off.Instance;
            return true;
        }
    }

    private void Stop(StopReason reason)
    {
        switch (status)
        {
            case VRStatus.Stopping { Reason: StopReason.Restart } restarting when reason is StopReason.UserRequested or StopReason.PluginUnloaded:
                status = new VRStatus.Stopping(restarting.Scope, restarting.Session, reason);
                break;
            case VRStatus.Stopping:
                break;
            case VRStatus.Active active:
                status = new VRStatus.Stopping(active.Scope, active.Session, reason);
                break;
        }
    }

    private void StopVR(VRStatus.Stopping stopping)
    {
        logger.Info("Stopping VR");
        logger.Debug($"Stop reason: {stopping.Reason}");
        stopping.Scope.Dispose();
        status = stopping.Reason == StopReason.Restart ? VRStatus.RestartPending.Instance : VRStatus.Off.Instance;
    }

    public bool ShouldRerunGameLoop()
    {
        lock (this)
        {
            return ActiveSession?.ShouldRerunGameLoop() ?? false;
        }
    }

    internal void OnExecuteDrawCommands(FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.ImmediateContext* context, int commandListId, Action executeOriginal)
    {
        lock (this)
        {
            if (RunningSession is VRSession session)
            {
                session.ExecuteCommands(context, commandListId, executeOriginal);
            }
            else
            {
                executeOriginal();
            }
        }
    }

    public bool OnPresentFrame()
    {
        lock (this)
        {
            if (exceptionHandler.HasException())
            {
                Stop(StopReason.Exception);
            }
            if (status is VRStatus.Active active and not VRStatus.Stopping)
            {
                status = active.Session.PollEvents() switch
                {
                    SessionEvent.Began => new VRStatus.Running(active.Scope, active.Session),
                    SessionEvent.Ended => new VRStatus.Idle(active.Scope, active.Session),
                    SessionEvent.Exited exited => new VRStatus.Stopping(active.Scope, active.Session, exited.Reason),
                    _ => status,
                };
            }
            switch (status)
            {
                case VRStatus.Stopping stopping:
                    StopVR(stopping);
                    return true;
                case VRStatus.Running running:
                    return running.Session.OnPresentFrame();
                default:
                    return true;
            }
        }
    }

    public void Dispose()
    {
        lock (this)
        {
            Stop(StopReason.PluginUnloaded);
            if (status is VRStatus.Stopping stopping)
            {
                StopVR(stopping);
            }
        }
    }

    internal void UpdateCamera(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* camera)
    {
        lock (this)
        {
            RunningSession?.UpdateCamera(camera);
        }
    }

    internal void RecenterCamera()
    {
        lock (this)
        {
            ActiveSession?.RecenterCamera();
        }
    }

    internal void UpdateVisibility()
    {
        lock (this)
        {
            RunningSession?.UpdateVisibility();
        }
    }

    internal void OnStartUI()
    {
        lock (this)
        {
            RunningSession?.OnStartUI();
        }
    }

    internal void OnStartUIRender(Eye? eye)
    {
        lock (this)
        {
            RunningSession?.OnStartUIRender(eye);
        }
    }

    internal void OnStartGameLoop()
    {
        lock (this)
        {
            try
            {
                if (ActiveSession is VRSession session)
                {
                    var ticks = session.OnStartGameLoop();
                    if (status is VRStatus.Running)
                    {
                        session.PrepareFrame(ticks);
                    }
                }
            }
            catch (FatalVRException e)
            {
                logger.Info($"Encountered a fatal VR error {e}");
                Stop(StopReason.FatalError);
                throw;
            }
        }
    }
    internal Point? ComputeMousePosition(Point point)
    {
        lock (this)
        {
            return ActiveSession?.ComputeMousePosition(point);
        }
    }

    internal void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        lock (this)
        {
            ActiveSession?.OnNamePlateUpdate(context, handlers);
        }
    }

    internal void UpdateGamepad(PadDevice* gamepadInput)
    {
        lock (this)
        {
            RunningSession?.UpdateGamepad(gamepadInput);
        }
    }

    internal CSRay? GetTargetRay(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* camera, int mouseX, int mouseY)
    {
        lock (this)
        {
            return RunningSession?.GetTargetRay(camera, mouseX, mouseY);
        }
    }

    internal bool ShouldDrawGameObject(bool shouldDraw, GameObject* gameObject, Silk.NET.Maths.Vector3D<float> cameraPosition, Silk.NET.Maths.Vector3D<float> lookAtPosition)
    {
        lock (this)
        {
            return ActiveSession?.ShouldDrawGameObject(shouldDraw, gameObject, cameraPosition, lookAtPosition) ?? shouldDraw;
        }
    }

    internal Silk.NET.Maths.Vector3D<float>? GetPlayerHeadLookAtTarget(GameObject* gameObject, int attachBoneIndex)
    {
        lock (this)
        {
            return RunningSession?.GetPlayerHeadLookAtTarget(gameObject, attachBoneIndex);
        }
    }

    internal bool ShouldDisableCameraVerticalFly()
    {
        lock (this)
        {
            return ActiveSession?.ShouldDisableCameraVerticalFly() ?? false;
        }
    }
}