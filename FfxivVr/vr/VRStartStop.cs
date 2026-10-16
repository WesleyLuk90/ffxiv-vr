namespace FfxivVR;

public class VRStartStop(
    VRLifecycle vrLifecycle,
    Transitions transitions
)
{
    private bool restartPending = false;

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
        vrLifecycle.RequestStop();
        transitions.PostStopVR();
    }

    public void RestartVR()
    {
        if (vrLifecycle.IsEnabled())
        {
            StopVR();
            restartPending = true;
        }
        else
        {
            StartVR();
        }
    }

    public void Update()
    {
        if (restartPending && !vrLifecycle.IsEnabled())
        {
            restartPending = false;
            StartVR();
        }
    }
}