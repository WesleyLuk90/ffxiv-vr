using Silk.NET.Direct3D11;

namespace FfxivVR;

public unsafe class DxDevice(
    ID3D11Device* device
)
{
    public ID3D11Device* Device { get; } = device;
}