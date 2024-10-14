cbuffer Camera
{
    float4x4 modelViewProjection;
};
struct VertexShaderOutput
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD;
};

struct VertexShaderInput
{
    float4 position : POSITION;
    float2 texcoord : TEXCOORD;
};

VertexShaderOutput main(VertexShaderInput input)
{
    VertexShaderOutput output;

    output.position = mul(modelViewProjection, input.position);
    output.texcoord = float4(input.texcoord, 0, 1);

    return output;
}
