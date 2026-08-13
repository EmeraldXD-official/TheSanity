sampler RenderImage : register(s0);

float uTime;
float uOpacity;

float4 TvHeadCRTPass(float2 uCoords : TEXCOORD0) : COLOR0
{
    float2 uv = uCoords;
    float shift = sin(uv.y * 80.0 + uTime * 20.0) * 0.003 * uOpacity;
    float r = tex2D(RenderImage, float2(uv.x + shift, uv.y)).r;
    float g = tex2D(RenderImage, uv).g;
    float b = tex2D(RenderImage, float2(uv.x - shift, uv.y)).b;
    float scanline = sin(uv.y * 600.0) * 0.04 * uOpacity;
    
    return float4(r - scanline, g - scanline, b - scanline, 1.0);
}

technique Technique1
{
    pass TvHeadCRTPass
    {
        PixelShader = compile ps_2_0 TvHeadCRTPass();
    }
}