using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class ChikGlobalProjectile : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Khusus Yoyo Chik (ProjectileID.Chik)
            if (projectile.type == ProjectileID.Chik)
            {
                if (projectile.owner == Main.myPlayer)
                {
                    int pulseCount = Main.rand.Next(4, 8); // Acak 4-7 proyektil
                    int pulseDamage = (int)(projectile.damage * 0.50f); // 50% damage Chik

                    for (int i = 0; i < pulseCount; i++)
                    {
                        // Velocity meluncur ke segala arah 360 derajat dengan kecepatan acak (7f - 13f)
                        float speed = Main.rand.NextFloat(7f, 13f);
                        Vector2 launchVelocity = Main.rand.NextVector2CircularEdge(speed, speed);

                        Projectile.NewProjectile(
                            projectile.GetSource_OnHit(target),
                            projectile.Center, // Sumber muncratan langsung dari posisi Yoyo Chik
                            launchVelocity,
                            ModContent.ProjectileType<ChikPulse>(),
                            pulseDamage,
                            projectile.knockBack * 0.4f,
                            projectile.owner
                        );
                    }
                }
            }
        }
    }
}