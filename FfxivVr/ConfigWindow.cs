using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;

namespace FfxivVR;
internal class ConfigWindow : Window
{
    private readonly Configuration config;
    private readonly VRLifecycle vrLifecycle;
    private readonly Action toggleVR;
    public ConfigWindow(Configuration configuration, VRLifecycle vrLifecycle, Action toggleVR) : base("FFXIV VR Settings")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize;

        Size = new Vector2(500, 500);

        this.config = configuration;
        this.vrLifecycle = vrLifecycle;
        this.toggleVR = toggleVR;
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("tabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                if (ImGui.Button(vrLifecycle.IsEnabled() ? "Stop VR" : "Start VR"))
                {
                    toggleVR();
                }
                Checkbox("Start VR at game launch if headset is available", ref config.StartVRAtBoot);
                Checkbox("Keep game window always on top", ref config.WindowAlwaysOnTop);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("View"))
            {
                Checkbox("Recenter Camera on View Change", ref config.RecenterOnViewChange);
                Checkbox("Disable cutscene black bars", ref config.DisableCutsceneLetterbox);
                Slider("World Scale", ref config.WorldScale);
                Slider("Gamma", ref config.Gamma, defaultValue: 2.2f);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("UI"))
            {
                Slider("UI Distance", ref config.UIDistance);
                Slider("UI Size", ref config.UISize);
                Checkbox("Keep UI In Front", ref config.KeepUIInFront);
                Checkbox("Scale the game window to fit on screen", ref config.FitWindowOnScreen);
                ComboDropdown("Switch HUD layout when starting VR", ["Disabled", "Hud Layout 1", "Hud Layout 2", "Hud Layout 3", "Hud Layout 4"], ref config.VRHudLayout);
                ComboDropdown("Switch HUD layout when stopping VR", ["Disabled", "Hud Layout 1", "Hud Layout 2", "Hud Layout 3", "Hud Layout 4"], ref config.DefaultHudLayout);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Controls"))
            {
                RenderControlsTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("First Person"))
            {
                Checkbox("Show Body", ref config.ShowBodyInFirstPerson);
                Checkbox("Disable Auto Face Target", ref config.DisableAutoFaceTargetInFirstPerson);
                Checkbox("Follow Head", ref config.FollowCharacter);
                Checkbox("Prevent camera from changing flying height", ref config.DisableCameraDirectionFlying);
                Checkbox("Enable hand tracking", ref config.HandTracking);
                Checkbox("Enable controller tracking", ref config.ControllerTracking);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Third Person"))
            {
                Checkbox("Match game to real floor position", ref config.MatchFloorPosition);
                Checkbox("Keep the camera level with the floor", ref config.KeepCameraHorizontal);
                Checkbox("Prevent camera from changing flying height", ref config.DisableCameraDirectionFlyingThirdPerson);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void RenderControlsTab()
    {
        if (ImGui.BeginTabBar("controls-tab"))
        {
            if (ImGui.BeginTabItem("Layer 1"))
            {
                RenderLayerEditor(0);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Layer 2"))
            {
                RenderLayerEditor(1);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Layer 3"))
            {
                RenderLayerEditor(2);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Layer 4"))
            {
                RenderLayerEditor(3);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
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
        if (ImGui.Combo($"##{label}", ref index, options, options.Length))
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

    private void Checkbox(string label, ref bool value)
    {
        var tempValue = value;
        if (ImGui.Checkbox(label, ref tempValue))
        {
            value = tempValue;
            config.Save();
        }
    }
    private void Slider(string label, ref float value, float defaultValue = 1.0f)
    {
        var tempValue = value;
        if (ImGui.SliderFloat(label, ref tempValue, 0.1f, 10f))
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
}