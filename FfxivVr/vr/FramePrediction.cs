namespace FfxivVR;
public class FramePrediction
{
    private VRSystem vrSystem;

    public FramePrediction(VRSystem vrSystem)
    {
        this.vrSystem = vrSystem;
    }

    private long? lastStart = null;
    private long? lastPrediction = null;
    public long GetPredictedFrameTime()
    {
        var now = vrSystem.Now();
        long estimatedDelay = 0;
        if (lastStart is long start && lastPrediction is long prediction)
        {
            estimatedDelay = prediction - start;
        }
        lastStart = now;
        return vrSystem.Now() + estimatedDelay;
    }

    public void MarkPredictedFrameTime(long predictedTime)
    {
        lastPrediction = predictedTime;
    }

    internal void Reset()
    {
        lastStart = null;
        lastPrediction = null;
    }
}