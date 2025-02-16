namespace FfxivVR.Tests;

using FfxivVR;
using Silk.NET.Maths;

class GeometryFactoryTest
{

    [Test]
    public void Plane()
    {
        var plane = GeometryFactory.Plane();
        Assert.That(plane, Has.Count.EqualTo(6));
    }

    [Test]
    public void Cylinder()
    {
        var cylinder = GeometryFactory.Cylinder(4);
        Assert.That(cylinder, Has.Count.EqualTo(24));
        Close(cylinder[0].Position, 1, 1, 1);
        Close(cylinder[6].Position, 1, 1, -1);
        Close(cylinder[12].Position, -1, 1, -1);
        Close(cylinder[18].Position, -1, 1, 1);
    }

    private void Close(Vector3D<float> a, float x, float y, float z)
    {
        Assert.That(a.X, Is.EqualTo(x).Within(0.01f));
        Assert.That(a.Y, Is.EqualTo(y).Within(0.01f));
        Assert.That(a.Z, Is.EqualTo(z).Within(0.01f));
    }
}