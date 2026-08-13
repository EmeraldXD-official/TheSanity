sampler RenderImage : register(s0);

float uTime;
float4 uColor;

float4 TvHeadTrailPass(float2 uCoords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(RenderImage, uCoords);
    float scanline = sin(uCoords.y * 100.0 + uTime * 10.0) * 0.15;
    color.rgb += scanline;
    return color * uColor;
}

technique Technique1
{
    pass TvHeadTrailPass
    {
        PixelShader = compile ps_2_0 TvHeadTrailPass();
    }
}