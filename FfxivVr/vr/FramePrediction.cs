namespace FfxivVR;

public class FramePrediction(
    VRSystem vrSystem
)
{


    private long? lastStart = null;
    private long? lastLastPrediction = null;
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
        return now + estimatedDelay;
    }

    public long? GetAltPredictedFrameTime()
    {
        if (lastPrediction is long last && lastLastPrediction is long lastLast)
        {
            return last + (last - lastLast);
        }
        return null;
    }

    public void MarkPredictedFrameTime(long predictedTime)
    {
        lastLastPrediction = lastPrediction;
        lastPrediction = predictedTime;
    }

    internal void Reset()
    {
        lastStart = null;
        lastPrediction = null;
    }
}