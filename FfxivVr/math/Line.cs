using Silk.NET.Maths;
namespace FfxivVR;
public class Line(Vector3D<float> Start, Vector3D<float> End)
{
    public Vector3D<float> Start { get; } = Start;
    public Vector3D<float> End { get; } = End;
}