
using Silk.NET.Maths;
using System;
using System.Collections.Generic;

namespace FfxivVR;
public static class GeometryFactory
{
    public static List<Vertex> Plane(Matrix4X4<float>? positionTransform = null, Matrix4X4<float>? uvTransform = null)
    {
        var tl = new Vertex(
            Vector3D.Transform(new Vector3D<float>(-1, 1, 0), positionTransform ?? Matrix4X4<float>.Identity),
            Vector2D.Transform(new Vector2D<float>(0, 0), uvTransform ?? Matrix4X4<float>.Identity));
        var tr = new Vertex(
            Vector3D.Transform(new Vector3D<float>(1, 1, 0), positionTransform ?? Matrix4X4<float>.Identity),
            Vector2D.Transform(new Vector2D<float>(1, 0), uvTransform ?? Matrix4X4<float>.Identity));
        var bl = new Vertex(
            Vector3D.Transform(new Vector3D<float>(-1, -1, 0), positionTransform ?? Matrix4X4<float>.Identity),
            Vector2D.Transform(new Vector2D<float>(0, 1), uvTransform ?? Matrix4X4<float>.Identity));
        var br = new Vertex(
            Vector3D.Transform(new Vector3D<float>(1, -1, 0), positionTransform ?? Matrix4X4<float>.Identity),
            Vector2D.Transform(new Vector2D<float>(1, 1), uvTransform ?? Matrix4X4<float>.Identity));
        return new List<Vertex>(){
            tl,
            tr,
            bl,
            bl,
            tr,
            br,
        };
    }
    public static List<Vertex> SegmentedPlane(int segments)
    {
        List<Vertex> vertices = new();
        var scale = 1f / segments;
        var size = 2 * scale;
        for (int y = 0; y < segments; y++)
        {
            for (int x = 0; x < segments; x++)
            {
                var positionTransformation = Matrix4X4.CreateScale(scale, scale, 1)
                 * Matrix4X4.CreateTranslation(size * 0.5f - 1 + size * x, -size * 0.5f + 1 - size * y, 0);
                var uvTransform = Matrix4X4.CreateScale(scale, scale, 1f)
                 * Matrix4X4.CreateTranslation(scale * x, scale * y, 0f);

                vertices.AddRange(Plane(positionTransform: positionTransformation, uvTransform: uvTransform));
            }
        }
        return vertices;
    }

    public static List<Vertex> Cylinder(int sides)
    {
        List<Vertex> vertices = new();
        for (int i = 0; i < sides; i++)
        {
            var transform = Matrix4X4.CreateScale(MathF.Tan(2 * MathF.PI / sides / 2), 1, 1)
                * Matrix4X4.CreateTranslation(Vector3D<float>.UnitZ)
                * Matrix4X4.CreateRotationY(2 * MathF.PI * i / sides);

            vertices.AddRange(Plane(positionTransform: transform));
        }
        return vertices;
    }
}