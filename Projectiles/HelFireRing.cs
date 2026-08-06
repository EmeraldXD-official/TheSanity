using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class HelFireRing : ModProjectile
    {
        // Menggunakan sprite FireRing.png di lokasi TheSanity/Projectiles/FireRing.png
        public override string Texture => "TheSanity/Projectiles/FireRing";

        public override void SetDefaults()
        {
            // UKURAN RING: 10 Block Radius (160 Pixel) = 320 Pixel Diameter
            // (Jika maksudnya 10 block diameter, ganti 320 menjadi 160)
            Projectile.width = 320;
            Projectile.height = 320;
            
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            
            Projectile.penetrate = -1; // Infinite Piercing agar bisa mengenai musuh secara berkelanjutan
            Projectile.tileCollide = false; // Menembus dinding
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2; // Terus diperbarui selama Yoyo Hel-Fire aktif
            
            // Mengatur cooldown hit agar damage terus-menerus masuk tiap 10 tick (~0.16 detik)
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            // Ambil index Yoyo induk dari ai[0]
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex < 0 || parentIndex >= Main.maxProjectiles)
            {
                Projectile.Kill();
                return;
            }

            Projectile parent = Main.projectile[parentIndex];

            // Jika Yoyo Hel-Fire mati/hilang, ring ikut hancur
            if (!parent.active || parent.type != ProjectileID.HelFire || parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            // KUNCI POSISI: Selalu menempel di pusat Yoyo Hel-Fire
            Projectile.Center = parent.Center;
            Projectile.timeLeft = 2; // Menjaga ring tetap hidup

            // ROTASI SEARAH JARUM JAM (Clockwise)
            Projectile.rotation += 0.08f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Inflict debuff Hellfire selama 4 detik (240 ticks)
            // Catatan: Di tModLoader 1.4.4, ID resmi Hellfire adalah BuffID.OnFire3 (ID: 323)
            target.AddBuff(BuffID.OnFire3, 240);
        }

        // Custom Rendering agar sprite 120x120 otomatis ter-scale pas dengan ukuran hitbox
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;

            // Menghitung faktor skala otomatis dari 120px ke ukuran hitbox (320px)
            float scale = Projectile.width / (float)texture.Width;

            // Menggambar aura api dengan sedikit warna menyala
            Main.EntitySpriteDraw(
                texture,
                drawPosition,
                null,
                Color.White * 0.9f,
                Projectile.rotation,
                origin,
                scale,
                SpriteEffects.None,
                0
            );

            return false; // Matikan gambar bawaan game
        }
    }
}