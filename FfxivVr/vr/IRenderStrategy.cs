using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Silk.NET.Maths;
using Silk.NET.OpenXR;

namespace FfxivVR;

public unsafe interface IRenderStrategy
{
    public VRSessionData? VRSessionData { get; }

    void Initialize(Vector2D<uint> size);
    void UpdateCamera(Camera* camera);
    void PrepareCameraPhase(View[] view, VRInputData inputData, VRCameraMode cameraType);
    void OnStartUI();
    void OnStartUIRender(Eye? eye);
    bool ShouldSecondRender();
    bool OnPresentFrame();
    void OnSessionEnd();
}