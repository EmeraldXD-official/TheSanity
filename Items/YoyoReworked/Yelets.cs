using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class YeletsGlobalProjectile : GlobalProjectile
    {
        public override void PostAI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.Yelets && projectile.owner == Main.myPlayer)
            {
                if (projectile.localAI[1] == 0)
                {
                    projectile.localAI[1] = 1; // Biar cuma spawn 1 set saat dilempar

                    // 1. Spawn Center Aura (FlowerPow)
                    Projectile.NewProjectile(
                        projectile.GetSource_FromAI(),
                        projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<YeletsCenterAura>(),
                        0,
                        0f,
                        projectile.owner,
                        ai0: projectile.whoAmI
                    );

                    // 2. Spawn 4 Orbiting Leaves (CrystalLeaf Costume)
                    for (int i = 0; i < 4; i++)
                    {
                        Projectile.NewProjectile(
                            projectile.GetSource_FromAI(),
                            projectile.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<YeletsLeaf>(),
                            projectile.damage, // 100% Damage dari Yelets
                            projectile.knockBack * 0.3f,
                            projectile.owner,
                            ai0: projectile.whoAmI,
                            ai1: i
                        );
                    }
                }
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Kontak Yoyo Yelets utama -> 100% Chance Venom
            if (projectile.type == ProjectileID.Yelets)
            {
                target.AddBuff(BuffID.Venom, 300);
            }
        }
    }
}