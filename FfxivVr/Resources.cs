using FFXIVClientStructs.FFXIV.Common.Math;
using Silk.NET.Direct3D11;
using Silk.NET.OpenXR;
using System;
using System.Runtime.InteropServices;

namespace FfxivVR;

unsafe public class Resources : IDisposable
{
    struct CameraConstants
    {
        Matrix4x4 viewProjm, modelViewProj, model;
        Vector4f color, pad1, pad2, pad3;
    }

    public static Vector4f[] Normals = new Vector4f[]
    {
        new Vector4f(1,0,0,0),
        new Vector4f(-1,0,0,0),
        new Vector4f(0,1,0,0),
        new Vector4f(0,-1,0,0),
        new Vector4f(0,0,1,0),
        new Vector4f(0,0,-1,0),
    };

    public static Vector4f[] VertexPositions = new Vector4f[]{
        new Vector4f(+0.5f, +0.5f, +0.5f, 1),
        new Vector4f(+0.5f, +0.5f, -0.5f, 1),
        new Vector4f(+0.5f, -0.5f, +0.5f, 1),
        new Vector4f(+0.5f, -0.5f, -0.5f, 1),
        new Vector4f(-0.5f, +0.5f, +0.5f, 1),
        new Vector4f(-0.5f, +0.5f, -0.5f, 1),
        new Vector4f(-0.5f, -0.5f, +0.5f, 1),
        new Vector4f(-0.5f, -0.5f, -0.5f, 1),
    };

    public static Vector4f[] CubeVertices = new Vector4f[]
    {
        VertexPositions[2], VertexPositions[1], VertexPositions[0], VertexPositions[2], VertexPositions[3], VertexPositions[1],
        VertexPositions[6], VertexPositions[4], VertexPositions[5], VertexPositions[6], VertexPositions[5], VertexPositions[7],
        VertexPositions[0], VertexPositions[1], VertexPositions[5], VertexPositions[0], VertexPositions[5], VertexPositions[4],
        VertexPositions[2], VertexPositions[6], VertexPositions[7], VertexPositions[2], VertexPositions[7], VertexPositions[3],
        VertexPositions[0], VertexPositions[4], VertexPositions[6], VertexPositions[0], VertexPositions[6], VertexPositions[2],
        VertexPositions[1], VertexPositions[3], VertexPositions[7], VertexPositions[1], VertexPositions[7], VertexPositions[5],
    };

    public static int[] CubeIndices = new int[]
    {
        0,1,2,3,4,5,
        6,7,8,9,10,11,
        12,13,14,15,16,17,
        18,19,20,21,22,23,
        24,25,26,27,28,29,
        30,31,32,33,34,35
    };
    private D3DBuffer vertexBuffer;
    private D3DBuffer indexBuffer;
    private D3DBuffer uniformBuffer;
    private D3DBuffer normalBuffer;
    private readonly ID3D11Device* device;
    private readonly ID3D11DeviceContext* context;

    public Resources(ID3D11Device* device, ID3D11DeviceContext* context)
    {
        this.device = device;
        this.context = context;
    }

    public void Initialize()
    {
        this.vertexBuffer = CreateBuffer(MemoryMarshal.AsBytes(new Span<Vector4f>(CubeVertices)), BindFlag.VertexBuffer);
        this.indexBuffer = CreateBuffer(MemoryMarshal.AsBytes(new Span<int>(CubeIndices)), BindFlag.IndexBuffer);
        this.uniformBuffer = CreateBuffer(new Span<byte>(new byte[sizeof(CameraConstants)]), BindFlag.ConstantBuffer);
        this.normalBuffer = CreateBuffer(MemoryMarshal.AsBytes(new Span<Vector4f>(Normals)), BindFlag.ConstantBuffer);
    }

    class D3DBuffer : IDisposable
    {
        ID3D11Buffer* buffer;
        uint length;

        internal D3DBuffer(ID3D11Buffer* buffer, uint length)
        {
            this.buffer = buffer;
            this.length = length;
        }

        public void Dispose()
        {
            buffer->Release();
        }
    }

    private D3DBuffer CreateBuffer(Span<byte> bytes, BindFlag bindFlag)
    {
        fixed (byte* p = bytes)
        {
            if (p == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            SubresourceData subresourceData = new SubresourceData(
                pSysMem: p,
                sysMemPitch: 0,
                sysMemSlicePitch: 0
            );
            BufferDesc description = new BufferDesc(
                byteWidth: (uint)bytes.Length,
                usage: Usage.Dynamic,
                bindFlags: (uint)bindFlag,
                cPUAccessFlags: (uint)CpuAccessFlag.Write,
                miscFlags: 0,
                structureByteStride: 0
            );
            ID3D11Buffer* buffer = null;
            device->CreateBuffer(ref description, ref subresourceData, ref buffer).D3D11Check("CreateBuffer");
            return new D3DBuffer(buffer, (uint)bytes.Length);
        }
    }

    private void SetBufferData(ID3D11Buffer* buffer, uint byteLength, void* pointer)
    {
        if (buffer == null)
        {
            return;
        }
        MappedSubresource mappedSubresource = new MappedSubresource();
        context->Map((ID3D11Resource*)buffer, 0, Map.WriteDiscard, 0, ref mappedSubresource).D3D11Check("Map");
        //Buffer.MemoryCopy()
    }

    public void Dispose()
    {
        this.vertexBuffer.Dispose();
        this.indexBuffer.Dispose();
        this.uniformBuffer.Dispose();
        this.normalBuffer.Dispose();
    }
}
