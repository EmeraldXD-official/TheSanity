// ==========================================
// TwinsBeamEnergy.fx — shader "energi mengalir" buat beam-beam Twins (CursedBeam/RetLaser,
// RetBeam, dan beam Last Stand) biar gak keliatan garis solid polos lagi. Efeknya 3 lapis,
// digabung di 1 pixel shader:
//   1. FLOW  — noise prosedural (hash/value-noise, GAK BUTUH texture noise eksternal, biar
//      gak ada resiko "missing asset") yang di-scroll sepanjang UV.x pakai uTime, kesannya
//      kayak energi/plasma yang MENGALIR di sepanjang badan beam, bukan diem statis.
//   2. EDGE GLOW — tepi beam (UV.y deket 0 atau 1) dibikin lebih terang dari intinya
//      (fresnel-style sederhana), jadi keliatan ada "selubung" cahaya di pinggir badan
//      beam, bukan cuma warna rata.
//   3. PULSE — seluruh intensitas energi ikut "berdenyut" halus (sin(uTime)), biar beam-nya
//      kerasa hidup walau lagi diem di 1 tempat (state Aiming/FullBeam yang gak gerak).
//
// PARAMETER (di-set dari C#, lihat TwinsShaderSystem.cs):
//   uTime     - waktu berjalan (detik), dipakai buat scroll flow & pulse
//   uColor    - warna energi (RGB, dicampur ADDITIVE ke atas sprite asli)
//   uOpacity  - kontrol keseluruhan seberapa kuat efeknya nempel (0..1)
//
// UV ASSUMSI: dipakai buat sprite beam yang MEMANJANG SEPANJANG SUMBU-X (kayak
// RedBeamMiddle.png yang di-stretch scale.X) — UV.x = sepanjang badan beam, UV.y = tebal
// beam (0 di satu tepi, 1 di tepi lainnya). Kalau nanti dipakai di sprite lain yang
// orientasinya beda, tinggal tukar aja X<->Y di baris "flow"/"edge" di bawah.
// ==========================================

sampler uImage0 : register(s0);

float uTime;
float4 uColor;
float uOpacity;

float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);

    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));

    float2 u = f * f * (3.0 - 2.0 * f); // smoothstep buat interpolasi halus

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 sampleColor : COLOR0) : COLOR0
{
    float4 baseColor = tex2D(uImage0, coords);

    // --- 1. FLOW: noise di-scroll sepanjang UV.x (arah badan beam) pakai uTime ---
    float2 flowUv = float2(coords.x * 14.0 - uTime * 3.2, coords.y * 5.0);
    float flowSample = valueNoise(flowUv);

    // Layer noise kedua, lebih rapat & lebih cepat, biar teksturnya gak keliatan "1 lapis"
    // doang - dicampur biar hasilnya lebih berisik/organik (kayak percikan energi kecil).
    float2 flowUv2 = float2(coords.x * 30.0 - uTime * 6.0, coords.y * 9.0 + 3.7);
    float flowSample2 = valueNoise(flowUv2);

    float flow = saturate(flowSample * 0.65 + flowSample2 * 0.35);
    flow = pow(flow, 1.6); // kontras dikit biar gak "kabut rata", ada spot yang lebih terang

    // --- 2. EDGE GLOW: makin deket ke tepi beam (UV.y -> 0 atau 1) makin terang ---
    float edgeDist = abs(coords.y - 0.5) * 2.0; // 0 di tengah, 1 di tepi
    float edgeGlow = pow(saturate(edgeDist), 2.2);

    // --- 3. PULSE: seluruh energi ikut berdenyut halus, gak statis ---
    float pulse = 0.82 + 0.18 * sin(uTime * 5.5);

    float energy = saturate(flow * 0.55 + edgeGlow * 0.75) * pulse;

    // Campur ADDITIVE: warna dasar sprite tetap ada, energi nambahin cahaya ekstra di
    // atasnya (bukan gantiin warna sepenuhnya) - dikali baseColor.a biar gak "bocor" keluar
    // siluet sprite (area transparan tetap transparan).
    float4 finalColor = baseColor * sampleColor;
    finalColor.rgb += uColor.rgb * energy * baseColor.a * uOpacity;
    finalColor.a = baseColor.a * sampleColor.a;

    return finalColor;
}

technique Technique1
{
    pass BeamEnergy
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
