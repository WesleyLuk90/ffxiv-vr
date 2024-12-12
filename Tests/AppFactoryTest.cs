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
    [Test, Timeout(10000)]
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
        AppFactory.GameInteropProvider = new Mock<IGameInteropProvider>().Object;
        AppFactory.CommandManager = new Mock<ICommandManager>().Object;
        AppFactory.Framework = new Mock<IFramework>().Object;
        AppFactory.NamePlateGui = new Mock<INamePlateGui>().Object;

        var factory = new AppFactory(device: new DxDevice(null));

        var host = factory.CreateSession();

        host.Services.GetRequiredService<RenderPipelineInjector>();
        host.Services.GetRequiredService<GameHooks>();
        host.Services.GetRequiredService<CommandHander>();
        host.Services.GetRequiredService<GameEvents>();
        host.Services.GetRequiredService<PluginUI>();

        var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<VRLifecycle>();

        scope.Dispose();
        host.Dispose();
    }
}