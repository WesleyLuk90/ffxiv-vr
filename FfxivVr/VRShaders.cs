using Silk.NET.Direct3D11;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace FfxivVR;

unsafe public class VRShaders
{
    private readonly ID3D11Device* device;
    private readonly Logger logger;
    private ID3D11PixelShader* pixelShader;
    private ID3D11VertexShader* vertexShader;

    public static byte[] LoadVertexShader()
    {
        using (var stream = typeof(VRShaders).Assembly.GetManifestResourceStream("FfxivVR.VertexShader.cso"))
        {
            if (stream == null)
            {
                throw new Exception("Failed to find vertex shader");
            }
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }
    public static byte[] LoadPixelShader()
    {
        using (var stream = typeof(VRShaders).Assembly.GetManifestResourceStream("FfxivVR.PixelShader.cso"))
        {
            if (stream == null)
            {
                throw new Exception("Failed to find pixel shader");
            }
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }

    public VRShaders(ID3D11Device* device, Logger logger)
    {
        this.device = device;
        this.logger = logger;
    }

    public void Initialize()
    {
        byte[] compiledVertexShader = LoadVertexShader();
        ID3D11VertexShader* vertexShader = null;
        device->CreateVertexShader(Unsafe.AsPointer(ref compiledVertexShader), (nuint)compiledVertexShader.Length, null, ref vertexShader);
        byte[] compiledPixelShader = LoadPixelShader();
        this.vertexShader = vertexShader;
        ID3D11PixelShader* pixelShader = null;
        device->CreatePixelShader(Unsafe.AsPointer(ref compiledPixelShader), (nuint)compiledVertexShader.Length, null, ref pixelShader);
        this.pixelShader = pixelShader;
    }

    public void Dispose()
    {
        vertexShader->Release();
        pixelShader->Release();
    }
}
