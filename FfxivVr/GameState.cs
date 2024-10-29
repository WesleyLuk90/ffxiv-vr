using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace FfxivVR;
unsafe public class GameState
{
    private IClientState clientState;

    public GameState(IClientState clientState)
    {
        this.clientState = clientState;
    }

    public bool IsFirstPerson()
    {
        return SceneCameraExtensions.GetCameraMode() == CameraMode.FirstPerson;
    }

    public bool IsGPosing()
    {
        return clientState.IsGPosing;
    }

    public Character* GetGposeTarget()
    {
        if (!clientState.IsGPosing)
        {
            return null;
        }
        var targetSystem = TargetSystem.Instance();
        var target = targetSystem->GPoseTarget;
        if (target == null)
        {
            return null;
        }
        if (target->GetObjectKind() != ObjectKind.Pc)
        {
            return null;
        }
        return (Character*)target;
    }

    internal bool IsInCutscene()
    {
        return Conditions.IsWatchingCutscene ||
            Conditions.IsOccupied ||
            Conditions.IsOccupiedInCutSceneEvent;
    }
}
