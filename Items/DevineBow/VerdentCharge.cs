using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TheSanity.Items.DevineBow
{
    // Projectile efek visual saja (tidak melukai siapa pun), muncul selama 1 detik
    // fase charge VerdentBow, menggunakan sprite ShineFlare sebagai kilau cahaya.
    public class VerdentCharge : ModProjectile
    {
        // Sesuaikan path ini dengan lokasi asli ShineFlare.png di folder project-mu,
        // formatnya "NamaMod/Path/Ke/Folder/ShineFlare" (tanpa ekstensi .png)
        public override string Texture => "TheSanity/Items/DevineBow/ShineFlare";

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60; // Samakan dengan ChargeDuration di VerdentBow (1 detik)
            Projectile.alpha = 255;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead || !owner.channel)
            {
                Projectile.Kill();
                return;
            }

            // Ikuti posisi ujung busur setiap tick (mengikuti arah hadap & gerak pemain selama charge).
            // Angka ini harus senada dengan MuzzleOffset di VerdentBow.cs supaya efeknya
            // menempel pas di ujung busur, bukan di tengah badan atau melayang jauh.
            Vector2 followOffset = owner.direction == 1 ? new Vector2(24, -6) : new Vector2(-24, -6);
            Projectile.Center = owner.Center + followOffset;

            // Progress 0 -> 1 selama 1 detik charge
            // CATATAN: ShineFlare.png adalah salib cahaya tipis yang sudah memanjang sampai
            // tepi kanvas (kemungkinan 512x512). Scale di sini SENGAJA dibuat sangat kecil
            // (0.04 - 0.25) supaya efeknya terlihat menempel di ujung busur, bukan menjulur
            // jauh melintasi layar. Naikkan pelan-pelan (misal jadi 0.3) kalau efeknya terlalu kecil.
            float progress = 1f - (Projectile.timeLeft / 60f);
            Projectile.scale = MathHelper.Lerp(0.04f, 0.25f, progress);
            Projectile.alpha = (int)MathHelper.Lerp(255, 40, progress);
            Projectile.rotation += 0.15f;

            // Partikel cahaya kecil berputar menuju titik charge
            if (Main.rand.NextBool(2))
            {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(18f, 18f);
                Dust d = Dust.NewDustPerfect(dustPos, 61, (Projectile.Center - dustPos) * 0.05f, 0, default, 1.5f);
                d.noGravity = true;
            }

            // Ledakan kilau kecil tepat sebelum charge selesai (menandakan volley akan ditembak)
            if (Projectile.timeLeft <= 5)
            {
                for (int i = 0; i < 12; i++)
                {
                    Vector2 dustVel = (MathHelper.TwoPi * i / 12f).ToRotationVector2() * 4f;
                   
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Pengaman: kalau asset ShineFlare belum ke-load / path salah, jangan sampai
            // melempar exception yang menghentikan proses Shoot() bow secara keseluruhan.
            if (!TextureAssets.Projectile[Projectile.type].IsLoaded)
            {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = tex.Size() / 2f;
            Color glowColor = Color.LightGreen * (1f - Projectile.alpha / 255f);

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, glowColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
