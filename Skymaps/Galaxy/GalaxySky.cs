using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;

namespace TheSanity.Content.Skies
{
    /// <summary>
    /// Background luar angkasa bertema spiral galaxy, dibuat mengikuti gaya referensi
    /// (spiral berbasis titik dengan gradasi warna emas -> ungu -> biru, rotasi diferensial,
    /// nebula, debu kosmik, starfield berkedip, dan bintang jatuh sesekali), plus vignette
    /// di tepi layar supaya lebih dramatis.
    ///
    /// "Rotasi diferensial" = titik-titik yang lebih dekat ke pusat berputar lebih cepat
    /// daripada yang di pinggir (seperti galaxy asli), bukan berputar seragam sebagai satu
    /// tekstur kaku. Ini yang bikin gerakannya terasa lebih hidup dibanding versi lama.
    ///
    /// Sepenuhnya prosedural (lihat GalaxyTextureGenerator) -> ringan, tidak butuh asset gambar.
    /// Semua elemen yang jumlahnya banyak (bintang, titik spiral, debu, partikel) pakai
    /// tekstur yang SAMA per kategori, jadi SpriteBatch bisa membatch-nya jadi sedikit
    /// draw call GPU walau kelihatannya ramai.
    /// </summary>
    public class GalaxySky : CustomSky
    {
        private bool _isActive;
        private float _fade;        // 0..1, untuk transisi fade in/out yang halus
        private float _armRotation; // akumulasi rotasi untuk titik-titik lengan spiral & inti (searah jarum jam)
        private float _hazeRotation; // rotasi lambat untuk lapisan kabut di belakang (subtle, jangan terlalu cepat)
        private float _time;
        private float _nextShootTimer;

        // Tekstur di-cache di level static supaya hanya dibuat SEKALI walau sky di-Load ulang
        private static Texture2D _galaxyHaze;
        private static Texture2D _softGlow;
        private static Texture2D _particleTex;
        private static Texture2D _solidPixel;
        private static Texture2D _streakTex;
        private static Texture2D _vignetteTex;

        // Warna inti / halo (dipakai untuk bloom bertingkat di tengah)
        private static readonly Color CenterColor = new Color(255, 248, 225); // putih keemasan hangat
        private static readonly Color InnerColor = new Color(255, 200, 140);  // emas/oranye
        private static readonly Color MidColor = new Color(180, 100, 235);    // ungu-pink
        private static readonly Color OuterColor = new Color(35, 45, 130);    // biru gelap
        private static readonly Color EdgeColor = new Color(24, 8, 42);       // dark blue + sentuhan dark purple

        // Warna untuk "wash" full-screen (background di belakang galaxy)
        private static readonly Color BackgroundDeepPurple = new Color(24, 8, 40);
        private static readonly Color BackgroundDeepBlue = new Color(6, 10, 32);

        // Palet nebula wisps (awan warna-warni yang melayang pelan)
        private static readonly Color[] NebulaPalette =
        {
            new Color(180, 60, 235),  // ungu
            new Color(60, 100, 230),  // biru-ungu
            new Color(150, 90, 220),  // magenta lembut
            new Color(50, 70, 210),   // biru
        };

        // Warna untuk ring glow raksasa di sekitar inti (emas -> ungu, meniru cincin cahaya referensi)
        private static readonly Color RingColorInner = new Color(255, 205, 110);
        private static readonly Color RingColorOuter = new Color(150, 85, 225);

        private const int ParticleCount = 80;    // partikel "hisapan" (suction) menuju pusat
        private const int StarCount = 160;       // bintang starfield di seluruh layar
        private const int NebulaWispCount = 5;

        private const int Arms = 3;              // jumlah lengan spiral
        private const int PointsPerArm = 70;      // titik per lengan (diturunkan demi performa)
        private const int CorePointCount = 50;    // gugusan titik terang di pusat
        private const int DustCount = 35;         // debu kosmik yang melayang di sekitar galaxy
        private const int MaxShootingStars = 2;   // batas jumlah bintang jatuh yang aktif bersamaan

        private const float ArmRadiusPixels = 300f; // "jari-jari" spiral dalam piksel pada baseScale = 1
        private const float ArmRotationSpeed = 0.05f; // rad/detik, kecepatan rotasi dasar (titik terluar)

        // Pengali global untuk semua lapisan cahaya/bloom. Turunkan angka ini kalau masih
        // kesilauan/kurang terlihat bentuk galaxy-nya; naikkan kalau mau lebih dramatis.
        private const float GlowIntensity = 0.45f;

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
            public Vector2 NormalizedPos; // 0..1 relatif terhadap layar, biar otomatis menyesuaikan resolusi
            public float Scale;
            public float TwinklePhase;
            public float TwinkleSpeed;
            public float BaseBrightness;
            public Color Tint;

            // Dua field ini yang bikin kelap-kelipnya kerasa ACAK, bukan cuma naik-turun
            // mulus kayak gelombang sinus. Tiap bintang punya "seed" & "kecepatan noise"
            // sendiri, jadi timing kedip masing-masing beda-beda & nggak keliatan pola.
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

        /// <summary>
        /// Satu titik pada lengan spiral ATAU gugusan inti. Posisinya disimpan dalam
        /// "unit space" (radius 1 = ArmRadiusPixels * baseScale) SEBELUM dirotasi, supaya
        /// tinggal dikali skala layar tiap frame dan dirotasi sesuai AngVelFactor masing-masing
        /// (itu yang menghasilkan efek rotasi diferensial).
        /// </summary>
        private struct GalaxyPoint
        {
            public float Ux, Uy;        // posisi dasar (unit-space, sebelum rotasi)
            public float AngVelFactor;  // makin besar = makin cepat berputar (titik dekat pusat = besar)
            public float Size;
            public float Alpha;
            public Color Color;
        }

        private struct DustMote
        {
            public float Angle;      // sudut orbit sendiri (independen, ditambah rotasi galaxy saat digambar)
            public float Dist;       // jarak dari pusat (unit-space)
            public float Speed;      // kecepatan orbit sendiri, bisa positif/negatif (arah campur)
            public float Size;
            public float Alpha;
            public Color Color;
        }

        private struct ShootingStar
        {
            public Vector2 Position;
            public Vector2 Velocity; // piksel/detik, arah + kecepatan jejak
            public float Life;
            public float MaxLife;
        }

        private SuctionParticle[] _particles;
        private Star[] _stars;
        private NebulaWisp[] _nebulaWisps;
        private GalaxyPoint[] _galaxyPoints; // lengan spiral + gugusan inti digabung jadi satu array
        private DustMote[] _dust;
        private readonly List<ShootingStar> _shootingStars = new List<ShootingStar>();
        private readonly Random _rand = new Random(12345);

        /// <summary>
        /// Noise 1D murah (value noise berbasis hash sin) buat efek kelap-kelip acak.
        /// Bukan random betulan (deterministik dari input n), tapi hasilnya kelihatan
        /// acak & konsisten tiap frame tanpa perlu nyimpen state tambahan.
        /// </summary>
        private static float Hash11(float n)
        {
            float s = (float)Math.Sin(n) * 43758.5453f;
            return s - (float)Math.Floor(s);
        }

        /// <summary>
        /// Value noise 1D: pilih dua titik hash bertetangga lalu di-interpolasi halus (smoothstep).
        /// Hasilnya "acak" tapi transisinya tetap mulus (nggak meloncat-loncat kasar tiap frame).
        /// </summary>
        private static float FlickerNoise(float t)
        {
            float ti = (float)Math.Floor(t);
            float tf = t - ti;
            float a = Hash11(ti);
            float b = Hash11(ti + 1f);
            float smooth = tf * tf * (3f - 2f * tf);
            return MathHelper.Lerp(a, b, smooth);
        }

        /// <summary>
        /// Inisialisasi "malas" (lazy): dipanggil sendiri saat pertama kali dibutuhkan,
        /// bukan lewat override Load() -> CustomSky/GameEffect di versi tModLoader ini
        /// tidak punya method Load() yang bisa di-override.
        /// </summary>
        private void EnsureInitialized()
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

        /// <summary>
        /// Membangun titik-titik lengan spiral (dengan gradasi warna emas -> ungu -> biru
        /// mengikuti jaraknya dari pusat) dan gugusan titik terang di inti.
        /// </summary>
        private void BuildGalaxyPoints()
        {
            _galaxyPoints = new GalaxyPoint[Arms * PointsPerArm + CorePointCount];
            int idx = 0;

            for (int arm = 0; arm < Arms; arm++)
            {
                float baseAngle = (arm / (float)Arms) * MathHelper.TwoPi;

                for (int i = 0; i < PointsPerArm; i++)
                {
                    float t = i / (float)PointsPerArm; // 0 = pusat, 1 = tepi

                    // Lilitan spiral logaritmik-ish + sedikit jitter biar tidak terlalu "rapi"
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
                        // Dekat pusat: emas hangat
                        color = new Color(255, (int)(215 + _rand.NextDouble() * 40), (int)(100 + _rand.NextDouble() * 80));
                        alpha = 0.75f + (float)_rand.NextDouble() * 0.25f;
                        size = 0.8f + (float)_rand.NextDouble() * 1.6f;
                    }
                    else if (t < 0.45f)
                    {
                        // Transisi: emas -> ungu/pink
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
                        // Tepi luar: biru
                        color = new Color(
                            (int)(70 + _rand.NextDouble() * 80),
                            (int)(90 + _rand.NextDouble() * 80),
                            (int)(200 + _rand.NextDouble() * 55));
                        alpha = 0.22f + (float)_rand.NextDouble() * 0.38f;
                        size = 0.35f + (float)_rand.NextDouble() * 0.85f;
                    }

                    // Rotasi diferensial: makin dekat pusat (t kecil), makin cepat berputar
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

            // Gugusan titik terang di inti: tersebar mendekati pusat dengan distribusi power
            // (lebih padat makin dekat ke tengah), warna emas hangat, berputar paling cepat.
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
                    AngVelFactor = 1f / 0.12f, // paling cepat, konsisten dengan formula di t=0
                    Size = 0.6f + (float)_rand.NextDouble() * 1.9f,
                    Alpha = 0.65f + (float)_rand.NextDouble() * 0.35f,
                    Color = new Color(255, (int)(220 + _rand.NextDouble() * 35), (int)(130 + _rand.NextDouble() * 70))
                };
            }
        }

        private void BuildDust()
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

        private void SpawnShootingStar(float baseScale)
        {
            if (_shootingStars.Count >= MaxShootingStars)
                return;

            float startX = (float)_rand.NextDouble() * Main.screenWidth;
            float startY = (float)_rand.NextDouble() * Main.screenHeight * 0.5f;
            float speed = (220f + (float)_rand.NextDouble() * 260f) * baseScale;
            float angle = MathHelper.ToRadians(18f + (float)_rand.NextDouble() * 16f); // agak ke bawah-kanan

            _shootingStars.Add(new ShootingStar
            {
                Position = new Vector2(startX, startY),
                Velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed,
                Life = 0f,
                MaxLife = 0.7f + (float)_rand.NextDouble() * 0.5f
            });
        }

        public override void Update(GameTime gameTime)
        {
            EnsureInitialized();

            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += delta;

            // Rotasi searah jarum jam (nilai positif = clockwise di ruang layar).
            _armRotation += delta * ArmRotationSpeed;
            _hazeRotation += delta * 0.02f; // kabut di belakang berputar jauh lebih lambat, cuma pemanis

            if (_isActive && _fade < 1f)
                _fade = Math.Min(1f, _fade + delta * 0.6f);
            else if (!_isActive && _fade > 0f)
                _fade = Math.Max(0f, _fade - delta * 0.6f);

            // Update partikel "hisapan": berputar searah jarum jam sambil perlahan tertarik ke pusat,
            // sampai benar-benar dekat titik pusat sebelum respawn.
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

            // Debu kosmik: tiap partikel punya kecepatan orbit sendiri (independen dari rotasi galaxy)
            for (int i = 0; i < _dust.Length; i++)
                _dust[i].Angle += delta * _dust[i].Speed;

            // Bintang jatuh: spawn berkala, hanya kalau sky sedang terlihat (hemat kerja saat fade = 0)
            if (_fade > 0.05f)
            {
                _nextShootTimer -= delta;
                if (_nextShootTimer <= 0f)
                {
                    float baseScale = Math.Max(Main.screenWidth, Main.screenHeight) / 900f;
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
        }

        public override Color OnTileColor(Color inColor) => inColor;

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
        {
            if (_fade <= 0f)
                return;

            EnsureInitialized();

            if (!(minDepth <= 0f && maxDepth >= 0f))
                return;

            Vector2 screenSize = new Vector2(Main.screenWidth, Main.screenHeight);
            Vector2 screenCenter = screenSize * 0.5f;
            float baseScale = Math.Max(Main.screenWidth, Main.screenHeight) / 900f;
            float armRadius = ArmRadiusPixels * baseScale;

            spriteBatch.End();

            // 0) BACKGROUND WASH: mengisi SELURUH layar dengan warna ungu <-> biru gelap
            // yang beranimasi (bukan tergantung jam in-game).
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            float washT = (float)(Math.Sin(_time * 0.12) * 0.5 + 0.5);
            Color washColor = Color.Lerp(BackgroundDeepPurple, BackgroundDeepBlue, washT);
            spriteBatch.Draw(_solidPixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), washColor * _fade);

            spriteBatch.End();

            // Sisa layer digambar additive di atas wash tadi
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            Vector2 glowOrigin = new Vector2(_softGlow.Width, _softGlow.Height) * 0.5f;
            Vector2 hazeOrigin = new Vector2(_galaxyHaze.Width, _galaxyHaze.Height) * 0.5f;
            Vector2 particleOrigin = new Vector2(_particleTex.Width, _particleTex.Height) * 0.5f;
            Vector2 streakOrigin = new Vector2(_streakTex.Width, _streakTex.Height * 0.5f); // origin di ujung "kepala" (kanan)

            // 1) NEBULA WISPS: awan warna-warni lembut yang melayang pelan di berbagai titik layar.
            for (int i = 0; i < _nebulaWisps.Length; i++)
            {
                NebulaWisp w = _nebulaWisps[i];
                float driftAngle = _time * w.DriftSpeed + w.DriftPhase;
                Vector2 driftOffset = new Vector2((float)Math.Cos(driftAngle), (float)Math.Sin(driftAngle * 0.7f)) * w.DriftRadius;
                Vector2 pos = (w.NormalizedAnchor + driftOffset) * screenSize;

                spriteBatch.Draw(_softGlow, pos, null, w.Color * (w.Alpha * _fade), 0f,
                    glowOrigin, baseScale * w.Scale, SpriteEffects.None, 0f);
            }

            // 2) STARFIELD: bintang-bintang kecil tersebar di seluruh layar, berkedip pelan.
            for (int i = 0; i < _stars.Length; i++)
            {
                Star s = _stars[i];

                // Lapisan 1: twinkle mulus (sinus) seperti sebelumnya -> naik-turun pelan.
                float twinkle = 0.5f + 0.5f * (float)Math.Sin(_time * s.TwinkleSpeed + s.TwinklePhase);

                // Lapisan 2: noise acak per-bintang -> ini yang bikin sebagian bintang tiba-tiba
                // meredup/menyala nggak beraturan, beda-beda waktu & kecepatan tiap bintang,
                // jadi keliatan "kelap-kelip acak" beneran, bukan berkedip serempak/berpola.
                float flicker = FlickerNoise(_time * s.FlickerRate + s.FlickerSeed);

                // Sesekali kasih "kedipan tajam" (dip mendadak) supaya makin kerasa alami,
                // kayak scintillation bintang asli -> bukan cuma naik-turun halus terus-terusan.
                float sharpDip = Hash11(s.FlickerSeed * 7.13f + (float)Math.Floor(_time * s.FlickerRate * 2.3f));
                float dipFactor = sharpDip > 0.92f ? 0.25f : 1f;

                float brightness = s.BaseBrightness
                    * MathHelper.Lerp(0.3f, 1f, twinkle)
                    * MathHelper.Lerp(0.35f, 1f, flicker)
                    * dipFactor;

                Vector2 pos = s.NormalizedPos * screenSize;
                spriteBatch.Draw(_particleTex, pos, null, s.Tint * (brightness * _fade), 0f,
                    particleOrigin, s.Scale * baseScale, SpriteEffects.None, 0f);
            }

            // 3) RING GLOW: cuma 2 cincin tipis di sekitar inti, emas -> ungu, sekadar
            // kedalaman halus sebelum kabut & titik-titik spiral digambar di atasnya.
            for (int ring = 2; ring >= 1; ring--)
            {
                float ringScale = baseScale * (armRadius / 100f) * (0.5f + ring * 0.6f);
                float ringAlpha = (0.06f / ring) * _fade * GlowIntensity;
                Color ringColor = Color.Lerp(RingColorInner, RingColorOuter, ring / 2f);
                spriteBatch.Draw(_softGlow, screenCenter, null, ringColor * ringAlpha, 0f,
                    glowOrigin, ringScale, SpriteEffects.None, 0f);
            }

            // 4) Halo terluar: dark blue + sentuhan dark purple. Ukuran & alpha jauh diturunkan
            // dibanding versi sebelumnya -> dulu ini yang bikin seluruh layar jadi silau/putih
            // dan menutupi bentuk spiral-nya.
            spriteBatch.Draw(_softGlow, screenCenter, null, EdgeColor * (0.35f * _fade * GlowIntensity), 0f,
                glowOrigin, baseScale * 2.6f, SpriteEffects.None, 0f);
            spriteBatch.Draw(_softGlow, screenCenter, null, OuterColor * (0.4f * _fade * GlowIntensity), 0f,
                glowOrigin, baseScale * 1.9f, SpriteEffects.None, 0f);

            // 5) Kabut spiral lembut di belakang titik-titik (rotasi sangat lambat, cuma pemanis tekstur).
            // Alpha diturunkan supaya titik-titik spiral di atasnya tetap jelas kelihatan, bukan ketutupan kabut.
            spriteBatch.Draw(_galaxyHaze, screenCenter, null, Color.White * (0.28f * _fade), _hazeRotation,
                hazeOrigin, (armRadius * 2f) / _galaxyHaze.Width, SpriteEffects.None, 0f);

            // 7) DEBU KOSMIK: titik-titik redup yang mengorbit pelan, membaur di antara lengan spiral
            for (int i = 0; i < _dust.Length; i++)
            {
                DustMote d = _dust[i];
                float finalAngle = d.Angle + _armRotation;
                Vector2 offset = new Vector2((float)Math.Cos(finalAngle), (float)Math.Sin(finalAngle) * 0.48f) * d.Dist * armRadius;

                spriteBatch.Draw(_particleTex, screenCenter + offset, null, d.Color * (d.Alpha * _fade), 0f,
                    particleOrigin, d.Size * baseScale * 0.5f, SpriteEffects.None, 0f);
            }

            // 8) LENGAN SPIRAL & INTI: titik-titik utama galaxy, dengan rotasi diferensial
            // (titik dekat pusat berputar lebih cepat daripada yang di pinggir).
            for (int i = 0; i < _galaxyPoints.Length; i++)
            {
                GalaxyPoint gp = _galaxyPoints[i];
                float localRot = _armRotation * gp.AngVelFactor;
                float cos = (float)Math.Cos(localRot);
                float sin = (float)Math.Sin(localRot);
                float wx = gp.Ux * cos - gp.Uy * sin;
                float wy = gp.Ux * sin + gp.Uy * cos;

                Vector2 pos = screenCenter + new Vector2(wx, wy) * armRadius;
                spriteBatch.Draw(_particleTex, pos, null, gp.Color * (gp.Alpha * _fade), 0f,
                    particleOrigin, gp.Size * baseScale, SpriteEffects.None, 0f);

                // CATATAN: dulu di sini ada glow tambahan per-titik (extra Draw call untuk tiap
                // titik yang cukup besar/terang). Itu dihapus karena dua alasan: (1) nambah
                // ratusan draw call ekstra tiap frame -> lag, dan (2) glow-nya numpuk-numpuk
                // jadi kabut putih yang justru MENUTUPI bentuk spiral, bukan mempercantiknya.
                // Titik individual sekarang dibiarkan tajam & jelas.
            }

            // 9) Inti terang di tengah, disederhanakan jadi 3 lapis (dulu 5) dan alpha/ukuran
            // diturunkan supaya tidak jadi bola putih raksasa yang menelan seluruh galaxy.
            spriteBatch.Draw(_softGlow, screenCenter, null, InnerColor * (0.45f * _fade * GlowIntensity), 0f,
                glowOrigin, baseScale * 0.65f, SpriteEffects.None, 0f);
            spriteBatch.Draw(_softGlow, screenCenter, null, CenterColor * (0.7f * _fade), 0f,
                glowOrigin, baseScale * 0.34f, SpriteEffects.None, 0f);
            spriteBatch.Draw(_softGlow, screenCenter, null, Color.White * (0.85f * _fade), 0f,
                glowOrigin, baseScale * 0.16f, SpriteEffects.None, 0f);

            // 10) RAY BEAM: 4 pancaran cahaya sangat tipis yang berputar melewati inti (kesan "megah"
            // tapi tidak menyilaukan -> alpha jauh diturunkan dari versi sebelumnya)
            float streakAngle = _armRotation * 0.6f;
            for (int i = 0; i < 4; i++)
            {
                float a = (i / 4f) * MathHelper.Pi + streakAngle;
                Vector2 rayScale = new Vector2(baseScale * armRadius * 0.008f, baseScale * 0.025f);
                spriteBatch.Draw(_softGlow, screenCenter, null, InnerColor * (0.07f * _fade * GlowIntensity), a,
                    glowOrigin, rayScale, SpriteEffects.None, 0f);
            }

            // 11) Partikel yang berputar searah jarum jam & "tersedot" menuju pusat
            for (int i = 0; i < _particles.Length; i++)
            {
                SuctionParticle p = _particles[i];
                Vector2 offset = new Vector2((float)Math.Cos(p.Angle), (float)Math.Sin(p.Angle)) * p.Radius * baseScale;
                float fadeByRadius = MathHelper.Clamp(p.Radius / 700f, 0.15f, 1f);

                spriteBatch.Draw(_particleTex, screenCenter + offset, null, p.Color * (fadeByRadius * _fade),
                    p.Angle, particleOrigin, p.Scale * baseScale, SpriteEffects.None, 0f);
            }

            // 12) BINTANG JATUH: jejak cahaya diagonal yang muncul sesekali, memudar seiring waktu
            for (int i = 0; i < _shootingStars.Count; i++)
            {
                ShootingStar s = _shootingStars[i];
                float progress = s.Life / s.MaxLife;
                float alpha = (1f - progress) * 0.85f * _fade;

                float trailLength = 90f * baseScale + s.Velocity.Length() * 0.05f;
                float rotation = (float)Math.Atan2(s.Velocity.Y, s.Velocity.X);
                Vector2 scale = new Vector2(trailLength / _streakTex.Width, 1.4f * baseScale);

                spriteBatch.Draw(_streakTex, s.Position, null, Color.White * alpha, rotation,
                    streakOrigin, scale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();

            // 13) VIGNETTE: menggelapkan tepi layar dengan sentuhan warna ungu gelap, supaya
            // fokus visual tetap tertarik ke tengah dan galaxy terasa lebih "dalam"/sinematik.
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            Vector2 vignetteOrigin = new Vector2(_vignetteTex.Width, _vignetteTex.Height) * 0.5f;
            Vector2 vignetteScale = new Vector2(screenSize.X / _vignetteTex.Width, screenSize.Y / _vignetteTex.Height);
            Color vignetteColor = Color.Lerp(Color.Black, EdgeColor, 0.5f);
            spriteBatch.Draw(_vignetteTex, screenCenter, null, vignetteColor * (0.5f * _fade), 0f,
                vignetteOrigin, vignetteScale, SpriteEffects.None, 0f);

            // Batch ini sengaja dibiarkan terbuka (tidak di-End() lagi) supaya Terraria bisa
            // melanjutkan proses gambar berikutnya di state AlphaBlend seperti semula.
        }

        public override float GetCloudAlpha() => 1f - _fade * 0.85f;

        public override void Activate(Vector2 position, params object[] args) => _isActive = true;

        public override void Deactivate(params object[] args) => _isActive = false;

        public override void Reset()
        {
            _isActive = false;
            _fade = 0f;
        }

        public override bool IsActive() => _isActive || _fade > 0f;
    }
}
