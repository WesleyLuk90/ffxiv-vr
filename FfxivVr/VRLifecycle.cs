using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using Silk.NET.Direct3D11;
using System;
using System.Numerics;

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


    public void StartFrame()
    {
        vrSession?.StartFrame(GetContext());
    }

    public bool SecondRender()
    {
        return vrSession?.SecondRender(GetContext()) ?? false;
    }

    public void EndFrame()
    {
        var renderTargetManager = RenderTargetManager.Instance();
        Texture* texture = GetGameRenderTexture(renderTargetManager);
        vrSession?.EndFrame(GetContext(), texture);
    }

    private static FFXIVClientStructs.Interop.Pointer<Texture> GetGameRenderTexture(RenderTargetManager* renderTargetManager)
    {
        return renderTargetManager->RenderTargets2[33];
    }

    public void Dispose()
    {
        DisableVR();
    }

    internal void UpdateViewMatrix(Matrix4x4* viewMatrix)
    {
        vrSession?.UpdateViewMatrix(viewMatrix);
    }

    internal void UpdateCamera(Camera* camera)
    {
        vrSession?.UpdateCamera(camera);
    }
}
