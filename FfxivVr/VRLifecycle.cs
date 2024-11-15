using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI;
using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace FfxivVR;
public unsafe class VRLifecycle : IDisposable
{
    private readonly Logger logger;
    private readonly XR xr;
    private readonly Configuration configuration;
    private readonly GameState gameState;
    private readonly RenderPipelineInjector renderPipelineInjector;
    private VRSession? vrSession;
    private readonly HookStatus hookStatus;
    private readonly VRDiagnostics diagnostics;
    private readonly GameModifier gameModifier;

    private readonly FreeCamera freeCamera;

    public VRLifecycle(
        Logger logger,
        XR xr,
        Configuration configuration,
        GameState gameState,
        RenderPipelineInjector renderPipelineInjector,
        HookStatus hookStatus,
        VRDiagnostics diagnostics,
        GameModifier gameModifier,
        FreeCamera freeCamera)
    {
        this.logger = logger;
        this.xr = xr;
        this.configuration = configuration;
        this.gameState = gameState;
        this.renderPipelineInjector = renderPipelineInjector;
        this.hookStatus = hookStatus;
        this.diagnostics = diagnostics;
        this.gameModifier = gameModifier;
        this.freeCamera = freeCamera;
    }

    public void EnableVR()
    {
        if (vrSession != null)
        {
            logger.Error("VR is already running");
            return;
        }
        logger.Info("Starting VR");
        vrSession = new VRSession(
            xr: xr,
            logger: logger,
            device: GetDevice(),
            configuration: configuration,
            gameState: gameState,
            renderPipelineInjector: renderPipelineInjector,
            hookStatus: hookStatus,
            diagnostics: diagnostics,
            gameModifier: gameModifier,
            freeCamera: freeCamera
        );
        try
        {
            vrSession.Initialize();
        }
        catch (Exception)
        {
            logger.Info("Failed to start VR");
            vrSession.Dispose();
            vrSession = null;
            throw;
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
                vrSession?.Dispose();
            }
            vrSession = null;
        }
    }

    private static ID3D11Device* GetDevice()
    {
        return (ID3D11Device*)Device.Instance()->D3D11Forwarder;
    }

    private static ID3D11DeviceContext* GetContext()
    {
        return (ID3D11DeviceContext*)Device.Instance()->D3D11DeviceContext;
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
            return vrSession?.PrePresent(GetContext()) ?? true;
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
            vrSession?.DoCopyRenderTexture(GetContext(), eye);
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
}
