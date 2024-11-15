using Silk.NET.OpenXR;

namespace FfxivVR;

public class VRState
{
    public SessionState State = SessionState.Unknown;
    public bool SessionRunning = false;
    public bool Exiting = false;

    internal bool IsActive()
    {
        return State == SessionState.Synchronized || State == SessionState.Visible || State == SessionState.Focused;
    }

}