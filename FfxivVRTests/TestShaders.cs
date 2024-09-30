using FfxivVR;

namespace FfxivVRTests;
[TestClass]
public unsafe class TestShaders
{
    [TestMethod]
    public void TestShaderLoad()
    {
        VRShaders.LoadPixelShader();
        VRShaders.LoadVertexShader();
    }
}
