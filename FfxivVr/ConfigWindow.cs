using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace FfxivVR;
internal class ConfigWindow : Window
{
    private readonly Configuration config;
    private readonly VRLifecycle vrLifecycle;
    private readonly Action toggleVR;
    private readonly GameConfigManager gameConfigManager;

    public ConfigWindow(
        Configuration configuration,
        VRLifecycle vrLifecycle,
        Action toggleVR,
        GameConfigManager gameConfigManager
        ) : base("FFXIV VR Settings")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize;

        Size = new Vector2(500, 500);

        this.config = configuration;
        this.vrLifecycle = vrLifecycle;
        this.toggleVR = toggleVR;
        this.gameConfigManager = gameConfigManager;
    }

    public override void Draw()
    {
        using (ImRaii.TabBar("tabs"))
        {
            using (var tab = ImRaii.TabItem("General"))
            {
                if (tab)
                {
                    if (ImGui.Button(vrLifecycle.IsEnabled() ? "Stop VR" : "Start VR"))
                    {
                        toggleVR();
                    }
                    Checkbox("Start VR at game launch if headset is available", ref config.StartVRAtBoot);
                    Checkbox("Keep game window always on top", ref config.WindowAlwaysOnTop);
                }
            }
            using (var tab = ImRaii.TabItem("View"))
            {
                if (tab)
                {
                    Checkbox("Recenter Camera on View Change", ref config.RecenterOnViewChange, "Recenters the camera when switching between first and third person.");
                    Checkbox("Disable cutscene black bars", ref config.DisableCutsceneLetterbox);
                    Slider("World Scale", ref config.WorldScale);
                    Slider("Gamma", ref config.Gamma, defaultValue: 2.2f);
                }
            }
            using (var tab = ImRaii.TabItem("UI"))
            {
                if (tab)
                {
                    Slider("UI Distance", ref config.UIDistance);
                    Slider("UI Size", ref config.UISize);
                    Checkbox("Keep UI In Front", ref config.KeepUIInFront);
                    Checkbox("Scale the game window to fit on screen", ref config.FitWindowOnScreen);
                    ComboDropdown("Switch HUD layout when starting VR", ["Disabled", "Hud Layout 1", "Hud Layout 2", "Hud Layout 3", "Hud Layout 4"], ref config.VRHudLayout);
                    ComboDropdown("Switch HUD layout when stopping VR", ["Disabled", "Hud Layout 1", "Hud Layout 2", "Hud Layout 3", "Hud Layout 4"], ref config.DefaultHudLayout);
                    SliderInt("UI Snap Angle", ref config.UITransitionAngle, min: 0, max: 180, "How far away you need to turn before the UI snaps in front of you. Set to 180 to disable.");
                }
            }
            using (var tab = ImRaii.TabItem("Controls"))
            {
                if (tab)
                {
                    RenderControlsTab();
                }
            }
            using (var tab = ImRaii.TabItem("First Person"))
            {
                if (tab)
                {
                    Checkbox("Show Body", ref config.ShowBodyInFirstPerson);
                    Checkbox("Disable Auto Face Target", ref config.DisableAutoFaceTargetInFirstPerson);
                    Checkbox("Follow Head", ref config.FollowCharacter, "Moves the camera to match your characters head.");
                    Checkbox("Prevent camera from changing flying height", ref config.DisableCameraDirectionFlying);
                    Checkbox("Enable hand tracking", ref config.HandTracking, "Uses your headsets hand tracking to control your characters hands and fingers, not all headsets are supported.");
                    Checkbox("Enable controller tracking", ref config.ControllerTracking, "Uses the VR controllers to control your characters hands.");
                }
            }
            using (var tab = ImRaii.TabItem("Third Person"))
            {
                if (tab)
                {
                    Checkbox("Fixed camera height", ref config.MatchFloorPosition);
                    Slider("Height offset", ref config.FloorHeightOffset, defaultValue: 0, min: -3, max: 3);
                    Checkbox("Keep the camera level", ref config.KeepCameraHorizontal);
                    Checkbox("Keep the cutscene camera level", ref config.KeepCutsceneCameraHorizontal, "Disable to ensure camera looks at the original cutscene direction.");
                    Checkbox("Prevent camera from changing flying height", ref config.DisableCameraDirectionFlyingThirdPerson);
                }
            }
            using (var tab = ImRaii.TabItem("Game Config"))
            {
                if (tab)
                {
                    RenderGameConfig();
                }
            }
        }
    }

    private void RenderGameConfig()
    {
        ImGui.Text("Change game settings when VR is started");
        gameConfigManager.GetOptions().ForEach(RenderConfigOption);
    }

    private void RenderConfigOption(GameOption option)
    {
        ImGui.Text(option.Label ?? option.GetID());
        if (option.IsBoolean())
        {
            int selected = ((int?)config.GetVRGameSetting(option.GetID()) ?? -1) + 1;
            if (ImGui.Combo($"##{option}-select", ref selected, ["Don't Change", "Off", "On"], 3))
            {
                if (selected == 0)
                {
                    config.SetVRGameSetting(option.GetID(), null);
                }
                else
                {
                    config.SetVRGameSetting(option.GetID(), (uint?)(selected - 1));
                }
            }
        }
        else if (option.Options is List<string> options)
        {
            int selected = ((int?)config.GetVRGameSetting(option.GetID()) ?? -1) + 1;
            var allOptions = new List<string>() { "Don't Change" };
            allOptions.AddRange(options);
            if (ImGui.Combo($"##{option}-select", ref selected, allOptions.ToArray(), options.Count() + 1))
            {
                if (selected == 0)
                {
                    config.SetVRGameSetting(option.GetID(), null);
                }
                else
                {
                    config.SetVRGameSetting(option.GetID(), (uint?)(selected - 1));
                }
            }
        }
        else
        {
            uint? configValue = config.GetVRGameSetting(option.GetID());
            bool disabled = configValue == null;
            int value = (int)(configValue ?? option.Properties.Default);
            if (ImGui.Checkbox($"Don't Change##{option}-box", ref disabled))
            {
                if (disabled)
                {
                    config.SetVRGameSetting(option.GetID(), null);
                }
                else
                {
                    config.SetVRGameSetting(option.GetID(), option.Properties.Default);
                }
            }
            using (ImRaii.Disabled(disabled))
            {
                if (ImGui.SliderInt($"##{option}-select", ref value, (int)option.Properties.Minimum, (int)option.Properties.Maximum))
                {
                    config.SetVRGameSetting(option.GetID(), (uint?)value);
                }
            }
        }
    }

    private void RenderControlsTab()
    {
        using (ImRaii.TabBar("controls-tab"))
        {
            using (var tab = ImRaii.TabItem("Layer 1"))
            {
                if (tab)
                {
                    RenderLayerEditor(0);
                }
            }
            using (var tab = ImRaii.TabItem("Layer 2"))
            {
                if (tab)
                {
                    RenderLayerEditor(1);
                }
            }
            using (var tab = ImRaii.TabItem("Layer 3"))
            {
                if (tab)
                {
                    RenderLayerEditor(2);
                }
            }
            using (var tab = ImRaii.TabItem("Layer 4"))
            {
                if (tab)
                {
                    RenderLayerEditor(3);
                }
            }
        }
    }

    private void RenderLayerEditor(int layer)
    {
        VRActionDropdown($"A Button", ref config.Controls[layer].AButton, layer);
        VRActionDropdown($"B Button", ref config.Controls[layer].BButton, layer);
        VRActionDropdown($"X Button", ref config.Controls[layer].XButton, layer);
        VRActionDropdown($"Y Button", ref config.Controls[layer].YButton, layer);
        VRActionDropdown($"Left Grip", ref config.Controls[layer].LeftGrip, layer);
        VRActionDropdown($"Left Trigger", ref config.Controls[layer].LeftTrigger, layer);
        VRActionDropdown($"Left Stick", ref config.Controls[layer].LeftStick, layer);
        VRActionDropdown($"Right Grip", ref config.Controls[layer].RightGrip, layer);
        VRActionDropdown($"Right Trigger", ref config.Controls[layer].RightTrigger, layer);
        VRActionDropdown($"Right Stick", ref config.Controls[layer].RightStick, layer);
        VRActionDropdown($"Start", ref config.Controls[layer].Start, layer);
        VRActionDropdown($"Select", ref config.Controls[layer].Select, layer);
    }

    private void VRActionDropdown(string label, ref VRAction button, int layer)
    {
        ImGui.Text(label);
        ImGui.SameLine();
        var values = Enum.GetValues<VRAction>().Select(a => a.ToString()).ToArray();
        int selected = (int)button;
        if (ImGui.Combo($"##{label}-{layer}", ref selected, values, values.Length))
        {
            button = (VRAction)selected;
            config.Save();
        }
    }

    private void ComboDropdown(string label, string[] options, ref int? value)
    {
        var index = 0;
        if (value is int v)
        {
            index = v + 1;
        }
        ImGui.Text(label);
        if (ImGui.Combo(EmptyLabel(label), ref index, options, options.Length))
        {
            if (index == 0)
            {
                value = null;
            }
            else
            {
                value = index - 1;
            }
            config.Save();
        }
    }

    private void Checkbox(string label, ref bool value, string? help = null)
    {
        var tempValue = value;
        if (ImGui.Checkbox(label, ref tempValue))
        {
            value = tempValue;
            config.Save();
        }
        Tooltip(help);
    }

    private void Tooltip(string? help = null)
    {
        if (help is string helpMessage)
        {
            if (ImGui.IsItemHovered())
            {
                using (ImRaii.Tooltip())
                {
                    ImGui.Text(helpMessage);
                }
            }
        }
    }
    private void Slider(string label, ref float value, float defaultValue = 1.0f, float min = 0.1f, float max = 10)
    {
        var tempValue = value;
        ImGui.Text(label);
        if (ImGui.SliderFloat(EmptyLabel(label), ref tempValue, min, max))
        {
            value = tempValue;
            config.Save();
        }
        ImGui.SameLine();
        if (ImGui.SmallButton($"Reset##{label}"))
        {
            value = defaultValue;
            config.Save();
        }
    }
    private void SliderInt(string label, ref int value, int min = 0, int max = 10, string? help = null)
    {
        var tempValue = value;
        ImGui.Text(label);
        if (ImGui.SliderInt(EmptyLabel(label), ref tempValue, min, max))
        {
            value = tempValue;
            config.Save();
        }
        Tooltip(help);
    }

    private string EmptyLabel(string label)
    {
        return $"##{label}";
    }
}