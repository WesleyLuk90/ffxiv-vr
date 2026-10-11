namespace FfxivVR.Tests;

using FfxivVR;
using Silk.NET.Maths;

class RayTest
{
    [Test]
    public void ThroughNormalizesDirection()
    {
        var ray = Ray.Through(new Vector3D<float>(1f, 2f, 3f), new Vector3D<float>(1f, 2f, -7f));
        Close(ray.Origin, 1, 2, 3);
        Close(ray.Direction, 0, 0, -1);
    }

    [Test]
    public void TransformRotatesAndTranslates()
    {
        var matrix = Matrix4X4.CreateRotationY(MathF.PI / 2) * Matrix4X4.CreateTranslation(1f, 2f, 3f);
        var ray = new Ray(Vector3D<float>.Zero, -Vector3D<float>.UnitZ).Transform(matrix);
        Close(ray.Origin, 1, 2, 3);
        Close(ray.Direction, -1, 0, 0);
    }

    [Test]
    public void TransformKeepsDirectionNormalized()
    {
        var ray = new Ray(new Vector3D<float>(1f, 0f, 0f), Vector3D<float>.UnitX).Transform(Matrix4X4.CreateScale(3f));
        Close(ray.Origin, 3, 0, 0);
        Close(ray.Direction, 1, 0, 0);
    }

    [Test]
    public void TransformedRayPassesThroughTransformedPoint()
    {
        var matrix = Matrix4X4.CreateRotationX(0.3f) * Matrix4X4.CreateRotationY(-1.1f) * Matrix4X4.CreateTranslation(10f, -4f, 2f);
        var origin = new Vector3D<float>(0.1f, 1.6f, 0.2f);
        var target = new Vector3D<float>(-0.4f, 1.2f, -1.5f);
        var ray = Ray.Through(origin, target).Transform(matrix);
        var expected = Vector3D.Normalize(Vector3D.Transform(target, matrix) - Vector3D.Transform(origin, matrix));
        Close(ray.Direction, expected.X, expected.Y, expected.Z);
    }

    private static void Close(Vector3D<float> a, float x, float y, float z)
    {
        Assert.That(a.X, Is.EqualTo(x).Within(0.001f));
        Assert.That(a.Y, Is.EqualTo(y).Within(0.001f));
        Assert.That(a.Z, Is.EqualTo(z).Within(0.001f));
    }
}