namespace FfxivVR;
public class GameState
{
    public bool IsFirstPerson()
    {
        return SceneCameraExtensions.GetCameraMode() == CameraMode.FirstPerson;
    }
}
