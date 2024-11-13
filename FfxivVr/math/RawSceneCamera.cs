using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System;
using System.Runtime.InteropServices;

namespace FfxivVR;

public enum CameraMode
{
    FirstPerson = 0,
    ThirdPerson = 1,
}

[StructLayout(LayoutKind.Explicit)]
public struct RawSceneCamera
{

    [FieldOffset(0x170)] public CameraMode CameraMode;
}