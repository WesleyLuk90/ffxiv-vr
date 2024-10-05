struct VertexShaderOutput
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
};

float4 main(VertexShaderOutput vertexShaderOutput) : SV_TARGET
{
    return vertexShaderOutput.color;
}