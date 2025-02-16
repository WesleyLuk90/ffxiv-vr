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
        Close(plane[0].Position, -1, 1, 0);
        Close(plane[0].UV, 0, 0);
        Close(plane[1].Position, 1, 1, 0);
        Close(plane[1].UV, 1, 0);
        Close(plane[2].Position, -1, -1, 0);
        Close(plane[2].UV, 0, 1);
        Close(plane[3].Position, -1, -1, 0);
        Close(plane[3].UV, 0, 1);
        Close(plane[4].Position, 1, 1, 0);
        Close(plane[4].UV, 1, 0);
        Close(plane[5].Position, 1, -1, 0);
        Close(plane[5].UV, 1, 1);
    }

    [Test]
    public void Cylinder()
    {
        var cylinder = GeometryFactory.Cylinder(4);
        Assert.That(cylinder, Has.Count.EqualTo(24));
        Close(cylinder[0].Position, -1, 1, 1);
        Close(cylinder[1].Position, 1, 1, 1);
        Close(cylinder[6].Position, 1, 1, 1);
        Close(cylinder[12].Position, 1, 1, -1);
        Close(cylinder[18].Position, -1, 1, -1);
    }

    [Test]
    public void SegmentedPlane()
    {
        var plane = GeometryFactory.SegmentedPlane(4);
        Assert.That(plane, Has.Count.EqualTo(96));
        Close(plane[0].Position, -1, 1, 0);
        Close(plane[0].UV, 0, 0);
        Close(plane[1].Position, -0.5f, 1, 0);
        Close(plane[1].UV, 0.25f, 0);
        Close(plane[6].Position, -0.5f, 1, 0);
        Close(plane[6].UV, 0.25f, 0);
        Close(plane[7].Position, 0, 1, 0);
        Close(plane[7].UV, 0.5f, 0);
        Close(plane[24].Position, -1, 0.5f, 0);
        Close(plane[24].UV, 0, 0.25f);
        Close(plane[25].Position, -0.5f, 0.5f, 0);
        Close(plane[25].UV, 0.25f, 0.25f);
    }

    private void Close(Vector3D<float> a, float x, float y, float z)
    {
        Assert.That(a.X, Is.EqualTo(x).Within(0.01f));
        Assert.That(a.Y, Is.EqualTo(y).Within(0.01f));
        Assert.That(a.Z, Is.EqualTo(z).Within(0.01f));
    }
    private void Close(Vector2D<float> a, float x, float y)
    {
        Assert.That(a.X, Is.EqualTo(x).Within(0.01f));
        Assert.That(a.Y, Is.EqualTo(y).Within(0.01f));
    }
}