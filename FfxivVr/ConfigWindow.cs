using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
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
        if (ImGui.CollapsingHeader("General"))
        {
            if (ImGui.Button(vrLifecycle.IsEnabled() ? "Stop VR" : "Start VR"))
            {
                toggleVR();
            }
            Checkbox("Start VR at game launch if headset is available", ref config.StartVRAtBoot);
        }
        if (ImGui.CollapsingHeader("View"))
        {
            Checkbox("Show Body in First Person", ref config.ShowBodyInFirstPerson);
            Checkbox("Recenter Camera on View Change", ref config.RecenterOnViewChange);
            Checkbox("Follow Head in First Person", ref config.FollowCharacter);
            Checkbox("Disable Auto Face Target in First Person", ref config.DisableAutoFaceTargetInFirstPerson);
            Checkbox("Match game to real floor position", ref config.MatchFloorPosition);
            Checkbox("Disable cutscene black bars", ref config.DisableCutsceneLetterbox);
            Slider("World Scale", ref config.WorldScale);
            Slider("Gamma", ref config.Gamma, defaultValue: 2.2f);
        }
        if (ImGui.CollapsingHeader("UI"))
        {
            Slider("UI Distance", ref config.UIDistance);
            Slider("UI Size", ref config.UISize);
            Checkbox("Keep UI In Front", ref config.KeepUIInFront);
            Checkbox("Scale the game window to fit on screen", ref config.FitWindowOnScreen);
            ComboDropdown("Switch HUD layout when starting VR", ["Disabled", "Hud Layout 1", "Hud Layout 2", "Hud Layout 3", "Hud Layout 4"], ref config.VRHudLayout);
            ComboDropdown("Switch HUD layout when stopping VR", ["Disabled", "Hud Layout 1", "Hud Layout 2", "Hud Layout 3", "Hud Layout 4"], ref config.DefaultHudLayout);
        }
        if (ImGui.CollapsingHeader("Controls"))
        {
            Checkbox("Prevent camera from changing flying height", ref config.DisableCameraDirectionFlying);
            Checkbox("Enable hand tracking", ref config.HandTracking);
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