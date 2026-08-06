using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class AmazonStinger : ModProjectile
    {
        // Menggunakan sprite internal dari Stinger vanilla (ID: 55)
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Stinger;

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            
            // Set damage ke Melee
            Projectile.DamageType = DamageClass.Melee; 
            
            // Infinite Piercing (bisa menembus musuh tanpa batas)
            Projectile.penetrate = -1; 
            
            // TIDAK bisa menembus blok (hancur jika menabrak tile)
            Projectile.tileCollide = true; 
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300; // Hilang setelah 5 detik
        }

        public override void AI()
        {
            // Karena sprite aslinya default mengarah ke atas, 
            // kita tambahkan PiOver2 (90 derajat) agar ujung paku menghadap ke arah terbang velocity
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Chance 30% memberikan debuff Poisoned selama 3 detik (180 ticks)
            if (Main.rand.NextFloat() < 0.30f)
            {
                target.AddBuff(BuffID.Poisoned, 180);
            }
        }
    }
}