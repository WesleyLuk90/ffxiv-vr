
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FfxivVR;
public static class GeometryFactory
{
    public static List<Vertex> Plane()
    {
        var tr = new Vertex(new Vector3D<float>(1, 1, 0), new Vector2D<float>(1, 0));
        var tl = new Vertex(new Vector3D<float>(-1, 1, 0), new Vector2D<float>(0, 0));
        var bl = new Vertex(new Vector3D<float>(-1, -1, 0), new Vector2D<float>(0, 1));
        var br = new Vertex(new Vector3D<float>(1, -1, 0), new Vector2D<float>(1, 1));
        return new List<Vertex>(){
            tr,
            tl,
            bl,
            tr,
            bl,
            br,
        };
    }

    public static List<Vertex> Cylinder(int sides)
    {
        Debug.WriteLine("Making a cylinder");
        List<Vertex> vertices = new();
        for (int i = 0; i < sides; i++)
        {
            var transform = Matrix4X4.CreateScale(new Vector3D<float>(MathF.Tan(2 * MathF.PI / sides / 2), 1, 1)) * Matrix4X4.CreateTranslation(Vector3D<float>.UnitZ) * Matrix4X4.CreateRotationY(2 * MathF.PI * i / sides);
            Trace.WriteLine(transform.ToString());

            var side = Plane();
            for (int j = 0; j < side.Count; j++)
            {
                var vertex = side[j];
                vertex.Position = Vector3D.Transform(side[j].Position, transform);
                side[j] = vertex;
            }
            vertices.AddRange(side);
        }
        return vertices;
    }
}