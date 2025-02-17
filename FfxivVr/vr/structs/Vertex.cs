using Silk.NET.Maths;
namespace FfxivVR;

public struct Vertex
{
    public Vector3D<float> Position;
    public Vector2D<float> UV;
    public Vertex(Vector3D<float> position, Vector2D<float> uv)
    {
        Position = position;
        UV = uv;
    }
}