using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin.Services;
using System;
using System.Diagnostics;

namespace FfxivVR;

public class GameClock(
    IDtrBar dtrBar
) : IDisposable
{
    private Stopwatch frameTimer = Stopwatch.StartNew();
    private Stopwatch updateTimer = Stopwatch.StartNew();
    private float frameTime = 0f;
    private float smoothingFactor = 0.9f;

    private IDtrBarEntry fpsEntry = dtrBar.Get("VR FPS", "VR FPS: N/A");

    public void Dispose()
    {
        fpsEntry.Remove();
    }

    public float MarkFrame()
    {
        var elapsed = (float)frameTimer.Elapsed.TotalSeconds;
        frameTimer.Restart();

        frameTime = frameTime * smoothingFactor + elapsed * (1 - smoothingFactor);
        if (updateTimer.ElapsedMilliseconds > 1000)
        {
            var fps = 1f / frameTime;
            updateTimer.Restart();
            fpsEntry.Text = $"VR FPS: {fps:F1}";
        }
        return elapsed;
    }

}