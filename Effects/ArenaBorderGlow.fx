// ==========================================
// ArenaBorderGlow.fx — REVISI KEDUA: kabut glow interior arena SEKARANG "nyebar nutupin
// seluruh WORLD", bukan cuma dalam lingkaran arena (Radius ~700) doang lagi - TAPI border
// fisik (donat + collision di ArenaBorderSystem/TwinsArenaGlobalNPC) TETAP di radius aslinya,
// cuma layer glow VISUAL ini aja yang di-scale independen jadi seukuran dunia.
//
// Makanya sekarang ada 2 radius terpisah yang dikirim dari C# (lihat
// DrawSingleArenaGlowFill di ArenaBorderSystem.cs):
//   - uGlowRampFraction = Radius arena ASLI / Radius DUNIA (dalam satuan UV 0..1 relatif
//     quad). Dari tengah quad sampai fraction ini, kecerahan di-ramp naik dari uInnerGlowMin
//     ke 1.0 (persis kayak perilaku "energi berkumpul ke tepi" versi sebelumnya, TAPI di-mapping
//     ke radius arena asli yang notabene cuma porsi KECIL dari quad raksasa ini sekarang).
//   - Setelah lewat uGlowRampFraction (artinya udah keluar dari radius arena asli, masuk ke
//     wilayah "dunia" di luar arena), kecerahan DIKUNCI full (1.0) terus sampai ke tepi quad -
//     inilah yang bikin glow-nya kelihatan "nutupin seluruh dunia" secara merata, bukan cuma
//     nongol di sekitar arena kecil doang.
//   - edgeCutoff TETAP motong tegas persis di tepi quad (sekarang = tepi DUNIA, bukan tepi
//     arena lagi) - safety net biar gak pernah nge-draw di luar radius yang di-set C#.
// ==========================================

sampler uImage0 : register(s0); // texture dasar dari SpriteBatch.Draw (auto-bind bawaan SpriteBatch, gak perlu di-set manual)

// Noise TurbulentNoise - texture+sampler EKSPLISIT (bukan cuma "sampler ... : register(s1)"
// polos) supaya bisa di-set manual lewat Effect.Parameters["uNoiseTextureObj"].SetValue(...) di C#
// - pola NATIVE tModLoader (persis dipakai FargosSoulsMod buat shader custom mereka sendiri),
// BUKAN lewat ManagedShader.SetTexture Luminance lagi.
texture uNoiseTextureObj;
sampler uNoiseTexture : register(s1) = sampler_state
{
    Texture = <uNoiseTextureObj>;
    AddressU = WRAP;
    AddressV = WRAP;
};

float uTime; // buat scroll noise, biar "bergejolak" bukan diem
float4 uColor; // warna pulse merah<->hijau SAAT INI (dari GetAnimatedColor sisi C#), sudah termasuk opacity border

// Seberapa terang bagian TENGAH arena minimal (0 = item polos di tengah kayak versi
// outward lama, 1 = serata tepi). >0 biar bagian tengah arena tetap nyala.
float uInnerGlowMin;

// Pengali kecerahan keseluruhan.
float uIntensity;

// Fraction (0..1, relatif radius QUAD SEKARANG = radius dunia) tempat brightness udah nyampe
// 1.0 penuh - ini SEHARUSNYA sama dengan (radius arena asli / radius dunia), jadi biasanya
// angka KECIL (arena cuma porsi kecil dari dunia). Di luar fraction ini, brightness dikunci
// full 1.0 sampai ke tepi quad.
float uGlowRampFraction;

// Seberapa besar noise di-tile - makin gede angkanya, makin "rapat"/kecil detail turbulensinya.
// Dinaikkan dari versi sebelumnya karena area yang dicover sekarang jauh lebih luas (seukuran
// dunia), biar detail noise-nya gak keliatan "diregangkan"/kegedean per-tile-nya.
static const float NoiseTileScale = 9.0;
static const float2 NoiseScrollSpeed = float2(0.05, -0.035);

// Lebar transisi anti-alias di tepi (dalam satuan UV distFromCenter) - SENGAJA kecil biar
// motongnya tegas/instan, cuma cukup buat nyamarin jaggy 1 pixel, bukan buat bikin fade.
static const float EdgeAACutoff = 0.006;

float4 MainPS(float2 coords : TEXCOORD0) : COLOR0
{
    float2 centered = coords - float2(0.5, 0.5);
    float distFromCenter = length(centered) * 2.0; // 0 di tengah quad, 1 tepat di tepi quad (radius DUNIA)

    // Potong TEGAS begitu keluar dari radius yang di-set C# (sekarang radius dunia).
    float edgeCutoff = 1.0 - smoothstep(1.0 - EdgeAACutoff, 1.0, distFromCenter);

    // Ramp brightness dari uInnerGlowMin ke 1.0 SELAMA masih di dalam radius arena asli
    // (uGlowRampFraction), lalu DIKUNCI 1.0 terus buat sisa jarak sampai tepi dunia -
    // inilah yang bikin efeknya "nutupin seluruh dunia" secara merata terang, bukan meredup
    // makin jauh dari arena.
    float rampT = saturate(distFromCenter / max(uGlowRampFraction, 0.0001));
    float radialGlow = lerp(uInnerGlowMin, 1.0, rampT);

    float2 noiseUV = coords * NoiseTileScale + uTime * NoiseScrollSpeed;
    float noiseSample = tex2D(uNoiseTexture, noiseUV).r;

    // Sample kedua di skala/arah beda biar teksturnya gak keliatan cuma 1 layer noise digeser lurus.
    float2 noiseUV2 = coords * (NoiseTileScale * 1.7) - uTime * NoiseScrollSpeed * 1.3;
    float noiseSample2 = tex2D(uNoiseTexture, noiseUV2).r;

    float combinedNoise = saturate(noiseSample * 0.65 + noiseSample2 * 0.55);

    float fillMask = edgeCutoff * radialGlow;

    float finalAlpha = saturate(fillMask * combinedNoise * uColor.a * uIntensity);

    // Premultiplied alpha - dipakai dengan BlendState.Additive di C#, sama pola kayak
    // TwinsAmbientFX/TwinsDeathExplosionFX (bagian gelap otomatis "hilang" kena additive).
    return float4(uColor.rgb * finalAlpha, finalAlpha);
}

technique Technique1
{
    pass ArenaBorderGlowPass
    {
        PixelShader = compile ps_3_0 MainPS();
    }
}
