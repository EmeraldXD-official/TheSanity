using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    // =====================================================================================
    // 🛑 [RED BEAM] Laser lurus statis, ditembak sekali sama RedCrystal. ai[0] = whoAmI player
    // yang jadi acuan smart-scan-nya. rotation di-set SEKALI pas spawn (lihat
    // RedCrystal.SpawnBeam) dan TIDAK berubah lagi selama beam ini hidup.
    //
    // 🛑 [SESUAI REQUEST - HIDUP LEBIH LAMA] Dulu beam cuma hidup 0,5 detik (30 tick) --
    // KEPENDEKAN sampai animasi "membesar"-nya nyaris gak sempet kelihatan. Sekarang hidup
    // BeamLifeTime = 90 tick (1,5 detik), dan durasi animasi membesarnya (GrowTicks) juga
    // dipanjangin jadi 20 tick (~0,33 detik) biar proses "tipis -> tebal"-nya beneran kebaca
    // mata, bukan sekelebat doang.
    //
    // 🛑 [SESUAI REQUEST - PANJANG "TAK TERBATAS"] ScanSafetyCap cuma jaring pengaman performa
    // (biar loop scan-nya gak jalan selamanya kalau somehow gak ada tembok sama sekali) --
    // secara praktis beam ini SELALU nembus sampai ketemu block solid pertama (smart-scan),
    // BUKAN batas visual/gameplay yang bakal kena di pertarungan normal.
    //
    // 🛑 [SESUAI REQUEST - PAKAI SPRITE PNG LAGI, TAPI TETAP MURAH] Balik pakai 3 sprite asli
    // (Bottom/Middle/Top) kayak versi awal, TAPI render-nya TIDAK LAGI nge-tile sprite Middle
    // berkali-kali sepanjang beam (itu yang bikin lag ala boss cacing pas beam-nya numpuk jauh).
    // Sekarang badan beam digambar dengan CUMA SATU sprite Middle yang di-STRETCH (scale.X)
    // buat nutupin seluruh sisa panjangnya di antara ujung Bottom & Top -- jadi TOTAL draw
    // call TETAP FIX (3 sprite: Bottom + 1 Middle stretched + Top) gak peduli beam-nya
    // sepanjang apa atau berapa banyak beam yang lagi numpuk bareng di layar.
    // =====================================================================================
    public class RedBeam : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedBeamMiddle";

        // 🛑 [TAK TERBATAS] Jaring pengaman performa doang -- lihat catatan panjang di atas.
        private const float ScanSafetyCap = 50000f; // ~3125 block, jauh di atas ukuran arena manapun
        private const float PastPlayerBuffer = 80f;
        private const float ScanStep = 32f; // 2 tile -- dikasarin dikit biar loop scan yang panjang tetep murah
        private const float BeamHitThickness = 22f; // ketebalan FINAL buat hit-detection (gak ikut animasi)
        private const int RecomputeInterval = 6; // recompute tiap 0,1 detik biar murah di performa

        private const int BeamDebuffTime = 120; // 2 detik

        // 🛑 [HIDUP LEBIH LAMA] SESUAI REQUEST: dari 30 tick (0,5 detik) jadi 90 tick (1,5 detik).
        private const int BeamLifeTime = 90;
        // 🛑 [ANIMASI MEMBESAR LEBIH LAMA] Dari 6 tick (0,1 detik) jadi 20 tick (~0,33 detik) di
        // awal (tipis -> tebal) supaya proses "melebar"-nya beneran kelihatan, bukan sekelebat.
        // Ketebalan mulai dari StartThickness (SAMA PERSIS kayak garis aim RedCrystal, biar
        // nyambung mulus) ke FullThickness. Di akhir umurnya (ShrinkTicks tick terakhir sebelum
        // Kill()), ketebalan mengecil balik ke nyaris 0.
        private const float StartThickness = 2f; // = lineThickness di RedCrystal.DrawAimLine
        private const float FullThickness = BeamHitThickness;
        private const int GrowTicks = 20;
        private const int ShrinkTicks = 15;

        private float beamLength = ScanSafetyCap;
        private int recomputeTimer = 0;
        private int lifeTimer = 0;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            // 🛑 [FIX LASER GAK KELIHATAN - INI AKAR MASALAHNYA] Sebelumnya di sini width/height
            // dipaksa RAKSASA (~100.000px), dengan ASUMSI Terraria bakal nyembunyiin (cull) beam
            // ini kalau hitboxnya kecil dan titik tembaknya keluar layar. Asumsi itu SALAH --
            // Projectile (beda sama NPC) TERNYATA GAK di-cull berdasarkan hitbox di sini sama
            // sekali. Buktinya: garis aim RedCrystal (lihat DrawAimLine() di RedCrystal.cs) itu
            // PANJANGNYA SAMA (50.000px) dan tetap kegambar mulus walau hitbox crystal-nya cuma
            // 28x28 kecil. Jadi hitbox raksasa itu manfaatnya NOL, tapi efek sampingnya FATAL:
            // Terraria.Projectile.NewProjectile() memperlakukan posisi yang dikasih sebagai POJOK
            // KIRI-ATAS hitbox (bukan titik tengah) -- begitu hitboxnya sebesar itu, titik pusat
            // beam yang BENERAN kegambar (Projectile.Center) kegeser PULUHAN RIBU pixel dari titik
            // tembak yang bener, alias render-nya nongol RATUSAN layar jauhnya dari kamera manapun.
            // Ini biang kerok "laser gak kelihatan"-nya. Sekarang hitbox dibalikin ke ukuran wajar
            // (sama kecilnya kayak RedCrystal), lebih murah buat engine juga.
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600;
            Projectile.alpha = 0;
        }

        public override void AI() {
            int targetIdx = (int)Projectile.ai[0];
            if (targetIdx < 0 || targetIdx >= Main.maxPlayers) {
                Projectile.Kill();
                return;
            }

            lifeTimer++;
            if (lifeTimer >= BeamLifeTime) {
                Projectile.Kill();
                return;
            }

            Player target = Main.player[targetIdx];

            recomputeTimer++;
            if (recomputeTimer >= RecomputeInterval) {
                recomputeTimer = 0;
                beamLength = ComputeBeamLength(target);
            }

            // Beam diem di titik tembaknya -- yang gerak nantinya cuma RedCrystal pas fase dash
            // (lihat RedCrystal.cs Stage.Dash), beam ini independen & tetap hidup di tempatnya.
            Projectile.velocity = Vector2.Zero;
        }

        // 🛑 [SMART SCAN - TAK TERBATAS] SELALU nembus block yang ada DI ANTARA titik tembak &
        // posisi player (jadi player nggak bisa "cheese" nyembunyi di balik tembok), TAPI
        // berhenti di block solid PERTAMA yang ketemu SETELAH posisi player (+ buffer kecil).
        private float ComputeBeamLength(Player target) {
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            if (!target.active || target.dead) return ScanSafetyCap;

            float projectedDist = Vector2.Dot(target.Center - Projectile.Center, dir);
            if (projectedDist < 0f) projectedDist = 0f;

            float scanStart = projectedDist + PastPlayerBuffer;
            if (scanStart >= ScanSafetyCap) return ScanSafetyCap;

            float dist = scanStart;
            while (dist < ScanSafetyCap) {
                Vector2 point = Projectile.Center + dir * dist;
                if (Collision.SolidCollision(point - new Vector2(4f, 4f), 8, 8)) {
                    return dist;
                }
                dist += ScanStep;
            }
            return ScanSafetyCap;
        }

        // Hitbox custom: garis tipis dari titik tembak sampai beamLength (bukan kotak default
        // projectile), supaya collision-nya sesuai visual laser yang panjang & bisa miring.
        // Damage-nya AKTIF dari tick pertama beam ini ada (gak nunggu animasi membesar kelar).
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 start = Projectile.Center;
            Vector2 end = start + dir * beamLength;

            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            Vector2 closest = ClosestPointOnSegment(start, end, targetCenter);
            float distSq = Vector2.DistanceSquared(closest, targetCenter);

            float hitRadius = BeamHitThickness * 0.5f + Math.Max(targetHitbox.Width, targetHitbox.Height) * 0.5f;
            return distSq <= hitRadius * hitRadius;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p) {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 0.0001f) return a;
            float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            return a + ab * t;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), BeamDebuffTime);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texBottom = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedBeamBottom").Value;
            Texture2D texMiddle = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedBeamMiddle").Value;
            Texture2D texTop = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedBeamTop").Value;

            // Sprite (Bottom/Middle/Top) semuanya digambar MENGHADAP KANAN, jadi rotasinya
            // langsung dipakai apa adanya, TANPA offset PiOver2.
            float rotation = Projectile.rotation;
            Vector2 dir = rotation.ToRotationVector2();
            Vector2 screenOrigin = Projectile.Center - Main.screenPosition;

            // 🛑 [MEMBESAR] Ketebalan (scale Y) nge-lerp StartThickness -> FullThickness di
            // GrowTicks tick pertama, lalu FullThickness -> nyaris 0 di ShrinkTicks tick
            // terakhir sebelum Kill(). Alpha warnanya TETAP PENUH sepanjang umur beam.
            float visualScale = GetVisualScale();

            Color coreColor = new Color(190, 15, 15);              // merah terang agak gelap (inti)
            Color glowColor = new Color(255, 70, 45) * 0.55f;      // merah lebih terang (glow)

            // 🛑 [FIX LAG - GLOW DIPANGKAS] Dulu outline-nya "dipalsuin" dengan nge-draw ulang
            // SELURUH beam 8 KALI (offset kecil ke 8 arah) di atas 3 sprite = 24 draw call CUMA
            // buat glow doang, ditambah 3 draw call core = 27 draw call PER BEAM PER FRAME. Kalau
            // ada belasan beam numpuk bareng (gampang kejadian pas banyak crystal nembak beruntun),
            // itu ratusan draw call ekstra tiap frame CUMA buat efek pinggiran yang tipis banget
            // bedanya -- ini sumber utama lag-nya, BUKAN soal transparent/nggak. Sekarang glow-nya
            // cukup SATU pass ekstra (3 sprite, digambar lebih LEBAR pakai visualScale yang
            // di-boost dikit, bukan di-offset ke 8 arah) -- visualnya tetap ada "aura" lembut di
            // pinggiran, tapi draw call PER BEAM turun dari 27 jadi 6 (~4-5x lebih murah).
            float glowScale = visualScale * 1.8f;

            // --- PASS 1: glow, Additive biar numpuk terang bukan malah nabrak jadi item ---
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            DrawBeamSegments(texBottom, texMiddle, texTop, screenOrigin, rotation, glowColor, glowScale);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // --- PASS 2: warna inti, digambar di atas glow, ketebalan normal ---
            DrawBeamSegments(texBottom, texMiddle, texTop, screenOrigin, rotation, coreColor, visualScale);

            return false;
        }

        // StartThickness -> FullThickness di GrowTicks tick pertama (nyambung mulus dari garis
        // aim RedCrystal yang emang udah setebal StartThickness itu). Lalu FullThickness ->
        // ~0 di ShrinkTicks terakhir sebelum Kill() beneran.
        private float GetVisualScale() {
            if (lifeTimer < GrowTicks) {
                float t = (float)lifeTimer / GrowTicks;
                return MathHelper.Lerp(StartThickness, FullThickness, t) / FullThickness;
            }
            int remaining = BeamLifeTime - lifeTimer;
            if (remaining < ShrinkTicks) {
                float t = MathHelper.Clamp((float)remaining / ShrinkTicks, 0f, 1f);
                return MathHelper.Lerp(0.05f, FullThickness, t) / FullThickness;
            }
            return 1f;
        }

        // 🛑 [SATU DRAW BUAT BADAN BEAM, BUKAN TILE] Dulu bagian tengah beam nge-tile texMiddle
        // berkali-kali (while-loop, sebanyak beamLength/texMiddle.Width kali) -- makin panjang
        // beamnya, makin banyak draw call, PERSIS pola render "boss cacing" yang lag parah kalau
        // banyak beam numpuk jauh. Sekarang texMiddle CUMA digambar SEKALI, di-STRETCH (scale.X)
        // biar nutupin seluruh jarak antara ujung Bottom & Top -- jadi TOTAL draw call buat
        // badan beam ini SELALU 3 (Bottom + Middle-stretched + Top), gak peduli beamLength-nya
        // berapa atau ada berapa banyak beam lain yang lagi aktif bareng.
        private void DrawBeamSegments(Texture2D texBottom, Texture2D texMiddle, Texture2D texTop, Vector2 origin, float rotation, Color drawColor, float visualScale) {
            Vector2 capScale = new Vector2(1f, visualScale);
            Vector2 dir = new Vector2((float)Math.Cos(rotation), (float)Math.Sin(rotation));

            Vector2 originBottom = new Vector2(0f, texBottom.Height / 2f);
            Main.EntitySpriteDraw(texBottom, origin, null, drawColor, rotation, originBottom, capScale, SpriteEffects.None, 0);

            float middleSpan = beamLength - texBottom.Width - texTop.Width;
            if (middleSpan > 0f) {
                Vector2 middlePos = origin + dir * texBottom.Width;
                Vector2 originMiddle = new Vector2(0f, texMiddle.Height / 2f);
                Vector2 middleScale = new Vector2(middleSpan / texMiddle.Width, visualScale);
                Main.EntitySpriteDraw(texMiddle, middlePos, null, drawColor, rotation, originMiddle, middleScale, SpriteEffects.None, 0);
            }

            Vector2 originTop = new Vector2(0f, texTop.Height / 2f);
            Vector2 topPos = origin + dir * Math.Max(beamLength - texTop.Width, texBottom.Width);
            Main.EntitySpriteDraw(texTop, topPos, null, drawColor, rotation, originTop, capScale, SpriteEffects.None, 0);
        }
    }
}
