namespace FfxivVR.Tests;

public class VRShadersTest
{
    [Test()]
    public void LoadShaders()
    {
        VRShaders.LoadPixelShader();
        VRShaders.LoadVertexShader();
    }
}