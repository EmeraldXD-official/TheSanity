using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    // nyalain sky ini PERSIS pas Pluto roar di Spawn Animation (Pattern 9, lihat PlutoSpawnDash.cs)
    // -- bukan dari awal Pluto ke-spawn.
    //
    // 🛑 [RALAT - ANTI SUNDIAL] SEMUA timer/animasi di file ini (rotasi planet, detak jantung,
    // petir, scanline gunung, dll) SEKARANG jalan berdasarkan WAKTU NYATA (real wall-clock time,
    // lewat Stopwatch manual), BUKAN dari `gameTime.ElapsedGameTime` bawaan Update(). Alasannya:
    // pas dipercepat pakai Enchanted Sundial, method Update() CustomSky ini bisa kepanggil
    // berkali-kali dalam waktu nyata yang SANGAT SINGKAT (buat nyimulasiin lompatan waktu ke pagi),
    // dan kalau kita percaya `gameTime.ElapsedGameTime` di tiap panggilan itu, hasilnya animasi
    // jadi keliatan dipercepat gila-gilaan (numpuk semua elapsed time yang "disimulasikan" itu).
    // Dengan Stopwatch manual, delta yang dihitung SELALU cuma sebesar waktu NYATA yang beneran
    // lewat antar panggilan Update() -- jadi kalau Update() dipanggil berkali-kali dalam sepersekian
    // detik nyata (efek si Sundial), delta-nya otomatis jadi kecil/nol, animasi TETAP jalan normal
    // ga ikut kepercepat.
    //
    // 🛑 [REVEAL "WUSH" DARI TENGAH] Begitu sky ini aktif, Planet Pluto POP duluan
    // (planetPopElapsed, bounce kecil), abis itu SEDIKIT jeda (RevealStartDelay) baru background-
    // nya "wush" nyebar CEPAT dari titik tengah layar ke seluruh layar (revealElapsed).
    //
    // 🛑 [DETAK JANTUNG - PlutoPulse.png] Tiap beberapa detik, dari BELAKANG sprite planet muncul
    // gelombang merah yang nyebar, BARENGAN itu si Planet ikut "berdetak" (scale kedut sesaat).
    //
    // 🛑 [PLANET SEKARANG PARALLAX - NGIKUTIN PLAYER] SESUAI REQUEST: planet BUKAN lagi nempel
    // statis pas persis di tengah layar terus -- sekarang dia geser dikit ngikutin arah gerak
    // player (lihat parallaxAnchor & GetPlanetParallaxOffset), kek efek depth background biasa,
    // TAPI di-clamp jaraknya (PlanetParallaxMaxOffset) biar ga kebablasan jauh dari tengah kalau
    // player lari jauh banget pas fight. PlutoPulse ikut kegeser bareng planet (biar tetep nempel
    // pas di belakangnya).
    //
    // 🛑 [RALAT - MOUNTAIN DIHAPUS] Layer PlutoMountain.png DIHAPUS TOTAL sesuai request. Efek
    // scanline yang tadinya nyapu di sprite gunung itu SEKARANG dipindah ke BACKGROUND UTAMA
    // (lihat DrawBackgroundScan) -- garis terang nyapu dari ujung atas layar ke bawah berulang,
    // dengan beberapa "ekor" fading di belakangnya, full-width layar.
    //
    // 🛑 [PETIR - PluFlash & PluBolt INDEPENDEN] Masing-masing punya timer & lokasi spawn sendiri,
    // gak lagi nyambung/bareng kayak sebelumnya.
    //
    // 🛑 [ASET YANG PERLU DITAMBAHIN MANUAL] taruh 3 file ini di:
    //   TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PluFlash.png
    //   TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PluBolt.png
    //   TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PlutoPulse.png    (96x96, putih polos)
    public class PlutoBackgroundSky : CustomSky
    {
        // Depth "background jauh" tempat sun/moon & sky vanilla lain digambar. Konvensi umum
        // tModLoader (dipakai juga di ExampleMod) buat CustomSky yang mau gantiin background jauh.
        private const float FarBackgroundDepth = 3f;

        private const string PlanetTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/plutoplanet";
        private const string FlashTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PluFlash";
        private const string BoltTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PluBolt";
        private const string PulseTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoBackground/PlutoPulse";

        // Muter searah jarum jam, PELAN -- ini radian per detik (waktu NYATA, lihat catatan anti-Sundial).
        private const float PlanetRotationSpeed = 0.035f;

        private bool isActive = false;
        private bool wasActiveLastFrame = false;
        private float intensity = 0f; // 0 = full ilang, 1 = full nutupin layar (buat transisi fade in/out yg mulus)

        // 🛑 [ANTI SUNDIAL] Stopwatch manual buat ngitung delta waktu NYATA antar panggilan
        // Update(), independen total dari `gameTime` bawaan yang bisa "dipercepat" pas ada
        // event kayak Enchanted Sundial.
        private static readonly Stopwatch RealTimeClock = Stopwatch.StartNew();
        private double lastRealTimeSeconds = -1d;

        // 🛑 [REVEAL "WUSH"] Lihat penjelasan panjang di komentar atas class.
        private const float PlanetPopDuration = 0.22f;   // planet pop duluan (bounce kecil)
        private const float RevealStartDelay = 0.10f;    // jeda dikit abis planet keliatan
        private const float RevealDuration = 0.40f;      // abis itu background wush CEPAT nyebar
        private float planetPopElapsed = 0f;
        private float revealElapsed = 0f;

        private float planetRotation = 0f;
        private float pillarPulseTimer = 0f;
        private float backgroundScanTimer = 0f;

        // 🛑 [PARALLAX PLANET] Titik acuan (posisi player world SAAT sky ini baru nyala) --
        // pergeseran planet dihitung dari SEBERAPA JAUH player udah gerak dari titik ini, BUKAN
        // dari posisi absolut player (biar selalu "fresh" mulai dari tengah tiap boss baru nyala).
        private const float PlanetParallaxFactorX = 0.10f;
        private const float PlanetParallaxFactorY = 0.05f;
        private const float PlanetParallaxMaxOffset = 220f; // clamp biar ga geser kebablasan jauh
        private Vector2 parallaxAnchor = Vector2.Zero;

        // 🛑 [DETAK JANTUNG]
        private const float PulseIntervalMin = 2.2f;
        private const float PulseIntervalMax = 3.6f;
        private const float PulseLifeDuration = 0.9f;   // berapa lama 1 gelombang nyebar sampai ilang
        private const float PulseStartCoverage = 0.55f; // gelombang mulai dari ~55% lebar planet (dari belakangnya)
        private const float PulseEndCoverage = 2.1f;    // nyebar sampai ~2.1x lebar planet
        private const float BeatDuration = 0.45f;       // durasi 1 "kedutan" detak planet
        private const float BeatStrength = 0.16f;       // seberapa kentara kedutannya (scale bump)
        private float pulseSpawnTimer = 0f;
        private float planetBeatTimer = 999f; // gede biar ga langsung "berdetak" di frame pertama
        private readonly List<PulseWave> pulses = new();

        // 🛑 [PETIR INDEPENDEN] Flash & Bolt masing-masing punya timer & list sendiri.
        private const float FlashIntervalMin = 0.5f;
        private const float FlashIntervalMax = 1.6f;
        private const float BoltIntervalMin = 0.8f;
        private const float BoltIntervalMax = 2.3f;
        private float flashSpawnTimer = 0f;
        private float boltSpawnTimer = 0f;
        private readonly List<FlashInstance> flashes = new();
        private readonly List<BoltInstance> bolts = new();

        // 🛑 [SCANLINE BACKGROUND UTAMA] Garis terang full-width yang nyapu dari atas ke bawah
        // layar berulang-ulang (lihat DrawBackgroundScan) -- dulu ada di sprite gunung, sekarang
        // dipindah ke background utama sesuai request.
        private const float BackgroundScanCycleDuration = 3.2f; // detik buat 1 sapuan penuh atas->bawah
        private const int BackgroundScanStripHeight = 18;       // tinggi garis scan (px layar)
        private const int BackgroundScanTrailCount = 5;         // berapa "ekor" garis di belakangnya
        private const float BackgroundScanTrailSpacing = 26f;   // jarak antar ekor (px layar)

        private Asset<Texture2D> planetAsset;
        private Asset<Texture2D> flashAsset;
        private Asset<Texture2D> boltAsset;
        private Asset<Texture2D> pulseAsset;

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
            planetBeatTimer = 999f;
            backgroundScanTimer = 0f;
            parallaxAnchor = Vector2.Zero;
            pulses.Clear();
            flashes.Clear();
            bolts.Clear();
        }

        public override bool IsActive() => isActive || intensity > 0.001f;

        public override Color OnTileColor(Color inColor) => inColor;

        // Awan vanilla ikut memudar pas sky kita masuk -- biar transisinya rapi, ga nabrak
        // sama awan putih yang ga nyambung sama tema merah-hitam ini.
        public override float GetCloudAlpha() => MathHelper.Clamp(1f - intensity, 0f, 1f);

        public override void Update(GameTime gameTime) {
            // 🛑 [ANTI SUNDIAL] dt DIHITUNG DARI STOPWATCH NYATA, BUKAN dari gameTime bawaan --
            // lihat penjelasan panjang di komentar atas class kenapa ini penting.
            double now = RealTimeClock.Elapsed.TotalSeconds;
            if (lastRealTimeSeconds < 0d) lastRealTimeSeconds = now; // panggilan pertama, ga ada delta
            float dt = (float)(now - lastRealTimeSeconds);
            lastRealTimeSeconds = now;
            if (dt <= 0f) return; // Update() nyusul kepanggil lagi dalam waktu nyata yg sama persis (efek Sundial) -- skip, jangan animasi maju sama sekali
            if (dt > 0.5f) dt = 1f / 60f; // safety net kalau ada lag spike gede / baru pertama load

            // Fade in/out halus pas boss mulai/selesai, bukan langsung "creg" nyala/mati.
            float fadeSpeed = 1.2f * dt;
            intensity = isActive
                ? MathHelper.Clamp(intensity + fadeSpeed, 0f, 1f)
                : MathHelper.Clamp(intensity - fadeSpeed, 0f, 1f);

            // 🛑 [RESET REVEAL + PARALLAX ANCHOR] Begitu transisi dari "mati total" ke "baru nyala"
            // kedeteksi, paksa semua progress balik ke 0 & re-anchor parallax ke posisi player
            // SEKARANG -- biar reveal & parallax-nya selalu "fresh" tiap kali Pluto baru muncul.
            if (isActive && !wasActiveLastFrame) {
                planetPopElapsed = 0f;
                revealElapsed = 0f;
                pulseSpawnTimer = Main.rand.NextFloat(0.6f, 1.2f); // pulse pertama ga langsung nyamber pas baru pop
                planetBeatTimer = 999f;
                backgroundScanTimer = 0f;
                parallaxAnchor = Main.LocalPlayer.Center;
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
                flashes.Clear();
                bolts.Clear();
                pulses.Clear();
                return;
            }

            planetRotation += PlanetRotationSpeed * dt;
            if (planetRotation > MathHelper.TwoPi) planetRotation -= MathHelper.TwoPi;

            pillarPulseTimer += dt * 1.3f;
            planetBeatTimer += dt;
            backgroundScanTimer += dt;

            UpdatePulse(dt);
            UpdateLightning(dt);
        }

        // 🛑 [DETAK JANTUNG] Munculin gelombang PlutoPulse baru tiap PulseIntervalMin-Max detik,
        // BARENGAN itu reset planetBeatTimer ke 0 biar planet ikut "berkedut" PERSIS di momen yang
        // sama gelombangnya mulai nyebar (lihat GetHeartbeatBump).
        private void UpdatePulse(float dt) {
            pulseSpawnTimer -= dt;
            if (pulseSpawnTimer <= 0f) {
                pulses.Add(new PulseWave { Life = 0f, MaxLife = PulseLifeDuration });
                planetBeatTimer = 0f;
                pulseSpawnTimer = Main.rand.NextFloat(PulseIntervalMin, PulseIntervalMax);
            }

            for (int i = pulses.Count - 1; i >= 0; i--) {
                pulses[i].Life += dt;
                if (pulses[i].Life >= pulses[i].MaxLife) pulses.RemoveAt(i);
            }
        }

        // 🛑 [PETIR INDEPENDEN] Flash & Bolt sekarang punya timer & interval SENDIRI-SENDIRI,
        // jadi kapan & di mana mereka nongol ga lagi nyambung satu sama lain.
        private void UpdateLightning(float dt) {
            int w = Main.screenWidth;
            int h = Main.screenHeight;

            flashSpawnTimer -= dt;
            if (flashSpawnTimer <= 0f && w > 0 && h > 0) {
                flashes.Add(new FlashInstance {
                    Position = new Vector2(Main.rand.NextFloat(w * 0.05f, w * 0.95f), Main.rand.NextFloat(h * 0.05f, h * 0.65f)),
                    Life = 0f,
                    MaxLife = Main.rand.NextFloat(0.30f, 0.50f),
                    Rotation = Main.rand.NextFloat(0f, MathHelper.TwoPi), // Flash boleh muter bebas.
                    Scale = Main.rand.NextFloat(0.35f, 0.80f),
                });
                flashSpawnTimer = MathHelper.Lerp(FlashIntervalMax, FlashIntervalMin, intensity) * (0.5f + Main.rand.NextFloat());
            }

            boltSpawnTimer -= dt;
            if (boltSpawnTimer <= 0f && w > 0 && h > 0) {
                bolts.Add(new BoltInstance {
                    Position = new Vector2(Main.rand.NextFloat(w * 0.05f, w * 0.95f), Main.rand.NextFloat(h * 0.05f, h * 0.65f)),
                    Life = 0f,
                    MaxLife = Main.rand.NextFloat(0.35f, 0.55f),
                    Scale = Main.rand.NextFloat(0.30f, 0.65f), // Bolt TIDAK dirotate -- dibiarin sesuai orientasi asli.
                });
                boltSpawnTimer = MathHelper.Lerp(BoltIntervalMax, BoltIntervalMin, intensity) * (0.5f + Main.rand.NextFloat());
            }

            for (int i = flashes.Count - 1; i >= 0; i--) {
                flashes[i].Life += dt;
                if (flashes[i].Life >= flashes[i].MaxLife) flashes.RemoveAt(i);
            }
            for (int i = bolts.Count - 1; i >= 0; i--) {
                bolts[i].Life += dt;
                if (bolts[i].Life >= bolts[i].MaxLife) bolts.RemoveAt(i);
            }
        }

        // Kurva "kedip ganda" khas kilat: nyambar cepet, sempet redup sekilas, nyambar lagi
        // lebih kecil, baru fade abis. Reusable buat Flash maupun Bolt.
        private static float GetFlickerAlpha(float life, float maxLife) {
            float t = life / maxLife;
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

        // 🛑 [DETAK JANTUNG] Kurva "kedutan" planet: naik cepet dari 0, balik turun ke 0 lagi
        // dalam BeatDuration detik -- dikaliin BeatStrength jadi tambahan scale sesaat.
        private static float GetHeartbeatBump(float beatTimer) {
            if (beatTimer >= BeatDuration || beatTimer < 0f) return 0f;
            float p = beatTimer / BeatDuration;
            return MathF.Sin(p * MathHelper.Pi) * (1f - p);
        }

        // 🛑 [PARALLAX PLANET] Seberapa jauh planet digeser dari posisi tengah normalnya,
        // berdasarkan seberapa jauh player udah gerak dari parallaxAnchor -- di-clamp biar ga
        // kebablasan walau player lari jauh banget selagi fight.
        private Vector2 GetPlanetParallaxOffset() {
            Vector2 playerDelta = Main.LocalPlayer.Center - parallaxAnchor;
            Vector2 offset = new(playerDelta.X * PlanetParallaxFactorX, playerDelta.Y * PlanetParallaxFactorY);
            if (offset.LengthSquared() > PlanetParallaxMaxOffset * PlanetParallaxMaxOffset) {
                offset = offset.SafeNormalize(Vector2.Zero) * PlanetParallaxMaxOffset;
            }
            return offset;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.001f) return;
            if (minDepth > FarBackgroundDepth || maxDepth < FarBackgroundDepth) return;

            int w = Main.screenWidth;
            int h = Main.screenHeight;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 planetPos = new Vector2(w * 0.5f, h * 0.34f) + GetPlanetParallaxOffset();

            DrawGradientBackground(spriteBatch, pixel, w, h);
            DrawBackgroundScan(spriteBatch, pixel, w, h);  // 🛑 scanline full-width di background utama (gantiin yang dulu di sprite gunung)
            DrawPillarGlow(spriteBatch, pixel, w, h);
            DrawPlutoPulse(spriteBatch, planetPos);      // 🛑 di belakang planet -- gelombangnya nyebar DARI BELAKANG planet, ikut parallax bareng.
            DrawPlutoPlanet(spriteBatch, planetPos);      // 🛑 SEKARANG parallax, ga statis lagi.
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

        // 🛑 [DETAK JANTUNG] Gelombang PlutoPulse (96x96 asli, putih polos) di-recolor merah &
        // di-scale BESAR (relatif ke lebar planet, otomatis nyesuain berapapun ukuran
        // plutoplanet.png yang beneran dipakai) -- mulai dari nutupin ~55% lebar planet (makanya
        // keliatan "keluar dari belakangnya"), terus nyebar sampai ~2.1x lebar planet sambil pudar.
        // `planetPos` dioper dari Draw() biar tetep nempel di posisi planet yang SEKARANG parallax.
        private void DrawPlutoPulse(SpriteBatch sb, Vector2 planetPos) {
            if (pulses.Count == 0) return;

            planetAsset ??= ModContent.Request<Texture2D>(PlanetTexturePath, AssetRequestMode.ImmediateLoad);
            pulseAsset ??= ModContent.Request<Texture2D>(PulseTexturePath, AssetRequestMode.ImmediateLoad);
            if (!planetAsset.IsLoaded || !pulseAsset.IsLoaded) return;

            Texture2D planetTex = planetAsset.Value;
            Texture2D pulseTex = pulseAsset.Value;
            Vector2 origin = pulseTex.Size() * 0.5f;

            float minScale = (planetTex.Width * PulseStartCoverage) / pulseTex.Width;
            float maxScale = (planetTex.Width * PulseEndCoverage) / pulseTex.Width;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (PulseWave p in pulses) {
                float t = EaseOutCubic(p.Life / p.MaxLife);
                float scale = MathHelper.Lerp(minScale, maxScale, t);
                float alpha = (1f - t) * intensity;
                if (alpha <= 0.01f) continue;

                Color color = new Color(255, 35, 30) * (alpha * 0.85f);
                sb.Draw(pulseTex, planetPos, null, color, planetRotation, origin, scale, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // Planet Pluto, muter pelan searah jarum jam. Planet ini yang "pop" MUNCUL DULUAN
        // (EaseOutBack, kesan mantul kecil) sebelum background gradient-nya sendiri selesai "wush"
        // nyebar dari tengah, DAN ikut "berdetak" (GetHeartbeatBump) PERSIS pas gelombang
        // PlutoPulse mulai nyebar. `planetPos` SEKARANG parallax (lihat GetPlanetParallaxOffset),
        // BUKAN lagi posisi tetap di tengah layar terus-terusan.
        private void DrawPlutoPlanet(SpriteBatch sb, Vector2 planetPos) {
            planetAsset ??= ModContent.Request<Texture2D>(PlanetTexturePath, AssetRequestMode.ImmediateLoad);
            if (!planetAsset.IsLoaded) return;

            Texture2D planet = planetAsset.Value;
            Vector2 origin = new(planet.Width * 0.5f, planet.Height * 0.5f);

            float popT = planetPopElapsed / PlanetPopDuration;
            float popScale = EaseOutBack(popT);
            float popAlpha = MathHelper.Clamp(popT, 0f, 1f);
            if (popAlpha <= 0f) return;

            float beatBump = GetHeartbeatBump(planetBeatTimer);
            float finalScale = popScale * (1f + beatBump * BeatStrength);

            // Halo merah lembut di belakang planet (bloom murahan: gambar planetnya sendiri,
            // diperbesar & additive transparan, biar berasa "berpendar") -- ikut kedut dikit juga.
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            float haloPulse = 0.7f + 0.3f * MathF.Sin(pillarPulseTimer * 0.6f);
            sb.Draw(planet, planetPos, null, new Color(255, 35, 35) * (0.32f * haloPulse * intensity * popAlpha), planetRotation, origin, 1.20f * finalScale, SpriteEffects.None, 0f);
            sb.Draw(planet, planetPos, null, new Color(255, 90, 90) * (0.16f * haloPulse * intensity * popAlpha), planetRotation, origin, 1.40f * finalScale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // Planet aslinya -- warna dibiarin natural (ga di-tint merah), biar tetep kebaca
            // sebagai "Planet Pluto", cuma alpha & scale-nya ngikutin pop-in + detak + fade sky.
            sb.Draw(planet, planetPos, null, Color.White * intensity * popAlpha, planetRotation, origin, finalScale, SpriteEffects.None, 0f);
        }

        // 🛑 [SCANLINE BACKGROUND UTAMA] Garis terang full-width yang nyapu dari ujung ATAS layar
        // ke BAWAH berulang-ulang, dikasih beberapa "ekor" fading di belakangnya biar ada kesan
        // gerak -- dulu ini nempel di sprite gunung, sekarang langsung di atas background utama.
        private void DrawBackgroundScan(SpriteBatch sb, Texture2D pixel, int w, int h) {
            float alpha = intensity;
            if (alpha <= 0.01f) return;

            float cycleT = (backgroundScanTimer % BackgroundScanCycleDuration) / BackgroundScanCycleDuration;
            float scanYBase = cycleT * h;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = 0; i < BackgroundScanTrailCount; i++) {
                float scanY = scanYBase - i * BackgroundScanTrailSpacing;
                if (scanY < 0f || scanY > h - BackgroundScanStripHeight) continue; // di luar layar / abis wrap -- skip aja

                float trailAlpha = (1f - i / (float)BackgroundScanTrailCount) * alpha;
                if (trailAlpha <= 0.01f) continue;

                Color color = new Color(255, 60, 40) * (trailAlpha * 0.55f);
                Rectangle dest = new(0, (int)scanY, w, BackgroundScanStripHeight);
                sb.Draw(pixel, dest, color);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // Kilat MERAH: Flash & Bolt INDEPENDEN -- masing-masing di-loop dari list-nya sendiri, ga
        // digambar berpasangan di titik & waktu yang sama.
        private void DrawLightning(SpriteBatch sb) {
            if (flashes.Count == 0 && bolts.Count == 0) return;

            flashAsset ??= ModContent.Request<Texture2D>(FlashTexturePath, AssetRequestMode.ImmediateLoad);
            boltAsset ??= ModContent.Request<Texture2D>(BoltTexturePath, AssetRequestMode.ImmediateLoad);
            if (!flashAsset.IsLoaded || !boltAsset.IsLoaded) return;

            Texture2D flashTex = flashAsset.Value;
            Texture2D boltTex = boltAsset.Value;
            Vector2 flashOrigin = flashTex.Size() * 0.5f;
            Vector2 boltOrigin = boltTex.Size() * 0.5f;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // 🛑 [RECOLOR MERAH] Tint lewat Color (bukan Color.White) + additive blend -- cara
            // paling ringan buat "narik" hue asli (biru) ke arah merah tanpa perlu shader baru.
            // Kalau ternyata di file asli-nya banyak area biru SOLID/opaque (bukan cuma glow
            // putih+alpha), hasil tint ini bisa keliatan agak gelap di area itu -- kabarin aja
            // kalau begitu, nanti dibikinin shader recolor khusus (mirip PlutoElectroRimLight.fx
            // yang udah ada) biar hasilnya presisi.
            foreach (FlashInstance f in flashes) {
                float alpha = GetFlickerAlpha(f.Life, f.MaxLife) * intensity;
                if (alpha <= 0.01f) continue;
                Color flashColor = new Color(255, 70, 60) * (alpha * 0.9f);
                sb.Draw(flashTex, f.Position, null, flashColor, f.Rotation, flashOrigin, f.Scale, SpriteEffects.None, 0f);
            }

            foreach (BoltInstance b in bolts) {
                float alpha = GetFlickerAlpha(b.Life, b.MaxLife) * intensity;
                if (alpha <= 0.01f) continue;
                Color boltColor = new Color(255, 45, 40) * alpha;
                sb.Draw(boltTex, b.Position, null, boltColor, 0f, boltOrigin, b.Scale, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
