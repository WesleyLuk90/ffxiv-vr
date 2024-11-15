using Silk.NET.Maths;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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