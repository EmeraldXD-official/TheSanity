sampler uImage0 : register(s0);

float uTime;
float4 uColor;
float4 uSecondaryColor;
float uPulseSpeed;
float uRimPower;   // controls how tight/sharp the rim glow is (higher = thinner edge)
float uIntensity;  // overall glow multiplier, bump this up for ultimates/enrage states

// Texel offset used for the cheap edge/rim detection below.
// ps_2_0-safe: no ddx/ddy, just manual neighbor sampling.
static const float2 texel = float2(0.0025, 0.0025);

float4 BossGlowPS(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(uImage0, coords);

    // Skip fully transparent pixels
    if (color.a <= 0.0)
        return color;

    // --- Rim / Fresnel-style edge glow -------------------------------------------------
    // Sample alpha in 4 directions; pixels near a silhouette edge have neighbors with
    // lower alpha, which we turn into a rim mask without needing normals or ddx/ddy.
    float aUp    = tex2D(uImage0, coords + float2(0, -texel.y)).a;
    float aDown  = tex2D(uImage0, coords + float2(0,  texel.y)).a;
    float aLeft  = tex2D(uImage0, coords + float2(-texel.x, 0)).a;
    float aRight = tex2D(uImage0, coords + float2( texel.x, 0)).a;
    float edge = saturate((4.0 - (aUp + aDown + aLeft + aRight)) * 0.35);
    float rim = pow(edge, max(uRimPower, 0.001));

    // --- Dual-tone gradient -------------------------------------------------------------
    // Flows the primary -> secondary aura color vertically over time, so the glow feels
    // alive instead of a flat single-color wash.
    float gradientT = saturate(coords.y + sin(uTime * (uPulseSpeed * 0.15) + coords.x * 4.0) * 0.15);
    float3 baseGlowColor = lerp(uColor.rgb, uSecondaryColor.rgb, gradientT);

    // --- Pulsing energy scanlines ---------------------------------------------------------
    float wave = sin(uTime * uPulseSpeed + coords.y * 10.0) * 0.35 + 0.65;
    float scanline = sin(coords.y * 60.0 - uTime * uPulseSpeed * 2.0) * 0.5 + 0.5;
    float energy = wave * (0.85 + scanline * 0.15);

    // --- Compose ---------------------------------------------------------------------------
    float3 core = lerp(color.rgb, baseGlowColor, 0.7) * energy;
    float3 rimGlow = uSecondaryColor.rgb * rim * (1.5 + wave);

    float3 finalColor = (core + rimGlow) * uIntensity;

    return float4(finalColor, color.a) * sampleColor;
}

technique Technique1
{
    pass BossGlowPass
    {
        PixelShader = compile ps_3_0 BossGlowPS();
    }
}
