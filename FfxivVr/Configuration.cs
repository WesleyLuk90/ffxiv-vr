using Dalamud.Configuration;
using System;
using System.Collections.Generic;

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
    public bool ControllerTracking = false;

    public bool MatchFloorPosition = false;
    public bool DisableCameraDirectionFlying = false;
    public bool DisableCameraDirectionFlyingThirdPerson = false;

    public bool KeepUIInFront = true;

    public float UISize = 1.0f;

    public int? VRHudLayout = null;
    public int? DefaultHudLayout = null;

    public bool DisableCutsceneLetterbox = true;

    public bool ShowBodyInFirstPerson = true;
    public bool KeepCameraHorizontal = true;
    public bool WindowAlwaysOnTop = false;

    public List<ControlLayer> Controls = [
        new ControlLayer(),
        new ControlLayer(),
        new ControlLayer(),
        new ControlLayer(),
    ];

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    public class ControlLayer
    {
        public VRAction LeftGrip = VRAction.L1;
        public VRAction LeftTrigger = VRAction.L2;
        public VRAction LeftStick = VRAction.L3;
        public VRAction RightGrip = VRAction.R1;
        public VRAction RightTrigger = VRAction.R2;
        public VRAction RightStick = VRAction.R3;
        public VRAction AButton = VRAction.A;
        public VRAction BButton = VRAction.B;
        public VRAction XButton = VRAction.X;
        public VRAction YButton = VRAction.Y;
        public VRAction Start = VRAction.Start;
        public VRAction Select = VRAction.Select;

        internal VRAction GetAction(VRButton button)
        {
            switch (button)
            {
                case VRButton.A: return AButton;
                case VRButton.B: return BButton;
                case VRButton.X: return XButton;
                case VRButton.Y: return YButton;
                case VRButton.Start: return Start;
                case VRButton.Select: return Select;
                case VRButton.LeftTrigger: return LeftTrigger;
                case VRButton.RightTrigger: return RightTrigger;
                case VRButton.LeftGrip: return LeftGrip;
                case VRButton.RightGrip: return RightGrip;
                case VRButton.LeftStick: return LeftStick;
                case VRButton.RightStick: return RightStick;
                default: return VRAction.None;
            }
        }
    }
}