using Silk.NET.Direct3D11;

namespace FfxivVR;

public unsafe class DxDeviceContext(
    ID3D11DeviceContext* context
)
{
    public ID3D11DeviceContext* Context { get; } = context;
}