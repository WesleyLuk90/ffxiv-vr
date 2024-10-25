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

    public VRLifecycle(Logger logger, String openxrDllPath, VRSettings settings, GameState gameState)
    {
        this.logger = logger;
        this.openxrDllPath = openxrDllPath;
        this.settings = settings;
        this.gameState = gameState;
    }

    private VRSession? vrSession;
    public void EnableVR()
    {
        logger.Info("Starting VR");
        DisableVR();
        vrSession = new VRSession(
            this.openxrDllPath,
            logger,
            device: GetDevice(),
            settings: settings,
            gameState: gameState
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
        if (vrSession != null)
        {
            logger.Info("Stopping VR");
            vrSession?.Dispose();
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
        vrSession?.PostPresent(GetContext());
    }

    public bool SecondRender()
    {
        return vrSession?.SecondRender(GetContext()) ?? false;
    }

    public void PrePresent()
    {
        var renderTargetManager = RenderTargetManager.Instance();
        Texture* texture = GetGameRenderTexture(renderTargetManager);
        vrSession?.PrePresent(GetContext(), texture);
    }

    private static FFXIVClientStructs.Interop.Pointer<Texture> GetGameRenderTexture(RenderTargetManager* renderTargetManager)
    {
        return renderTargetManager->RenderTargets2[33];
    }

    public void Dispose()
    {
        DisableVR();
    }

    internal void UpdateCamera(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* camera)
    {
        vrSession?.UpdateCamera(camera);
    }

    internal void RecenterCamera()
    {
        vrSession?.RecenterCamera();
    }
    internal void ConfigureUIRender()
    {
        vrSession?.ConfigureUIRender(GetContext());
    }

    internal void UpdateVisibility()
    {
        vrSession?.UpdateVisibility();
    }
}
