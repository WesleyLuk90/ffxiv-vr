using FfxivVR;
using Silk.NET.OpenXR;

namespace FfxivVRTests
{
    internal class TestSystem
    {
        void TestInitialize()
        {
            var system = new VRSystem(
                xr: XR.GetApi(),
                device: null,
                logger: new Logger()
                );
            system.Initialize();
        }
    }
}
