using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class CorruptYoyoGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private int eaterTimer = 0;

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Main Yoyo (CorruptYoyo / Malaise) -> 20% chance Cursed Inferno
            if (projectile.type == ProjectileID.CorruptYoyo)
            {
                if (Main.rand.NextFloat() < 0.20f)
                {
                    target.AddBuff(BuffID.CursedInferno, 180); // 3 detik
                }
            }
        }

        public override void AI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.CorruptYoyo)
            {
                eaterTimer++;

                // 1,7 detik = 102 ticks (1.7 x 60)
                if (eaterTimer >= 102)
                {
                    eaterTimer = 0; // Reset timer

                    if (projectile.owner == Main.myPlayer)
                    {
                        int eaterDamage = (int)(projectile.damage * 0.35f); // 35% damage Malaise

                        // Spawn 4 MalaiseEater
                        for (int i = 0; i < 4; i++)
                        {
                            // Memberikan arah awal acak (velocity) agar menyebar sebelum AI homing TinyEater aktif
                            Vector2 launchVelocity = Main.rand.NextVector2Circular(6f, 6f);

                            Projectile.NewProjectile(
                                projectile.GetSource_FromAI(),
                                projectile.Center,
                                launchVelocity,
                                ModContent.ProjectileType<MalaiseEater>(),
                                eaterDamage,
                                projectile.knockBack * 0.35f,
                                projectile.owner
                            );
                        }
                    }
                }
            }
        }
    }
}