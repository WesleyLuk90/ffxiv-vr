using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System;
using System.IO;

namespace FfxivVR;

public unsafe class AppFactory
{
    [PluginService] public static ISigScanner SigScanner { get; set; } = null!;

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] public static IPluginLog Log { get; set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; set; } = null!;
    [PluginService] public static IClientState ClientState { get; set; } = null!;
    [PluginService] public static IGameGui GameGui { get; set; } = null!;
    [PluginService] public static IGameConfig GameConfig { get; set; } = null!;
    [PluginService] public static ITargetManager TargetManager { get; set; } = null!;
    [PluginService] public static IGamepadState GamepadState { get; set; } = null!;
    [PluginService] public static IGameInteropProvider GameInteropProvider { get; set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] public static IFramework Framework { get; set; } = null!;
    [PluginService] public static INamePlateGui NamePlateGui { get; set; } = null!;
    [PluginService] public static IDtrBar DtrBar { get; set; } = null!;

    private DxDevice? device = null;
    public AppFactory()
    {
    }
    public AppFactory(DxDevice device)
    {
        this.device = device;
    }
    public IHost CreateSession()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(CreateXR());
        builder.Services.AddSingleton(LoadConfiguration());

        builder.Services.AddSingleton(PluginInterface);
        builder.Services.AddSingleton(SigScanner);
        builder.Services.AddSingleton(Log);
        builder.Services.AddSingleton(ChatGui);
        builder.Services.AddSingleton(ClientState);
        builder.Services.AddSingleton(GameGui);
        builder.Services.AddSingleton(GameConfig);
        builder.Services.AddSingleton(TargetManager);
        builder.Services.AddSingleton(GamepadState);
        builder.Services.AddSingleton(GameInteropProvider);
        builder.Services.AddSingleton(CommandManager);
        builder.Services.AddSingleton(Framework);
        builder.Services.AddSingleton(NamePlateGui);
        builder.Services.AddSingleton(DtrBar);

        builder.Services.AddSingleton<CommandHander>();
        builder.Services.AddSingleton<ConfigManager>();
        builder.Services.AddSingleton<ConfigWindow>();
        builder.Services.AddSingleton<Debugging>();
        builder.Services.AddSingleton<DebugWindow>();
        builder.Services.AddSingleton<ExceptionHandler>();
        builder.Services.AddSingleton<GameConfigManager>();
        builder.Services.AddSingleton<GameEvents>();
        builder.Services.AddSingleton<GameHooks>();
        builder.Services.AddSingleton<GameState>();
        builder.Services.AddSingleton<HookStatus>();
        builder.Services.AddSingleton<NameplateModifier>();
        builder.Services.AddSingleton<HudLayoutManager>();
        builder.Services.AddSingleton<Logger>();
        builder.Services.AddSingleton<PluginUI>();
        builder.Services.AddSingleton<SkeletonModifier>();
        builder.Services.AddSingleton<RenderPipelineInjector>();
        builder.Services.AddSingleton<Transitions>();
        builder.Services.AddSingleton<VRLifecycle>();
        builder.Services.AddSingleton<VRStartStop>();

        builder.Services.AddScoped(x => GetDevice());
        builder.Services.AddScoped<DalamudRenderer>();
        builder.Services.AddScoped<EventHandler>();
        builder.Services.AddScoped<FramePrediction>();
        builder.Services.AddScoped<GameClock>();
        builder.Services.AddScoped<InputManager>();
        builder.Services.AddScoped<Renderer>();
        builder.Services.AddScoped<ResolutionManager>();
        builder.Services.AddScoped<Resources>();
        builder.Services.AddScoped<VRCamera>();
        builder.Services.AddScoped<VRActionService>();
        builder.Services.AddScoped<VRSession>();
        builder.Services.AddScoped<VRShaders>();
        builder.Services.AddScoped<VRSpace>();
        builder.Services.AddScoped<VRState>();
        builder.Services.AddScoped<VRSwapchains>();
        builder.Services.AddScoped<VRSystem>();
        builder.Services.AddScoped<VRUI>();
        builder.Services.AddScoped<WaitFrameService>();
        builder.Services.AddScoped<RenderManager>();
        builder.Services.AddScoped<VRInputService>();
        builder.Services.AddScoped<InverseKinematics>();
        builder.Services.AddScoped<BodySkeletonModifier>();
        builder.Services.AddScoped<HandTrackingSkeletonModifier>();
        builder.Services.AddScoped<ControllerTrackingSkeletonModifier>();
        builder.Services.AddScoped<GameModifier>();
        builder.Services.AddScoped<FirstPersonManager>();
        return builder.Build();
    }

    private DxDevice GetDevice()
    {
        if (device != null)
        {
            return device;
        }
        return new DxDevice((ID3D11Device*)Device.Instance()->D3D11Forwarder);
    }

    private Configuration LoadConfiguration()
    {
        return PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

    }
    private XR CreateXR()
    {
        var dir = PluginInterface.AssemblyLocation.Directory ?? throw new NullReferenceException("Assembly Location missing");
        var dllPath = Path.Combine(dir.ToString(), "openxr_loader.dll");
        return new XR(XR.CreateDefaultContext([dllPath]));
    }
}