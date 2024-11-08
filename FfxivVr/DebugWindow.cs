using Dalamud.Interface.Windowing;
using ImGuiNET;
using System.Numerics;

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
        var input = Debugging.DebugInfo;
        ImGui.InputTextMultiline("Debug", ref input, 10000, new Vector2(400, 400));
    }
}

static class Debugging
{
    public static string DebugInfo = "";
}