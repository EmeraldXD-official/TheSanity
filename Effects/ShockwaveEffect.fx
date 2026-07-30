// ==================================================================================
// ShockwaveEffect.fx
// Screen-space radial distortion shader (dipakai buat efek shockwave saat ledakan).
// Adaptasi dari tutorial "Shockwave Effect for tModLoader" (forums.terraria.org,
// thread 81685) -- source code di thread itu memang dibagikan bebas untuk dipakai/
// diadaptasi siapa saja.
//
// CARA COMPILE:
// File ini HARUS di-compile jadi .xnb sebelum bisa dipakai (tModLoader tidak bisa
// load .fx mentah). Pakai salah satu dari:
//   1. tModLoader's built-in shader compiler (lewat tModLoader Dev tools / mfbuild), atau
//   2. Aplikasi standalone "fxc.exe" (DirectX shader compiler) dari Windows SDK, atau
//   3. Tanya di tModLoader Discord (#modder-support) kalau bingung setup toolchain-nya.
// Hasil compile-nya ("ShockwaveEffect.xnb") ditaruh di:
//   ModSources/TheSanity/Effects/ShockwaveEffect.xnb
// ==================================================================================

sampler uImage0 : register(s0);

float2 uScreenResolution;
float2 uScreenPosition;
float2 uTargetPosition;

// uColor dipakai buat 3 parameter shockwave (bukan warna beneran, cuma dipinjem
// karena XNA effect di tModLoader cuma nyediain slot bawaan ini):
//   uColor.x = jumlah ripple/gelombang
//   uColor.y = "size" -> sebenarnya scalar buat dot field, makin besar = ripple makin RAPAT
//   uColor.z = kecepatan rambat gelombang
float3 uColor;

float uOpacity; // kekuatan distorsi
float uProgress; // posisi rambatan gelombang saat ini (0 = titik ledakan)

// 🛑 [TAMBAHAN WARNA SHOCKWAVE] uTint.rgb = warna yang mau "dicampur" ke pita gelombang,
// uTint.a = seberapa kuat campuran warnanya (0 = ga ada warna sama sekali / transparan
// polos kayak semula, 1 = pita gelombang full ketutup warna itu). Kalau parameter ini ga
// pernah di-set dari C# (misal ada kode lama yang manggil Trigger() tanpa tint), nilai
// default-nya 0 semua (termasuk alpha-nya) -- jadi otomatis GA ADA perubahan visual sama
// sekali dibanding versi shockwave lama yang cuma distorsi polos. Aman buat backward-compat.
float4 uTint;

// 🛑 [TAMBAHAN JANGKAUAN/RANGE] uMaxRange = jarak maksimum (dalam PIXEL layar, bukan UV/tile)
// dari uTargetPosition di mana shockwave-nya masih boleh keliatan. Di luar jarak ini, ripple
// di-mask jadi 0 (ketutup balik ke gambar asli, ga ada distorsi/tint sama sekali).
// Dikasih SOFT FALLOFF (bukan garis potong tajam) selebar ~15% dari uMaxRange itu sendiri,
// biar batas jangkauannya ga keliatan kayak "tembok tak kasat mata" yang motong distorsi
// secara tiba-tiba/kotak.
//
// PENTING BUAT BACKWARD-COMPAT: kalau parameter ini ga pernah di-set dari C# (misal kode lama
// yang manggil Trigger()/TriggerOneShot() tanpa oper jangkauan), nilai default-nya 0 -- dan 0
// (atau negatif) sengaja diartikan "TANPA BATAS JANGKAUAN" (perilaku lama, shockwave nyapu
// sejauh rumus ripple aslinya, ga ada pemotongan tambahan sama sekali).
float uMaxRange;

#define PI 3.14159265359

float4 Shockwave(float4 position : SV_POSITION, float2 coords : TEXCOORD0) : COLOR0
{
    float2 targetCoords = (uTargetPosition - uScreenPosition) / uScreenResolution;
    float2 centreCoords = (coords - targetCoords) * (uScreenResolution / uScreenResolution.y);
    float dotField = dot(centreCoords, centreCoords);
    float ripple = dotField * uColor.y * PI - uProgress * uColor.z;

    if (ripple < 0 && ripple > uColor.x * -2 * PI)
    {
        ripple = saturate(sin(ripple));
    }
    else
    {
        ripple = 0;
    }

    // 🛑 [MASK JANGKAUAN/RANGE] centreCoords itu di-normalisasi terhadap TINGGI layar (lihat
    // baris hitung centreCoords di atas -- y-nya 1.0 = setinggi layar), jadi buat dapetin jarak
    // PIXEL sebenarnya dari titik ledakan, tinggal kali panjang vector-nya sama uScreenResolution.y.
    float distanceInPixels = length(centreCoords) * uScreenResolution.y;

    // uMaxRange <= 0 => TANPA BATAS (perilaku lama, backward-compat, dipakai PlutoElectroBall).
    // uMaxRange > 0  => ripple di-fade halus mulai dari jangkauan itu sampai +15% lebih jauh,
    // biar ga ada garis potong kotak/tajam yang keliatan aneh di batas jangkauannya.
    float rangeSoftEdge = max(uMaxRange * 0.15, 1.0);
    float rangeMask = (uMaxRange <= 0.0) ? 1.0 : saturate(1.0 - (distanceInPixels - uMaxRange) / rangeSoftEdge);
    ripple *= rangeMask;

    float2 sampleCoords = coords + ((ripple * uOpacity / uScreenResolution) * centreCoords);

    float4 texColor = tex2D(uImage0, sampleCoords);

    // 🛑 [TAMBAHAN WARNA SHOCKWAVE] "ripple" di titik ini udah dalam rentang 0..1 (kekuatan
    // pita gelombang di pixel ini). Dipakai juga buat nge-blend warna uTint ke pixel yang
    // lagi kena pita gelombangnya -- makin di tengah pita (ripple mendekati 1), makin kental
    // warnanya; makin ke tepi/luar pita, makin transparan/balik ke warna asli layar.
    float tintAmount = saturate(ripple) * uTint.a;
    texColor.rgb = lerp(texColor.rgb, uTint.rgb, tintAmount);

    return texColor;
}

// ==================================================================================
// PASS KEDUA: BULGE (kubah/lensa cembung sesaat)
// Beda mekanisme dari Shockwave di atas: kalau Shockwave itu gelombang sinus yang
// MERAMBAT dari pusat ke tepi layar seiring waktu (kerasa kayak "riak air"), Bulge ini
// SATU kubah distorsi yang nongol DI TEMPAT (ga merambat kemana-mana), lalu membesar
// sesaat & mereda -- efeknya lebih mirip "kaca cembung muncul sekilas", mirip gaya efek
// pecahnya shield Celestial Pillar / shockwave boss-boss di mod Infernum.
//
// uColor.x dipakai ulang di sini sebagai RADIUS kubahnya (bukan ripple count lagi --
// pass ini punya arti parameter sendiri, ga kepake bareng sama pass Shockwave di atas).
// uProgress di sini artinya juga beda: 0..1 = "envelope" kekuatan kubah saat ini
// (dihitung & dinaikturunkan dari sisi C#, BUKAN dipakai buat majuin rambatan kayak di
// pass Shockwave). uOpacity = seberapa kuat dorongan lensa-nya di puncak.
// ==================================================================================
float4 Bulge(float4 position : SV_POSITION, float2 coords : TEXCOORD0) : COLOR0
{
    float2 targetCoords = (uTargetPosition - uScreenPosition) / uScreenResolution;
    float2 centreCoords = (coords - targetCoords) * (uScreenResolution / uScreenResolution.y);
    float dist = length(centreCoords);

    float radius = max(uColor.x, 0.0001);
    float falloff = saturate(1.0 - dist / radius);
    falloff = falloff * falloff; // pusat kubah paling nonjol, tepinya ngilang halus

    float bulge = falloff * uProgress * uOpacity;

    // Dorong coordinate sampling ke arah LUAR dari pusat (proporsional jarak, biar
    // kerasa "ngembung" bukan cuma geser rata) -- tanda minus biar arah dorongannya
    // konsisten kayak lensa cembung (bukan cekung).
    float2 sampleCoords = coords - ((bulge / uScreenResolution) * centreCoords);

    float4 texColor = tex2D(uImage0, sampleCoords);

    float tintAmount = falloff * uProgress * uTint.a;
    texColor.rgb = lerp(texColor.rgb, uTint.rgb, tintAmount);

    return texColor;
}

technique Technique1
{
    pass Shockwave
    {
        PixelShader = compile ps_2_0 Shockwave();
    }

    pass Bulge
    {
        PixelShader = compile ps_2_0 Bulge();
    }
}
