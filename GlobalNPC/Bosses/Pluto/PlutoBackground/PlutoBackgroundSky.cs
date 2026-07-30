using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoBackground
{
    // 🛑 [CARA KERJA] Ini CustomSky vanilla-style, PERSIS sistem yang dipakai Terraria buat
    // background Vortex Pillar / Nebula Pillar pas Lunar Event -- makanya begitu sky ini aktif,
    // dia otomatis digambar DI ATAS matahari & bulan (nutupin keduanya) sama kayak vanilla,
    // TANPA perlu kita utak-atik render sun/moon manual sama sekali.
    //
    // Aktivasi/nonaktivasi diatur dari PlutoBackgroundSystem.cs (ModSystem terpisah), yang
    // sekarang nyalain sky ini PERSIS pas Pluto roar di Spawn Animation (Pattern 9, lihat
    // PlutoSpawnDash.cs) -- bukan dari awal Pluto ke-spawn.
    //
    // 🛑 [REVEAL "WUSH" DARI TENGAH] SESUAI REQUEST: begitu sky ini aktif, Planet Pluto POP duluan
    // (planetPopElapsed, bounce kecil), abis itu SEDIKIT jeda (RevealStartDelay) baru background-
    // nya "wush" nyebar CEPAT dari titik tengah layar ke seluruh layar (revealElapsed, dipakai buat
    // nge-gate grid cell background berdasarkan jarak ke tengah -- lihat DrawGradientBackground).
    // Efek radial ini CUMA jalan selama window transient reveal-nya (~RevealDuration detik), abis
    // itu balik gambar strip biasa (murah, sama kayak versi lama) karena udah full ke-reveal.
    //
    // 🛑 [PETIR - REWRITE TOTAL] Dulu petir digambar manual pakai garis zig-zag (midpoint
    // displacement). SEKARANG pakai 2 sprite: PluFlash.png (boleh dirotate acak) & PluBolt.png
    // (TIDAK dirotate, "biarin aja" sesuai orientasi asli-nya) -- keduanya di-recolor MERAH (di
    // file asli warnanya biru) lewat tint Color + additive blend, ukuran macem-macem tapi
    // dibatasin biar ga gede-gede banget, dan lokasi spawn-nya RANDOM di seluruh langit (bukan
    // cuma dari atas doang kayak sebelumnya).
    //
    // 🛑 [LAUT DARAH] Ditambahin lapisan gelombang sinus animasi di bagian bawah layar (reuse
    // helper DrawLine yang sama dipakai gambar petir), numpuk beberapa layer beda kecepatan/
    // amplitudo biar keliatan berlapis kek permukaan lautan darah yang bergerak pelan.
    //
    // 🛑 [ASET YANG PERLU DITAMBAHIN MANUAL] taruh 2 file ini di:
    //   TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PluFlash.png
    //   TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PluBolt.png
    public class PlutoBackgroundSky : CustomSky
    {
        // Depth "background jauh" tempat sun/moon & sky vanilla lain digambar. Konvensi umum
        // tModLoader (dipakai juga di ExampleMod) buat CustomSky yang mau gantiin background jauh.
        private const float FarBackgroundDepth = 3f;

        private const string PlanetTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/plutoplanet";
        private const string FlashTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PluFlash";
        private const string BoltTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PluBolt";

        // Muter searah jarum jam, PELAN -- ini radian per detik.
        private const float PlanetRotationSpeed = 0.035f;

        private bool isActive = false;
        private bool wasActiveLastFrame = false;
        private float intensity = 0f; // 0 = full ilang, 1 = full nutupin layar (buat transisi fade in/out yg mulus)

        // 🛑 [REVEAL "WUSH"] Lihat penjelasan panjang di komentar atas class.
        private const float PlanetPopDuration = 0.22f;   // planet pop duluan (bounce kecil)
        private const float RevealStartDelay = 0.10f;    // jeda dikit abis planet keliatan
        private const float RevealDuration = 0.40f;      // abis itu background wush CEPAT nyebar
        private float planetPopElapsed = 0f;
        private float revealElapsed = 0f;

        private float planetRotation = 0f;
        private float pillarPulseTimer = 0f;
        private float bloodOceanTimer = 0f;

        private float boltSpawnTimer = 0f;
        private readonly List<LightningStrike> strikes = new();

        private Asset<Texture2D> planetAsset;
        private Asset<Texture2D> flashAsset;
        private Asset<Texture2D> boltAsset;

        private class LightningStrike
        {
            public Vector2 Position;
            public float Life;
            public float MaxLife;
            public float FlashRotation;
            public float FlashScale;
            public float BoltScale;
        }

        public override void Activate(Vector2 position, params object[] args) {
            isActive = true;
        }

        public override void Deactivate(params object[] args) {
            isActive = false;
        }

        public override void Reset() {
            isActive = false;
            wasActiveLastFrame = false;
            intensity = 0f;
            planetPopElapsed = 0f;
            revealElapsed = 0f;
            strikes.Clear();
        }

        public override bool IsActive() => isActive || intensity > 0.001f;

        public override Color OnTileColor(Color inColor) => inColor;

        // Awan vanilla ikut memudar pas sky kita masuk -- biar transisinya rapi, ga nabrak
        // sama awan putih yang ga nyambung sama tema merah-hitam ini.
        public override float GetCloudAlpha() => MathHelper.Clamp(1f - intensity, 0f, 1f);

        public override void Update(GameTime gameTime) {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt <= 0f) return;

            // Fade in/out halus pas boss mulai/selesai, bukan langsung "creg" nyala/mati.
            float fadeSpeed = 1.2f * dt;
            intensity = isActive
                ? MathHelper.Clamp(intensity + fadeSpeed, 0f, 1f)
                : MathHelper.Clamp(intensity - fadeSpeed, 0f, 1f);

            // 🛑 [RESET REVEAL] Begitu transisi dari "mati total" ke "baru nyala" kedeteksi, paksa
            // planetPopElapsed & revealElapsed balik ke 0 -- biar reveal-nya selalu "fresh" tiap
            // kali Pluto baru muncul lagi (bukan nyambung dari sisa progress sebelumnya).
            if (isActive && !wasActiveLastFrame) {
                planetPopElapsed = 0f;
                revealElapsed = 0f;
            }
            wasActiveLastFrame = isActive;

            if (isActive) {
                planetPopElapsed = Math.Min(planetPopElapsed + dt, PlanetPopDuration);
                if (planetPopElapsed >= RevealStartDelay) {
                    revealElapsed = Math.Min(revealElapsed + dt, RevealDuration);
                }
            }
            else {
                // Nutup: biarin reveal-nya luntur balik ke 0 (dibarengi intensity yang juga fade),
                // biar kalau nanti nyala lagi keliatan seger dari awal, bukan nyambung setengah jalan.
                planetPopElapsed = Math.Max(planetPopElapsed - dt, 0f);
                revealElapsed = Math.Max(revealElapsed - dt, 0f);
            }

            if (intensity <= 0f) {
                strikes.Clear();
                return;
            }

            planetRotation += PlanetRotationSpeed * dt;
            if (planetRotation > MathHelper.TwoPi) planetRotation -= MathHelper.TwoPi;

            pillarPulseTimer += dt * 1.3f;
            bloodOceanTimer += dt;

            UpdateLightning(dt);
        }

        private void UpdateLightning(float dt) {
            boltSpawnTimer -= dt;
            if (boltSpawnTimer <= 0f) {
                SpawnLightningStrike();
                // Makin gede intensity (makin "masuk" ke fase boss), makin sering kilatnya nyamber.
                boltSpawnTimer = MathHelper.Lerp(2.6f, 0.7f, intensity) * (0.5f + Main.rand.NextFloat());
            }

            for (int i = strikes.Count - 1; i >= 0; i--) {
                strikes[i].Life += dt;
                if (strikes[i].Life >= strikes[i].MaxLife) strikes.RemoveAt(i);
            }
        }

        // 🛑 [LOKASI RANDOM LOKASI PETIR] SESUAI REQUEST: sekarang posisinya BENER-BENER random
        // di seluruh area langit (bukan cuma dari atas & sekitar tengah doang kayak sebelumnya).
        // Ukuran (scale) juga di-random tapi dibatasin biar ga gede-gede banget nutupin layar.
        private void SpawnLightningStrike() {
            int w = Main.screenWidth;
            int h = Main.screenHeight;
            if (w <= 0 || h <= 0) return;

            Vector2 pos = new(
                Main.rand.NextFloat(w * 0.05f, w * 0.95f),
                Main.rand.NextFloat(h * 0.05f, h * 0.65f)
            );

            strikes.Add(new LightningStrike {
                Position = pos,
                Life = 0f,
                MaxLife = Main.rand.NextFloat(0.35f, 0.55f),
                // Flash boleh muter bebas -- SESUAI REQUEST.
                FlashRotation = Main.rand.NextFloat(0f, MathHelper.TwoPi),
                FlashScale = Main.rand.NextFloat(0.35f, 0.75f),
                // Bolt TIDAK dirotate (dibiarin sesuai orientasi asli) -- SESUAI REQUEST.
                BoltScale = Main.rand.NextFloat(0.30f, 0.60f),
            });
        }

        // Kurva "kedip ganda" khas kilat: nyambar cepet, sempet redup sekilas, nyambar lagi
        // lebih kecil, baru fade abis. Bukan cuma naik-turun linear biasa.
        private static float GetStrikeAlpha(LightningStrike s) {
            float t = s.Life / s.MaxLife;
            if (t < 0.12f) return t / 0.12f;
            if (t < 0.22f) return 1f - (t - 0.12f) / 0.10f;
            if (t < 0.30f) return (t - 0.22f) / 0.08f * 0.85f;
            return MathHelper.Clamp(0.85f * (1f - (t - 0.30f) / 0.70f), 0f, 1f);
        }

        private static float EaseOutCubic(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            return 1f - (float)Math.Pow(1f - t, 3f);
        }

        // Easing dengan sedikit "overshoot" (lewat 1 dikit sebelum settle) -- ini yang bikin
        // planet keliatan "pop" (kayak muncul dengan pantulan kecil), bukan cuma fade linear.
        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t = MathHelper.Clamp(t, 0f, 1f);
            float p = t - 1f;
            return 1f + c3 * (p * p * p) + c1 * (p * p);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.001f) return;
            if (minDepth > FarBackgroundDepth || maxDepth < FarBackgroundDepth) return;

            int w = Main.screenWidth;
            int h = Main.screenHeight;
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            DrawGradientBackground(spriteBatch, pixel, w, h);
            DrawPillarGlow(spriteBatch, pixel, w, h);
            DrawPlutoPlanet(spriteBatch, w, h);
            DrawBloodOcean(spriteBatch, pixel, w, h);
            DrawLightning(spriteBatch);
        }

        // Background polos: MERAH di bawah, geser jadi HITAM ke atas. Selama window reveal
        // (revealElapsed < RevealDuration) di-gambar per-cell (grid) supaya bisa di-gate
        // berdasarkan jarak ke tengah layar -- efeknya jadi "wush" lingkaran nyebar dari tengah.
        // Abis reveal-nya kelar, balik ke strip full-width biasa (jauh lebih murah).
        private void DrawGradientBackground(SpriteBatch sb, Texture2D pixel, int w, int h) {
            Color topColor = Color.Black;
            Color bottomColor = new(130, 8, 8);

            if (revealElapsed < RevealDuration) {
                const int cols = 14;
                const int rows = 20;
                const float edgeBand = 140f;

                Vector2 center = new(w * 0.5f, h * 0.5f);
                float maxRadius = (float)Math.Sqrt(w * (double)w + h * (double)h) * 0.5f + 80f;
                float revealRadius = MathHelper.Lerp(0f, maxRadius, EaseOutCubic(revealElapsed / RevealDuration));

                float cellW = w / (float)cols;
                float cellH = h / (float)rows;

                for (int r = 0; r < rows; r++) {
                    float t = r / (float)(rows - 1);
                    Color rowColor = Color.Lerp(topColor, bottomColor, t);

                    for (int c = 0; c < cols; c++) {
                        Vector2 cellCenter = new((c + 0.5f) * cellW, (r + 0.5f) * cellH);
                        float dist = Vector2.Distance(cellCenter, center);
                        float cellReveal = 1f - MathHelper.Clamp((dist - revealRadius) / edgeBand, 0f, 1f);
                        if (cellReveal <= 0f) continue;

                        Color cellColor = rowColor * (intensity * cellReveal);
                        Rectangle dest = new((int)(c * cellW) - 2, (int)(r * cellH) - 2, (int)cellW + 4, (int)cellH + 4);
                        sb.Draw(pixel, dest, cellColor);
                    }
                }
            }
            else {
                const int strips = 64;
                float stripHeight = h / (float)strips;

                for (int i = 0; i < strips; i++) {
                    float t = i / (float)(strips - 1);
                    Color c = Color.Lerp(topColor, bottomColor, t) * intensity;
                    Rectangle dest = new(0, (int)(i * stripHeight) - 2, w, (int)stripHeight + 4);
                    sb.Draw(pixel, dest, c);
                }
            }
        }

        // Kolom cahaya merah nge-pulse berdiri tegak, ala glow pillar Nebula Pillar tapi merah
        // terang. Ikutan di-gate pelan-pelan sama revealAlpha biar ga langsung nongol duluan
        // sebelum background dasarnya sendiri kelar "wush".
        private void DrawPillarGlow(SpriteBatch sb, Texture2D pixel, int w, int h) {
            float revealAlpha = MathHelper.Clamp(revealElapsed / RevealDuration, 0f, 1f);
            if (revealAlpha <= 0f) return;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Span<float> pillarXFrac = stackalloc float[] { 0.26f, 0.74f };
            Span<float> pulseOffset = stackalloc float[] { 0f, 1.9f };

            for (int p = 0; p < pillarXFrac.Length; p++) {
                float centerX = w * pillarXFrac[p];
                float pulse = 0.55f + 0.45f * MathF.Sin(pillarPulseTimer + pulseOffset[p]);
                float baseAlpha = pulse * intensity * revealAlpha;

                const int bars = 22;
                float halfWidth = w * 0.10f;
                float barWidth = (halfWidth * 2f / bars) + 2f;

                for (int i = 0; i < bars; i++) {
                    float frac = i / (float)(bars - 1);
                    float offset = MathHelper.Lerp(-halfWidth, halfWidth, frac);
                    float distNorm = Math.Abs(offset) / halfWidth;
                    float falloff = 1f - distNorm * distNorm; // makin ke tepi makin transparan
                    if (falloff <= 0f) continue;

                    Color glow = new Color(255, 20, 20) * (baseAlpha * falloff * 0.14f);
                    Rectangle dest = new((int)(centerX + offset - barWidth * 0.5f), 0, (int)barWidth, h);
                    sb.Draw(pixel, dest, glow);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // Planet Pluto di tengah-tengah background, muter pelan searah jarum jam. SESUAI REQUEST:
        // planet ini yang "pop" MUNCUL DULUAN (pakai EaseOutBack biar ada kesan mantul kecil)
        // sebelum background gradient-nya sendiri selesai "wush" nyebar dari tengah.
        private void DrawPlutoPlanet(SpriteBatch sb, int w, int h) {
            planetAsset ??= ModContent.Request<Texture2D>(PlanetTexturePath, AssetRequestMode.ImmediateLoad);
            if (!planetAsset.IsLoaded) return;

            Texture2D planet = planetAsset.Value;
            Vector2 pos = new(w * 0.5f, h * 0.34f);
            Vector2 origin = new(planet.Width * 0.5f, planet.Height * 0.5f);

            float popT = planetPopElapsed / PlanetPopDuration;
            float popScale = EaseOutBack(popT);
            float popAlpha = MathHelper.Clamp(popT, 0f, 1f);
            if (popAlpha <= 0f) return;

            // Halo merah lembut di belakang planet (bloom murahan: gambar planetnya sendiri,
            // diperbesar & additive transparan, biar berasa "berpendar").
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            float haloPulse = 0.7f + 0.3f * MathF.Sin(pillarPulseTimer * 0.6f);
            sb.Draw(planet, pos, null, new Color(255, 35, 35) * (0.32f * haloPulse * intensity * popAlpha), planetRotation, origin, 1.20f * popScale, SpriteEffects.None, 0f);
            sb.Draw(planet, pos, null, new Color(255, 90, 90) * (0.16f * haloPulse * intensity * popAlpha), planetRotation, origin, 1.40f * popScale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // Planet aslinya -- warna dibiarin natural (ga di-tint merah), biar tetep kebaca
            // sebagai "Planet Pluto", cuma alpha & scale-nya ngikutin pop-in + fade in/out sky.
            sb.Draw(planet, pos, null, Color.White * intensity * popAlpha, planetRotation, origin, popScale, SpriteEffects.None, 0f);
        }

        // 🩸 [LAUT DARAH] Beberapa layer gelombang sinus digambar sebagai garis patah-patah (reuse
        // DrawLine yang sama dipakai buat kilat), numpuk beberapa layer beda kecepatan & amplitudo
        // biar keliatan berlapis kek permukaan lautan darah yang bergerak pelan di bagian bawah layar.
        private void DrawBloodOcean(SpriteBatch sb, Texture2D pixel, int w, int h) {
            float revealAlpha = MathHelper.Clamp(revealElapsed / RevealDuration, 0f, 1f);
            float alpha = intensity * revealAlpha;
            if (alpha <= 0.01f) return;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            DrawWaveLayer(sb, pixel, w, h, 0.80f, 14f, 0.010f, 0.6f, 5f, new Color(120, 10, 10) * (alpha * 0.55f));
            DrawWaveLayer(sb, pixel, w, h, 0.86f, 10f, 0.016f, -0.9f, 4f, new Color(200, 20, 20) * (alpha * 0.50f));
            DrawWaveLayer(sb, pixel, w, h, 0.93f, 7f, 0.024f, 1.3f, 3f, new Color(255, 60, 50) * (alpha * 0.65f));

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawWaveLayer(SpriteBatch sb, Texture2D pixel, int w, int h, float baseYFrac, float amplitude, float freq, float speed, float thickness, Color color) {
            const int segments = 36;
            float segW = w / (float)segments;

            Vector2 prev = new(0f, h * baseYFrac + MathF.Sin(bloodOceanTimer * speed) * amplitude);
            for (int i = 1; i <= segments; i++) {
                float x = i * segW;
                float y = h * baseYFrac + MathF.Sin(x * freq + bloodOceanTimer * speed) * amplitude;
                Vector2 cur = new(x, y);
                DrawLine(sb, pixel, prev, cur, thickness, color);
                prev = cur;
            }
        }

        // Kilat MERAH: Flash (boleh dirotate acak, ukuran macem-macem) + Bolt (dibiarin sesuai
        // orientasi asli, cuma lokasinya yang random) digambar bareng di titik yang sama tiap
        // strike -- keduanya di-recolor merah lewat tint Color + additive blend (asetnya sendiri
        // biru di file asli).
        private void DrawLightning(SpriteBatch sb) {
            if (strikes.Count == 0) return;

            flashAsset ??= ModContent.Request<Texture2D>(FlashTexturePath, AssetRequestMode.ImmediateLoad);
            boltAsset ??= ModContent.Request<Texture2D>(BoltTexturePath, AssetRequestMode.ImmediateLoad);
            if (!flashAsset.IsLoaded || !boltAsset.IsLoaded) return;

            Texture2D flashTex = flashAsset.Value;
            Texture2D boltTex = boltAsset.Value;
            Vector2 flashOrigin = flashTex.Size() * 0.5f;
            Vector2 boltOrigin = boltTex.Size() * 0.5f;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (LightningStrike s in strikes) {
                float alpha = GetStrikeAlpha(s) * intensity;
                if (alpha <= 0.01f) continue;

                // 🛑 [RECOLOR MERAH] Tint lewat Color (bukan Color.White) + additive blend --
                // ini cara paling ringan buat "narik" hue asli (biru) ke arah merah tanpa perlu
                // shader baru. Kalau ternyata di file asli-nya banyak area biru SOLID/opaque
                // (bukan cuma glow putih+alpha), hasil tint ini bisa keliatan agak gelap di area
                // itu -- kabarin aja kalau begitu, nanti dibikinin shader recolor khusus (mirip
                // PlutoElectroRimLight.fx yang udah ada) biar hasilnya presisi.
                Color flashColor = new Color(255, 70, 60) * (alpha * 0.9f);
                Color boltColor = new Color(255, 45, 40) * alpha;

                sb.Draw(flashTex, s.Position, null, flashColor, s.FlashRotation, flashOrigin, s.FlashScale, SpriteEffects.None, 0f);
                sb.Draw(boltTex, s.Position, null, boltColor, 0f, boltOrigin, s.BoltScale, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 0.01f) return;

            float angle = MathF.Atan2(edge.Y, edge.X);
            sb.Draw(pixel, start, null, color, angle, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }
    }
}
