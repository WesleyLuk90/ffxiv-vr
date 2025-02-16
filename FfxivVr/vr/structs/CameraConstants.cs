using Silk.NET.Maths;

namespace FfxivVR;

public struct CameraConstants
{
    public Matrix4X4<float> modelViewProjection;

    public CameraConstants(Matrix4X4<float> modelViewProjection)
    {
        this.modelViewProjection = modelViewProjection;
    }
}