struct VertexShaderOutput
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD;
};

Texture2D tex;
SamplerState tex_sampler;

float4 main(VertexShaderOutput vertexShaderOutput) : SV_TARGET
{
    return tex.Sample(tex_sampler, vertexShaderOutput.texcoord);
}