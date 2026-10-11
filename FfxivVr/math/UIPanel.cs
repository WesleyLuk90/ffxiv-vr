using Silk.NET.Maths;
using System;

namespace FfxivVR;

public class UIPanel(Matrix4X4<float> modelMatrix, float curvature, float aspectRatio)
{
    public Matrix4X4<float> ModelMatrix { get; } = modelMatrix;
    public float Curvature { get; } = curvature;
    public float AspectRatio { get; } = aspectRatio;

    private Matrix4X4<float> WorldToModel
    {
        get
        {
            Matrix4X4.Invert(ModelMatrix, out var worldToModel);
            return worldToModel;
        }
    }

    public Line? Intersect(Ray ray)
    {
        var worldToModel = WorldToModel;
        var origin = Vector3D.Transform(ray.Origin, worldToModel);
        var direction = Vector3D.Transform(ray.Origin + ray.Direction, worldToModel) - origin;
        float? fallback = null;
        foreach (var t in IntersectModelSurface(origin, direction))
        {
            if (t <= 0)
            {
                continue;
            }
            if (IsInsideViewport(ModelToViewport(origin + direction * t)))
            {
                return ray.ToLine(t);
            }
            fallback ??= t;
        }
        return fallback is { } distance ? ray.ToLine(distance) : null;
    }

    private float[] IntersectModelSurface(Vector3D<float> origin, Vector3D<float> direction)
    {
        if (Curvature <= 0)
        {
            if (MathF.Abs(direction.Z) < 1e-6f)
            {
                return [];
            }
            return [-origin.Z / direction.Z];
        }
        var radius = 1 / Curvature;
        var offsetZ = origin.Z - radius;
        var a = direction.X * direction.X + direction.Z * direction.Z;
        var b = 2 * (origin.X * direction.X + offsetZ * direction.Z);
        var c = origin.X * origin.X + offsetZ * offsetZ - radius * radius;
        var discriminant = b * b - 4 * a * c;
        if (a < 1e-12f || discriminant < 0)
        {
            return [];
        }
        var root = MathF.Sqrt(discriminant);
        return [(-b - root) / (2 * a), (-b + root) / (2 * a)];
    }

    private Vector2D<float> ModelToViewport(Vector3D<float> modelPosition)
    {
        var x = modelPosition.X;
        if (Curvature > 0)
        {
            var radius = 1 / Curvature;
            x = radius * MathF.Atan2(modelPosition.X, radius - modelPosition.Z);
        }
        var y = modelPosition.Y / AspectRatio;
        return new Vector2D<float>((x + 1) / 2, (1 - y) / 2);
    }

    private static bool IsInsideViewport(Vector2D<float> v)
    {
        return v.X >= 0 && v.X <= 1 && v.Y >= 0 && v.Y <= 1;
    }

    public Vector3D<float> ViewportToWorld(Vector2D<float> viewportPosition)
    {
        var x = viewportPosition.X * 2 - 1;
        var y = (1 - viewportPosition.Y * 2) * AspectRatio;
        var z = 0f;
        if (Curvature > 0)
        {
            var radius = 1 / Curvature;
            var angle = x / radius;
            x = radius * MathF.Sin(angle);
            z = radius * (1 - MathF.Cos(angle));
        }
        return Vector3D.Transform(new Vector3D<float>(x, y, z), ModelMatrix);
    }

    public Vector2D<float>? WorldToViewport(Vector3D<float> worldPosition)
    {
        var v = ModelToViewport(Vector3D.Transform(worldPosition, WorldToModel));
        if (!IsInsideViewport(v))
        {
            return null;
        }
        return v;
    }
}