using Dalamud.Configuration;
using System;

namespace FfxivVR;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public float WorldScale = 1.0f;
    public float UIDistance = 1.0f;
    public float Gamma = 2.2f;
    public bool FollowCharacter = false;
    public bool RecenterOnViewChange = true;
    public bool DisableAutoFaceTargetInFirstPerson = false;
    public bool StartVRAtBoot = false;
    public bool FitWindowOnScreen = true;

    public bool HandTracking = false;

    public bool MatchFloorPosition = false;
    public bool DisableCameraDirectionFlying = false;

    public bool KeepUIInFront = true;

    public float UISize = 1.0f;

    public int? VRHudLayout = null;
    public int? DefaultHudLayout = null;

    public bool DisableCutsceneLetterbox = true;

    public bool ShowBodyInFirstPerson = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}