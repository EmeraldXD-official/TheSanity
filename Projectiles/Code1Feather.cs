using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class Code1Feather : ModProjectile
    {
        // Menggunakan sprite Harpy Feather vanilla (ID: 38)
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.HarpyFeather;

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            
            // Set damage ke Melee
            Projectile.DamageType = DamageClass.Melee; 
            
            // Penetrate 1 (hancur setelah mengenai 1 musuh)
            Projectile.penetrate = 1; 
            
            Projectile.tileCollide = true; // Hancur jika menabrak blok
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300; // Hilang otomatis dalam 5 detik
        }

        public override void AI()
        {
            // PERBAIKAN ROTASI:
            // Menggunakan PiOver2 (90 derajat) agar moncong bulu sejajar lurus dengan arah gerak velocity
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // LOGIKA HOMING (Mengejar musuh terdekat)
            NPC target = FindNearestNPC(400f);
            if (target != null)
            {
                Vector2 targetDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                // Menyesuaikan arah secara bertahap (homing)
                Projectile.velocity = Vector2.Normalize(Vector2.Lerp(Projectile.velocity, targetDirection * 12f, 0.08f)) * 12f;
            }
        }

        private NPC FindNearestNPC(float maxDistance)
        {
            NPC nearest = null;
            float minDistance = maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy())
                {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearest = npc;
                    }
                }
            }

            return nearest;
        }
    }
}