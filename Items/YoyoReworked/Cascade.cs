using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class CascadeGlobalProjectile : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Khusus Yoyo Cascade (ProjectileID.Cascade)
            if (projectile.type == ProjectileID.Cascade)
            {
                // Chance 20% memicu ledakan di lokasi Yoyo Cascade
                if (Main.rand.NextFloat() < 0.20f && projectile.owner == Main.myPlayer)
                {
                    int blastDamage = (int)(projectile.damage * 0.35f); // 35% damage dari Cascade

                    // Spawn projectile blast tepat di posisi Yoyo (projectile.Center) dan diam di tempat (Vector2.Zero)
                    Projectile.NewProjectile(
                        projectile.GetSource_OnHit(target),
                        projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<CascadeBlast>(),
                        blastDamage,
                        projectile.knockBack * 0.2f,
                        projectile.owner
                    );
                }
            }
        }
    }
}