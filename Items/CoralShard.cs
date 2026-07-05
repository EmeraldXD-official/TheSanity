using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity.Items
{
    public class CoralShard : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.aiStyle = 1; // Standar peluru
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
        }
        public override void AI()
{
    // Membuat jejak partikel air/gelembung saat peluru terbang
    if (Main.rand.NextBool(3))
    {
        Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0f, 0f, 150, default, 0.8f);
    }
}

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Efek Bleeding (Perdarahan)
            target.AddBuff(BuffID.Bleeding, 180); // Bleeding selama 3 detik

            // Efek partikel karang hancur
            for (int i = 0; i < 5; i++)
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Sand, 0f, 0f, 100, default, 1f);
            }
            
        }
    }
}