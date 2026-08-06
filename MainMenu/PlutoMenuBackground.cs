using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TheSanity.Content.Skies
{
    /// <summary>
    /// Versi khusus menu dari PlutoBackgroundSky. Efeknya SAMA PERSIS gayanya (gradient merah-hitam,
    /// scanline nyapu, pillar glow berdenyut, planet Pluto muter + detak jantung PlutoPulse, petir
    /// Flash & Bolt independen) tapi dipanggil langsung dari ModMenu.PreDrawLogo, bukan lewat
    /// SkyManager/CustomSky. Makanya kelasnya berdiri sendiri (static) dan:
    ///
    ///   - Selalu full-bright / full-intensity (tidak ada fade in/out atau reveal "wush" dari
    ///     tengah kayak PlutoBackgroundSky, soalnya di menu ga ada konsep boss aktif/nonaktif).
    ///   - Tidak ada parallax planet (butuh Main.LocalPlayer yang gerak di dunia -- ga relevan
    ///     di layar menu), planet cuma diam muter di tempat.
    ///   - Ngitung delta time sendiri pakai Stopwatch, soalnya ModMenu.PreDrawLogo tidak
    ///     dikasih parameter GameTime.
    ///   - Reuse texture Pluto yang sama (plutoplanet, PluFlash, PluBolt, PlutoPulse) dari
    ///     TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/ -- tidak perlu aset baru.
    ///
    /// Cara pakai (lihat RealityModMenu di SanityMenuThemes.cs):
    ///
    ///     public override bool PreDrawLogo(SpriteBatch spriteBatch, ...)
    ///     {
    ///         PlutoMenuBackground.Draw(spriteBatch);
    ///         logoColor = Color.White;
    ///         return true;
    ///     }
    /// </summary>
    public static class PlutoMenuBackground
    {
        private const string PlanetTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/plutoplanet";
        private const string FlashTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PluFlash";
        private const string BoltTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PluBolt";
        private const string PulseTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PlutoPulse";

        // Muter searah jarum jam, PELAN -- radian per detik (waktu nyata, lihat catatan Stopwatch).
        private const float PlanetRotationSpeed = 0.035f;

        // Delta dibatasi maksimal segini per frame -> jaga-jaga kalau ada lag spike / menu
        // baru dibuka lagi setelah lama tidak aktif, supaya animasi tidak "meloncat" jauh.
        private const float MaxDelta = 0.05f;

        private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private static double _lastElapsedSeconds;

        private static float _planetRotation;
        private static float _pillarPulseTimer;
        private static float _backgroundScanTimer;

        // 🛑 [DETAK JANTUNG]
        private const float PulseIntervalMin = 2.2f;
        private const float PulseIntervalMax = 3.6f;
        private const float PulseLifeDuration = 0.9f;   // berapa lama 1 gelombang nyebar sampai ilang
        private const float PulseStartCoverage = 0.55f; // gelombang mulai dari ~55% lebar planet
        private const float PulseEndCoverage = 2.1f;    // nyebar sampai ~2.1x lebar planet
        private const float BeatDuration = 0.45f;       // durasi 1 "kedutan" detak planet
        private const float BeatStrength = 0.16f;       // seberapa kentara kedutannya (scale bump)
        private static float _pulseSpawnTimer = 1.2f;
        private static float _planetBeatTimer = 999f;
        private static readonly List<PulseWave> _pulses = new List<PulseWave>();

        // 🛑 [PETIR INDEPENDEN] Flash & Bolt masing-masing punya timer & list sendiri.
        private const float FlashIntervalMin = 0.5f;
        private const float FlashIntervalMax = 1.6f;
        private const float BoltIntervalMin = 0.8f;
        private const float BoltIntervalMax = 2.3f;
        private static float _flashSpawnTimer = 0.5f;
        private static float _boltSpawnTimer = 0.8f;
        private static readonly List<FlashInstance> _flashes = new List<FlashInstance>();
        private static readonly List<BoltInstance> _bolts = new List<BoltInstance>();

        // 🛑 [SCANLINE BACKGROUND UTAMA]
        private const float BackgroundScanCycleDuration = 3.2f; // detik buat 1 sapuan penuh atas->bawah
        private const int BackgroundScanStripHeight = 18;       // tinggi garis scan (px layar)
        private const int BackgroundScanTrailCount = 5;         // berapa "ekor" garis di belakangnya
        private const float BackgroundScanTrailSpacing = 26f;   // jarak antar ekor (px layar)

        private static Asset<Texture2D> _planetAsset;
        private static Asset<Texture2D> _flashAsset;
        private static Asset<Texture2D> _boltAsset;
        private static Asset<Texture2D> _pulseAsset;

        // Seed beda dari versi in-game (PlutoBackgroundSky pakai Main.rand) biar pola petir/pulse
        // di menu ga kebetulan identik sama pas lagi fight Pluto.
        private static readonly Random _rand = new Random(98765);

        private class PulseWave
        {
            public float Life;
            public float MaxLife;
        }

        private class FlashInstance
        {
            public Vector2 Position;
            public float Life;
            public float MaxLife;
            public float Rotation;
            public float Scale;
        }

        private class BoltInstance
        {
            public Vector2 Position;
            public float Life;
            public float MaxLife;
            public float Scale;
        }

        private static float RandRange(float min, float max) => min + (float)_rand.NextDouble() * (max - min);

        private static float ConsumeDelta()
        {
            double now = _stopwatch.Elapsed.TotalSeconds;
            double delta = _lastElapsedSeconds <= 0d ? 0d : now - _lastElapsedSeconds;
            _lastElapsedSeconds = now;

            float d = (float)delta;
            if (d < 0f) d = 0f;
            if (d > MaxDelta) d = MaxDelta;
            return d;
        }

        private static void UpdateState(float dt)
        {
            if (dt <= 0f) return;

            _planetRotation += PlanetRotationSpeed * dt;
            if (_planetRotation > MathHelper.TwoPi) _planetRotation -= MathHelper.TwoPi;

            _pillarPulseTimer += dt * 1.3f;
            _backgroundScanTimer += dt;

            UpdatePulse(dt);
            UpdateLightning(dt);
        }

        // 🛑 [DETAK JANTUNG] Munculin gelombang PlutoPulse baru tiap PulseIntervalMin-Max detik,
        // BARENGAN itu reset planetBeatTimer ke 0 biar planet ikut "berkedut" di momen yang sama.
        private static void UpdatePulse(float dt)
        {
            _pulseSpawnTimer -= dt;
            if (_pulseSpawnTimer <= 0f)
            {
                _pulses.Add(new PulseWave { Life = 0f, MaxLife = PulseLifeDuration });
                _planetBeatTimer = 0f;
                _pulseSpawnTimer = RandRange(PulseIntervalMin, PulseIntervalMax);
            }

            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                _pulses[i].Life += dt;
                if (_pulses[i].Life >= _pulses[i].MaxLife) _pulses.RemoveAt(i);
            }

            _planetBeatTimer += dt;
        }

        // 🛑 [PETIR INDEPENDEN] Flash & Bolt punya timer & interval sendiri-sendiri.
        private static void UpdateLightning(float dt)
        {
            int w = Main.screenWidth;
            int h = Main.screenHeight;

            _flashSpawnTimer -= dt;
            if (_flashSpawnTimer <= 0f && w > 0 && h > 0)
            {
                _flashes.Add(new FlashInstance
                {
                    Position = new Vector2(RandRange(w * 0.05f, w * 0.95f), RandRange(h * 0.05f, h * 0.65f)),
                    Life = 0f,
                    MaxLife = RandRange(0.30f, 0.50f),
                    Rotation = RandRange(0f, MathHelper.TwoPi), // Flash boleh muter bebas.
                    Scale = RandRange(0.35f, 0.80f),
                });
                _flashSpawnTimer = FlashIntervalMin * (0.5f + (float)_rand.NextDouble());
            }

            _boltSpawnTimer -= dt;
            if (_boltSpawnTimer <= 0f && w > 0 && h > 0)
            {
                _bolts.Add(new BoltInstance
                {
                    Position = new Vector2(RandRange(w * 0.05f, w * 0.95f), RandRange(h * 0.05f, h * 0.65f)),
                    Life = 0f,
                    MaxLife = RandRange(0.35f, 0.55f),
                    Scale = RandRange(0.30f, 0.65f), // Bolt TIDAK dirotate -- dibiarin sesuai orientasi asli.
                });
                _boltSpawnTimer = BoltIntervalMin * (0.5f + (float)_rand.NextDouble());
            }

            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                _flashes[i].Life += dt;
                if (_flashes[i].Life >= _flashes[i].MaxLife) _flashes.RemoveAt(i);
            }

            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                _bolts[i].Life += dt;
                if (_bolts[i].Life >= _bolts[i].MaxLife) _bolts.RemoveAt(i);
            }
        }

        private static float GetFlickerAlpha(float life, float maxLife)
        {
            float t = life / maxLife;
            if (t < 0.12f) return t / 0.12f;
            if (t < 0.22f) return 1f - (t - 0.12f) / 0.10f;
            if (t < 0.30f) return (t - 0.22f) / 0.08f * 0.85f;
            return MathHelper.Clamp(0.85f * (1f - (t - 0.30f) / 0.70f), 0f, 1f);
        }

        private static float EaseOutCubic(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return 1f - (float)Math.Pow(1f - t, 3f);
        }

        // 🛑 [DETAK JANTUNG] Kurva "kedutan" planet: naik cepet dari 0, balik turun ke 0 lagi
        // dalam BeatDuration detik -- dikaliin BeatStrength jadi tambahan scale sesaat.
        private static float GetHeartbeatBump(float beatTimer)
        {
            if (beatTimer >= BeatDuration || beatTimer < 0f) return 0f;
            float p = beatTimer / BeatDuration;
            return MathF.Sin(p * MathHelper.Pi) * (1f - p);
        }

        /// <summary>
        /// Gambar background Pluto penuh satu layar. Panggil ini dari dalam PreDrawLogo
        /// SEBELUM logo digambar (jangan Begin/End spriteBatch lagi di luar method ini,
        /// method ini yang mengurus siklus End/Begin-nya sendiri dan akan meninggalkan
        /// spriteBatch dalam keadaan Begin(AlphaBlend) supaya aman dipakai kode setelahnya).
        /// </summary>
        public static void Draw(SpriteBatch spriteBatch)
        {
            float delta = ConsumeDelta();
            UpdateState(delta);

            int w = Main.screenWidth;
            int h = Main.screenHeight;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 planetPos = new Vector2(w * 0.5f, h * 0.34f);

            // Tutup batch yang sedang dipakai menu (biasanya AlphaBlend polos, sama seperti
            // dulu dipakai buat gambar RealityBG langsung), supaya kita bisa ganti-ganti
            // blend state untuk lapisan-lapisan Pluto.
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            // 0) Gradient dasar: MERAH di bawah, geser jadi HITAM ke atas (full-strip, tidak ada
            //    reveal "wush" dari tengah kayak versi in-game, soalnya di menu selalu full-bright).
            DrawGradientBackground(spriteBatch, pixel, w, h);

            // 1) Scanline nyapu dari atas ke bawah
            DrawBackgroundScan(spriteBatch, pixel, w, h);

            // 2) Kolom cahaya merah berdenyut ala pillar glow
            DrawPillarGlow(spriteBatch, pixel, w, h);

            // 3) Gelombang detak jantung PlutoPulse (di belakang planet)
            DrawPlutoPulse(spriteBatch, planetPos);

            // 4) Planet Pluto muter + halo + detak
            DrawPlutoPlanet(spriteBatch, planetPos);

            // 5) Petir Flash & Bolt
            DrawLightning(spriteBatch);

            // Batch dibiarkan Begin(AlphaBlend) di sini supaya kode menu setelahnya
            // (gambar logo, dsb) tetap aman melanjutkan draw.
        }

        private static void DrawGradientBackground(SpriteBatch sb, Texture2D pixel, int w, int h)
        {
            Color topColor = Color.Black;
            Color bottomColor = new Color(130, 8, 8);

            const int strips = 64;
            float stripHeight = h / (float)strips;

            for (int i = 0; i < strips; i++)
            {
                float t = i / (float)(strips - 1);
                Color c = Color.Lerp(topColor, bottomColor, t);
                Rectangle dest = new Rectangle(0, (int)(i * stripHeight) - 2, w, (int)stripHeight + 4);
                sb.Draw(pixel, dest, c);
            }
        }

        private static void DrawBackgroundScan(SpriteBatch sb, Texture2D pixel, int w, int h)
        {
            float cycleT = (_backgroundScanTimer % BackgroundScanCycleDuration) / BackgroundScanCycleDuration;
            float scanYBase = cycleT * h;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            for (int i = 0; i < BackgroundScanTrailCount; i++)
            {
                float scanY = scanYBase - i * BackgroundScanTrailSpacing;
                if (scanY < 0f || scanY > h - BackgroundScanStripHeight) continue; // di luar layar / abis wrap -- skip aja

                float trailAlpha = 1f - i / (float)BackgroundScanTrailCount;
                if (trailAlpha <= 0.01f) continue;

                Color color = new Color(255, 60, 40) * (trailAlpha * 0.55f);
                Rectangle dest = new Rectangle(0, (int)scanY, w, BackgroundScanStripHeight);
                sb.Draw(pixel, dest, color);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        }

        private static void DrawPillarGlow(SpriteBatch sb, Texture2D pixel, int w, int h)
        {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            Span<float> pillarXFrac = stackalloc float[] { 0.26f, 0.74f };
            Span<float> pulseOffset = stackalloc float[] { 0f, 1.9f };

            for (int p = 0; p < pillarXFrac.Length; p++)
            {
                float centerX = w * pillarXFrac[p];
                float pulse = 0.55f + 0.45f * MathF.Sin(_pillarPulseTimer + pulseOffset[p]);
                float baseAlpha = pulse;

                const int bars = 22;
                float halfWidth = w * 0.10f;
                float barWidth = (halfWidth * 2f / bars) + 2f;

                for (int i = 0; i < bars; i++)
                {
                    float frac = i / (float)(bars - 1);
                    float offset = MathHelper.Lerp(-halfWidth, halfWidth, frac);
                    float distNorm = Math.Abs(offset) / halfWidth;
                    float falloff = 1f - distNorm * distNorm; // makin ke tepi makin transparan
                    if (falloff <= 0f) continue;

                    Color glow = new Color(255, 20, 20) * (baseAlpha * falloff * 0.14f);
                    Rectangle dest = new Rectangle((int)(centerX + offset - barWidth * 0.5f), 0, (int)barWidth, h);
                    sb.Draw(pixel, dest, glow);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        }

        private static void DrawPlutoPulse(SpriteBatch sb, Vector2 planetPos)
        {
            if (_pulses.Count == 0) return;

            _planetAsset ??= ModContent.Request<Texture2D>(PlanetTexturePath, AssetRequestMode.ImmediateLoad);
            _pulseAsset ??= ModContent.Request<Texture2D>(PulseTexturePath, AssetRequestMode.ImmediateLoad);
            if (!_planetAsset.IsLoaded || !_pulseAsset.IsLoaded) return;

            Texture2D planetTex = _planetAsset.Value;
            Texture2D pulseTex = _pulseAsset.Value;
            Vector2 origin = pulseTex.Size() * 0.5f;

            float minScale = (planetTex.Width * PulseStartCoverage) / pulseTex.Width;
            float maxScale = (planetTex.Width * PulseEndCoverage) / pulseTex.Width;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            foreach (PulseWave p in _pulses)
            {
                float t = EaseOutCubic(p.Life / p.MaxLife);
                float scale = MathHelper.Lerp(minScale, maxScale, t);
                float alpha = 1f - t;
                if (alpha <= 0.01f) continue;

                Color color = new Color(255, 35, 30) * (alpha * 0.85f);
                sb.Draw(pulseTex, planetPos, null, color, _planetRotation, origin, scale, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        }

        // Planet Pluto, muter pelan searah jarum jam, ikut "berdetak" (GetHeartbeatBump) PERSIS
        // pas gelombang PlutoPulse mulai nyebar. Posisinya diam di titik tetap (tidak parallax
        // kayak versi in-game, soalnya di menu ga ada player yang bergerak).
        private static void DrawPlutoPlanet(SpriteBatch sb, Vector2 planetPos)
        {
            _planetAsset ??= ModContent.Request<Texture2D>(PlanetTexturePath, AssetRequestMode.ImmediateLoad);
            if (!_planetAsset.IsLoaded) return;

            Texture2D planet = _planetAsset.Value;
            Vector2 origin = new Vector2(planet.Width * 0.5f, planet.Height * 0.5f);

            float beatBump = GetHeartbeatBump(_planetBeatTimer);
            float finalScale = 1f + beatBump * BeatStrength;

            // Halo merah lembut di belakang planet (bloom murahan), ikut kedut dikit juga.
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            float haloPulse = 0.7f + 0.3f * MathF.Sin(_pillarPulseTimer * 0.6f);
            sb.Draw(planet, planetPos, null, new Color(255, 35, 35) * (0.32f * haloPulse), _planetRotation, origin, 1.20f * finalScale, SpriteEffects.None, 0f);
            sb.Draw(planet, planetPos, null, new Color(255, 90, 90) * (0.16f * haloPulse), _planetRotation, origin, 1.40f * finalScale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            // Planet aslinya -- warna dibiarin natural (ga di-tint merah), biar tetep kebaca
            // sebagai "Planet Pluto".
            sb.Draw(planet, planetPos, null, Color.White, _planetRotation, origin, finalScale, SpriteEffects.None, 0f);
        }

        // Kilat MERAH: Flash & Bolt INDEPENDEN -- masing-masing di-loop dari list-nya sendiri.
        private static void DrawLightning(SpriteBatch sb)
        {
            if (_flashes.Count == 0 && _bolts.Count == 0) return;

            _flashAsset ??= ModContent.Request<Texture2D>(FlashTexturePath, AssetRequestMode.ImmediateLoad);
            _boltAsset ??= ModContent.Request<Texture2D>(BoltTexturePath, AssetRequestMode.ImmediateLoad);
            if (!_flashAsset.IsLoaded || !_boltAsset.IsLoaded) return;

            Texture2D flashTex = _flashAsset.Value;
            Texture2D boltTex = _boltAsset.Value;
            Vector2 flashOrigin = flashTex.Size() * 0.5f;
            Vector2 boltOrigin = boltTex.Size() * 0.5f;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            foreach (FlashInstance f in _flashes)
            {
                float alpha = GetFlickerAlpha(f.Life, f.MaxLife);
                if (alpha <= 0.01f) continue;
                Color flashColor = new Color(255, 70, 60) * (alpha * 0.9f);
                sb.Draw(flashTex, f.Position, null, flashColor, f.Rotation, flashOrigin, f.Scale, SpriteEffects.None, 0f);
            }

            foreach (BoltInstance b in _bolts)
            {
                float alpha = GetFlickerAlpha(b.Life, b.MaxLife);
                if (alpha <= 0.01f) continue;
                Color boltColor = new Color(255, 45, 40) * alpha;
                sb.Draw(boltTex, b.Position, null, boltColor, 0f, boltOrigin, b.Scale, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        }
    }
}
