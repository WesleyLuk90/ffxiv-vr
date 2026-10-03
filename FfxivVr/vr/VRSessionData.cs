using Silk.NET.OpenXR;
using System;

namespace FfxivVR;

public class VRSessionData(
    View[] views,
    VRInputData vrInputData,
    VRCameraMode cameraMode
)
{
    public View[] Views = views;
    public VRCameraMode CameraMode = cameraMode;
    private GameCamera? GameCamera = null;
    public VRInputData VRInputData { get; } = vrInputData;

    internal void ResetGameCamera()
    {
        GameCamera = null;
    }

    // Create it once per eye so we have consistent data
    public GameCamera? GetGameCamera(Func<GameCamera?> factory)
    {
        if (GameCamera is GameCamera g)
        {
            return g;
        }
        GameCamera = factory();
        return GameCamera;
    }
}