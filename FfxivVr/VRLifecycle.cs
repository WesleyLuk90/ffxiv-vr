using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Silk.NET.Direct3D11;
using System;

namespace FfxivVR;
public unsafe class VRLifecycle : IDisposable
{
    private Logger logger;
    private readonly string openxrDllPath;
    private readonly VRSettings settings;
    private readonly GameState gameState;
    private readonly RenderPipelineInjector renderPipelineInjector;

    public VRLifecycle(Logger logger, String openxrDllPath, VRSettings settings, GameState gameState, RenderPipelineInjector renderPipelineInjector)
    {
        this.logger = logger;
        this.openxrDllPath = openxrDllPath;
        this.settings = settings;
        this.gameState = gameState;
        this.renderPipelineInjector = renderPipelineInjector;
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
            settings: settings,
            gameState: gameState,
            renderPipelineInjector: renderPipelineInjector
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
    public void DisableVR()
    {
        if (vrSession is VRSession session)
        {
            session.State.SessionRunning = false;
            disposeTimer = 100;
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


    public void PostPresent()
    {
        lock (this)
        {
            vrSession?.PostPresent(GetContext());
        }
    }

    public bool SecondRender()
    {
        lock (this)
        {
            return vrSession?.SecondRender(GetContext()) ?? false;
        }
    }

    public void PrePresent()
    {
        lock (this)
        {
            var renderTargetManager = RenderTargetManager.Instance();
            Texture* texture = GetGameRenderTexture(renderTargetManager);
            vrSession?.PrePresent(GetContext(), texture);
        }
    }

    private static FFXIVClientStructs.Interop.Pointer<Texture> GetGameRenderTexture(RenderTargetManager* renderTargetManager)
    {
        return renderTargetManager->RenderTargets2[33];
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

    internal void DoCopyRenderTexture(bool isLeft)
    {
        lock (this)
        {
            vrSession?.DoCopyRenderTexture(GetContext(), isLeft);
        }
    }

    private int disposeTimer = -1;
    internal void FrameworkUpdate()
    {
        if (disposeTimer > 0)
        {
            disposeTimer--;
            if (disposeTimer == 0)
            {
                RealDisableVR();
            }
        }
    }
}
