using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace FfxivVR;
internal class DebugWindow : Window
{
    public DebugWindow() : base("FFXIV VR Debug")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new System.Numerics.Vector2(400, 400);
    }

    public override void Draw()
    {
        ImGui.Text(Debugging.DebugInfo);
    }
}

static class Debugging
{
    public static string DebugInfo = "";
}