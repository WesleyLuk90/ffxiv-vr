using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
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
    HudLayoutManager hudLayoutManager,
    IGamepadState gamepadState,
    GameState gameState,
    Debugging debugging,
    Configuration configuration
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

            var character = gameState.getCharacterOrGpose();
            var internalSceneCamera = gameState.GetInternalSceneCamera();
            if (character != null && internalSceneCamera != null)
            {
                debugging.DebugShow("Char Rotation", character->Rotation);
                debugging.DebugShow("Camera Rotation", internalSceneCamera->CurrentHRotation);
                debugging.DebugShow("Rotation", debugging.XRotation);
            }
            if (debugging.ManualRotation && !DisableHeadRotation() && character != null && internalSceneCamera != null)
            {
                character->SetRotation(debugging.XRotation + debugging.XRotation + MathF.PI);
                // internalSceneCamera->CurrentHRotation = debugging.XRotation + MathF.PI;
            }
        });
    }
    private unsafe bool DisableHeadRotation()
    {
        var conditions = Conditions.Instance();
        // Summoning bell
        return conditions->OccupiedInQuestEvent ||
        conditions->OccupiedSummoningBell ||
        conditions->OccupiedInCutSceneEvent ||
        conditions->SufferingStatusAffliction ||
        conditions->SufferingStatusAffliction2 ||
        conditions->SufferingStatusAffliction63 ||
        conditions->BetweenAreas ||
        conditions->BetweenAreas51 ||
        conditions->RolePlaying;
    }

    public void Dispose()
    {
        framework.Update -= FrameworkUpdate;
        clientState.Login -= Login;
        clientState.Logout -= Logout;
        namePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
    }
}