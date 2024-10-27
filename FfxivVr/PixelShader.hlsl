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
    // Apply gamma, not sure why but this does fix it
    return float4(pow(color.r, 2.2), pow(color.g, 2.2), pow(color.b, 2.2), color.a);
}