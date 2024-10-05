using Silk.NET.Direct3D11;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FfxivVR;

unsafe public class VRShaders
{
    private readonly ID3D11Device* device;
    private readonly Logger logger;
    private readonly ID3D11DeviceContext* context;
    private ID3D11PixelShader* pixelShader;
    private ID3D11VertexShader* vertexShader;
    private byte[] vertexShaderBinary;
    private byte[] pixelShaderBinary;

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

    public VRShaders(ID3D11Device* device, Logger logger, ID3D11DeviceContext* context)
    {
        vertexShaderBinary = LoadVertexShader();
        pixelShaderBinary = LoadPixelShader();
        this.device = device;
        this.logger = logger;
        this.context = context;
    }

    public void Initialize()
    {
        ID3D11VertexShader* vertexShader = null;
        fixed (byte* p = vertexShaderBinary)
        {
            device->CreateVertexShader(p, (nuint)vertexShaderBinary.Length, null, ref vertexShader)
                .D3D11Check("CreateVertexShader");
        }
        this.vertexShader = vertexShader;

        ID3D11PixelShader* pixelShader = null;
        fixed (byte* p = pixelShaderBinary)
        {
            device->CreatePixelShader(p, (nuint)pixelShaderBinary.Length, null, ref pixelShader)
                .D3D11Check("CreatePixelShader");
        }
        this.pixelShader = pixelShader;

        context->VSSetShader(vertexShader, null, 0);
        context->PSSetShader(pixelShader, null, 0);
        var bstring = Marshal.StringToCoTaskMemAnsi("POSITION");
        Native.WithStringPointer("POSITION", (positionStr) =>
        {
            Native.WithStringPointer("COLOR", (colorStr) =>
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
                        semanticName: (byte*)positionStr,
                        semanticIndex: 0,
                        format: Silk.NET.DXGI.Format.FormatR32G32B32A32Float,
                        inputSlot: 0,
                        alignedByteOffset: 12,
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
                        device->CreateInputLayout(pInputElement, (uint)inputElementDescSpan.Length, pVertexShader, (nuint)vertexShaderSpan.Length, &inputLayout).D3D11Check("CreateInputLayout");
                        context->IASetInputLayout(inputLayout); ;
                    }
                }
            });
        });
    }

    public void SetShaders()
    {
        return;
        var bstring = Marshal.StringToCoTaskMemAnsi("TEXCOORD");
        var inputElementDesc = new InputElementDesc(
            semanticName: (byte*)bstring,
            semanticIndex: 0,
            format: Silk.NET.DXGI.Format.FormatR32G32B32A32Float,
            inputSlot: 0,
            alignedByteOffset: 0,
            inputSlotClass: InputClassification.PerVertexData,
            instanceDataStepRate: 0
        );
        ID3D11InputLayout* layout = null;
        fixed (byte* p = vertexShaderBinary)
        {
            device->CreateInputLayout(&inputElementDesc, 1, p, (uint)vertexShaderBinary.Length, &layout).D3D11Check("CreateInputLayout");
        }
        context->IASetInputLayout(layout);
        layout->Release();
        Marshal.FreeCoTaskMem(bstring);

        context->IASetPrimitiveTopology(Silk.NET.Core.Native.D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);

        var rasterizerDesc = new RasterizerDesc(
            fillMode: FillMode.Solid,
            cullMode: CullMode.Back,
            frontCounterClockwise: true,
            depthBias: null,
            depthBiasClamp: 0,
            slopeScaledDepthBias: 0,
            depthClipEnable: false,
            scissorEnable: true,
            multisampleEnable: false,
            antialiasedLineEnable: false
          );
        ID3D11RasterizerState* rasterizerState = null;
        device->CreateRasterizerState(ref rasterizerDesc, ref rasterizerState).D3D11Check("CreateRasterizerState");
        context->RSSetState(rasterizerState);
        rasterizerState->Release();

        var depthStencilDesc = new DepthStencilDesc(
            depthEnable: true,
            depthWriteMask: DepthWriteMask.All,
            depthFunc: ComparisonFunc.LessEqual,
            stencilEnable: false,
            stencilReadMask: 0xff,
            stencilWriteMask: 0xff
        );

        ID3D11DepthStencilState* depthStencilState = null;
        device->CreateDepthStencilState(&depthStencilDesc, &depthStencilState);
        context->OMSetDepthStencilState(depthStencilState, 0xFFFFFFFF);
        depthStencilState->Release();

        var blendDesc = new BlendDesc(
            alphaToCoverageEnable: true,
            independentBlendEnable: true
        );
        blendDesc.RenderTarget[0] = new RenderTargetBlendDesc(
            renderTargetWriteMask: (byte)ColorWriteEnable.All,
            blendEnable: true,
            srcBlend: Blend.SrcAlpha,
            destBlend: Blend.Src1Alpha,
            blendOp: BlendOp.Add,
            srcBlendAlpha: Blend.One,
            destBlendAlpha: Blend.Zero,
            blendOpAlpha: BlendOp.Add
        );
        ID3D11BlendState* blendState = null;
        device->CreateBlendState(ref blendDesc, &blendState).D3D11Check("CreateBlendState");
        var floats = new float[4] { 0, 0, 0, 0 };
        fixed (float* p = floats)
        {
            context->OMSetBlendState(blendState, p, 0xffffffff);
        }
        blendState->Release();
    }

    public void Dispose()
    {
        vertexShader->Release();
        pixelShader->Release();
    }
}
