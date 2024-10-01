using FfxivVR;
using Silk.NET.OpenXR;
using System.Runtime.InteropServices;

namespace FfxivVRTests;
[TestClass]
public unsafe class TestGeometry
{
    [TestMethod]
    public void TestSpan()
    {
        Assert.AreEqual(sizeof(Vector4f) * Geometry.CubeVertices.Length, 36 * 4 * 4);

        var span = new Span<Vector4f>(Geometry.CubeVertices);
        var bytes = MemoryMarshal.AsBytes(span);
        Assert.AreEqual(bytes.Length, 36 * 4 * 4);

        Assert.AreEqual(new Span<byte>(new byte[sizeof(Vector4f) * 10]).Length, 10 * 4 * 4);
    }
}
