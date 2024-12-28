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
}