using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace FfxivVR;
internal class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly Configuration defaultConfig = new Configuration();

    public ConfigWindow(Configuration configuration) : base("FFXIV VR Settings")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(400, 400);
        this.config = configuration;
    }


    public void Dispose()
    {
    }

    public override void Draw()
    {
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
                config.RecenterOnViewChange = followCharacterMovement;
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
