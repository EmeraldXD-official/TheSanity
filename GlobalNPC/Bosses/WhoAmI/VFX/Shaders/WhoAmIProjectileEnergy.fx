// ================================================================================================
// WhoAmIProjectileEnergy.fx — "Lucille Karma" tier single-pass projectile shader (REWRITE)
// ================================================================================================
// Source .fx aslinya udah nggak ada (cuma .xnb hasil compile yang sempet ke-upload) - file ini
// ditulis ULANG DARI NOL, dicocokin ke parameter yang emang udah dipanggil dari sisi C#
// (WhoAmIShaderSystem.TryDrawProjectileEnergy, WhoAmI_VFX_ShaderSystem.cs):
//   uTexelSize     (float2) - 1/lebar & 1/tinggi TEXTURE ATLAS PENUH (bukan cuma satu frame animasi)
//   uNeonColor     (float3) - warna ring outline, dari GetArchetypeNeonColor() (WhoAmI_VFX_ProjectileShader.cs)
//   uWeaponColor   (float3) - warna tema archetype senjata, dari GetWeaponCopyColor()
//   uOutlineWidthPx(float)  - lebar ring outline dalam satuan texel
//   uChromaShiftPx (float)  - seberapa jauh channel R/B di-geser buat efek chromatic aberration
//   uTime          (float)  - Main.GlobalTimeWrappedHourly, buat animasi pulsa/rotasi shift
//   uOpacity       (float)  - opacity keseluruhan pass ini
//   uFrameUVMin/Max(float2) - BARU, ditambahin bareng rewrite ini: batas UV frame animasi yang
//                             lagi digambar SEKARANG di dalam atlas, biar sampling outline/chroma
//                             di-clamp ke frame ini doang - tanpa ini, proyektil beranimasi bisa
//                             "bocor" baca pixel dari frame tetangga di sprite sheet-nya pas
//                             offset sampling-nya kena ke pinggir frame.
//
// EFEK YANG DIHASILKAN (menggantikan DrawNeonOutlinePass() CPU 8-draw fallback di
// WhoAmI_VFX_ProjectileShader.cs kalau file ini berhasil di-compile jadi .xnb):
//   1. Neon rim outline di sekeliling silhouette sprite (edge-detection dari alpha channel,
//      8 arah, biar ring-nya nutup rapat di semua sisi bentuk apapun - bulat, panjang, bersudut).
//   2. Chromatic aberration/fringe di badan sprite sendiri (channel R/B digeser berlawanan arah,
//      arah gesernya muter pelan seiring uTime, bukan cuma 1 axis statis - kerasa "energi nggak
//      stabil" beneran, bukan cuma efek 1-arah yang gampang ketebak matanya).
//   3. Tint warna archetype senjata (uWeaponColor) di badan + warna neon terpisah (uNeonColor) di
//      ring, jadi dua elemen itu bisa dibedain warnanya (misal badan tetap warna archetype tapi
//      ring-nya lebih putih/terang - lihat GetArchetypeNeonColor yang udah nge-blend ke arah putih).
//   4. Pulsa lembut di intensitas ring, senada breathing yang dipakai di seluruh VFX boss lain.
//
// KONVENSI FILE: pixel-shader-only technique, nggak declare VertexShader sendiri - pola yang sama
// dipakai shader lain di project ini (lihat parameter WhoAmIGroundSigil.fx di
// WhoAmI_VFX_GroundSigil.cs: uColor/uOpacity/uTime/uRotationSpeed, dipanggil dengan cara yang sama
// persis - SpriteSortMode.Immediate + custom effect). FNA/tModLoader nyuntik vertex-transform
// default-nya sendiri buat SpriteBatch custom-effect pass kalau nggak di-override eksplisit di
// technique-nya. KALAU pipeline compile shader project ini butuh konvensi beda (nama
// technique/pass spesifik, shader profile beda dari ps_3_0), sesuaikan blok "technique" di paling
// bawah - itu SATU-SATUNYA bagian yang bergantung ke toolchain compile-nya, logic pixel shader di
// atasnya portable ke profile manapun yang support tex2D + loop.
// ================================================================================================

sampler uImage0 : register(s0);

float2 uTexelSize;
float3 uNeonColor;
float3 uWeaponColor;
float uOutlineWidthPx;
float uChromaShiftPx;
float uTime;
float uOpacity;
float2 uFrameUVMin;
float2 uFrameUVMax;

// Sample dengan clamp manual ke batas frame animasi saat ini (lihat catatan uFrameUVMin/Max di
// atas) - dipakai buat SEMUA sampling di bawah (base + tetangga outline + tap chroma) supaya
// nggak ada satupun yang bocor baca frame tetangga di sprite sheet.
float4 SampleClamped(float2 uv)
{
    float2 clampedUv = clamp(uv, uFrameUVMin, uFrameUVMax);
    return tex2D(uImage0, clampedUv);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float4 baseSample = SampleClamped(coords);

    // ---- 1) CHROMATIC ABERRATION - fringe di badan sprite sendiri ----
    // Arah shift muter pelan seiring uTime (bukan axis horizontal statis) - kesannya energi yang
    // nggak stabil/bergetar, bukan efek 1-arah yang keliatan "kaku".
    float shiftAngle = uTime * 1.3;
    float2 shiftDir = float2(cos(shiftAngle), sin(shiftAngle)) * uChromaShiftPx * uTexelSize;

    float4 redTap = SampleClamped(coords - shiftDir);
    float4 blueTap = SampleClamped(coords + shiftDir);

    float3 chromaColor = float3(redTap.r, baseSample.g, blueTap.b);
    float chromaAlpha = max(baseSample.a, max(redTap.a, blueTap.a));

    // ---- 2) NEON RIM OUTLINE - edge-detection berbasis alpha, 8 arah ----
    // Bandingin alpha di titik ini vs alpha tetangga sejauh uOutlineWidthPx texel: kalau titik ini
    // TRANSPARAN tapi ada tetangga yang OPAQUE, titik ini persis di pinggiran luar silhouette -
    // itu yang jadi ring neon-nya. Ambil MAX dari 8 arah biar ring-nya nutup rapat semua sisi,
    // bukan cuma sisi yang kebetulan searah 1 offset doang.
    float2 outlineStep = uOutlineWidthPx * uTexelSize;
    float neighborMaxAlpha = 0;
    for (int i = 0; i < 8; i++)
    {
        float angle = i * 0.7853981634; // step 2*PI/8 = 45 derajat
        float2 offset = float2(cos(angle), sin(angle)) * outlineStep;
        neighborMaxAlpha = max(neighborMaxAlpha, SampleClamped(coords + offset).a);
    }
    float outlineMask = saturate(neighborMaxAlpha - baseSample.a);

    // ---- 3) GABUNGIN ----
    // Pulsa lembut buat ring-nya - senada pola sin(time*freq) yang dipakai di seluruh VFX boss
    // lain (WhoAmI_VFX.cs / WhoAmI_VFX_Attacks.cs), biar nggak keliatan "flat"/statis.
    float pulse = 0.75 + 0.25 * sin(uTime * 4.0);

    float3 tintedBody = chromaColor * lerp(float3(1, 1, 1), uWeaponColor, 0.55);
    float3 rimColor = uNeonColor * pulse;

    float3 finalColor = tintedBody + rimColor * outlineMask;
    float finalAlpha = saturate(chromaAlpha + outlineMask) * uOpacity;

    return float4(finalColor, finalAlpha) * color;
}

technique Technique1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
