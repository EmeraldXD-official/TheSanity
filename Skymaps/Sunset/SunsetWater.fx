// SunsetWater.fx
// Simulasi permukaan air laut yang lebih "natural": gelombang dibentuk dari beberapa
// lapis sinus (beda frekuensi/arah/kecepatan -> gelombang terlihat organik, tidak berulang
// secara jelas), lalu kemiringan (slope) gelombang itu dipakai untuk menentukan highlight
// cahaya (specular) -- persis seperti cara shader air pada umumnya bekerja.
//
// PENTING: parameter di bawah WAJIB lengkap sesuai daftar standar screen-shader tModLoader,
// walau sebagian tidak dipakai di pass ini.

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
sampler uImage2 : register(s2);
sampler uImage3 : register(s3);
float3 uColor;
float3 uSecondaryColor;
float2 uScreenResolution;
float2 uScreenPosition;
float2 uTargetPosition;
float2 uDirection;
float uOpacity;
float uTime;
float uIntensity;
float uProgress;
float2 uImageSize1;
float2 uImageSize2;
float2 uImageSize3;
float2 uImageOffset;
float uSaturation;
float4 uSourceRect;
float2 uZoom;

static const float HORIZON_Y = 0.60;

// Tinggi gelombang gabungan di titik (x,y) pada waktu t -- 4 lapis sinus dengan
// frekuensi, arah, dan kecepatan berbeda supaya polanya terasa alami, bukan berulang.
float WaveHeight(float2 p, float t)
{
    float h = 0.0;
    h += sin(p.x * 22.0 + t * 1.3) * 0.50;
    h += sin(p.x * 9.0 - p.y * 6.0 + t * 0.7) * 0.35;
    h += sin(p.x * 35.0 + p.y * 18.0 - t * 2.1) * 0.15;
    h += sin(p.x * 4.0 + t * 0.35) * 0.60; // ombak besar & pelan (swell)
    return h;
}

float4 FilterSunsetWater(float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords;
    float4 result;

    if (uv.y > HORIZON_Y)
    {
        float depthBelow = saturate((uv.y - HORIZON_Y) / (1.0 - HORIZON_Y));
        float aspect = uScreenResolution.x / max(uScreenResolution.y, 1.0);
        float2 wc = float2(uv.x * aspect, uv.y * 3.0);

        float h = WaveHeight(wc, uTime);

        // Turunan numerik sederhana -> perkiraan "kemiringan" gelombang di titik ini
        float eps = 0.0025;
        float hX = WaveHeight(wc + float2(eps, 0.0), uTime);
        float slope = (hX - h) / eps;

        // Distorsi UV mengikuti tinggi & kemiringan gelombang -> riak yang benar-benar
        // "mengikuti bentuk permukaan", bukan sekadar sinus acak di layar
        float rippleStrength = 0.0034 * uIntensity * (0.25 + depthBelow);
        uv.y += h * rippleStrength;
        uv.x += slope * rippleStrength * 0.5;

        float4 baseColor = tex2D(uImage0, uv);

        // SPECULAR: cahaya "mantul" hanya muncul di puncak gelombang yang landai (slope kecil),
        // dan difokuskan ke jalur menuju tengah layar (arah matahari) -- persis seperti pantulan
        // matahari asli di air, makin melebar & makin jelas makin dekat ke kamera.
        float pathFocus = 1.0 - saturate(abs(uv.x - 0.5) / (0.07 + depthBelow * 0.45));
        float crest = saturate(h * 0.5 + 0.5);
        float flatness = saturate(1.0 - abs(slope) * 0.7);
        float specular = pow(crest, 9.0) * flatness;
        float sparkle = specular * pathFocus * (0.35 + depthBelow * 0.9) * uIntensity;

        float3 sparkleColor = float3(1.0, 0.82, 0.5);
        baseColor.rgb += sparkleColor * sparkle * 1.6;

        // Pita cahaya lembut mengikuti puncak gelombang (bukan cuma titik specular tajam),
        // memberi kesan "gelombang cahaya" menyebar di permukaan
        float lightBand = pow(crest, 3.0) * 0.10 * uIntensity * (0.3 + depthBelow * 0.7);
        baseColor.rgb += lightBand * float3(0.6, 0.7, 1.0);

        result = baseColor;
    }
    else
    {
        result = tex2D(uImage0, uv);
    }

    // Dusk color grading lembut ke seluruh layar (termasuk block/tile asli)
    float3 duskTint = float3(0.88, 0.80, 0.92);
    result.rgb *= lerp(float3(1.0, 1.0, 1.0), duskTint, uIntensity);
    result.rgb *= lerp(1.0, 0.86, uIntensity);

    return result;
}

technique Technique1
{
    pass FilterSunsetWater
    {
        PixelShader = compile ps_3_0 FilterSunsetWater();
    }
}
