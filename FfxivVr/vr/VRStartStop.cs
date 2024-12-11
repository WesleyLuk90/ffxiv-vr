namespace FfxivVR;

public class VRStartStop(
    VRLifecycle vrLifecycle,
    Transitions transitions
)
{
    private readonly VRLifecycle vrLifecycle = vrLifecycle;
    private readonly Transitions transitions = transitions;

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