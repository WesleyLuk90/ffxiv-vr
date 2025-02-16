cbuffer Camera
{
    float4x4 modelViewProjection;
    float curvature;
    float padding1, padding2, padding3;
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

    float4 position = input.position;
    if (curvature > 0) {
        float radius = 1 / curvature;
        float angle = position.x / radius;
        position.x = sin(angle);
        position.z = radius * (1 - cos(angle));
    }
    output.position = mul(modelViewProjection, position);
    output.texcoord = input.texcoord;

    return output;
}
