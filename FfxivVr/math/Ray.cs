using Silk.NET.Maths;
namespace FfxivVR;

public class Ray(Vector3D<float> Origin, Vector3D<float> Direction)
{
    public Vector3D<float> Origin { get; } = Origin;
    public Vector3D<float> Direction { get; } = Direction;

    internal Line ToLine(float distance)
    {
        return new Line(Origin, Origin + Direction * distance);
    }

    public static Ray Through(Vector3D<float> origin, Vector3D<float> through)
    {
        return new Ray(origin, Vector3D.Normalize(through - origin));
    }

    public Ray Transform(Matrix4X4<float> matrix)
    {
        var origin = Vector3D.Transform(Origin, matrix);
        return Through(origin, Vector3D.Transform(Origin + Direction, matrix));
    }
}