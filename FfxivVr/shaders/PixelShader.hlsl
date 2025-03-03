struct VertexShaderOutput
{
    float4 position : SV_POSITION;
    float2 texcoord : TEXCOORD;
};

cbuffer PixelShaderConstants
{
    int mode;
    float gamma, padding1, padding2;
    float4 constantColor;
    float2 uvScale;
    float2 uvOffset;
};

Texture2D tex;
SamplerState tex_sampler;

float4 main(VertexShaderOutput vertexShaderOutput) : SV_TARGET
{
    float4 color;
    float2 texCoord = vertexShaderOutput.texcoord * uvScale + uvOffset;
    if (mode == 0)
    {
        color = tex.Sample(tex_sampler, texCoord) * constantColor;
    }
    else if (mode == 1) // Draw circle
    {
        float d2 = length(texCoord - float2(0.5, 0.5));
        if (d2 < 0.5)
        {
            color = constantColor;
        }
        else
        {
            color = float4(0, 0, 0, 0);
        }
    }
    else if (mode == 2) // Invert alpha mode, dalamud renders its UI with weird inverted alpha values
    {
        color = tex.Sample(tex_sampler, texCoord);
        color.a = 1 - color.a;
    }
    else if (mode == 3) // Fill mode
    {
        color = constantColor;
    }
    
    else
    {
        color = float4(1, 0, 0, 1);
    }
    
    // Apply gamma, not sure why but this does fix it
    return float4(pow(abs(color.r), gamma), pow(abs(color.g), gamma), pow(abs(color.b), gamma), color.a);
}