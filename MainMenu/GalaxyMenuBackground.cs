using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace TheSanity.Content.Skies
{
    /// <summary>
    /// Versi khusus menu dari GalaxySky. Efeknya SAMA PERSIS gayanya (lengan spiral titik
    /// dengan rotasi diferensial, nebula, debu kosmik, starfield kelap-kelip acak, bintang
    /// jatuh, vignette) tapi dipanggil langsung dari ModMenu.PreDrawLogo, bukan lewat
    /// SkyManager/CustomSky. Makanya kelasnya berdiri sendiri (static) dan:
    ///
    ///   - Selalu full-bright (tidak ada fade in/out berdasarkan equipment kayak GalaxySky).
    ///   - Ngitung delta time sendiri pakai Stopwatch, soalnya ModMenu.PreDrawLogo tidak
    ///     dikasih parameter GameTime.
    ///
    /// Cara pakai (lihat TwilightModMenu di SanityMenuThemes.cs):
    ///
    ///     public override bool PreDrawLogo(SpriteBatch spriteBatch, ...)
    ///     {
    ///         GalaxyMenuBackground.Draw(spriteBatch);
    ///         logoColor = Color.White;
    ///         return true;
    ///     }
    /// </summary>
    public static class GalaxyMenuBackground
    {
        private static float _armRotation;
        private static float _hazeRotation;
        private static float _time;
        private static float _nextShootTimer;

        private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private static double _lastElapsedSeconds;

        private static Texture2D _galaxyHaze;
        private static Texture2D _softGlow;
        private static Texture2D _particleTex;
        private static Texture2D _solidPixel;
        private static Texture2D _streakTex;
        private static Texture2D _vignetteTex;

        private static readonly Color CenterColor = new Color(255, 248, 225);
        private static readonly Color InnerColor = new Color(255, 200, 140);
        private static readonly Color MidColor = new Color(180, 100, 235);
        private static readonly Color OuterColor = new Color(35, 45, 130);
        private static readonly Color EdgeColor = new Color(24, 8, 42);

        private static readonly Color BackgroundDeepPurple = new Color(24, 8, 40);
        private static readonly Color BackgroundDeepBlue = new Color(6, 10, 32);

        private static readonly Color[] NebulaPalette =
        {
            new Color(180, 60, 235),
            new Color(60, 100, 230),
            new Color(150, 90, 220),
            new Color(50, 70, 210),
        };

        private static readonly Color RingColorInner = new Color(255, 205, 110);
        private static readonly Color RingColorOuter = new Color(150, 85, 225);

        private const int ParticleCount = 80;
        private const int StarCount = 160;
        private const int NebulaWispCount = 5;

        private const int Arms = 3;
        private const int PointsPerArm = 70;
        private const int CorePointCount = 50;
        private const int DustCount = 35;
        private const int MaxShootingStars = 2;

        private const float ArmRadiusPixels = 300f;
        private const float ArmRotationSpeed = 0.05f;
        private const float GlowIntensity = 0.45f;

        // Delta dibatasi maksimal segini per frame -> jaga-jaga kalau ada lag spike / menu
        // baru dibuka lagi setelah lama tidak aktif, supaya animasi tidak "meloncat" jauh.
        private const float MaxDelta = 0.05f;

        private struct SuctionParticle
        {
            public float Angle;
            public float Radius;
            public float Speed;
            public float Scale;
            public Color Color;
        }

        private struct Star
        {
            public Vector2 NormalizedPos;
            public float Scale;
            public float TwinklePhase;
            public float TwinkleSpeed;
            public float BaseBrightness;
            public Color Tint;
            public float FlickerSeed;
            public float FlickerRate;
        }

        private struct NebulaWisp
        {
            public Vector2 NormalizedAnchor;
            public float DriftRadius;
            public float DriftSpeed;
            public float DriftPhase;
            public float Scale;
            public float Alpha;
            public Color Color;
        }

        private struct GalaxyPoint
        {
            public float Ux, Uy;
            public float AngVelFactor;
            public float Size;
            public float Alpha;
            public Color Color;
        }

        private struct DustMote
        {
            public float Angle;
            public float Dist;
            public float Speed;
            public float Size;
            public float Alpha;
            public Color Color;
        }

        private struct ShootingStar
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
        }

        private static SuctionParticle[] _particles;
        private static Star[] _stars;
        private static NebulaWisp[] _nebulaWisps;
        private static GalaxyPoint[] _galaxyPoints;
        private static DustMote[] _dust;
        private static readonly List<ShootingStar> _shootingStars = new List<ShootingStar>();
        private static readonly Random _rand = new Random(54321); // seed beda dari GalaxySky biar polanya tidak identik

        private static float Hash11(float n)
        {
            float s = (float)Math.Sin(n) * 43758.5453f;
            return s - (float)Math.Floor(s);
        }

        private static float FlickerNoise(float t)
        {
            float ti = (float)Math.Floor(t);
            float tf = t - ti;
            float a = Hash11(ti);
            float b = Hash11(ti + 1f);
            float smooth = tf * tf * (3f - 2f * tf);
            return MathHelper.Lerp(a, b, smooth);
        }

        private static void EnsureInitialized()
        {
            if (_galaxyHaze == null)
            {
                GalaxyTextureGenerator.Generate(
                    Main.instance.GraphicsDevice,
                    out _galaxyHaze, out _softGlow, out _particleTex, out _solidPixel,
                    out _streakTex, out _vignetteTex);
            }

            if (_particles == null)
            {
                _particles = new SuctionParticle[ParticleCount];
                for (int i = 0; i < ParticleCount; i++)
                {
                    _particles[i] = new SuctionParticle
                    {
                        Angle = (float)(_rand.NextDouble() * MathHelper.TwoPi),
                        Radius = 60f + (float)_rand.NextDouble() * 760f,
                        Speed = 0.15f + (float)_rand.NextDouble() * 0.35f,
                        Scale = 0.4f + (float)_rand.NextDouble() * 0.9f,
                        Color = Color.Lerp(MidColor, InnerColor, (float)_rand.NextDouble())
                    };
                }
            }

            if (_stars == null)
            {
                _stars = new Star[StarCount];
                for (int i = 0; i < StarCount; i++)
                {
                    Color tint = _rand.NextDouble() < 0.75
                        ? Color.White
                        : Color.Lerp(InnerColor, Color.White, (float)_rand.NextDouble());

                    _stars[i] = new Star
                    {
                        NormalizedPos = new Vector2((float)_rand.NextDouble(), (float)_rand.NextDouble()),
                        Scale = 0.06f + (float)_rand.NextDouble() * 0.22f,
                        TwinklePhase = (float)(_rand.NextDouble() * MathHelper.TwoPi),
                        TwinkleSpeed = 0.6f + (float)_rand.NextDouble() * 1.8f,
                        BaseBrightness = 0.35f + (float)_rand.NextDouble() * 0.65f,
                        Tint = tint,
                        FlickerSeed = (float)_rand.NextDouble() * 1000f,
                        FlickerRate = 0.5f + (float)_rand.NextDouble() * 2.2f
                    };
                }
            }

            if (_nebulaWisps == null)
            {
                _nebulaWisps = new NebulaWisp[NebulaWispCount];
                for (int i = 0; i < NebulaWispCount; i++)
                {
                    _nebulaWisps[i] = new NebulaWisp
                    {
                        NormalizedAnchor = new Vector2((float)_rand.NextDouble(), (float)_rand.NextDouble()),
                        DriftRadius = 0.03f + (float)_rand.NextDouble() * 0.05f,
                        DriftSpeed = 0.05f + (float)_rand.NextDouble() * 0.08f,
                        DriftPhase = (float)(_rand.NextDouble() * MathHelper.TwoPi),
                        Scale = 2.2f + (float)_rand.NextDouble() * 2.6f,
                        Alpha = 0.10f + (float)_rand.NextDouble() * 0.10f,
                        Color = NebulaPalette[_rand.Next(NebulaPalette.Length)]
                    };
                }
            }

            if (_galaxyPoints == null)
                BuildGalaxyPoints();

            if (_dust == null)
                BuildDust();
        }

        private static void BuildGalaxyPoints()
        {
            _galaxyPoints = new GalaxyPoint[Arms * PointsPerArm + CorePointCount];
            int idx = 0;

            for (int arm = 0; arm < Arms; arm++)
            {
                float baseAngle = (arm / (float)Arms) * MathHelper.TwoPi;

                for (int i = 0; i < PointsPerArm; i++)
                {
                    float t = i / (float)PointsPerArm;

                    float jitter = ((float)_rand.NextDouble() - 0.5f) * 0.5f;
                    float wind = baseAngle + t * 3.1f + jitter;

                    float scatter = 0.06f + t * 0.20f;
                    float ox = ((float)_rand.NextDouble() - 0.5f) * scatter;
                    float oy = ((float)_rand.NextDouble() - 0.5f) * scatter * 0.55f;

                    float ux = (float)Math.Cos(wind) * t + ox;
                    float uy = (float)Math.Sin(wind) * t + oy * 0.9f;

                    Color color;
                    float alpha;
                    float size;

                    if (t < 0.15f)
                    {
                        color = new Color(255, (int)(215 + _rand.NextDouble() * 40), (int)(100 + _rand.NextDouble() * 80));
                        alpha = 0.75f + (float)_rand.NextDouble() * 0.25f;
                        size = 0.8f + (float)_rand.NextDouble() * 1.6f;
                    }
                    else if (t < 0.45f)
                    {
                        float b = (t - 0.15f) / 0.3f;
                        color = new Color(
                            (int)(255 - b * 130),
                            (int)(200 - b * 120),
                            (int)(130 + b * 110));
                        alpha = 0.5f + (float)_rand.NextDouble() * 0.4f;
                        size = 0.5f + (float)_rand.NextDouble() * 1.2f;
                    }
                    else
                    {
                        color = new Color(
                            (int)(70 + _rand.NextDouble() * 80),
                            (int)(90 + _rand.NextDouble() * 80),
                            (int)(200 + _rand.NextDouble() * 55));
                        alpha = 0.22f + (float)_rand.NextDouble() * 0.38f;
                        size = 0.35f + (float)_rand.NextDouble() * 0.85f;
                    }

                    float angVelFactor = 1f / (0.12f + t * 0.88f);

                    _galaxyPoints[idx++] = new GalaxyPoint
                    {
                        Ux = ux,
                        Uy = uy,
                        AngVelFactor = angVelFactor,
                        Size = size,
                        Alpha = alpha,
                        Color = color
                    };
                }
            }

            for (int i = 0; i < CorePointCount; i++)
            {
                float ang = (float)(_rand.NextDouble() * MathHelper.TwoPi);
                float dist = (float)Math.Pow(_rand.NextDouble(), 2.8) * 0.14f;

                float ux = (float)Math.Cos(ang) * dist;
                float uy = (float)Math.Sin(ang) * dist * 0.7f;

                _galaxyPoints[idx++] = new GalaxyPoint
                {
                    Ux = ux,
                    Uy = uy,
                    AngVelFactor = 1f / 0.12f,
                    Size = 0.6f + (float)_rand.NextDouble() * 1.9f,
                    Alpha = 0.65f + (float)_rand.NextDouble() * 0.35f,
                    Color = new Color(255, (int)(220 + _rand.NextDouble() * 35), (int)(130 + _rand.NextDouble() * 70))
                };
            }
        }

        private static void BuildDust()
        {
            _dust = new DustMote[DustCount];
            for (int i = 0; i < DustCount; i++)
            {
                float t = 0.3f + (float)Math.Pow(_rand.NextDouble(), 1.4) * 0.65f;
                float direction = _rand.NextDouble() < 0.5 ? 1f : -1f;

                _dust[i] = new DustMote
                {
                    Angle = (float)(_rand.NextDouble() * MathHelper.TwoPi),
                    Dist = t,
                    Speed = (0.05f + (float)_rand.NextDouble() * 0.10f) * direction,
                    Size = 1.5f + (float)_rand.NextDouble() * 3.5f,
                    Alpha = 0.07f + (float)_rand.NextDouble() * 0.18f,
                    Color = new Color(
                        (int)(140 + _rand.NextDouble() * 80),
                        (int)(80 + _rand.NextDouble() * 70),
                        (int)(200 + _rand.NextDouble() * 55))
                };
            }
        }

        private static void SpawnShootingStar(float baseScale)
        {
            if (_shootingStars.Count >= MaxShootingStars)
                return;

            float startX = (float)_rand.NextDouble() * Main.screenWidth;
            float startY = (float)_rand.NextDouble() * Main.screenHeight * 0.5f;
            float speed = (220f + (float)_rand.NextDouble() * 260f) * baseScale;
            float angle = MathHelper.ToRadians(18f + (float)_rand.NextDouble() * 16f);

            _shootingStars.Add(new ShootingStar
            {
                Position = new Vector2(startX, startY),
                Velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed,
                Life = 0f,
                MaxLife = 0.7f + (float)_rand.NextDouble() * 0.5f
            });
        }

        /// <summary>
        /// Ngitung delta time sejak Draw() terakhir dipanggil, pakai Stopwatch karena
        /// ModMenu.PreDrawLogo tidak dikasih GameTime. Kalau menu ini baru pertama kali
        /// digambar (atau baru dibuka lagi setelah lama), delta di-clamp supaya tidak "meloncat".
        /// </summary>
        private static float ConsumeDelta()
        {
            double now = _stopwatch.Elapsed.TotalSeconds;
            double delta = now - _lastElapsedSeconds;
            _lastElapsedSeconds = now;

            if (delta < 0d || delta > MaxDelta)
                delta = MaxDelta;

            return (float)delta;
        }

        private static void UpdateState(float delta, float baseScale)
        {
            _time += delta;
            _armRotation += delta * ArmRotationSpeed;
            _hazeRotation += delta * 0.02f;

            for (int i = 0; i < _particles.Length; i++)
            {
                SuctionParticle p = _particles[i];
                p.Angle += delta * p.Speed;
                p.Radius -= delta * (18f + p.Speed * 40f);

                if (p.Radius < 4f)
                {
                    p.Radius = 700f + (float)_rand.NextDouble() * 120f;
                    p.Angle = (float)(_rand.NextDouble() * MathHelper.TwoPi);
                    p.Speed = 0.15f + (float)_rand.NextDouble() * 0.35f;
                    p.Scale = 0.4f + (float)_rand.NextDouble() * 0.9f;
                }

                _particles[i] = p;
            }

            for (int i = 0; i < _dust.Length; i++)
                _dust[i].Angle += delta * _dust[i].Speed;

            _nextShootTimer -= delta;
            if (_nextShootTimer <= 0f)
            {
                SpawnShootingStar(baseScale);
                _nextShootTimer = 3.5f + (float)_rand.NextDouble() * 5.5f;
            }

            for (int i = _shootingStars.Count - 1; i >= 0; i--)
            {
                ShootingStar s = _shootingStars[i];
                s.Position += s.Velocity * delta;
                s.Life += delta;

                if (s.Life >= s.MaxLife)
                    _shootingStars.RemoveAt(i);
                else
                    _shootingStars[i] = s;
            }
        }

        /// <summary>
        /// Gambar background galaxy penuh satu layar. Panggil ini dari dalam PreDrawLogo
        /// SEBELUM logo digambar (jangan Begin/End spriteBatch lagi di luar method ini,
        /// method ini yang mengurus siklus End/Begin-nya sendiri dan akan meninggalkan
        /// spriteBatch dalam keadaan Begin(AlphaBlend) supaya aman dipakai kode setelahnya).
        /// </summary>
        public static void Draw(SpriteBatch spriteBatch)
        {
            EnsureInitialized();

            Vector2 screenSize = new Vector2(Main.screenWidth, Main.screenHeight);
            Vector2 screenCenter = screenSize * 0.5f;
            float baseScale = Math.Max(Main.screenWidth, Main.screenHeight) / 900f;
            float armRadius = ArmRadiusPixels * baseScale;

            float delta = ConsumeDelta();
            UpdateState(delta, baseScale);

            // Tutup batch yang sedang dipakai menu (biasanya AlphaBlend polos, sama seperti
            // dulu dipakai buat gambar TwilightBG langsung), supaya kita bisa ganti-ganti
            // blend state untuk lapisan-lapisan galaxy.
            spriteBatch.End();

            // 0) WASH: warna dasar ungu <-> biru gelap yang beranimasi, mengisi seluruh layar
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            float washT = (float)(Math.Sin(_time * 0.12) * 0.5 + 0.5);
            Color washColor = Color.Lerp(BackgroundDeepPurple, BackgroundDeepBlue, washT);
            spriteBatch.Draw(_solidPixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), washColor);

            spriteBatch.End();

            // Sisa layer digambar additive di atas wash tadi
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            Vector2 glowOrigin = new Vector2(_softGlow.Width, _softGlow.Height) * 0.5f;
            Vector2 hazeOrigin = new Vector2(_galaxyHaze.Width, _galaxyHaze.Height) * 0.5f;
            Vector2 particleOrigin = new Vector2(_particleTex.Width, _particleTex.Height) * 0.5f;
            Vector2 streakOrigin = new Vector2(_streakTex.Width, _streakTex.Height * 0.5f);

            // 1) Nebula wisps
            for (int i = 0; i < _nebulaWisps.Length; i++)
            {
                NebulaWisp w = _nebulaWisps[i];
                float driftAngle = _time * w.DriftSpeed + w.DriftPhase;
                Vector2 driftOffset = new Vector2((float)Math.Cos(driftAngle), (float)Math.Sin(driftAngle * 0.7f)) * w.DriftRadius;
                Vector2 pos = (w.NormalizedAnchor + driftOffset) * screenSize;

                spriteBatch.Draw(_softGlow, pos, null, w.Color * w.Alpha, 0f,
                    glowOrigin, baseScale * w.Scale, SpriteEffects.None, 0f);
            }

            // 2) Starfield dengan kelap-kelip acak (twinkle sinus + noise + sesekali dip tajam)
            for (int i = 0; i < _stars.Length; i++)
            {
                Star s = _stars[i];
                float twinkle = 0.5f + 0.5f * (float)Math.Sin(_time * s.TwinkleSpeed + s.TwinklePhase);
                float flicker = FlickerNoise(_time * s.FlickerRate + s.FlickerSeed);
                float sharpDip = Hash11(s.FlickerSeed * 7.13f + (float)Math.Floor(_time * s.FlickerRate * 2.3f));
                float dipFactor = sharpDip > 0.92f ? 0.25f : 1f;

                float brightness = s.BaseBrightness
                    * MathHelper.Lerp(0.3f, 1f, twinkle)
                    * MathHelper.Lerp(0.35f, 1f, flicker)
                    * dipFactor;

                Vector2 pos = s.NormalizedPos * screenSize;
                spriteBatch.Draw(_particleTex, pos, null, s.Tint * brightness, 0f,
                    particleOrigin, s.Scale * baseScale, SpriteEffects.None, 0f);
            }

            // 3) Ring glow tipis di sekitar inti
            for (int ring = 2; ring >= 1; ring--)
            {
                float ringScale = baseScale * (armRadius / 100f) * (0.5f + ring * 0.6f);
                float ringAlpha = (0.06f / ring) * GlowIntensity;
                Color ringColor = Color.Lerp(RingColorInner, RingColorOuter, ring / 2f);
                spriteBatch.Draw(_softGlow, screenCenter, null, ringColor * ringAlpha, 0f,
                    glowOrigin, ringScale, SpriteEffects.None, 0f);
            }

            // 4) Halo terluar (tipis, biar tidak nutupin bentuk spiral)
            spriteBatch.Draw(_softGlow, screenCenter, null, EdgeColor * (0.35f * GlowIntensity), 0f,
                glowOrigin, baseScale * 2.6f, SpriteEffects.None, 0f);
            spriteBatch.Draw(_softGlow, screenCenter, null, OuterColor * (0.4f * GlowIntensity), 0f,
                glowOrigin, baseScale * 1.9f, SpriteEffects.None, 0f);

            // 5) Kabut spiral lembut di belakang titik-titik
            spriteBatch.Draw(_galaxyHaze, screenCenter, null, Color.White * 0.28f, _hazeRotation,
                hazeOrigin, (armRadius * 2f) / _galaxyHaze.Width, SpriteEffects.None, 0f);

            // 6) Debu kosmik
            for (int i = 0; i < _dust.Length; i++)
            {
                DustMote d = _dust[i];
                float finalAngle = d.Angle + _armRotation;
                Vector2 offset = new Vector2((float)Math.Cos(finalAngle), (float)Math.Sin(finalAngle) * 0.48f) * d.Dist * armRadius;

                spriteBatch.Draw(_particleTex, screenCenter + offset, null, d.Color * d.Alpha, 0f,
                    particleOrigin, d.Size * baseScale * 0.5f, SpriteEffects.None, 0f);
            }

            // 7) Lengan spiral & inti (rotasi diferensial)
            for (int i = 0; i < _galaxyPoints.Length; i++)
            {
                GalaxyPoint gp = _galaxyPoints[i];
                float localRot = _armRotation * gp.AngVelFactor;
                float cos = (float)Math.Cos(localRot);
                float sin = (float)Math.Sin(localRot);
                float wx = gp.Ux * cos - gp.Uy * sin;
                float wy = gp.Ux * sin + gp.Uy * cos;

                Vector2 pos = screenCenter + new Vector2(wx, wy) * armRadius;
                spriteBatch.Draw(_particleTex, pos, null, gp.Color * gp.Alpha, 0f,
                    particleOrigin, gp.Size * baseScale, SpriteEffects.None, 0f);
            }

            // 8) Inti terang bertingkat (3 lapis, tidak berlebihan)
            spriteBatch.Draw(_softGlow, screenCenter, null, InnerColor * (0.45f * GlowIntensity), 0f,
                glowOrigin, baseScale * 0.65f, SpriteEffects.None, 0f);
            spriteBatch.Draw(_softGlow, screenCenter, null, CenterColor * 0.7f, 0f,
                glowOrigin, baseScale * 0.34f, SpriteEffects.None, 0f);
            spriteBatch.Draw(_softGlow, screenCenter, null, Color.White * 0.85f, 0f,
                glowOrigin, baseScale * 0.16f, SpriteEffects.None, 0f);

            // 9) Ray beam tipis yang berputar
            float streakAngle = _armRotation * 0.6f;
            for (int i = 0; i < 4; i++)
            {
                float a = (i / 4f) * MathHelper.Pi + streakAngle;
                Vector2 rayScale = new Vector2(baseScale * armRadius * 0.008f, baseScale * 0.025f);
                spriteBatch.Draw(_softGlow, screenCenter, null, InnerColor * (0.07f * GlowIntensity), a,
                    glowOrigin, rayScale, SpriteEffects.None, 0f);
            }

            // 10) Partikel hisapan menuju pusat
            for (int i = 0; i < _particles.Length; i++)
            {
                SuctionParticle p = _particles[i];
                Vector2 offset = new Vector2((float)Math.Cos(p.Angle), (float)Math.Sin(p.Angle)) * p.Radius * baseScale;
                float fadeByRadius = MathHelper.Clamp(p.Radius / 700f, 0.15f, 1f);

                spriteBatch.Draw(_particleTex, screenCenter + offset, null, p.Color * fadeByRadius,
                    p.Angle, particleOrigin, p.Scale * baseScale, SpriteEffects.None, 0f);
            }

            // 11) Bintang jatuh
            for (int i = 0; i < _shootingStars.Count; i++)
            {
                ShootingStar s = _shootingStars[i];
                float progress = s.Life / s.MaxLife;
                float alpha = (1f - progress) * 0.85f;

                float trailLength = 90f * baseScale + s.Velocity.Length() * 0.05f;
                float rotation = (float)Math.Atan2(s.Velocity.Y, s.Velocity.X);
                Vector2 scale = new Vector2(trailLength / _streakTex.Width, 1.4f * baseScale);

                spriteBatch.Draw(_streakTex, s.Position, null, Color.White * alpha, rotation,
                    streakOrigin, scale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();

            // 12) Vignette + tetap dibiarkan Begin(AlphaBlend) supaya kode menu setelahnya
            // (gambar logo, dsb) tetap aman melanjutkan draw di batch ini.
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            Vector2 vignetteOrigin = new Vector2(_vignetteTex.Width, _vignetteTex.Height) * 0.5f;
            Vector2 vignetteScale = new Vector2(screenSize.X / _vignetteTex.Width, screenSize.Y / _vignetteTex.Height);
            Color vignetteColor = Color.Lerp(Color.Black, EdgeColor, 0.5f);
            spriteBatch.Draw(_vignetteTex, screenCenter, null, vignetteColor * 0.5f, 0f,
                vignetteOrigin, vignetteScale, SpriteEffects.None, 0f);
        }
    }
}
