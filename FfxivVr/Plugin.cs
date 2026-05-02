using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace FfxivVR;

public sealed unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog EarlyLogger { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private IHost AppHost;
    public Plugin()
    {
        EarlyLogger.Debug("Loading VR Plugin");
        var appFactory = PluginInterface.Create<AppFactory>() ?? throw new NullReferenceException("Failed to create AppFactory");
        AppHost = appFactory.CreateSession();

        AppHost.Services.GetRequiredService<RenderPipelineInjector>().Initialize();
        AppHost.Services.GetRequiredService<GameHooks>().Initialize();
        AppHost.Services.GetRequiredService<CommandHander>().Initialize();
        AppHost.Services.GetRequiredService<GameEvents>().Initialize();
        AppHost.Services.GetRequiredService<PluginUI>().Initialize();

        EarlyLogger.Debug("Loaded VR Plugin");
        ChatGui.Print("Loaded VR Plugin");
    }
    public void Dispose()
    {
        AppHost.Dispose();
    }
}