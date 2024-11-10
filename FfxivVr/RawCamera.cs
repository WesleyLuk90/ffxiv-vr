using System.Runtime.InteropServices;

namespace FfxivVR;

// Cast from FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CameraManager.Instance()->CurrentCamera
[StructLayout(LayoutKind.Explicit)]
public struct RawCamera
{
    [FieldOffset(0x120)] public float CurrentHRotation;
    [FieldOffset(0x124)] public float CurrentVRotation;
}
