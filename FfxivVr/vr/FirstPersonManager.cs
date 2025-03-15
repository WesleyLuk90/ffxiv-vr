using Dalamud.Game.Config;
using FFXIVClientStructs.FFXIV.Client.Game;
using Newtonsoft.Json;
using SharpDX.Win32;
using System;

namespace FfxivVR;

public unsafe class FirstPersonManager(
    GameState gameState,
    VRSpace vrSpace,
    Configuration configuration,
    GameConfigManager gameConfigManager,
    Debugging debugging
)
{

    private bool DisableHeadRotation()
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
    public bool IsFirstPerson { get; private set; } = false;
    public void Update()
    {
        if (Conditions.Instance()->OccupiedInCutSceneEvent)
        {
            return;
        }
        var changed = false;
        var internalCamera = gameState.GetInternalGameCamera();
        var activeCamera = gameState.GetActiveCamera();
        // Transition to first person can either be done by changing the camera to first person
        if (internalCamera->CameraMode == CameraView.FirstPerson)
        {
            internalCamera->CameraMode = CameraView.ThirdPerson;
            IsFirstPerson = !IsFirstPerson;
            changed = true;
        }
        // Or zooming out while in first person
        else if (IsFirstPerson && activeCamera->Distance > 2)
        {
            IsFirstPerson = !IsFirstPerson;
            changed = true;
        }
        if (IsFirstPerson)
        {
            // Fix the distance to 2 so we can detect the zoom
            activeCamera->Distance = 2;
        }
        if (changed)
        {
            if (changed && !IsFirstPerson)
            {
                activeCamera->Distance = 1.5f;
            }
            if (configuration.RecenterOnViewChange)
            {
                vrSpace.RecenterCamera();
            }
            UpdateFirstPersonSettings();
        }
    }

    private bool ShouldUpdateHeadRotation()
    {
        return configuration.EnableHeadRelativeMovement && IsFirstPerson && !DisableHeadRotation();
    }

    private uint StandardMoveMode = 0;
    public void UpdateFirstPersonSettings()
    {
        if (IsFirstPerson)
        {
            if (configuration.DisableAutoFaceTargetInFirstPerson)
            {
                gameConfigManager.SetSetting(UiControlOption.AutoFaceTargetOnAction.ToString(), 0);
            }
            if (configuration.EnableStandardMovementInFirstPerson)
            {
                gameConfigManager.SetSetting(UiControlOption.MoveMode.ToString(), StandardMoveMode);
            }
        }
        else
        {
            gameConfigManager.Revert(UiControlOption.AutoFaceTargetOnAction.ToString());
            gameConfigManager.Revert(UiControlOption.MoveMode.ToString());
        }
    }


    private float offset = 0;
    private float? lastRotation = null;
    // Test both WASD and controller joystick, they behave differently
    public void UpdateRotation(float yaw)
    {
        var internalSceneCamera = gameState.GetInternalSceneCamera();
        var character = gameState.getCharacterOrGpose();
        if (ShouldUpdateHeadRotation() && internalSceneCamera != null && character != null)
        {
            // Any difference in rotation we assume is from the player rotating their character
            // Compute the difference and apply it to the offset
            if (lastRotation is { } r)
            {
                debugging.DebugShow("Rotation delta", character->Rotation - r);
                offset = character->Rotation - r;
            }
            debugging.DebugShow("Char Rotation", internalSceneCamera->CurrentHRotation);
            debugging.DebugShow("Cmaera rotation", character->Rotation);
            character->SetRotation(yaw + MathF.PI + offset);
            internalSceneCamera->CurrentHRotation = yaw;
            lastRotation = character->Rotation;
        }
    }
}