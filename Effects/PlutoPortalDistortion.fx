// ==================================================================================
// PlutoPortalDistortion.fx
// Full-screen "swirl/suction" distortion buat PlutoPortal, radius 20 block dari
// portal-nya. Bikinnya niru struktur BlackHoleShader.fx (Wrath of the Gods) yang
// dikasih sebagai referensi -- sama-sama full-screen filter (Filters.Scene), sama-sama
// pola "rotasi UV berdasarkan jarak ke source position", tapi ditambah SATU hal baru:
// EXEMPTION MASK, biar Head/Body/Tail/Hook/Chain/Portal/Laser Aim tetep kegambar
// normal (gak ikut kewarp) walaupun mereka ada persis di tengah radius distorsinya.
//
// Cara exemption-nya kerja: tiap part yang mau "immune" lapor posisi + radius mereka
// (dalam screen-space UV 0..1) tiap tick lewat PlutoPortalDistortionSystem. Di pixel
// shader ini, tiap pixel dicek: seberapa deket dia ke salah satu zona exempt itu?
// Kalau di DALAM zona -> pixel itu dijamin sample dari coords ASLI (gak di-warp),
// walaupun secara matematis seharusnya kena distorsi. Di LUAR zona -> normal warp.
// ==================================================================================

sampler baseTexture : register(s0);

#define MAX_PORTAL_SOURCES 4
#define MAX_EXEMPT_ZONES 28

float globalTime;

// --- Data sumber distorsi (portal-nya sendiri) ---
int portalCount;
float2 sourcePositions[MAX_PORTAL_SOURCES];   // screen-space UV (0..1)
float sourceStrengths[MAX_PORTAL_SOURCES];    // 0..1, biasanya ngikutin Projectile.scale & alpha
float distortionRadius;                        // radius 20 block, udah dikonversi ke UV (relatif lebar layar)

// --- Data zona kebal (exemption) ---
int exemptCount;
float2 exemptPositions[MAX_EXEMPT_ZONES];      // screen-space UV (0..1)
float exemptRadii[MAX_EXEMPT_ZONES];           // radius per-part, dalam UV (relatif lebar layar)

float2 aspectRatioCorrectionFactor; // (screenWidth/screenHeight, 1) -- biar jarak gak "lonjong"

// Maksimum sudut rotasi vortex (radian) & kekuatan sedotan ke tengah, di titik paling deket portal.
static const float MaxSwirlAngle = 2.35619; // ~135 derajat
static const float MaxPullStrength = 0.05;

float InverseLerp(float from, float to, float x)
{
    return saturate((x - from) / (to - from));
}

float2 RotatedBy(float2 v, float theta)
{
    float s = sin(theta);
    float c = cos(theta);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

// Pseudo-noise prosedural (sin-based) biar swirl-nya kerasa organik dikit, tanpa
// perlu nambahin dependency texture noise terpisah.
float ProceduralWobble(float2 coords, float time)
{
    return sin(coords.x * 18.0 + time * 2.0) * sin(coords.y * 14.0 - time * 1.6) * 0.5;
}

// Ngitung seberapa "kebal" pixel ini terhadap distorsi (0 = kena penuh, 1 = full immune).
// Dihitung dari SEMUA zona exempt yang aktif (Head, Body, Tail, Hook, Chain, Portal, Laser Aim, dst).
float ComputeExemptionMask(float2 correctedCoords)
{
    float mask = 0;
    for (int i = 0; i < MAX_EXEMPT_ZONES; i++)
    {
        if (i >= exemptCount) break;

        float r = exemptRadii[i];
        if (r <= 0.00001) continue;

        float2 exemptCorrected = (exemptPositions[i] - 0.5) * aspectRatioCorrectionFactor + 0.5;
        float d = distance(correctedCoords, exemptCorrected);

        // Falloff lembut di tepi zona (85% radius -> 100% radius) biar transisinya
        // gak ada "patahan" garis tegas di sekeliling tiap part.
        float local = 1 - smoothstep(r * 0.85, r, d);
        mask = max(mask, local);
    }
    return saturate(mask);
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 correctedCoords = (coords - 0.5) * aspectRatioCorrectionFactor + 0.5;

    // 1) Exemption mask duluan, dihitung dari coords ASLI (sebelum di-warp sama sekali).
    float exemptionMask = ComputeExemptionMask(correctedCoords);

    // 2) Akumulasi warp dari semua portal aktif (mirip loop lensing di BlackHoleShader.fx,
    //    tapi di sini efeknya "vortex suction" bukan gravitational lensing murni).
    float2 warpedCoords = coords;
    for (int i = 0; i < MAX_PORTAL_SOURCES; i++)
    {
        if (i >= portalCount) break;

        float2 sourcePos = sourcePositions[i];
        float strength = sourceStrengths[i];
        if (strength <= 0.0001) continue;

        float2 sourceCorrected = (sourcePos - 0.5) * aspectRatioCorrectionFactor + 0.5;
        float dist = max(distance(correctedCoords, sourceCorrected), 0.0001);

        float falloff = exp(-dist / distortionRadius * 2.0);
        float wobble = 1.0 + ProceduralWobble(correctedCoords, globalTime) * 0.15;

        // Rotasi (efek "muter" khas vortex/wormhole)
        float swirlAngle = strength * MaxSwirlAngle * falloff * wobble;
        warpedCoords = RotatedBy(warpedCoords - sourcePos, swirlAngle) + sourcePos;

        // Sedotan radial ke tengah (efek "tersedot masuk")
        float2 dirToSource = normalize(warpedCoords - sourcePos + 0.00001);
        float pull = strength * MaxPullStrength * falloff;
        warpedCoords -= dirToSource * pull;
    }

    // 3) Sample dua-duanya: versi distorsi (background/musuh lain) & versi asli (buat exempt part).
    float4 distortedColor = tex2D(baseTexture, warpedCoords);
    float4 originalColor = tex2D(baseTexture, coords);

    // 4) Zona exempt menang total -> tampil normal, sisanya kena distorsi penuh.
    return lerp(distortedColor, originalColor, exemptionMask);
}

technique Technique1
{
    pass PortalDistortionPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
