﻿using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace FfxivVR;
public unsafe class GameState(
    IClientState clientState,
    IGameGui gameGui)
{

    public bool IsFirstPerson()
    {
        // Some cutscenes this flag is set, e.g. Logging in and out of the inn
        return GetInternalGameCamera()->CameraMode == CameraView.FirstPerson && !Conditions.IsOccupiedInCutSceneEvent;
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

    internal bool IsInCutscene()
    {
        return Conditions.IsWatchingCutscene ||
            Conditions.IsOccupied ||
            Conditions.IsOccupiedInCutSceneEvent;
    }

    internal bool IsBetweenAreas()
    {
        return Conditions.IsInBetweenAreas;
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

    public InternalSceneCamera* GetInternalSceneCamera()
    {
        return InternalSceneCamera.FromCamera(GetCurrentCamera());
    }

    public InternalGameCamera* GetInternalGameCamera()
    {
        return InternalGameCamera.FromCamera(GetActiveCamera());
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
        return Conditions.IsOccupiedInCutSceneEvent;
    }
}