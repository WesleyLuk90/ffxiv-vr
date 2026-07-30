using FFXIVClientStructs.FFXIV.Shader;
using FfxivVR;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using System.Collections.Generic;

namespace FfxivVr;

public unsafe class ShaderConstantsManager(
    Logger logger,
    RunOnce runOnce
)
{

    ID3D11Resource* lastResource = null;
    MappedSubresource* lastMappedResource = null;
    internal void Map(OriginalFunctions originalFunctions, ID3D11DeviceContext* context, ID3D11Resource* pResource, uint subresource, Silk.NET.Direct3D11.Map mapType, uint mapFlags, MappedSubresource* pMappedResource)
    {
        if (bufferDescriptions.ContainsKey((nint)pResource))
        {
            lastResource = pResource;
            lastMappedResource = pMappedResource;
        }
    }
    internal void Unmap(OriginalFunctions originalFunctions, ID3D11DeviceContext* context, ID3D11Resource* pResource, uint subresource)
    {
        if (lastResource != null && pResource != null)
        {
            if (lastResource != pResource)
            {
                logger.Debug($"Invalid map/unmap detected 0x{(nint)lastResource:X}!=0x{(nint)pResource:X}");
            }
            else
            {
                runOnce.Run("unmap", $"{(nint)pResource}", () =>
                {
                    var desc = bufferDescriptions[(nint)pResource];
                    Vector4D<float>* pointer = (Vector4D<float>*)lastMappedResource->PData;
                    var count = desc.ByteWidth / sizeof(Vector4D<float>);
                    logger.Debug($"Size: {desc.ByteWidth} 0x{(nint)pResource:X}");
                    for (var i = 0; i < count; i++)
                    {
                        logger.Debug($"{i}: {pointer[i]}");
                    }
                });
            }
        }
        lastResource = null;
        lastMappedResource = null;
    }
    private Dictionary<nint, BufferDesc> bufferDescriptions = new();
    internal void VSSetConstantBuffersDetour(OriginalFunctions originalFunctions, ID3D11DeviceContext* context, uint startSlot, uint numBuffers, ID3D11Buffer** ppConstantBuffers)
    {
        logger.Trace($"Scanning buffers {numBuffers}");
        for (var i = 0; i < numBuffers; i++)
        {
            ID3D11Buffer* buffer = ppConstantBuffers[i];
            if (buffer == null)
            {
                continue;
            }
            if (!bufferDescriptions.ContainsKey((nint)buffer))
            {
                BufferDesc desc = new BufferDesc();
                buffer->GetDesc(&desc);
                if (desc.ByteWidth > sizeof(CameraParameter) && desc.ByteWidth < 4096)
                {
                    bufferDescriptions.Add((nint)buffer, desc);
                    logger.Debug($"Buffer Index:{startSlot + i} 0x{(nint)buffer:X} ByteWidth:{desc.ByteWidth}");
                }
            }
        }
    }
}