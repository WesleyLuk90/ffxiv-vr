using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Silk.NET.Maths;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;

namespace FfxivVR;
internal class DebugWindow : Window
{
    public DebugWindow() : base("FFXIV VR Debug")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize;
        Size = new Vector2(450, 700);
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("tabs"))
        {

            if (ImGui.BeginTabItem("Debug"))
            {
                var input = Debugging.DebugInfo;
                var toShow = string.Join("\n", input.OrderBy(e => e.Key).Select((entry) => $"{entry.Key}: {entry.Value}"));
                ImGui.InputTextMultiline("##Debug", ref toShow, 10000, new Vector2(400, 400));
                var xRotation = Debugging.XRotation;
                if (ImGui.SliderAngle("X Rotation", ref xRotation, -180, 180))
                {
                    Debugging.XRotation = xRotation;
                }
                var yRotation = Debugging.YRotation;
                if (ImGui.SliderAngle("Y Rotation", ref yRotation, -180, 180))
                {
                    Debugging.YRotation = yRotation;
                }
                var zRotation = Debugging.ZRotation;
                if (ImGui.SliderAngle("Z Rotation", ref zRotation, -180, 180))
                {
                    Debugging.ZRotation = zRotation;
                }
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Modes"))
            {
                ImGui.Checkbox("Hide Head", ref Debugging.HideHead);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }
}

static class Debugging
{
    public static ConcurrentDictionary<string, string> DebugInfo = new();
    public static float XRotation = 0;
    public static float YRotation = 0;
    public static float ZRotation = 0;
    public static bool HideHead = false;

    public static Vector3D<float>? Location = null;

    public static void DebugShow(string key, object? value)
    {
        if (value is Vector3D<float> vec)
        {
            value = $"<{vec.X:n3}, {vec.Y:n3}, {vec.Z:n3}>";
        }
        else if (value is Quaternion<float> quat)
        {
            value = $"<{quat.X:n3}, {quat.Y:n3}, {quat.Z:n3}, {quat.W:n3}>";
        }
        DebugInfo[key] = value?.ToString() ?? "null";
    }
    public static Quaternion<float> GetRotation()
    {
        return Quaternion<float>.CreateFromYawPitchRoll(YRotation, XRotation, ZRotation);
    }

    public static void DrawLocation()
    {
        if (Location is not Vector3D<float> loc)
        {
            return;
        }
        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(0, 0));
        ImGui.Begin("Canvas",
            ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing);
        ImGui.SetWindowSize(ImGui.GetIO().DisplaySize);
        Plugin.GameGui.WorldToScreen(new Vector3(loc.X, loc.Y, loc.Z), out Vector2 vec);
        ImGui.GetWindowDrawList().AddCircleFilled(vec, 10, ImGui.GetColorU32(new Vector4(0.8f, 0f, 0f, 1f)));
        ImGui.End();
    }
}