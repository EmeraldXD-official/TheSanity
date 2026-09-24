// ================================================================================================
// WhoAmIPatternMarkerNoise.fx
// ================================================================================================
// Shader per-pixel buat lapisan noise TAMBAHAN di atas silang "+" pattern marker WhoAmI.
// Beda sama noise sprite-based yang udah ada di WhoAmI_VFX_PatternMarker.cs (yang numpang
// auraNoiseTexture, digambar berlapis pakai SpriteBatch biasa) - shader ini bikin turbulence-nya
// LANGSUNG dari formula matematika (value-noise/FBM) per-pixel, real-time, TANPA butuh texture
// noise sama sekali. Dipasang sebagai lapisan EKSTRA (draw pass ke-2, additive) di atas semua
// yang udah ada - jadi kalau di-nonaktifin/gagal load, silangnya tetap tampil normal kayak
// sebelumnya (lihat null-check di sisi C#).
//
// CARA KERJA SINGKAT:
//   1. Sample alpha dari sprite glow yang sama (uImage0 - di-set otomatis sama SpriteBatch dari
//      texture yang dipassing ke Draw()), jadi shader ini "napel" ke bentuk lengan yang sama
//      persis, bukan gambar bentuk baru.
//   2. Bikin 2 layer FBM (fractal noise) yang scroll ke arah BERLAWANAN & kecepatan beda -> hasil
//      gabungannya = turbulence yang kerasa "energik"/gak beraturan, bukan noise statis.
//   3. Turbulence itu dipakai buat modulasi ALPHA (bagian noise-nya rendah jadi transparan -
//      efek "robek/crackle energi" di tepi lengan) SEKALIGUS modulasi BRIGHTNESS (bagian
//      turbulence tinggi jadi lebih terang, kayak percikan).
//   4. Warnanya di-gradasi CORE (dekat pusat lengan) -> MID -> OUTER (ujung lengan) pakai 3
//      parameter warna yang di-set dari C# (markCore/markMid/markOuter yang udah dihitung di
//      DrawPatternMarkerVFX) - biar konsisten sama gradasi yang udah ada di layer sprite lainnya.
//
// CATATAN INTEGRASI:
//   - File .fx ini harus ada di: TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/Shaders/
//     (sudah dicocokin sama path yang dipakai ModContent.Request<Effect> di
//     WhoAmI_VFX_PatternMarker.cs -> EnsurePatternMarkerShaderLoaded()). tModLoader otomatis
//     compile .fx jadi .xnb pas build mod, gak perlu compile manual.
//   - Kalau compiler kamu keberatan sama ps_3_0 (jarang, tapi ada beberapa setup lama), turunin
//     jadi ps_2_0 dan kurangin OCTAVES di fbm() jadi 2 (loop dengan literal count kecil biasanya
//     masih ke-unroll otomatis walau di profile ps_2_0).
// ================================================================================================

sampler uImage0 : register(s0);

float uTime;
float3 uColorCore;
float3 uColorMid;
float3 uColorOuter;
float uNoiseScale;
float uNoiseStrength;

// ---- hash & value-noise dasar (gak butuh texture, murni matematika) ----
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

    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

// 3 octave FBM - cukup buat kesan "bertekstur/organik" tanpa berat di GPU
float fbm(float2 p)
{
    float total = 0.0;
    float amp = 0.55;
    for (int i = 0; i < 3; i++)
    {
        total += valueNoise(p) * amp;
        p *= 2.05;
        amp *= 0.5;
    }
    return total;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float4 baseTex = tex2D(uImage0, coords);
    if (baseTex.a <= 0.001)
        return float4(0, 0, 0, 0);

    // Layer 1: scroll ke satu arah
    float2 uvA = coords * uNoiseScale + float2(uTime * 0.16, -uTime * 0.11);
    float noiseA = fbm(uvA);

    // Layer 2: scroll arah & kecepatan beda, skala lebih rapat -> nabrak sama layer 1 jadi
    // turbulence, bukan cuma noise scroll 1 arah yang keliatan "seragam"
    float2 uvB = coords * uNoiseScale * 2.3 - float2(uTime * 0.22, uTime * 0.09);
    float noiseB = fbm(uvB);

    float turbulence = saturate(noiseA * 0.6 + noiseB * 0.55);

    // Gradasi core->mid->outer berdasar jarak dari TENGAH tekstur sprite (0.5, 0.5 di UV) - ini
    // nyambung sama origin yang dipakai spriteBatch.Draw di sisi C# (glowOrigin = tengah texture)
    float distFromCenter = saturate(length(coords - float2(0.5, 0.5)) * 2.0);
    float3 grad = lerp(uColorCore, uColorMid, saturate(distFromCenter * 1.8));
    grad = lerp(grad, uColorOuter, saturate((distFromCenter - 0.45) * 1.8));

    // Turbulence modulasi alpha (bagian "kosong" noise-nya bikin efek crackle/robek tepi) dan
    // brightness (bagian "penuh" noise-nya kerasa nge-flash kayak percikan energi)
    float alphaMod = lerp(1.0 - uNoiseStrength, 1.0, turbulence);
    float finalAlpha = baseTex.a * alphaMod * color.a;
    float3 finalColor = grad * (0.55 + 0.55 * turbulence) * color.rgb;

    // Premultiplied alpha (konsisten sama additive blend yang dipakai di sisi C#)
    return float4(finalColor * finalAlpha, finalAlpha);
}

technique Technique1
{
    pass PatternMarkerNoisePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
