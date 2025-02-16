using Silk.NET.Maths;

namespace FfxivVR;

public struct CameraConstants(Matrix4X4<float> modelViewProjection, float curvature)
{
    public Matrix4X4<float> ModelViewProjection = modelViewProjection;

    public float Curvature = curvature;
    public float padding1, padding2, padding3 = 0;
}