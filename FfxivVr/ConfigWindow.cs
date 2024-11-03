using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace FfxivVR;
internal class ConfigWindow : Window, IDisposable
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


    public void Dispose()
    {
    }

    public override void Draw()
    {
        if (ImGui.CollapsingHeader("General"))
        {
            if (ImGui.Button(vrLifecycle.IsEnabled() ? "Stop VR" : "Start VR"))
            {
                toggleVR();
            }
            var startVRAtBoot = config.StartVRAtBoot;
            if (ImGui.Checkbox("Start VR at game launch if headset is available", ref startVRAtBoot))
            {
                config.StartVRAtBoot = startVRAtBoot;
                config.Save();
            }
        }
        if (ImGui.CollapsingHeader("View"))
        {
            var recenterOnViewChange = config.RecenterOnViewChange;
            if (ImGui.Checkbox("Recenter Camera on View Change", ref recenterOnViewChange))
            {
                config.RecenterOnViewChange = recenterOnViewChange;
                config.Save();
            }
            var followCharacterMovement = config.FollowCharacter;
            if (ImGui.Checkbox("Follow Head in First Person", ref followCharacterMovement))
            {
                config.FollowCharacter = followCharacterMovement;
                config.Save();
            }
            var recenter = config.DisableAutoFaceTargetInFirstPerson;
            if (ImGui.Checkbox("Disable Auto Face Target in First Person", ref recenter))
            {
                config.DisableAutoFaceTargetInFirstPerson = recenter;
                config.Save();
            }
            var worldScale = config.WorldScale;
            if (ImGui.SliderFloat("World Scale", ref worldScale, 0.1f, 10f))
            {
                config.WorldScale = worldScale;
                config.Save();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Reset##worldscale"))
            {
                config.WorldScale = defaultConfig.WorldScale; config.Save();
            }

            var gamma = config.Gamma;
            if (ImGui.SliderFloat("Gamma", ref gamma, 0.1f, 10f))
            {
                config.Gamma = gamma;
                config.Save();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Reset##gamma"))
            {
                config.Gamma = defaultConfig.Gamma;
                config.Save();
            }
        }
        if (ImGui.CollapsingHeader("UI"))
        {
            var uiDistance = config.UIDistance;
            if (ImGui.SliderFloat("UI Distance", ref uiDistance, 0.1f, 10f))
            {
                config.UIDistance = uiDistance;
                config.Save();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Reset##uidistance"))
            {
                config.UIDistance = defaultConfig.UIDistance;
                config.Save();
            }
        }
    }
}
