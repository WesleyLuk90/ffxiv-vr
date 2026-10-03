using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using System.Numerics;

namespace FfxivVR;

public class DebugWindow : Window
{
    private readonly Debugging debugging;
    private readonly GameState gameState;

    public DebugWindow(Debugging debugging, GameState gameState) : base("FFXIV VR Debug")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize;
        Size = new Vector2(450, 700);
        this.debugging = debugging;
        this.gameState = gameState;
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
            using (var tab = ImRaii.TabItem("Custom Data"))
            {
                if (tab)
                {
                    renderCustomData();
                }
            }
        }
    }
    private unsafe void renderCustomData()
    {
        var sceneCameraEx = gameState.GetSceneCameraExtended();
        var gameCamera = gameState.GetGameCameraExtended();
        var charBase = gameState.GetCharacterBaseExtended();
        var charExt = gameState.GetCharacterExtended();
        var charOrGpose = gameState.getCharacterOrGpose();
        var currentCamera = gameState.GetCurrentCamera();

        if (sceneCameraEx != null)
        {
            ImGui.Text($"Camera Extras 0x{Convert.ToString((long)sceneCameraEx, 16)}");
            ImGui.Text($"Horizontal Rotation Rad:{sceneCameraEx->CurrentHRotation:F2} Deg:{float.RadiansToDegrees(sceneCameraEx->CurrentHRotation):F2}");
            ImGui.Text($"Vertical Rotation Rad:{sceneCameraEx->CurrentVRotation:F2} Deg:{float.RadiansToDegrees(sceneCameraEx->CurrentVRotation):F2}");
        }

        if (gameCamera != null)
        {
            ImGui.Text($"Game Camera Address: 0x{Convert.ToString((long)gameCamera, 16)}");
            ImGui.Text($"DirectionHorizontal: {float.RadiansToDegrees(gameCamera->DirectionHorizontal):F2}°");
            ImGui.Text($"DirectionVertical: {float.RadiansToDegrees(gameCamera->DirectionVertical):F2}°");
            ImGui.Text($"CameraMode: {gameCamera->CameraMode}");
        }

        if (charExt != null && charOrGpose != null && currentCamera != null)
        {
            ImGui.Text($"Character Extended Address: 0x{Convert.ToString((long)charExt, 16)}");
            ImGui.Text($"FixHeadPosition: {charExt->FixHeadPosition:F2} == {currentCamera->Position.Y - charOrGpose->Position.Y:F2}");
        }

        if (charBase != null)
        {
            ImGui.Text($"Character Base Address: 0x{Convert.ToString((long)charBase, 16)}");
            ImGui.Text($"Height: {charBase->Height:F2}");
        }
    }
}