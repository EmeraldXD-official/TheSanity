using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Skeletron
{
    // === Beam Charge Blaster (dipakai SkelySkull, pattern 3) ===
    // Laser lurus sekali tembak. Sprite WhiteBeam.png (74x26, motif chevron
    // dobel) DITILING berulang sepanjang beam — bukan di-stretch 1 gambar
    // doang, biar motifnya gak gepeng pas beam-nya panjang.
    //
    // 3 fase (semua dihitung dari Projectile.ai[0], auto-sync standar bareng
    // paket projectile — gak perlu SendExtraAI manual):
    //   Growing -> beam manjang dari 0 ke MaxLength (damage MASIH 0)
    //   Active  -> panjang penuh, damage AKTIF (BeamDamage)
    //   Fading  -> panjang tetep, alpha turun ke 0 lalu Kill()
    // Sama pola kayak BigBoneSpike: damage cuma nyala pas fase "bahaya".
    public class ChargeBlaster : ModProjectile
    {
        const int GrowTime = 8;
        const int ActiveTime = 14;
        const int FadeTime = 10;
        public const int TotalDuration = GrowTime + ActiveTime + FadeTime;

        const float MaxLength = 1200f; // sengaja jauh lebih dari jarak layar biasa, biar beam kerasa "nembus"
        const float Thickness = 22f;   // tebal hitbox & visual beam

        // === Frame sprite beam (ChargeBlaster.png, 74x26) ===
        // Sprite-nya BUKAN 1 gambar utuh buat di-tile — dia 3 frame nempel
        // berurutan ke kanan: [cap awal (bulat)] [body (lurus, tileable)]
        // [ujung (lancip)]. Lebar cap & body 25px, sisa lebar tekstur
        // (tex.Width - 50) otomatis jadi lebar ujung, biar tetep bener
        // kalau suatu saat asetnya diganti/di-resize.
        const int BeamStartCapWidth = 25;
        const int BeamBodyWidth = 25;

        public const int BeamDamage = 26;

        // tint biru muda sesuai request user
        static readonly Color TintColor = new Color(150, 210, 255);

        Vector2 direction = Vector2.UnitY;
        float currentLength = 0f;

        // FIX (phase 2): dulu Thickness selalu konstan, padahal sekarang
        // Head sendiri (bukan cuma SkelySkull) bisa nembakin beam ini dalam
        // versi "dilebarin" biar keliatan sepadan sama ukuran boss.
        // Dititipin lewat ai[1] pas NewProjectile() (lihat Fire() di bawah),
        // dibaca sekali di OnSpawn. ai[1] <= 0 (termasuk default 0 dari
        // pemanggil lama yang belum tau parameter ini) dianggap "1x normal",
        // BUKAN beam hilang, biar backward-compatible sama SkelySkull yang
        // manggil Fire() tanpa parameter ini.
        float thicknessMul = 1f;
        float EffectiveThickness => Thickness * thicknessMul;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Skeletron/ChargeBlaster";

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration + 5; // buffer kecil
            Projectile.damage = 0; // aktif belakangan pas fase Active
        }

        public override void OnSpawn(IEntitySource source)
        {
            // CATATAN: arah beam "dititipkan" lewat parameter velocity pas
            // NewProjectile() (lihat Fire() di bawah) — bukan beneran dipakai
            // buat gerak, cuma numpang bawa data arah. Begitu sampai sini
            // langsung dibaca terus di-nolkan biar beam-nya DIAM di tempat
            // (cuma manjang, gak geser posisi).
            direction = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = direction.ToRotation();

            thicknessMul = Projectile.ai[1] > 0f ? Projectile.ai[1] : 1f;
        }

        public override void AI()
        {
            Projectile.ai[0]++;
            float timer = Projectile.ai[0];

            if (timer <= GrowTime)
            {
                currentLength = MaxLength * EaseOutCubic(timer / GrowTime);
                Projectile.damage = 0;
            }
            else if (timer <= GrowTime + ActiveTime)
            {
                currentLength = MaxLength;
                Projectile.damage = BeamDamage;
            }
            else
            {
                currentLength = MaxLength;
                Projectile.damage = 0;

                if (timer >= TotalDuration)
                    Projectile.Kill();
            }

            if (Main.rand.NextBool(2))
                SpawnEdgeSpark();
        }

        void SpawnEdgeSpark()
        {
            Vector2 pos = Projectile.Center + direction * Main.rand.NextFloat(currentLength);
            Vector2 perp = direction.RotatedBy(MathHelper.PiOver2);
            Dust d = Dust.NewDustPerfect(pos + perp * Main.rand.NextFloat(-EffectiveThickness / 2f, EffectiveThickness / 2f),
                ModContent.DustType<VoidSparkDust>(), Vector2.Zero);
            d.noGravity = true;
            d.color = TintColor;
            d.scale = 0.6f;
        }

        // CATATAN: signature CheckAABBvLineCollision (khususnya urutan &
        // jumlah parameter) bisa beda dikit antar versi tModLoader — cek
        // dokumentasi ModProjectile/Collision terkini kalau ternyata gak
        // cocok pas compile, sama kayak catatan versi-dependent lain di
        // file-file boss ini.
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (Projectile.damage <= 0 || currentLength <= 1f) return false;

            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + direction * currentLength;

            // FIX: versi tModLoader ini nuntut parameter terakhir sebagai
            // `ref float collisionPoint` (bukan optional) — dia dipakai
            // internal buat nyimpen posisi titik collision di sepanjang
            // garis, tapi kita gak butuh nilainya jadi cuma didiemin.
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, EffectiveThickness, ref collisionPoint);
        }

        static float EaseOutCubic(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            float f = t - 1f;
            return f * f * f + 1f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;

            float fadeAlpha = 1f;
            float timer = Projectile.ai[0];
            if (timer <= GrowTime)
                fadeAlpha = timer / GrowTime; // fade-in bareng manjang
            else if (timer > GrowTime + ActiveTime)
                fadeAlpha = 1f - MathHelper.Clamp((timer - GrowTime - ActiveTime) / FadeTime, 0f, 1f);

            Color drawColor = Color.Lerp(TintColor, Color.White, 0.3f) * fadeAlpha;
            float thicknessScale = EffectiveThickness / tex.Height;
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f); // pivot di ujung KIRI, dipakai sama buat tiap segmen (origin itu relatif ke sourceRect masing2, bukan ke tex penuh)

            int tipWidth = tex.Width - BeamStartCapWidth - BeamBodyWidth;

            // 1) Cap awal (bulat, statis) — nempel di pangkal beam. Di-clip
            // dari KANAN kalau beam masih kependekan (awal fase Growing).
            float drawn = 0f;
            int capDraw = (int)MathHelper.Clamp(currentLength, 0f, BeamStartCapWidth);
            if (capDraw > 0)
            {
                Rectangle capSrc = new Rectangle(0, 0, capDraw, tex.Height);
                Main.spriteBatch.Draw(tex, basePos, capSrc, drawColor, Projectile.rotation,
                    origin, new Vector2(1f, thicknessScale), SpriteEffects.None, 0f);
            }
            drawn += capDraw;

            // 2) Sisain ruang buat ujung di paling belakang currentLength,
            // baru sisanya diisi body yang di-tile berulang (bukan seluruh
            // tex, cuma potongan body-nya doang biar polanya gak gepeng/pecah).
            float tipSpace = MathHelper.Clamp(currentLength - drawn, 0f, tipWidth);
            float bodySpace = MathHelper.Max(0f, currentLength - drawn - tipSpace);

            for (float d = 0f; d < bodySpace; d += BeamBodyWidth)
            {
                int segWidth = (int)MathHelper.Clamp(bodySpace - d, 1f, BeamBodyWidth);
                Rectangle bodySrc = new Rectangle(BeamStartCapWidth, 0, segWidth, tex.Height);
                Vector2 drawPos = basePos + direction * (drawn + d);

                Main.spriteBatch.Draw(tex, drawPos, bodySrc, drawColor, Projectile.rotation,
                    origin, new Vector2(1f, thicknessScale), SpriteEffects.None, 0f);
            }
            drawn += bodySpace;

            // 3) Ujung (lancip, statis) — selalu digambar PAS di currentLength
            // (bukan numpuk abis body), biar lancipnya kelihatan di ujung
            // beam yang sebenarnya. Kalau ruang belum cukup (awal Growing),
            // ambil potongan dari sisi KANAN source (bagian paling lancip)
            // biar ujung tetep "nongol" duluan pas beam baru mulai manjang.
            if (tipSpace > 0f)
            {
                int tipDraw = (int)tipSpace;
                int tipSrcX = BeamStartCapWidth + BeamBodyWidth + (tipWidth - tipDraw);
                Rectangle tipSrc = new Rectangle(tipSrcX, 0, tipDraw, tex.Height);
                Vector2 drawPos = basePos + direction * drawn;

                Main.spriteBatch.Draw(tex, drawPos, tipSrc, drawColor, Projectile.rotation,
                    origin, new Vector2(1f, thicknessScale), SpriteEffects.None, 0f);
            }

            return false;
        }

        // === Dipanggil dari SkelySkull.Fire() (dan sekarang juga langsung
        // dari Head sendiri pas phase 2, lihat BeamMimic di
        // SkeletronReworkGlobalNPC) ===
        // thicknessScale opsional: 1f = ukuran normal (dipakai SkelySkull),
        // >1f = beam "dilebarin" (dipakai Head pas mimic pattern 3 biar
        // proporsional sama ukuran boss). Dititipin lewat ai[1], dibaca di
        // OnSpawn — lihat catatan thicknessMul di atas.
        public static void Fire(IEntitySource source, Vector2 origin, Vector2 direction, float thicknessScale = 1f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Projectile.NewProjectile(source, origin, direction, ModContent.ProjectileType<ChargeBlaster>(), 0, 0f, Main.myPlayer, 0f, thicknessScale);
        }
    }
}