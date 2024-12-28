
using Silk.NET.Maths;

namespace FfxivVR.Tests;

public class MathFactoryTests
{
    [Test]
    public void AcuteAngleBetween()
    {
        TestAcuteAngleBetween(90, 45, -45);
        TestAcuteAngleBetween(360, -360, 0);
        TestAcuteAngleBetween(360 + 45, -360, -45);
        TestAcuteAngleBetween(360 - 45, -360, 45);
        TestAcuteAngleBetween(170, -170, 20);
        TestAcuteAngleBetween(-170, 170, -20);
    }

    private void TestAcuteAngleBetween(float from, float to, float expected)
    {
        Assert.That(MathFactory.AcuteAngleBetween(float.DegreesToRadians(from), float.DegreesToRadians(to)),
            Is.EqualTo(float.DegreesToRadians(expected)).Within(0.001f));
    }

    [Test]
    public void RotateOnto()
    {
        void Test(Vector3D<float> from, Vector3D<float> to)
        {
            var rotation = MathFactory.RotateOnto(from, to);
            var rotated = Vector3D.Transform(from, rotation);
            Assert.That(Vector3D.Dot(Vector3D.Normalize(rotated), Vector3D.Normalize(to)), Is.EqualTo(1).Within(0.01f));
        }
        Test(new Vector3D<float>(1, 0, 0), new Vector3D<float>(0, 1, 0));
        Test(new Vector3D<float>(0, 1, 0), new Vector3D<float>(0, 0, 1));
        Test(new Vector3D<float>(0, 0, 1), new Vector3D<float>(1, 0, 0));
        Test(new Vector3D<float>(0, 0, 2), new Vector3D<float>(1, 0, 0));
        Test(new Vector3D<float>(1, 0, 0), new Vector3D<float>(1, 0, 0));
        Test(new Vector3D<float>(0, 0, 1), new Vector3D<float>(1, 0, -1));
        Test(new Vector3D<float>(1.01f, 0, 0), new Vector3D<float>(1.01f, 0, 0));
        Test(new Vector3D<float>(-1.01f, 0, 0), new Vector3D<float>(1.01f, 0, 0));
    }
}