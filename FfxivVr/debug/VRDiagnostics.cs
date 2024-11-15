using Silk.NET.Direct3D11;
using System.Diagnostics;
using System.Threading;

namespace FfxivVR;
public class VRDiagnostics(Logger logger)
{
    private readonly Logger logger = logger;

    private long LeftEyeCopy = 0;
    private long LeftEyeRender = 0;
    private long RightEyeCopy = 0;
    private long RightEyeRender = 0;
    private Stopwatch stopwatch = new Stopwatch();
    private string textureInfo = "No Texture Info";
    public void OnStart()
    {
        LeftEyeCopy = 0;
        LeftEyeRender = 0;
        RightEyeCopy = 0;
        RightEyeRender = 0;
        stopwatch.Reset();
        stopwatch.Start();
        textureInfo = "No Texture Info";
    }

    public void OnStop()
    {
        stopwatch.Stop();
    }

    public void OnCopy(Eye eye)
    {
        if (eye == Eye.Left)
        {
            Interlocked.Increment(ref LeftEyeCopy);
        }
        else
        {
            Interlocked.Increment(ref RightEyeCopy);
        }
    }
    public void OnRender(Eye eye)
    {
        if (eye == Eye.Left)
        {
            Interlocked.Increment(ref LeftEyeRender);
        }
        else
        {
            Interlocked.Increment(ref RightEyeRender);
        }
    }

    public void LogTextures(string log)
    {
        textureInfo = log;
    }

    public unsafe void Print()
    {
        if (!stopwatch.IsRunning)
        {
            logger.Error("Please run while vr is active");
            return;
        }
        var renderTexture = GameTextures.GetGameRenderTexture();
        var texture = (ID3D11Texture2D*)(renderTexture->D3D11Texture2D);
        var description = new Texture2DDesc();
        if (texture != null)
        {
            texture->GetDesc(&description);
        }
        logger.Info("Diagnostic Info");
        logger.Info($"Left Renders {LeftEyeCopy}/{LeftEyeRender}");
        logger.Info($"Right Renders {RightEyeCopy}/{RightEyeRender}");
        logger.Info($"Textures: {textureInfo}");
        logger.Info($"Timer: {stopwatch.Elapsed}");
        logger.Info($"Game Render Texture: {description.Width}x{description.Height} {description.Format}");
    }
}