namespace FfxivVR.Tests;

using FfxivVR;
using Silk.NET.Maths;

class UIPanelTest
{
    private static readonly Vector2D<float>[] ViewportPoints = [
        new(0.5f, 0.5f),
        new(0.01f, 0.02f),
        new(0.99f, 0.98f),
        new(0.1f, 0.9f),
        new(0.75f, 0.25f),
        new(0.95f, 0.5f),
    ];

    private static UIPanel FlatPanel() => new UIPanel(Matrix4X4.CreateTranslation(0f, 0f, -1f), curvature: 0, aspectRatio: 0.5f);

    private static UIPanel TransformedPanel(float curvature) => new UIPanel(
        Matrix4X4.CreateScale(1.5f) * Matrix4X4.CreateTranslation(0f, 0.2f, -2f) * Matrix4X4.CreateRotationY(0.6f),
        curvature,
        aspectRatio: 9f / 16f);

    [Test]
    public void FlatIntersectCenter()
    {
        var line = FlatPanel().Intersect(new Ray(Vector3D<float>.Zero, -Vector3D<float>.UnitZ));
        Assert.That(line, Is.Not.Null);
        Close(line!.End, 0, 0, -1);
        Close(FlatPanel().WorldToViewport(line.End), 0.5f, 0.5f);
    }

    [Test]
    public void FlatIntersectOffCenter()
    {
        var line = FlatPanel().Intersect(Ray.Through(Vector3D<float>.Zero, new Vector3D<float>(0.5f, 0.25f, -1f)));
        Assert.That(line, Is.Not.Null);
        Close(line!.End, 0.5f, 0.25f, -1);
        Close(FlatPanel().WorldToViewport(line.End), 0.75f, 0.25f);
    }

    [Test]
    public void FlatIntersectBehindViewerMisses()
    {
        Assert.That(FlatPanel().Intersect(new Ray(Vector3D<float>.Zero, Vector3D<float>.UnitZ)), Is.Null);
    }

    [Test]
    public void FlatIntersectParallelMisses()
    {
        Assert.That(FlatPanel().Intersect(new Ray(Vector3D<float>.Zero, Vector3D<float>.UnitX)), Is.Null);
    }

    [Test]
    public void FlatIntersectOutsidePanelHasNoViewportPosition()
    {
        var line = FlatPanel().Intersect(Ray.Through(Vector3D<float>.Zero, new Vector3D<float>(2f, 0f, -1f)));
        Assert.That(line, Is.Not.Null);
        Close(line!.End, 2, 0, -1);
        Assert.That(FlatPanel().WorldToViewport(line.End), Is.Null);
    }

    [Test]
    public void FlatViewportToWorldCorners()
    {
        var panel = FlatPanel();
        Close(panel.ViewportToWorld(new(0, 0)), -1, 0.5f, -1);
        Close(panel.ViewportToWorld(new(1, 1)), 1, -0.5f, -1);
    }

    [Test]
    public void CurvedViewportToWorldMatchesShader()
    {
        var panel = new UIPanel(Matrix4X4.CreateTranslation(0f, 0f, -1f), curvature: 1, aspectRatio: 1);
        Close(panel.ViewportToWorld(new(0.5f, 0.5f)), 0, 0, -1);
        Close(panel.ViewportToWorld(new(1f, 0.5f)), MathF.Sin(1), 0, -MathF.Cos(1));
        Close(panel.ViewportToWorld(new(0f, 0.5f)), -MathF.Sin(1), 0, -MathF.Cos(1));
    }

    [Test]
    public void CurvatureMatchingDistanceWrapsAroundViewer()
    {
        var panel = new UIPanel(Matrix4X4.CreateTranslation(0f, 0f, -2f), curvature: 0.5f, aspectRatio: 1);
        foreach (var point in ViewportPoints)
        {
            var world = panel.ViewportToWorld(point);
            var horizontalDistance = MathF.Sqrt(world.X * world.X + world.Z * world.Z);
            Assert.That(horizontalDistance, Is.EqualTo(2).Within(0.001f));
        }
    }

    [TestCase(0f)]
    [TestCase(0.25f)]
    [TestCase(1f)]
    public void ViewportRoundTrip(float curvature)
    {
        var panel = TransformedPanel(curvature);
        foreach (var point in ViewportPoints)
        {
            Close(panel.WorldToViewport(panel.ViewportToWorld(point)), point.X, point.Y);
        }
    }

    [TestCase(0f)]
    [TestCase(0.25f)]
    [TestCase(1f)]
    public void IntersectThroughViewportPointHitsIt(float curvature)
    {
        var panel = TransformedPanel(curvature);
        var origins = new[] { Vector3D<float>.Zero, new Vector3D<float>(0.2f, -0.1f, 0.3f), new Vector3D<float>(-0.5f, 0.4f, -0.2f) };
        foreach (var origin in origins)
        {
            foreach (var point in ViewportPoints)
            {
                var target = panel.ViewportToWorld(point);
                var line = panel.Intersect(Ray.Through(origin, target));
                Assert.That(line, Is.Not.Null);
                Close(line!.End, target.X, target.Y, target.Z);
                Close(panel.WorldToViewport(line.End), point.X, point.Y);
            }
        }
    }

    [Test]
    public void CurvedIntersectSkipsBackOfCylinder()
    {
        var panel = new UIPanel(Matrix4X4.CreateTranslation(0f, 0f, -1f), curvature: 1, aspectRatio: 1);
        var line = panel.Intersect(new Ray(new Vector3D<float>(0f, 0f, 5f), -Vector3D<float>.UnitZ));
        Assert.That(line, Is.Not.Null);
        Close(line!.End, 0, 0, -1);
        Close(panel.WorldToViewport(line.End), 0.5f, 0.5f);
    }

    [Test]
    public void CurvedIntersectOffArcHasNoViewportPosition()
    {
        var panel = new UIPanel(Matrix4X4.CreateTranslation(0f, 0f, -1f), curvature: 1, aspectRatio: 1);
        var line = panel.Intersect(new Ray(Vector3D<float>.Zero, Vector3D<float>.UnitZ));
        Assert.That(line, Is.Not.Null);
        Close(line!.End, 0, 0, 1);
        Assert.That(panel.WorldToViewport(line.End), Is.Null);
    }

    [Test]
    public void CurvedIntersectMissesCylinder()
    {
        var panel = new UIPanel(Matrix4X4.CreateTranslation(0f, 0f, -1f), curvature: 1, aspectRatio: 1);
        Assert.That(panel.Intersect(new Ray(new Vector3D<float>(5f, 0f, 0f), Vector3D<float>.UnitY)), Is.Null);
    }

    private static void Close(Vector3D<float> a, float x, float y, float z)
    {
        Assert.That(a.X, Is.EqualTo(x).Within(0.001f));
        Assert.That(a.Y, Is.EqualTo(y).Within(0.001f));
        Assert.That(a.Z, Is.EqualTo(z).Within(0.001f));
    }

    private static void Close(Vector2D<float>? a, float x, float y)
    {
        Assert.That(a, Is.Not.Null);
        Assert.That(a!.Value.X, Is.EqualTo(x).Within(0.001f));
        Assert.That(a.Value.Y, Is.EqualTo(y).Within(0.001f));
    }
}