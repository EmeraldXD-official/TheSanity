using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart;

namespace TheSanity.GlobalNPC.Bosses.Pluto
{
    // 🛑 [FIX AIM LASER] Sengaja dipisah jadi ModSystem sendiri (bukan digambar di dalam
    // PlutoHead.PreDraw() lagi), soalnya ModNPC.PreDraw() CUMA dipanggil Terraria kalau
    // hitbox si NPC sendiri (NPC.width/height + NPC.Center) masih lolos cek onscreen.
    // Itu artinya walau kode di dalam PreDraw() bisa aja gambar sampai ribuan pixel jauhnya,
    // begitu Pluto (titik pusatnya) keluar dari area culling, SELURUH PreDraw() -- termasuk
    // laser-nya -- ikut nggak dipanggil sama sekali, laser jadi hilang padahal seharusnya
    // masih "nembus" layar.
    //
    // Solusinya: gambar laser ini di PostDrawTiles(), yang jalan TIAP FRAME tanpa syarat,
    // independen dari NPC manapun lagi onscreen atau nggak -- persis pola yang udah dipakai
    // buat border arena (PlutoArenaBorderSystem.PostDrawTiles()). PostDrawTiles() jalan
    // sebelum pass NPC normal, jadi laser otomatis kegambar di BAWAH Pluto sendiri.
    public class PlutoAimLaserSystem : ModSystem
    {
        // Ganti texture ini kalau nama/path WhiteBeam berubah
        private const string LaserTexturePath = "TheSanity/Projectiles/WhiteBeam";

        // Berapa detik sebelum Pluto meluncur (dash) laser-nya ilang duluan (telegraph "aim").
        // 102 tick / 60 = 1.7 detik sebelum currentAimTimer nyampe titik dash beneran mulai.
        private const int LaserHideAtTick = 102;

        // 🛑 [FIX SMOOTH] Balik ke step kecil kayak versi paling awal (dulu 8f) supaya gradasi
        // merah-hitamnya halus lagi, TANPA balik ke masalah lama (garis kepotong / boros draw
        // call). Caranya: kita cuma gambar bagian laser yang KELIHATAN di layar (lihat
        // TryGetVisibleRange di bawah), jadi walaupun step-nya kecil, jumlah iterasi tetap
        // terbatas ~seukuran layar -- bukan seukuran total panjang laser.
        private const float LaserStep = 8f;

        // Batas pengaman jumlah segmen per frame (jaga-jaga kalau rotasi/posisi aneh bikin
        // rentang kelihatan jadi kegedean). Di kondisi normal nggak bakal kesentuh.
        private const int MaxSegmentsPerFrame = 600;

        private const float LaserScale = 0.35f;

        // Layer outline: sama warnanya & animasinya, cuma lebih GEDE (jadi kerasa kayak
        // "halo"/glow di luar beam inti) dan jauh lebih transparan.
        private const float OutlineScale = 0.85f;
        private const float OutlineOpacity = 0.28f;
        private const float CoreOpacity = 0.95f;

        public override void PostDrawTiles() {
            NPC pluto = FindActivePlutoHead();
            if (pluto == null) return;

            // Sama persis kondisi lama: cuma nyala pas Pattern 3 (Teleport Dash), Stage 0 (Aiming)
            if (pluto.ai[0] != 3f || pluto.ai[1] != 0f) return;

            int currentAimTimer = (int)pluto.ai[2];
            if (currentAimTimer >= LaserHideAtTick) return;

            DrawAimLaser(pluto);
        }

        private static NPC FindActivePlutoHead() {
            int plutoType = ModContent.NPCType<PlutoHead>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == plutoType) {
                    return npc;
                }
            }
            return null;
        }

        private static void DrawAimLaser(NPC npc) {
            Texture2D laserTex = ModContent.Request<Texture2D>(LaserTexturePath).Value;
            SpriteBatch spriteBatch = Main.spriteBatch;

            Vector2 beamDir = npc.rotation.ToRotationVector2();
            Rectangle middleSlice = new Rectangle(laserTex.Width / 2, 0, 1, laserTex.Height);
            Vector2 drawOrigin = new Vector2(0, laserTex.Height / 2f);

            // 🛑 [FIX "ADA BATASNYA" + BENERAN INFINITE] Alih-alih nentuin "panjang maksimal"
            // (angka tetap yang pasti bisa kepotong di suatu titik), kita hitung LANGSUNG rentang
            // jarak (tMin..tMax) di sepanjang garis yang beneran melintasi kotak layar saat ini.
            // Jadi garisnya secara logis tetap "tak terbatas" (nembus terus ke arah rotasi
            // Pluto), tapi yang digambar cuma potongan yang emang kelihatan -- persis kayak
            // cara game beneran nge-render garis "infinite".
            float margin = 400f; // buffer biar outline/glow di tepi layar juga ikut kegambar
            Rectangle screenRect = new Rectangle(
                (int)(Main.screenPosition.X - margin),
                (int)(Main.screenPosition.Y - margin),
                Main.screenWidth + (int)(margin * 2f),
                Main.screenHeight + (int)(margin * 2f)
            );

            if (!TryGetVisibleRange(npc.Center, beamDir, screenRect, out float tMin, out float tMax)) {
                return; // garis nggak nyentuh layar sama sekali, nggak usah gambar apa-apa
            }

            int segmentCount = (int)Math.Ceiling((tMax - tMin) / LaserStep);
            segmentCount = Math.Clamp(segmentCount, 1, MaxSegmentsPerFrame);
            float actualStep = (tMax - tMin) / segmentCount;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // Pass 1: OUTLINE -- digambar duluan (di belakang), lebih besar & jauh lebih transparan
            DrawLaserPass(spriteBatch, laserTex, middleSlice, drawOrigin, npc, beamDir, tMin, actualStep, segmentCount, OutlineScale, OutlineOpacity);

            // Pass 2: CORE -- beam utama, di atas outline
            DrawLaserPass(spriteBatch, laserTex, middleSlice, drawOrigin, npc, beamDir, tMin, actualStep, segmentCount, LaserScale, CoreOpacity);

            spriteBatch.End();
        }

        private static void DrawLaserPass(SpriteBatch spriteBatch, Texture2D laserTex, Rectangle middleSlice, Vector2 drawOrigin,
            NPC npc, Vector2 beamDir, float tMin, float step, int segmentCount, float thicknessScale, float maxOpacity) {

            for (int i = 0; i <= segmentCount; i++) {
                float distanceCovered = tMin + (i * step);
                Vector2 drawPos = npc.Center + (beamDir * distanceCovered) - Main.screenPosition;

                Color flashColor = GetLaserFlashColor(distanceCovered) * maxOpacity;

                // Sedikit overlap antar segmen (1.5x step) biar nggak ada celah tipis pas
                // rotasi/scale bikin ada rounding, tanpa bikin animasinya kelihatan pecah.
                Vector2 segmentScale = new Vector2((step * 1.5f) / middleSlice.Width, thicknessScale);
                spriteBatch.Draw(laserTex, drawPos, middleSlice, flashColor, npc.rotation, drawOrigin, segmentScale, SpriteEffects.None, 0f);
            }
        }

        // Warna gradasi merah-hitam yang jalan sepanjang laser -- dipisah jadi method sendiri
        // biar dipakai bareng-bareng sama pass outline & pass core (animasinya harus identik).
        private static Color GetLaserFlashColor(float distanceCovered) {
            float wavePhase = (Main.GlobalTimeWrappedHourly * 32f) - (distanceCovered * 0.012f);
            float colorLerpFactor = (float)(Math.Sin(wavePhase) + 1f) / 2f;
            return Color.Lerp(Color.Black, Color.Red, colorLerpFactor);
        }

        // Ray-AABB intersection (slab method) buat nyari rentang jarak [tMin, tMax] di sepanjang
        // arah beamDir (dari titik origin) yang masih ada di dalam rect. tMin dipaksa minimal 0
        // soalnya laser cuma maju ke depan (searah rotasi Pluto), nggak pernah ke belakang.
        private static bool TryGetVisibleRange(Vector2 origin, Vector2 dir, Rectangle rect, out float tMin, out float tMax) {
            tMin = 0f;
            tMax = float.MaxValue;

            if (!SlabIntersect(origin.X, dir.X, rect.Left, rect.Right, ref tMin, ref tMax)) return false;
            if (!SlabIntersect(origin.Y, dir.Y, rect.Top, rect.Bottom, ref tMin, ref tMax)) return false;

            tMin = Math.Max(tMin, 0f);
            return tMax > tMin;
        }

        private static bool SlabIntersect(float originComp, float dirComp, float boundMin, float boundMax, ref float tMin, ref float tMax) {
            if (Math.Abs(dirComp) < 0.0001f) {
                // Sejajar sama sumbu ini -- kalau originnya udah di luar slab, nggak akan pernah kena
                return originComp >= boundMin && originComp <= boundMax;
            }

            float t1 = (boundMin - originComp) / dirComp;
            float t2 = (boundMax - originComp) / dirComp;
            if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }

            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
            return tMin <= tMax;
        }
    }
}
