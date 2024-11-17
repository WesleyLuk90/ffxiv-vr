using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using System.Runtime.InteropServices;
namespace FfxivVR;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct InternalSceneCamera
{
    public static InternalSceneCamera* FromCamera(Camera* camera) => (InternalSceneCamera*)camera;
    [FieldOffset(0x120)] public float CurrentHRotation;
    [FieldOffset(0x124)] public float CurrentVRotation;
}