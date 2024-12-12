namespace FfxivVR;

public class VRStartStop(
    VRLifecycle vrLifecycle,
    Transitions transitions
)
{
    public void ToggleVR()
    {
        if (vrLifecycle.IsEnabled())
        {
            StopVR();
        }
        else
        {
            StartVR();
        }
    }
    public void StartVR()
    {
        if (!transitions.PreStartVR())
        {
            return;
        }
        vrLifecycle.EnableVR();
        if (vrLifecycle.IsEnabled())
        {
            transitions.PostStartVR();
        }
        else
        {
            transitions.PostStopVR();
        }
    }
    public void StopVR()
    {
        vrLifecycle.DisableVR();
        transitions.PostStopVR();
    }
}