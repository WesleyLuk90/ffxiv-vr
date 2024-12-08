using System.Diagnostics;

namespace FfxivVR;

public class GameClock
{
    private Stopwatch stopwatch = Stopwatch.StartNew();

    public float GetTicks()
    {
        var elapsed = stopwatch.Elapsed.TotalSeconds;
        stopwatch.Restart();
        return (float)elapsed;
    }
}