using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using System.Runtime.InteropServices;
namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct SceneCameraExtended
{
    public static SceneCameraExtended* FromCamera(Camera* camera) => (SceneCameraExtended*)camera;
    [FieldOffset(0x130)] public float CurrentHRotation;
    [FieldOffset(0x134)] public float CurrentVRotation;
}