struct VertexShaderOutput
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD;
};

cbuffer PixelShaderConstants
{
    int mode;
    float gamma, b, c;
    float4 constantColor;
};

Texture2D tex;
SamplerState tex_sampler;

float4 main(VertexShaderOutput vertexShaderOutput) : SV_TARGET
{
    float4 color;
    if (mode == 0)
    {
        color = tex.Sample(tex_sampler, vertexShaderOutput.texcoord);
    }
    else if (mode == 1) // Draw circle
    {
        float d2 = length(vertexShaderOutput.texcoord - float2(0.5, 0.5));
        if (d2 < 0.5)
        {
            color = constantColor;
        }
        else
        {
            color = float4(0, 0, 0, 0);
        }
    }
    else
    {
        color = float4(1, 0, 0, 1);
    }
        
    // Apply gamma, not sure why but this does fix it
    return float4(pow(abs(color.r), gamma), pow(abs(color.g), gamma), pow(abs(color.b), gamma), color.a);
}