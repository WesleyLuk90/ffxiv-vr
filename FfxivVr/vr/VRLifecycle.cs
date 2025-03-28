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
        GameState gameState
) : IDisposable
{
    private IServiceScope? scope;
    public VRSession? vrSession;
    public void EnableVR()
    {
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
    public void DisableVR()
    {
        if (vrSession is VRSession session)
        {
            session.State.SessionRunning = false;
            session.State.Exiting = true;
        }
    }

    private void RealDisableVR()
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

    public bool ShouldSecondRender()
    {
        lock (this)
        {
            return vrSession?.ShouldSecondRender() ?? false;
        }
    }

    // Returns whether we should call the original present function
    public bool PrePresent()
    {
        lock (this)
        {
            if (vrSession?.State?.Exiting == true)
            {
                RealDisableVR();
            }
            return vrSession?.PrePresent() ?? true;
        }
    }

    public void Dispose()
    {
        RealDisableVR();
    }

    internal void UpdateCamera(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* camera)
    {
        var active = gameState.GetActiveCamera();
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

    internal void PreUIRender()
    {
        lock (this)
        {
            vrSession?.PreUIRender();
        }
    }

    internal void DoCopyRenderTexture(Eye eye)
    {
        lock (this)
        {
            vrSession?.DoCopyRenderTexture(eye);
        }
    }

    internal void PrepareVRRender()
    {
        lock (this)
        {
            vrSession?.PrepareVRRender();
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

    internal void UpdateLetterboxing(InternalLetterboxing* internalLetterbox)
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

    internal bool ShouldDisableCameraVerticalFly()
    {
        lock (this)
        {
            return vrSession?.ShouldDisableCameraVerticalFly() ?? false;
        }
    }
}