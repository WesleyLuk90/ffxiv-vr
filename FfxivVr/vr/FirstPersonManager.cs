using Dalamud.Game.Config;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace FfxivVR;

public unsafe class FirstPersonManager(
    GameState gameState,
    VRSpace vrSpace,
    Configuration configuration,
    GameConfigManager gameConfigManager
)
{
    public bool IsFirstPerson { get; private set; } = false;
    public void Update()
    {
        if (Conditions.IsOccupiedInCutSceneEvent)
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

}