using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System;
using System.IO;

namespace FfxivVR;

public unsafe class VRShaders
{
    private readonly DxDevice device;
    private readonly Logger logger;
    private ID3D11PixelShader* pixelShader;
    private ID3D11VertexShader* vertexShader;
    private byte[] vertexShaderBinary;
    private byte[] pixelShaderBinary;
    private ID3D11InputLayout* inputLayout;

    public static byte[] LoadVertexShader()
    {
        using (var stream = typeof(VRShaders).Assembly.GetManifestResourceStream("FfxivVR.shaders.VertexShader.cso"))
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
        using (var stream = typeof(VRShaders).Assembly.GetManifestResourceStream("FfxivVR.shaders.PixelShader.cso"))
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

    public VRShaders(DxDevice device, Logger logger)
    {
        vertexShaderBinary = LoadVertexShader();
        pixelShaderBinary = LoadPixelShader();
        this.device = device;
        this.logger = logger;
    }

    public void Initialize()
    {
        ID3D11VertexShader* vertexShader = null;
        fixed (byte* p = vertexShaderBinary)
        {
            device.Device->CreateVertexShader(p, (nuint)vertexShaderBinary.Length, null, ref vertexShader)
                .D3D11Check("CreateVertexShader");
        }
        this.vertexShader = vertexShader;

        ID3D11PixelShader* pixelShader = null;
        fixed (byte* p = pixelShaderBinary)
        {
            device.Device->CreatePixelShader(p, (nuint)pixelShaderBinary.Length, null, ref pixelShader)
                .D3D11Check("CreatePixelShader");
        }
        this.pixelShader = pixelShader;

        Native.WithAnsiStringPointer("POSITION", (positionStr) =>
        {
            Native.WithAnsiStringPointer("TEXCOORD", (colorStr) =>
            {
                InputElementDesc[] inputElementDesc = [
                    new InputElementDesc(
                        semanticName: (byte*)positionStr,
                        semanticIndex: 0,
                        format: Silk.NET.DXGI.Format.FormatR32G32B32Float,
                        inputSlot: 0,
                        alignedByteOffset: 0,
                        inputSlotClass: InputClassification.PerVertexData,
                        instanceDataStepRate: 0
                    ),
                    new InputElementDesc(
                        semanticName: (byte*)colorStr,
                        semanticIndex: 0,
                        format: Silk.NET.DXGI.Format.FormatR32G32Float,
                        inputSlot: 0,
                        alignedByteOffset: (uint)sizeof(Vector3f),
                        inputSlotClass: InputClassification.PerVertexData,
                        instanceDataStepRate: 0
                    )
                ];
                var inputElementDescSpan = new Span<InputElementDesc>(inputElementDesc);
                fixed (InputElementDesc* pInputElement = inputElementDescSpan)
                {
                    var vertexShaderSpan = new Span<byte>(vertexShaderBinary);
                    fixed (byte* pVertexShader = vertexShaderSpan)
                    {
                        ID3D11InputLayout* inputLayout = null;
                        device.Device->CreateInputLayout(pInputElement, (uint)inputElementDescSpan.Length, pVertexShader, (nuint)vertexShaderSpan.Length, &inputLayout).D3D11Check("CreateInputLayout");
                        this.inputLayout = inputLayout;

                    }
                }
            });
        });
    }

    public void SetShaders(ID3D11DeviceContext* context)
    {
        context->IASetInputLayout(inputLayout);
        context->VSSetShader(vertexShader, null, 0);
        context->PSSetShader(pixelShader, null, 0);
    }

    public void Dispose()
    {
        if (vertexShader != null)
        {
            vertexShader->Release();
        }
        if (pixelShader != null)
        {
            pixelShader->Release();
        }
        if (inputLayout != null)
        {
            inputLayout->Release();
        }
    }
}