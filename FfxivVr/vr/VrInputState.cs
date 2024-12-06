using Silk.NET.Maths;
using System.Collections.Generic;

namespace FfxivVR;

public class VrInputState
{
    public Vector2D<float> LeftStick = new Vector2D<float>();
    public Vector2D<float> RightStick = new Vector2D<float>();

    public HashSet<VRButton> Pressed = new();
    public bool IsPhysicalController()
    {
        return Pressed.Contains(VRButton.A) || Pressed.Contains(VRButton.B) || Pressed.Contains(VRButton.X) || Pressed.Contains(VRButton.Y) || LeftStick.LengthSquared > 0 || RightStick.Length > 0;
    }
}