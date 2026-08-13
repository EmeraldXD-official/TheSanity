sampler uImage0 : register(s0);

float3 uColor;
float uOpacity;
float uTime;
float2 uScreenResolution;

// Efek: vignette (gelap di tepi) + chromatic aberration (pecah warna RGB tipis
// di tepi layar) + color grading (menyatukan warna layar ke uColor) + pulse halus.
float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float2 centered = coords - 0.5;
    float dist = length(centered);
    float2 dir = centered / max(dist, 0.0001);

    // Chromatic aberration: sampling kanal R & B digeser sedikit ke arah tepi.
    // Makin jauh dari tengah, makin kuat efeknya -> khas tampilan "shader" boss fight.
    float aberration = 0.004 * saturate(dist * 1.4) * uOpacity;
    float r = tex2D(uImage0, coords - dir * aberration).r;
    float g = tex2D(uImage0, coords).g;
    float b = tex2D(uImage0, coords + dir * aberration).b;
    float4 color = float4(r, g, b, 1);

    // Vignette: menggelapkan tepi layar supaya mata fokus ke tengah arena.
    float vignette = smoothstep(0.9, 0.25, dist);
    color.rgb *= lerp(0.45, 1.0, vignette) * uOpacity + (1 - uOpacity);

    // Color grading: menyatukan warna layar sedikit ke arah uColor (tint fase boss).
    color.rgb = lerp(color.rgb, color.rgb * uColor, 0.28 * uOpacity);

    // Pulse halus mengikuti waktu, terasa di tepi layar seperti energi berdenyut.
    float pulse = 0.5 + 0.5 * sin(uTime * 0.6);
    color.rgb += uColor * pulse * 0.03 * (1 - vignette) * uOpacity;

    return color;
}

technique Technique1
{
    pass CosmicVignettePass
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
