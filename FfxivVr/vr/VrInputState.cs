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
        return LeftStick.LengthSquared > 0 || RightStick.Length > 0;
    }
}