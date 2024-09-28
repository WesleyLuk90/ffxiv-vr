using Silk.NET.OpenXR;

namespace FfxivVRTests
{
    [TestClass]
    unsafe public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            Assert.AreEqual(96, sizeof(CompositionLayerProjectionView));

            var views = new CompositionLayerProjectionView[10];
            var pointers = new CompositionLayerProjectionView*[views.Length];
            fixed (CompositionLayerProjectionView* viewPointer = &views[0])
            {
                for (int i = 0; i < views.Length; i++)
                {
                    pointers[i] = &viewPointer[i];
                }
                Assert.AreEqual((uint)pointers[0] + 96, (uint)pointers[1]);
                Assert.AreEqual((uint)pointers[1] + 96, (uint)pointers[2]);

                CompositionLayerProjectionView* listPointer = pointers;
            }
        }
    }
}
