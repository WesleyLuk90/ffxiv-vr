using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.Gui.NamePlate;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Drawing;
using static FfxivVR.VRSystem;
using CSRay = FFXIVClientStructs.FFXIV.Client.Graphics.Ray;

namespace FfxivVR;
public unsafe class VRLifecycle : IDisposable
{
    private readonly IServiceScopeFactory scopeFactory;
    private IServiceScope? scope;
    public VRSession? vrSession;
    private readonly Logger logger;
    private readonly Configuration configuration;
    private readonly GameModifier gameModifier;

    private readonly Debugging debugging;

    public VRLifecycle(
        IServiceScopeFactory scopeFactory,
        Logger logger,
        Configuration configuration,
        GameModifier gameModifier,
        Debugging debugging)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        this.configuration = configuration;
        this.gameModifier = gameModifier;
        this.debugging = debugging;
    }


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
            if (e is MissingDXHook)
            {
                logger.Error("SteamVR requires Dalamud Settings > Wait for plugins before game loads to be enabled. Please enable the setting and restart the game.");
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
            gameModifier.HideHeadMesh(force: true);
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
            gameModifier.UpdateLetterboxing(internalLetterbox);
        }
    }

    internal void UpdateGamepad(GamepadInput* gamepadInput)
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
}