
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
}