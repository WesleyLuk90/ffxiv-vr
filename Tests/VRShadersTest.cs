namespace FfxivVR.Tests;
public class VRShadersTest
{
    [Test()]
    public unsafe void LoadShaders()
    {
        VRShaders.LoadPixelShader();
        VRShaders.LoadVertexShader();
    }
}