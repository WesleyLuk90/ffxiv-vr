using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI;
using Silk.NET.Direct3D11;
using System;

namespace FfxivVR;
public unsafe class VRLifecycle : IDisposable
{
    private Logger logger;
    private readonly string openxrDllPath;
    private readonly Configuration configuration;
    private readonly GameState gameState;
    private readonly RenderPipelineInjector renderPipelineInjector;
    private readonly IGameGui gameGui;
    private readonly IClientState clientState;
    private readonly ITargetManager targetManager;


    public VRLifecycle(
        Logger logger,
        string openxrDllPath,
        Configuration configuration,
        GameState gameState,
        RenderPipelineInjector renderPipelineInjector,
        IGameGui gameGui,
        IClientState clientState,
        ITargetManager targetManager
        )
    {
        this.logger = logger;
        this.openxrDllPath = openxrDllPath;
        this.configuration = configuration;
        this.gameState = gameState;
        this.renderPipelineInjector = renderPipelineInjector;
        this.gameGui = gameGui;
        this.clientState = clientState;
        this.targetManager = targetManager;
    }

    private VRSession? vrSession;
    public void EnableVR()
    {
        if (vrSession != null)
        {
            logger.Error("VR is already running");
            return;
        }
        logger.Info("Starting VR");
        vrSession = new VRSession(
            this.openxrDllPath,
            logger,
            device: GetDevice(),
            configuration: configuration,
            gameState: gameState,
            renderPipelineInjector: renderPipelineInjector,
            gameGui: gameGui,
            targetManager: targetManager,
            clientState: clientState
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
    public void PrePresent()
    {
        lock (this)
        {
            if (vrSession?.State?.Exiting == true)
            {
                RealDisableVR();
            }
            vrSession?.PrePresent(GetContext());
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

    internal void UpdateNamePlates(AddonNamePlate* namePlate)
    {
        lock (this)
        {
            vrSession?.UpdateNamePlates(namePlate);
        }
    }
}
