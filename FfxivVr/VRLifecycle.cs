using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Silk.NET.Direct3D11;
using System;

namespace FfxivVR;
public unsafe class VRLifecycle : IDisposable
{
    private Logger logger;
    private readonly string openxrDllPath;

    public VRLifecycle(Logger logger, String openxrDllPath)
    {
        this.logger = logger;
        this.openxrDllPath = openxrDllPath;
    }

    private VRSession? vrSession;
    public void EnableVR()
    {
        logger.Info("Starting VR");
        DisableVR();
        vrSession = new VRSession(
            this.openxrDllPath,
            logger,
            device: GetDevice()
        );
        vrSession.Initialize();
    }
    public void DisableVR()
    {
        logger.Info("Stopping VR");
        vrSession?.Dispose();
        vrSession = null;
    }

    private static ID3D11Device* GetDevice()
    {
        return (ID3D11Device*)Device.Instance()->D3D11Forwarder;
    }

    private static ID3D11DeviceContext* GetContext()
    {
        return (ID3D11DeviceContext*)Device.Instance()->D3D11DeviceContext;
    }


    public void StartFrame()
    {
        vrSession?.StartFrame(GetContext());
    }

    public void EndFrame()
    {
        var renderTargetManager = RenderTargetManager.Instance();
        Texture* texture = renderTargetManager->RenderTargets2[33];
        vrSession?.EndFrame(GetContext());//, (ID3D11Texture2D*)texture->D3D11Texture2D);
    }

    public void Dispose()
    {
        DisableVR();
    }
}
