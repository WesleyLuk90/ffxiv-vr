using FfxivVR;
using Silk.NET.OpenXR;
using static FfxivVR.VRSystem;

namespace FfxivVRTests;

[TestClass]
public class TestSystem
{
    [TestMethod]
    public unsafe void TestInitialize()
    {
        var system = new VRSystem(
            xr: XR.GetApi(),
            device: null,
            logger: new Logger()
            );
        Assert.ThrowsException<FormFactorUnavailableException>(() => system.Initialize(), "Form factor unavailable, make sure the headset is connected");
    }
}
