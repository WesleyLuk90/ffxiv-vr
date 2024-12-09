using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System;
using System.Collections.Generic;
using System.Drawing;
using static FfxivVR.VRSystem;

namespace FfxivVR;
public unsafe class VRLifecycle : IDisposable
{
    private readonly Logger logger;
    private readonly XR xr;
    private readonly Configuration configuration;
    private readonly GameState gameState;
    private readonly RenderPipelineInjector renderPipelineInjector;
    private IHost? host;
    private VRSession? vrSession;
    private readonly HookStatus hookStatus;
    private readonly GameModifier gameModifier;

    private readonly FreeCamera freeCamera;

    public VRLifecycle(
        Logger logger,
        XR xr,
        Configuration configuration,
        GameState gameState,
        RenderPipelineInjector renderPipelineInjector,
        HookStatus hookStatus,
        GameModifier gameModifier,
        FreeCamera freeCamera)
    {
        this.logger = logger;
        this.xr = xr;
        this.configuration = configuration;
        this.gameState = gameState;
        this.renderPipelineInjector = renderPipelineInjector;
        this.hookStatus = hookStatus;
        this.gameModifier = gameModifier;
        this.freeCamera = freeCamera;
    }

    private IHost CreateSession()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(xr);
        builder.Services.AddSingleton(logger);
        builder.Services.AddSingleton(new DxDevice(GetDevice()));
        builder.Services.AddSingleton(configuration);
        builder.Services.AddSingleton(gameState);
        builder.Services.AddSingleton(renderPipelineInjector);
        builder.Services.AddSingleton(hookStatus);
        builder.Services.AddSingleton(gameModifier);
        builder.Services.AddSingleton(freeCamera);
        builder.Services.AddSingleton<VRSystem>();
        builder.Services.AddSingleton<VRState>();
        builder.Services.AddSingleton<VRSwapchains>();
        builder.Services.AddSingleton<Resources>();
        builder.Services.AddSingleton<VRSpace>();
        builder.Services.AddSingleton<VRCamera>();
        builder.Services.AddSingleton<ResolutionManager>();
        builder.Services.AddSingleton<Renderer>();
        builder.Services.AddSingleton<WaitFrameService>();
        builder.Services.AddSingleton<VRInput>();
        builder.Services.AddSingleton<IVRInput>(x => x.GetRequiredService<VRInput>());
        builder.Services.AddSingleton<EventHandler>();
        builder.Services.AddSingleton<FramePrediction>();
        builder.Services.AddSingleton<VRSession>();
        builder.Services.AddSingleton<VRShaders>();
        builder.Services.AddSingleton<DalamudRenderer>();
        builder.Services.AddSingleton<InputManager>();
        builder.Services.AddSingleton<VRUI>();
        builder.Services.AddSingleton<GameClock>();
        return builder.Build();
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
            var host = CreateSession();
            this.host = host;
            vrSession = host.Services.GetRequiredService<VRSession>(); ;

            vrSession.Initialize();
        }
        catch (Exception e)
        {
            if (e is FormFactorUnavailableException)
            {
                logger.Error("Failed to start VR, headset not found");
            }
            else
            {
                logger.Error($"Failed to start VR {e}");
            }
            vrSession = null;
            this.host?.Dispose();
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
                host?.Dispose();
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
        if (Debugging.HideHead)
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

    internal Ray? GetTargetRay(FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* camera)
    {
        lock (this)
        {
            return vrSession?.GetTargetRay(camera);
        }
    }
}