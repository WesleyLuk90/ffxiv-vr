struct VertexShaderOutput
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD;
};

Texture2D tex;
SamplerState tex_sampler;

float4 main(VertexShaderOutput vertexShaderOutput) : SV_TARGET
{
    float4 color = tex.Sample(tex_sampler, vertexShaderOutput.texcoord);
    color.a = 1;
    return color;
}