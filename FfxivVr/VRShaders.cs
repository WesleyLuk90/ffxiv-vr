using Silk.NET.Direct3D11;
using System;
using System.IO;
using System.Resources;

namespace FfxivVR;

unsafe public class VRShaders
{
    private readonly ID3D11Device* device;
    private readonly Logger logger;

    public static string LoadVertexShader()
    {
        using (var stream = typeof(VRShaders).Assembly.GetManifestResourceStream("FfxivVR.VertexShader.cso"))
        {
            if (stream == null)
            {
                throw new Exception("Failed to find vertex shader");
            }
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
    public static string LoadPixelShader()
    {
        using (var stream = typeof(VRShaders).Assembly.GetManifestResourceStream("FfxivVR.PixelShader.cso"))
        {
            if (stream == null)
            {
                throw new Exception("Failed to find pixel shader");
            }
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
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
        var rm = new ResourceManager("FfxivVR.VertexShader.cso", typeof(VRShaders).Assembly);
        logger.Info(rm.GetString("FfxivVR.VertexShader.cso")?.ToString());
        //device->CreateVertexShader()

    }
}
