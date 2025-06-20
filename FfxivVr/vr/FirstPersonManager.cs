using Dalamud.Game.Config;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;

namespace FfxivVR;

public unsafe class FirstPersonManager(
    GameState gameState,
    VRSpace vrSpace,
    Configuration configuration,
    GameConfigManager gameConfigManager
)
{

    private bool DisableBodyRotation()
    {
        var conditions = Conditions.Instance();
        return conditions->Dead ||
        conditions->Crafting ||
        conditions->Gathering ||
        conditions->RidingPillion ||
        conditions->PlayingMiniGame ||
        conditions->PlayingLordOfVerminion ||
        conditions->TradeOpen ||
        conditions->Fishing ||
        conditions->MeldingMateria ||
        conditions->OccupiedInQuestEvent ||
        conditions->OccupiedSummoningBell ||
        conditions->OccupiedInCutSceneEvent ||
        conditions->SufferingStatusAffliction ||
        conditions->SufferingStatusAffliction2 ||
        conditions->SufferingStatusAffliction63 ||
        conditions->BetweenAreas ||
        conditions->BetweenAreas51 ||
        conditions->RolePlaying ||
        conditions->InThatPosition || // Using /sit
        conditions->Jumping61; // Gold Saucer juming fountains
    }
    public bool IsFirstPerson { get; private set; } = false;

    private bool? WasInCombat = null;
    public void Update()
    {
        if (Conditions.Instance()->OccupiedInCutSceneEvent)
        {
            return;
        }
        var changed = false;
        var internalCamera = gameState.GetGameCameraExtended();
        var activeCamera = gameState.GetActiveCamera();
        if (configuration.AutoCombatViewMode != null && WasInCombat != null && WasInCombat != Conditions.Instance()->InCombat)
        {
            var CombatThirdPerson = configuration.AutoCombatViewMode == (int)Configuration.AutoCombatView.ThirdPerson;
            if (Conditions.Instance()->InCombat)
            {
                changed = IsFirstPerson == CombatThirdPerson;
                IsFirstPerson = !CombatThirdPerson;
            }
            else
            {
                changed = IsFirstPerson == !CombatThirdPerson;
                IsFirstPerson = CombatThirdPerson;
            }

            // Transition to first person can either be done by changing the camera to first person
        }
        else if (internalCamera->CameraMode == CameraView.FirstPerson)
        {
            internalCamera->CameraMode = CameraView.ThirdPerson;
            IsFirstPerson = !IsFirstPerson;
            changed = true;
        }
        // Or zooming out while in first person
        else if (IsFirstPerson && activeCamera->Distance > 2)
        {
            IsFirstPerson = false;
            changed = true;
        }
        WasInCombat = Conditions.Instance()->InCombat;
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
        return configuration.EnableHeadRelativeMovement && IsFirstPerson && !DisableBodyRotation();
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

    public float? GetOffset()
    {
        if (!ShouldUpdateHeadRotation())
        {
            return null;
        }
        return offset;
    }
    private float? offset = null;
    private float? lastRotation = null;
    // Test both WASD and controller joystick, they behave differently
    // Test UI rotation
    public void UpdateRotation(float yaw)
    {
        var internalSceneCamera = gameState.GetSceneCameraExtended();
        var character = gameState.getCharacterOrGpose();
        if (ShouldUpdateHeadRotation() && internalSceneCamera != null && character != null)
        {
            // Any difference in rotation we assume is from the player rotating their character
            // Compute the difference and apply it to the offset
            var off = offset ?? character->Rotation - yaw + MathF.PI;
            offset = off;
            if (lastRotation is { } r)
            {
                // Use camera rotation instead so that auto face target works
                var delta = (internalSceneCamera->CurrentHRotation - r) % (MathF.PI * 2);
                offset += delta;
            }
            character->SetRotation(yaw + off + MathF.PI);
            internalSceneCamera->CurrentHRotation = yaw + off;
            lastRotation = internalSceneCamera->CurrentHRotation;
        }
        else
        {
            offset = null;
            lastRotation = null;
        }
    }
}