using FFXIVClientStructs.FFXIV.Client.Game;
using System.Runtime.InteropServices;

namespace FfxivVR;

public enum CameraView
{
    FirstPerson = 0,
    ThirdPerson = 1,
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct InternalGameCamera
{
    public static InternalGameCamera* FromCamera(Camera* camera) => (InternalGameCamera*)camera;
    [FieldOffset(0x170)] public CameraView CameraMode;
}