using System;

namespace FfxivVR;

public class FatalVRException : Exception
{
    public FatalVRException(string message) : base(message)
    {

    }
}