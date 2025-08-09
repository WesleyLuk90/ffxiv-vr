using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Silk.NET.Maths;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;

namespace FfxivVR;

public class DebugWindow : Window
{
    private readonly Debugging debugging;

    public DebugWindow(Debugging debugging) : base("FFXIV VR Debug")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize;
        Size = new Vector2(450, 700);
        this.debugging = debugging;
    }

    public override void Draw()
    {
        using (ImRaii.TabBar("tabs"))
        {
            using (var tab = ImRaii.TabItem("Debug"))
            {
                if (tab)
                {
                    var input = debugging.DebugInfo;
                    var toShow = string.Join("\n", input.OrderBy(e => e.Key).Select((entry) => $"{entry.Key}: {entry.Value}"));
                    ImGui.InputTextMultiline("##Debug", ref toShow, 10000, new Vector2(400, 400));
                    var xRotation = debugging.XRotation;
                    if (ImGui.SliderAngle("X Rotation", ref xRotation, -180, 180))
                    {
                        debugging.XRotation = xRotation;
                    }
                    var yRotation = debugging.YRotation;
                    if (ImGui.SliderAngle("Y Rotation", ref yRotation, -180, 180))
                    {
                        debugging.YRotation = yRotation;
                    }
                    var zRotation = debugging.ZRotation;
                    if (ImGui.SliderAngle("Z Rotation", ref zRotation, -180, 180))
                    {
                        debugging.ZRotation = zRotation;
                    }
                }
            }
            using (var tab = ImRaii.TabItem("Controls"))
            {
                if (tab)
                {
                    ImGui.Checkbox("Trace Logging", ref debugging.Trace);
                    ImGui.Checkbox("Force Hide Head", ref debugging.HideHead);
                    ImGui.Checkbox("Always Motion Controls", ref debugging.AlwaysMotionControls);
                    ImGui.Checkbox("Enable tracking in 3rd person", ref debugging.ForceTracking);
                    ImGui.InputInt("Index", ref debugging.Index);
                    ImGui.SliderFloat("Float", ref debugging.Float, -1, 1);
                }
            }
        }
    }
}

public class Debugging(
    IGameGui gameGui
)
{
    public ConcurrentDictionary<string, string> DebugInfo = new();
    public float XRotation = 0;
    public float YRotation = 0;
    public float ZRotation = 0;

    public int Index = 0;
    public float Float = 0;
    public bool HideHead = false;
    public bool AlwaysMotionControls = false;
    public bool Trace = false;

    public Vector3D<float>? Location = null;
    private readonly IGameGui gameGui = gameGui;

    public bool ForceTracking = false;
    public void DebugShow(string key, object? value)
    {
        if (value is Vector3D<float> vec)
        {
            value = $"<{vec.X:n3}, {vec.Y:n3}, {vec.Z:n3}>";
        }
        else if (value is Vector2D<float> vec2)
        {
            value = $"<{vec2.X:n3}, {vec2.Y:n3}>";
        }
        else if (value is Quaternion<float> quat)
        {
            value = $"<{quat.X:n3}, {quat.Y:n3}, {quat.Z:n3}, {quat.W:n3}>";
        }
        DebugInfo[key] = value?.ToString() ?? "null";
    }
    public Quaternion<float> GetRotation()
    {
        return Quaternion<float>.CreateFromYawPitchRoll(YRotation, XRotation, ZRotation);
    }

    public void DrawLocation()
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
        gameGui.WorldToScreen(new Vector3(loc.X, loc.Y, loc.Z), out Vector2 vec);
        ImGui.GetWindowDrawList().AddCircleFilled(vec, 10, ImGui.GetColorU32(new Vector4(0.8f, 0f, 0f, 1f)));
        ImGui.End();
    }
}