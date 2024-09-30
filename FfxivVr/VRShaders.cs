using Silk.NET.Direct3D11;
using System;
using System.IO;

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
        var compiledVertexShader = LoadVertexShader();
        ID3D11VertexShader* vertexShader = null;
        fixed (byte* p = compiledVertexShader)
        {
            device->CreateVertexShader(p, (nuint)compiledVertexShader.Length, null, ref vertexShader)
                .D3D11Check("CreateVertexShader");
        }
        this.vertexShader = vertexShader;

        byte[] compiledPixelShader = LoadPixelShader();
        ID3D11PixelShader* pixelShader = null;
        fixed (byte* p = compiledPixelShader)
        {
            device->CreatePixelShader(p, (nuint)compiledPixelShader.Length, null, ref pixelShader)
                .D3D11Check("CreatePixelShader");
        }
        this.pixelShader = pixelShader;

        SubresourceData data = new SubresourceData(
            pSysMem: null, //Fixme
            sysMemPitch: 10,
            sysMemSlicePitch: 0
        );
        BufferDesc desc = new BufferDesc(
            byteWidth: 10,
            usage: Usage.Dynamic,
            bindFlags: 10,
            cPUAccessFlags: (uint)CpuAccessFlag.Write,
            miscFlags: 0,
            structureByteStride: 0
        );
        ID3D11Buffer* buffer = null;
        device->CreateBuffer(ref desc, ref data, ref buffer).D3D11Check("CreateBuffer");
    }

    public void Dispose()
    {
        vertexShader->Release();
        pixelShader->Release();
    }
}
