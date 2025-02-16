using Silk.NET.Maths;

namespace FfxivVR;

public unsafe partial class Resources
{
    public struct CameraConstants
    {
        Matrix4X4<float> modelViewProjection;

        public CameraConstants(Matrix4X4<float> modelViewProjection)
        {
            this.modelViewProjection = modelViewProjection;
        }
    }
}