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
    private readonly Configuration defaultConfig = new Configuration();

    public ConfigWindow(Configuration configuration, VRLifecycle vrLifecycle, Action toggleVR) : base("FFXIV VR Settings")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(400, 400);
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
            Checkbox("Recenter Camera on View Change", ref config.RecenterOnViewChange);
            Checkbox("Follow Head in First Person", ref config.FollowCharacter);
            Checkbox("Disable Auto Face Target in First Person", ref config.DisableAutoFaceTargetInFirstPerson);
            Checkbox("Match game to real floor position", ref config.MatchFloorPosition);
            Slider("World Scale", ref config.WorldScale);
            Slider("Gamma", ref config.Gamma, defaultValue: 2.2f);
        }
        if (ImGui.CollapsingHeader("UI"))
        {
            Slider("UI Distance", ref config.UIDistance);
            Slider("UI Size", ref config.UISize);
            Checkbox("Keep UI In Front", ref config.KeepUIInFront);
            Checkbox("Scale the game window to fit on screen", ref config.FitWindowOnScreen);
        }
        if (ImGui.CollapsingHeader("Controls"))
        {
            Checkbox("Prevent camera from changing flying height", ref config.DisableCameraDirectionFlying);
            Checkbox("Enable hand tracking", ref config.HandTracking);
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