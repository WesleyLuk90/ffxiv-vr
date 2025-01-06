using Dalamud.Game.Config;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace FfxivVR;

public unsafe class FirstPersonManager(
    GameState gameState,
    Logger logger,
    VRSpace vrSpace,
    Configuration configuration,
    GameConfigManager gameConfigManager,
    Debugging debugging
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
        if (internalCamera->CameraMode == CameraView.FirstPerson)
        {
            internalCamera->CameraMode = CameraView.ThirdPerson;
            IsFirstPerson = !IsFirstPerson;
            changed = true;
        }
        else if (IsFirstPerson && activeCamera->Distance > 2)
        {
            IsFirstPerson = !IsFirstPerson;
            changed = true;
        }
        if (IsFirstPerson)
        {
            if (configuration.FollowCharacter)
            {
                activeCamera->Distance = 2;
            }
            else
            {
                activeCamera->Distance = 0;
            }
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
    public void UpdateFirstPersonSettings(bool forceDisable = false)
    {
        if (IsFirstPerson && !forceDisable)
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