namespace Tests;

using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FfxivVR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.IO;

public unsafe class AppFactoryTests
{
    [Test]
    public void CreateSession()
    {
        var pluginInterface = new Mock<IDalamudPluginInterface>();
        pluginInterface.Setup(i => i.AssemblyLocation)
            .Returns(new FileInfo("net8.0-windows"));
        AppFactory.PluginInterface = pluginInterface.Object;
        AppFactory.SigScanner = new Mock<ISigScanner>().Object;
        AppFactory.Log = new Mock<IPluginLog>().Object;
        AppFactory.ChatGui = new Mock<IChatGui>().Object;
        AppFactory.ClientState = new Mock<IClientState>().Object;
        AppFactory.GameGui = new Mock<IGameGui>().Object;
        AppFactory.TargetManager = new Mock<ITargetManager>().Object;
        AppFactory.GameConfig = new Mock<IGameConfig>().Object;
        AppFactory.GamepadState = new Mock<IGamepadState>().Object;

        var factory = new AppFactory(device: null);

        var host = factory.CreateSession();

        host.Services.GetRequiredService<GameHooks>();
        host.Services.GetRequiredService<RenderPipelineInjector>();
        host.Services.GetRequiredService<GameHooks>();
        host.Services.GetRequiredService<GameHooks>();
        host.Services.GetRequiredService<ConfigWindow>();
        host.Services.GetRequiredService<DebugWindow>();
        host.Services.GetRequiredService<Logger>();
        host.Services.GetRequiredService<ExceptionHandler>();
        host.Services.GetRequiredService<VRLifecycle>();
        host.Services.GetRequiredService<GamepadManager>();
        host.Services.GetRequiredService<Configuration>();
        host.Services.GetRequiredService<GameState>();
        host.Services.GetRequiredService<ConfigWindow>();
        host.Services.GetRequiredService<FreeCamera>();
        host.Services.GetRequiredService<HudLayoutManager>();
        host.Services.GetRequiredService<ConfigManager>();
        host.Services.GetRequiredService<Transitions>();
        host.Services.GetRequiredService<DebugWindow>();
        host.Services.GetRequiredService<HookStatus>();

        var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<VRLifecycle>();

        scope.Dispose();
        host.Dispose();
    }
}