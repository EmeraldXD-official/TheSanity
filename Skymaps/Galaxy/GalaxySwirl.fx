// GalaxySwirl.fx
// Shader untuk GalaxySky: distorsi swirl lembut di sekitar pusat layar + bloom murah pada
// piksel terang (inti galaxy & partikel).
//
// PENTING: parameter di bawah ini WAJIB lengkap sesuai daftar standar screen-shader tModLoader,
// walau sebagian besar tidak dipakai di pass ini -- soalnya game otomatis coba nge-set semua
// parameter ini setiap frame, dan kalau ada yang tidak dideklarasikan shader bisa crash.
// (Sumber: tModLoader wiki, "Expert Shader Guide")

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

float4 FilterGalaxySwirl(float2 coords : TEXCOORD0) : COLOR0
{
    float2 center = float2(0.5, 0.5);
    float2 toCenter = coords - center;
    float dist = length(toCenter);

    // Distorsi swirl lembut: paling kuat dekat tengah layar, menghilang di tepi
    float swirl = uIntensity * saturate(1.0 - dist * 1.6);
    float angle = swirl * 0.6;
    float s = sin(angle);
    float c = cos(angle);

    float2 rotated = float2(
        toCenter.x * c - toCenter.y * s,
        toCenter.x * s + toCenter.y * c
    );
    float2 sampleCoords = center + rotated;

    float4 color = tex2D(uImage0, sampleCoords);

    // Bloom murah: hanya menambah glow pada piksel yang sudah terang (inti/partikel)
    float luminance = dot(color.rgb, float3(0.299, 0.587, 0.114));
    float bloom = saturate((luminance - 0.6) * 2.0);
    color.rgb += bloom * float3(0.55, 0.35, 0.9) * uIntensity;

    return color;
}

technique Technique1
{
    pass GalaxySwirl
    {
        PixelShader = compile ps_3_0 FilterGalaxySwirl();
    }
}
