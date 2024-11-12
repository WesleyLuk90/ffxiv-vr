﻿using Dalamud.Interface.Windowing;
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
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize;
    }

    public override void Draw()
    {
        var input = Debugging.DebugInfo;
        var toShow = string.Join("\n", input.Select((k, v) => $"{k}: {v}"));
        ImGui.InputTextMultiline("Debug", ref toShow, 10000, new Vector2(400, 400));
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
    }
}

static class Debugging
{
    public static ConcurrentDictionary<string, string> DebugInfo = new();
    public static float XRotation = 0;
    public static float YRotation = 0;
    public static float ZRotation = 0;
    public static bool DebugMode = false;

    public static void DebugShow(string key, object value)
    {
        DebugInfo[key] = value?.ToString() ?? "null";
    }
    public static Quaternion<float> GetRotation()
    {
        return Quaternion<float>.CreateFromYawPitchRoll(YRotation, XRotation, ZRotation);
    }
}