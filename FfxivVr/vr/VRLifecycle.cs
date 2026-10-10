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
        Configuration configuration,
        Debugging debugging,
        ExceptionHandler exceptionHandler
) : IDisposable
{
    private IServiceScope? scope;
    public VRSession? vrSession;

    public void EnableVR()
    {
        exceptionHandler.ClearExceptions();
        if (vrSession != null)
        {
            logger.Error("VR is already running");
            return;
        }
        logger.Info("Starting VR");
        try
        {
            var scope = scopeFactory.CreateScope();
            this.scope = scope;
            vrSession = scope.ServiceProvider.GetRequiredService<VRSession>(); ;

            vrSession.Initialize();
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
            vrSession = null;
            this.scope?.Dispose();
            this.scope = null;
        }
    }

    public bool IsEnabled()
    {
        return vrSession != null;
    }
    public void RequestStop()
    {
        if (vrSession is VRSession session)
        {
            session.State.SessionRunning = false;
            session.State.Exiting = true;
        }
    }

    // Try and only call this in the PresetFrame call, so that all threads are synchronized and not executing concurrently otherwise it can crash
    private void StopVR()
    {
        if (vrSession != null)
        {
            logger.Info("Stopping VR");
            lock (this)
            {
                this.scope?.Dispose();
                this.scope = null;
            }
            vrSession = null;
        }
    }

    public bool ShouldRerunGameLoop()
    {
        lock (this)
        {
            return vrSession?.ShouldRerunGameLoop() ?? false;
        }
    }

    internal void OnExecuteDrawCommands(FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.ImmediateContext* context, int commandListId, Action executeOriginal)
    {
        lock (this)
        {
            if (vrSession == null)
            {
                executeOriginal();
            }
            else
            {
                vrSession.ExecuteCommands(context, commandListId, executeOriginal);
            }
        }
    }

    // Returns whether we should call the original present function
    // This point is synchronized between the render and game thread
    public bool OnPresentFrame()
    {
        lock (this)
        {
            if (vrSession?.State?.Exiting == true || exceptionHandler.HasException())
            {
                StopVR();
            }
            return vrSession?.OnPresentFrame() ?? true;
        }
    }

    public void Dispose()
    {
        StopVR();
    }

    internal void UpdateCamera(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* camera)
    {
        lock (this)
        {
            vrSession?.UpdateCamera(camera);
        }
    }

    internal void RecenterCamera()
    {
        lock (this)
        {
            vrSession?.RecenterCamera();
        }
    }

    internal void UpdateVisibility()
    {
        if (debugging.HideHead)
        {
            scope?.ServiceProvider.GetRequiredService<GameModifier>().HideHeadMesh(force: true);
        }
        lock (this)
        {
            vrSession?.UpdateVisibility();
        }
    }

    internal void OnStartUI()
    {
        lock (this)
        {
            vrSession?.OnStartUI();
        }
    }

    internal void OnStartUIRender(Eye? eye)
    {
        lock (this)
        {
            vrSession?.OnStartUIRender(eye);
        }
    }

    internal void OnStartGameLoop()
    {
        lock (this)
        {
            try
            {
                vrSession?.OnStartGameLoop();
            }
            catch (FatalVRException e)
            {
                logger.Info($"Encountered a fatal VR error ${e}");
                RequestStop();
                throw;
            }
        }
    }
    internal Point? ComputeMousePosition(Point point)
    {
        lock (this)
        {
            return vrSession?.ComputeMousePosition(point);
        }
    }

    internal void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        lock (this)
        {
            vrSession?.OnNamePlateUpdate(context, handlers);
        }
    }

    internal void UpdateLetterboxing(LetterboxingExtended* internalLetterbox)
    {
        // Always enable regardless of VR
        if (configuration.DisableCutsceneLetterbox)
        {
            scope?.ServiceProvider.GetRequiredService<GameModifier>().UpdateLetterboxing(internalLetterbox);
        }
    }

    internal void UpdateGamepad(PadDevice* gamepadInput)
    {
        lock (this)
        {
            vrSession?.UpdateGamepad(gamepadInput);
        }
    }

    internal CSRay? GetTargetRay(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* camera)
    {
        lock (this)
        {
            return vrSession?.GetTargetRay(camera);
        }
    }

    internal bool ShouldDrawGameObject(bool shouldDraw, GameObject* gameObject, Silk.NET.Maths.Vector3D<float> cameraPosition, Silk.NET.Maths.Vector3D<float> lookAtPosition)
    {
        lock (this)
        {
            return vrSession?.ShouldDrawGameObject(shouldDraw, gameObject, cameraPosition, lookAtPosition) ?? shouldDraw;
        }
    }

    internal Silk.NET.Maths.Vector3D<float>? GetPlayerHeadLookAtTarget(GameObject* gameObject, int attachBoneIndex)
    {
        lock (this)
        {
            return vrSession?.GetPlayerHeadLookAtTarget(gameObject, attachBoneIndex);
        }
    }

    internal bool ShouldDisableCameraVerticalFly()
    {
        lock (this)
        {
            return vrSession?.ShouldDisableCameraVerticalFly() ?? false;
        }
    }
}