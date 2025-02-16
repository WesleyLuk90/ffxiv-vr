using Silk.NET.OpenXR;

namespace FfxivVR;

public struct Vertex
{
    public Vector3f Position;
    public Vector2f UV;
    public Vertex(Vector3f position, Vector2f uv)
    {
        Position = position;
        UV = uv;
    }
}