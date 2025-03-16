using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

namespace FfxivVR;

public class GameEvents(
    INamePlateGui namePlateGui,
    IClientState clientState,
    Transitions transitions,
    IFramework framework,
    ExceptionHandler exceptionHandler,
    VRLifecycle vrLifecycle,
    HudLayoutManager hudLayoutManager
) : IDisposable
{

    public void Initialize()
    {
        namePlateGui.OnDataUpdate += OnNamePlateUpdate;
        clientState.Login += Login;
        clientState.Logout += Logout;
        framework.Update += FrameworkUpdate;
    }

    private void Logout(int type, int code)
    {
        transitions.OnLogout();
    }

    private void Login()
    {
        transitions.OnLogin();
    }

    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        vrLifecycle.OnNamePlateUpdate(context, handlers);
    }


    private unsafe void FrameworkUpdate(IFramework framework)
    {
        exceptionHandler.FaultBarrier(() =>
        {
            hudLayoutManager.Update();
        });
    }

    public void Dispose()
    {
        framework.Update -= FrameworkUpdate;
        clientState.Login -= Login;
        clientState.Logout -= Logout;
        namePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
    }
}