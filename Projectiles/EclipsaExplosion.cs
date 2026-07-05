using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class EclipsaExplosion : ModProjectile
    {
        private int maxLife = 20; // Durasi ledakan

        public override void SetDefaults()
        {
            Projectile.width = 64; 
            Projectile.height = 64;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee; // Agar damage terhitung Melee
            Projectile.penetrate = -1; // -1 membuat proyektil menembus banyak musuh (AOE)
            Projectile.timeLeft = maxLife; 
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            float lifePercent = (float)Projectile.timeLeft / (float)maxLife;

            // 1. Efek Membesar (Scale)
            // Dimulai dari skala kecil (0.5f) membesar ke (1.5f)
            Projectile.scale = 0.5f + (1.0f - lifePercent);
            
            // 2. Efek Fade Out (Transparansi)
            // Mengubah alpha agar perlahan menghilang
            Projectile.alpha = (int)(255 * (1.0f - lifePercent));

            // 3. Putaran ledakan
            Projectile.rotation += 0.2f;
        }
    }
}