using FFXIVClientStructs.FFXIV.Client.Game;
using System.Runtime.InteropServices;

namespace FfxivVR;

public enum CameraView
{
    FirstPerson = 0,
    ThirdPerson = 1,
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct GameCameraExtended
{
    public static GameCameraExtended* FromCamera(Camera* camera) => (GameCameraExtended*)camera;
    [FieldOffset(0x130)] public float DirectionHorizontal;
    [FieldOffset(0x134)] public float DirectionVertical;
    [FieldOffset(0x180)] public CameraView CameraMode;
}