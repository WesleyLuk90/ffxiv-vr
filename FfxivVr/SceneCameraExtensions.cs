using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System;

namespace FfxivVR;

public enum CameraMode
{
    FirstPerson = 0,
    ThirdPerson = 1,
}
public static class SceneCameraExtensions
{
    public static unsafe CameraMode GetCameraMode()
    {
        var modePtr = (IntPtr)CameraManager.Instance()->GetActiveCamera() + 0x170;
        var mode = *(int*)modePtr;
        return (CameraMode)mode;
    }
}

