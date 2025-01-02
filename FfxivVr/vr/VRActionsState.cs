using Silk.NET.Maths;
using System.Collections.Generic;

namespace FfxivVR;

public class VRActionsState
{
    public Vector2D<float> LeftStick = new Vector2D<float>();
    public Vector2D<float> RightStick = new Vector2D<float>();

    public HashSet<VRButton> Pressed = new();

    public bool IsPhysicalController()
    {
        // B and Y are sometimes triggered with virtual desktop hand tracking so ignore them
        return Pressed.Contains(VRButton.A) || Pressed.Contains(VRButton.X) || LeftStick.LengthSquared > 0 || RightStick.Length > 0;
    }
}