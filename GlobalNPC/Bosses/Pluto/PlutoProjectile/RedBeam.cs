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
    // 🛑 [RED BEAM] Laser lurus statis, ditembak sekali sama RedCrystal, lalu HIDUP CUMA 0,5
    // DETIK (auto-Kill sendiri lewat lifeTimer -- independen, gak nunggu RedCrystal-nya dash/
    // mati). ai[0] = whoAmI player yang jadi acuan smart-scan-nya. rotation di-set SEKALI pas
    // spawn (lihat RedCrystal.SpawnBeam) dan TIDAK berubah lagi selama beam ini hidup.
    //
    // 🛑 [ANIMASI MUNCUL/HILANG] Beam "timbul" (tipis -> tebal) di beberapa tick pertama, dan
    // "mengecil"/fade balik ke tipis-transparan di beberapa tick terakhir sebelum mati -- lihat
    // GetVisualScale().
    //
    // 🛑 [SMART SCAN] Beam ini SELALU nembus block yang ada DI ANTARA titik tembak & posisi
    // player (jadi player nggak bisa "cheese" nyembunyi di balik tembok), TAPI berhenti di
    // block solid PERTAMA yang ketemu SETELAH posisi player (+ buffer kecil). Jarak "sampai
    // player" dihitung pakai proyeksi dot-product di sepanjang arah beam -- jadi otomatis
    // benar buat beam arah manapun (horizontal, vertikal, diagonal), bukan cuma kiri-kanan,
    // dan blok yang levelnya "sejajar" player tapi masih di depan tetap ditembus wajar.
    // =====================================================================================
    public class RedBeam : ModProjectile
    {
        // 🛑 [FIX MissingResourceException] Loader tModLoader WAJIB dikasih 1 path Texture yang
        // valid buat tiap ModProjectile, walau kita gambar semuanya manual lewat PreDraw() (3
        // part: Bottom/Middle/Top). Tanpa override ini, dia bakal nebak default
        // ".../PlutoProjectile/RedBeam" (file yang emang sengaja gak dibikin, soalnya asetnya
        // 3 sprite terpisah) dan bikin mod gagal load. Diarahin ke Middle karena paling netral.
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedBeamMiddle";

        private const float MaxBeamLength = 2600f;
        private const float PastPlayerBuffer = 80f;
        private const float ScanStep = 16f; // 1 tile
        private const float BeamHitThickness = 22f;
        private const int RecomputeInterval = 6; // recompute tiap 0,1 detik biar murah di performa

        private const int BeamDebuffTime = 120; // 2 detik

        // 🛑 [AUTO-KILL 0,5 DETIK] SESUAI REQUEST: beam sekarang gak lagi idup selama
        // Projectile.timeLeft (3600 tick / 1 menit) -- dia bunuh diri sendiri (Kill()) tepat
        // 30 tick (0,5 detik) setelah nembak, gak peduli RedCrystal-nya lagi ngapain.
        private const int BeamLifeTime = 30; // 0,5 detik @60 tick/detik
        // 🛑 [ANIMASI MUNCUL/HILANG] Beam gak lagi langsung nongol/ilang instan -- di FadeTicks
        // pertama dia "timbul" (tipis -> tebal penuh), dan di FadeTicks terakhir sebelum mati
        // dia "mengecil"/fade balik ke tipis -> transparan. Dihitung dari lifeTimer, BUKAN dari
        // histori posisi, jadi murah & gak butuh state tambahan selain 1 int timer.
        private const int FadeTicks = 6; // ~0,1 detik buat animasi muncul & ilang

        private float beamLength = MaxBeamLength;
        private int recomputeTimer = 0;
        private int lifeTimer = 0;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            // 🛑 [FIX HILANG SAAT ORIGIN OFF-SCREEN] Terraria nge-skip PreDraw() sebuah projectile
            // kalau bounding box (width/height) bawaannya dianggap di luar layar -- padahal beam
            // ini SECARA VISUAL bisa "menjulur" jauh (sampai MaxBeamLength = 2600px) dari titik
            // originnya. Kalau width/height cuma 8x8 (ukuran lama), begitu titik ORIGIN (paling
            // deket RedCrystal) keluar layar dikit aja, Terraria anggep seluruh projectile ini
            // "di luar layar" dan LANGSUNG skip manggil PreDraw() sama sekali -- padahal ujung
            // beam yang jauh masih kelihatan di layar. Fix: bikin bounding box-nya persegi GEDE
            // yang nutupin radius MaxBeamLength ke SEGALA arah (karena beam bisa ngarah ke mana
            // aja), supaya Terraria tetap manggil PreDraw() selama SEBAGIAN mana pun beam ini
            // masih kelihatan -- ini AMAN buat collision karena Colliding() di-override total
            // (gak pernah pakai width/height bawaan buat deteksi hit).
            int hitboxSize = (int)(MaxBeamLength * 2f);
            Projectile.width = hitboxSize;
            Projectile.height = hitboxSize;
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

            // 🛑 [AUTO-KILL 0,5 DETIK] Beam sekarang punya umur sendiri yang pendek & pasti --
            // gak lagi nunggu RedCrystal-nya dash/mati atau nunggu timeLeft abis.
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

        private float ComputeBeamLength(Player target) {
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            if (!target.active || target.dead) return MaxBeamLength;

            float projectedDist = Vector2.Dot(target.Center - Projectile.Center, dir);
            if (projectedDist < 0f) projectedDist = 0f;

            float scanStart = projectedDist + PastPlayerBuffer;
            if (scanStart >= MaxBeamLength) return MaxBeamLength;

            float dist = scanStart;
            while (dist < MaxBeamLength) {
                Vector2 point = Projectile.Center + dir * dist;
                if (Collision.SolidCollision(point - new Vector2(4f, 4f), 8, 8)) {
                    return dist;
                }
                dist += ScanStep;
            }
            return MaxBeamLength;
        }

        // Hitbox custom: garis tipis dari titik tembak sampai beamLength (bukan kotak default
        // projectile), supaya collision-nya sesuai visual laser yang panjang & bisa miring.
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

            // 🛑 Beda sama RedCrystal: sprite beam (Bottom/Middle/Top) semuanya digambar MENGHADAP
            // KANAN, jadi rotasinya langsung dipakai apa adanya, TANPA offset PiOver2.
            float rotation = Projectile.rotation;
            Vector2 dir = rotation.ToRotationVector2();
            Vector2 screenOrigin = Projectile.Center - Main.screenPosition;

            // 🛑 [MUNCUL/HILANG] 0 di awal umur -> 1 (di FadeTicks pertama, "timbul"), tetap 1 di
            // tengah, lalu 1 -> 0 di FadeTicks terakhir sebelum Kill() ("mengecil"/fade away).
            // Dipakai buat scale ketebalan (Y) beam SEKALIGUS alpha warnanya.
            float visualScale = GetVisualScale();

            // 🛑 [RECOLOR] Sebelumnya Color.White (polos, warna asli texture apa adanya). Sekarang
            // dikasih 2 warna: INTI merah terang tapi agak gelap, dan OUTLINE/GLOW merah yang lebih
            // TERANG dari inti (bukan lebih gelap kayak outline biasa), digambar duluan di belakang
            // pakai blend Additive supaya numpuk jadi efek nyala, bukan garis pinggir keras.
            Color coreColor = new Color(190, 15, 15) * visualScale;              // merah terang agak gelap (inti)
            Color outlineColor = new Color(255, 70, 45) * 0.55f * visualScale;   // merah lebih terang (outline/glow)

            // Offset kecil ke 8 arah -- ini yang bikin efek "outline" ngelilingin sprite inti,
            // soalnya kita gak punya akses gampang ke pixel-shader outline murni di draw call biasa.
            Vector2[] outlineOffsets = {
                new Vector2( 2f,  0f), new Vector2(-2f,  0f),
                new Vector2( 0f,  2f), new Vector2( 0f, -2f),
                new Vector2( 2f,  2f), new Vector2(-2f,  2f),
                new Vector2( 2f, -2f), new Vector2(-2f, -2f),
            };

            // --- PASS 1: outline/glow, Additive biar numpuk terang bukan malah nabrak jadi item ---
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            foreach (Vector2 off in outlineOffsets) {
                DrawBeamSegments(texBottom, texMiddle, texTop, screenOrigin + off, dir, rotation, outlineColor, visualScale);
            }
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // --- PASS 2: warna inti, digambar di atas outline, TANPA offset ---
            DrawBeamSegments(texBottom, texMiddle, texTop, screenOrigin, dir, rotation, coreColor, visualScale);

            return false;
        }

        // 0 -> 1 pas baru nembak ("timbul"), 1 pas lagi solid di tengah umur, 1 -> 0 pas mau mati
        // ("mengecil"/fade away). Murni fungsi dari lifeTimer, gak butuh state tambahan.
        private float GetVisualScale() {
            if (lifeTimer < FadeTicks) {
                return (float)lifeTimer / FadeTicks;
            }
            int remaining = BeamLifeTime - lifeTimer;
            if (remaining < FadeTicks) {
                return MathHelper.Clamp((float)remaining / FadeTicks, 0f, 1f);
            }
            return 1f;
        }

        // Dipisah jadi method sendiri karena dipanggil 2x (outline & inti) -- cuma beda warna &
        // titik origin (offset), logic Bottom/Middle/Top-nya sama persis kayak versi lama.
        // `visualScale` dipakai buat scale ketebalan (Y) sprite -- ini yang bikin efek "timbul"
        // (tipis ke tebal) & "mengecil" (tebal ke tipis) pas beam baru nongol/mau ilang.
        private void DrawBeamSegments(Texture2D texBottom, Texture2D texMiddle, Texture2D texTop, Vector2 origin, Vector2 dir, float rotation, Color drawColor, float visualScale) {
            Vector2 segScale = new Vector2(1f, visualScale);

            Vector2 originBottom = new Vector2(0f, texBottom.Height / 2f);
            Main.EntitySpriteDraw(texBottom, origin, null, drawColor, rotation, originBottom, segScale, SpriteEffects.None, 0);

            float drawn = texBottom.Width;
            float middleEnd = Math.Max(beamLength - texTop.Width, texBottom.Width);
            Vector2 originMiddle = new Vector2(0f, texMiddle.Height / 2f);

            while (drawn < middleEnd) {
                Vector2 segPos = origin + dir * drawn;
                Main.EntitySpriteDraw(texMiddle, segPos, null, drawColor, rotation, originMiddle, segScale, SpriteEffects.None, 0);
                drawn += texMiddle.Width;
            }

            Vector2 originTop = new Vector2(0f, texTop.Height / 2f);
            Vector2 topPos = origin + dir * Math.Max(beamLength - texTop.Width, 0f);
            Main.EntitySpriteDraw(texTop, topPos, null, drawColor, rotation, originTop, segScale, SpriteEffects.None, 0);
        }
    }
}
