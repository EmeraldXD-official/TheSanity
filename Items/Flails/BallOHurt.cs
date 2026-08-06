using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class GlobalBallOHurt : GlobalProjectile
    {
        public override void AI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.BallOHurt)
            {
                // Hitung mundur timer cooldown (60 tick = 1 detik)
                if (projectile.localAI[0] > 0)
                {
                    projectile.localAI[0]--;
                }
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type == ProjectileID.BallOHurt)
            {
                // Hanya memicu jika cooldown 1 detik (localAI[0] <= 0) sudah habis
                if (projectile.localAI[0] <= 0)
                {
                    projectile.localAI[0] = 60; // Lock selama 60 tick (1 detik)

                    if (Main.myPlayer == projectile.owner)
                    {
                        int totalDirections = 8;
                        
                        int vileDamage = (int)(projectile.damage * 0.5f);
                        if (vileDamage < 1) vileDamage = 1;

                        for (int i = 0; i < totalDirections; i++)
                        {
                            float angle = MathHelper.TwoPi / totalDirections * i;
                            Vector2 direction = angle.ToRotationVector2();

                            Projectile.NewProjectile(
                                projectile.GetSource_OnHit(target),
                                projectile.Center,
                                direction * 0.001f,
                                ModContent.ProjectileType<BallOHurtVileShaft>(),
                                vileDamage,
                                projectile.knockBack * 0.5f,
                                projectile.owner,
                                1f,  // ai[0] = Segmen ke-1 (Mulai)
                                10f  // ai[1] = Total 10 segmen badan (Segmen ke-11 otomatis jadi Tip/Pucuk)
                            );
                        }
                    }
                }
            }
        }
    }
}