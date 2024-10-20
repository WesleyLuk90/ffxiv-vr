using System;

namespace FfxivVR;
public enum Eye
{
    Left,
    Right,
}

public static class EyeExtensions
{
    public static int ToIndex(this Eye eye)
    {
        switch (eye)
        {
            case Eye.Left:
                return 0;
            case Eye.Right:
                return 1;
            default:
                throw new Exception("Invalid eye");
        }
    }
}