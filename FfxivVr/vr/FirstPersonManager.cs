using Dalamud.Game.Config;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace FfxivVR;

public unsafe class FirstPersonManager(
    GameState gameState,
    Logger logger,
    VRSpace vrSpace,
    Configuration configuration,
    IGameConfig gameConfig
)
{
    public bool IsFirstPerson { get; private set; } = false;
    private float? Distance = null;
    public void Update()
    {
        if (Conditions.IsOccupiedInCutSceneEvent)
        {
            return;
        }
        var changed = false;
        var internalCamera = gameState.GetInternalGameCamera();
        if (internalCamera->CameraMode == CameraView.FirstPerson)
        {
            internalCamera->CameraMode = CameraView.ThirdPerson;
            IsFirstPerson = !IsFirstPerson;
            changed = true;
        }
        else if (IsFirstPerson && Distance is { } d && d < gameState.GetActiveCamera()->Distance)
        {
            IsFirstPerson = !IsFirstPerson;
            changed = true;
        }
        Distance = gameState.GetActiveCamera()->Distance;
        if (changed)
        {
            if (IsFirstPerson)
            {
                ThirdToFirstPerson();
            }
            if (!IsFirstPerson)
            {
                FirstToThirdPerson();
            }
        }
    }
    public void FirstToThirdPerson()
    {
        if (configuration.RecenterOnViewChange)
        {
            vrSpace.RecenterCamera();
        }
        if (configuration.DisableAutoFaceTargetInFirstPerson)
        {
            gameConfig.Set(UiControlOption.AutoFaceTargetOnAction, true);
        }
    }


    public void ThirdToFirstPerson()
    {
        if (configuration.RecenterOnViewChange)
        {
            vrSpace.RecenterCamera();
        }
        if (configuration.DisableAutoFaceTargetInFirstPerson)
        {
            gameConfig.Set(UiControlOption.AutoFaceTargetOnAction, false);
        }
    }

}