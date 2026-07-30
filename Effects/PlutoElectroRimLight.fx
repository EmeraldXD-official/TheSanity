// =========================================================================
// PlutoElectroRimLight.fx
//
// Rim-light "fake normal" -- bagian sprite yang menghadap sumber cahaya
// (PlutoElectroBall) ikut nyala, bagian yang membelakangi tetep gelap
// normal. Caranya: sample alpha texture beberapa langkah ke ARAH sumber
// cahaya (uLightDir, sudah dalam local-space texture, lihat komentar di
// PlutoHead.cs). Kalau di titik itu si sprite "kosong" (alpha rendah),
// berarti pixel yg lagi diproses ini ada di TEPI yang menghadap cahaya
// -> dikasih glow. Kalau arah situ masih "isi" (masih ada sprite),
// berarti bukan tepi yg menghadap cahaya -> tetep gelap.
//
// Ini shader SM2.0 (ps_2_0), sesuai batasan pipeline shader Terraria/FNA
// (lihat catatan di SAYA_SETUJU.txt kategori 20/misc).
// =========================================================================

sampler uImage0 : register(s0);

float2 uLightDir;   // arah ke sumber cahaya (sudah di-unrotate ke local-space texture), normalized
float uIntensity;   // seberapa kuat glow RIM-nya (tepi yg menghadap cahaya)
float uAmbient;     // seberapa kuat "wash"/fill cahaya ke SELURUH badan yg kesorot (bukan cuma tepi)
float3 uLightColor; // warna cahaya si ElectroBall (oranye-kemerahan)
float2 uRimWidth;   // lebar sampling per-axis dalam UV unit (beda x/y krn texture non-square)

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float4 baseColor = tex2D(uImage0, coords);

    if (baseColor.a < 0.02)
        return baseColor * color;

    // 4 sample jarak beda2 ke arah cahaya -> rim jadi lebih halus & lebih TEBAL
    float edge1 = tex2D(uImage0, coords + uLightDir * uRimWidth * 1.0).a;
    float edge2 = tex2D(uImage0, coords + uLightDir * uRimWidth * 2.0).a;
    float edge3 = tex2D(uImage0, coords + uLightDir * uRimWidth * 3.0).a;
    float edge4 = tex2D(uImage0, coords + uLightDir * uRimWidth * 4.5).a;

    float rim = saturate((1.0 - edge1) * 0.45 + (1.0 - edge2) * 0.30 + (1.0 - edge3) * 0.15 + (1.0 - edge4) * 0.10);
    rim *= baseColor.a;

    // Ambient fill: SELURUH permukaan yg kesorot ikut kebagian cahaya dikit (rata, ga peduli
    // arah), biar berasa "keguyur" cahaya bola, bukan cuma tepinya doang yg nyala kayak versi awal.
    float ambient = uAmbient * baseColor.a;

    float3 litColor = baseColor.rgb * color.rgb;
    litColor += uLightColor * (rim * uIntensity + ambient);

    return float4(litColor, baseColor.a * color.a);
}

technique Technique1
{
    pass RimLightPass
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
