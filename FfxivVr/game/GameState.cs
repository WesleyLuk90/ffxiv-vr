using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Silk.NET.Maths;

namespace FfxivVR;

public unsafe class GameState(
    IClientState clientState,
    IGameGui gameGui)
{

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
        if (targetSystem == null)
        {
            return null;
        }
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

    public Character* getCharacterOrGpose()
    {
        Character* character = GetGposeTarget();
        if (IsGPosing() && character == null)
        {
            return null;
        }
        if (character != null)
        {
            return character;
        }
        var player = clientState.LocalPlayer;
        if (player == null)
        {
            return null;
        }
        return (Character*)player!.Address;
    }

    public bool IsPlayer(uint entityID)
    {
        var character = getCharacterOrGpose();
        if (character == null)
        {
            return false;
        }
        return character->GameObject.EntityId == entityID;
    }

    public InternalCharacter* GetInternalCharacter()
    {
        return InternalCharacter.FromCharacter(getCharacterOrGpose());
    }

    public Vector3D<float> GetFixedHeadPosition()
    {
        var character = getCharacterOrGpose();
        var internalCharacter = GetInternalCharacter();

        if (character == null || internalCharacter == null)
        {
            // Provide a sane default here incase there is no character
            return GetCurrentCamera()->LookAtVector.ToVector3D();
        }
        return character->Position.ToVector3D() + new Vector3D<float>(0, internalCharacter->FixHeadPosition, 0);
    }

    internal bool IsInCutscene()
    {
        return Conditions.Instance()->WatchingCutscene ||
            Conditions.Instance()->Occupied ||
            Conditions.Instance()->OccupiedInCutSceneEvent;
    }

    internal bool IsBetweenAreas()
    {
        return Conditions.Instance()->BetweenAreas;
    }

    // Returns the game camera distance regardless of walls
    public float? GetGameCameraDistance()
    {
        var currentCamera = GetActiveCamera();
        if (currentCamera == null)
        {
            return null;
        }
        return currentCamera->Distance;
    }

    public FFXIVClientStructs.FFXIV.Client.Game.Camera* GetActiveCamera()
    {
        var manager = FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance();
        if (manager == null)
        {
            return null;
        }
        return manager->GetActiveCamera();
    }

    public FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera* GetCurrentCamera()
    {
        var manager = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CameraManager.Instance();
        if (manager == null)
        {
            return null;
        }
        return manager->CurrentCamera;
    }

    public SceneCameraExtended* GetSceneCameraExtended()
    {
        return SceneCameraExtended.FromCamera(GetCurrentCamera());
    }

    public GameCameraExtended* GetGameCameraExtended()
    {
        return GameCameraExtended.FromCamera(GetActiveCamera());
    }

    private AddonFade* FadeMiddle = null;
    private AddonFade* FadeBack = null;
    private AddonFade* GetFadeMiddle()
    {
        if (FadeMiddle != null)
        {
            return FadeMiddle;
        }
        FadeMiddle = (AddonFade*)gameGui.GetAddonByName("FadeMiddle");
        return FadeMiddle;
    }
    private AddonFade* GetFadeBack()
    {
        if (FadeBack != null)
        {
            return FadeBack;
        }
        FadeBack = (AddonFade*)gameGui.GetAddonByName("FadeBack");
        return FadeBack;
    }

    public float GetFade()
    {
        var fade = 0f;
        var middle = GetFadeMiddle();
        if (middle != null)
        {
            fade = AddonFade.GetAlpha(middle);
        }
        var back = GetFadeBack();
        if (back != null)
        {
            fade = float.Max(fade, AddonFade.GetAlpha(back));
        }
        return fade;
    }

    internal bool IsOccupiedInCutSceneEvent()
    {
        // Set during
        // Inn login/logout
        // Quest cutscene
        // Dungeon cutscene
        return Conditions.Instance()->OccupiedInCutSceneEvent;
    }
}