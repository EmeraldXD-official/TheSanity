sampler uImage0 : register(s0); // Textur utama (Extra_197)
sampler uImage1 : register(s1); // Noise Texture

float uTime;
float4 uColor;
float4 uSecondaryColor;

float4 MainPS(float2 uv : TEXCOORD0) : COLOR0
{
    // 1. Dua layer noise dengan kecepatan & skala berbeda, hasilnya energi terasa lebih hidup / tidak monoton
    float2 noiseUV1 = uv;
    noiseUV1.x -= uTime * 2.0;
    noiseUV1.y += sin(uTime * 5.0 + uv.x * 10.0) * 0.1;

    float2 noiseUV2 = uv * 1.7;
    noiseUV2.x += uTime * 3.3;
    noiseUV2.y -= cos(uTime * 4.0 + uv.x * 6.0) * 0.08;

    float4 noiseA = tex2D(uImage1, noiseUV1);
    float4 noiseB = tex2D(uImage1, noiseUV2);
    float combinedNoise = saturate(noiseA.r * 0.65 + noiseB.r * 0.55);

    // 2. Distorsi tepi laser berdasarkan noise gabungan (lebih bertekstur dibanding 1 layer)
    float2 mainUV = uv;
    mainUV.y += (combinedNoise - 0.5) * 0.18;
    float4 baseTex = tex2D(uImage0, mainUV);

    // 3. Inti panas putih di tengah laser + gradasi warna primer/sekunder ke arah tepi
    float distFromCenter = abs(uv.y - 0.5);
    float hotCore = smoothstep(0.12, 0.0, distFromCenter);
    float coreFactor = smoothstep(0.32, 0.02, distFromCenter);

    float4 gradientColor = lerp(uSecondaryColor, uColor, coreFactor);
    float4 finalColor = lerp(gradientColor, float4(1.0, 1.0, 1.0, 1.0), hotCore * 0.85);

    // 4. Denyut intensitas ringan agar laser terasa berdenyut, tidak statis
    float pulse = 0.85 + 0.15 * sin(uTime * 18.0);

    // 5. Alpha memudar halus di ujung laser (pangkal/ujung X) & tepi atas-bawah (Y)
    float edgeFadeY = sin(saturate(uv.y) * 3.14159);
    float edgeFadeX = smoothstep(0.0, 0.06, uv.x) * smoothstep(1.0, 0.94, uv.x);
    float alphaFade = edgeFadeY * edgeFadeX * saturate(combinedNoise + 0.25) * pulse;

    return baseTex * finalColor * alphaFade;
}

technique Technique1
{
    pass MainPass
    {
        PixelShader = compile ps_3_0 MainPS();
    }
}