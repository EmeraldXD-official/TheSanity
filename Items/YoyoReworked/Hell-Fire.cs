using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class HelFireGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private bool hasSpawnedRing = false;

        public override void AI(Projectile projectile)
        {
            // Khusus Yoyo Hel-Fire (ProjectileID.HelFire)
            if (projectile.type == ProjectileID.HelFire)
            {
                // Spawn Fire Ring 1x begitu Yoyo melayang
                if (!hasSpawnedRing && projectile.owner == Main.myPlayer)
                {
                    hasSpawnedRing = true;

                    int ringDamage = (int)(projectile.damage * 0.80f); // 80% damage dari Hel-Fire

                    // Spawn HelFireRing dan teruskan ID yoyo melalui ai0
                    Projectile.NewProjectile(
                        projectile.GetSource_FromAI(),
                        projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<HelFireRing>(),
                        ringDamage,
                        projectile.knockBack * 0.2f,
                        projectile.owner,
                        ai0: projectile.whoAmI
                    );
                }
            }
        }
    }
}