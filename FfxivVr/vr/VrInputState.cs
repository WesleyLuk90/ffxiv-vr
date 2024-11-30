using Dalamud.Game.ClientState.GamePad;
using Silk.NET.Maths;

namespace FfxivVR;

public class VrInputState
{
    public Vector2D<float> LeftStick = new Vector2D<float>();
    public Vector2D<float> RightStick = new Vector2D<float>();

    public GamepadButtons ButtonsPressed = 0;
    public GamepadButtons ButtonsReleased = 0;
    public GamepadButtons ButtonsRepeat = 0;
    public GamepadButtons ButtonsRaw = 0;

    public bool IsPhysicalController()
    {
        return LeftStick.LengthSquared > 0 || RightStick.Length > 0;
    }
}