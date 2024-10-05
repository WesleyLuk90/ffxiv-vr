struct VertexShaderOutput
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
};

struct VertexShaderInput
{
    float4 position : POSITION;
    float4 color : COLOR;
};

VertexShaderOutput main(VertexShaderInput input)
{
    VertexShaderOutput output;

    output.position = input.position;
    output.color = input.color;

    return output;
}
