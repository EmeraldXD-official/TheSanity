using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class Code2GlobalProjectile : GlobalProjectile
    {
        public override void PostAI(Projectile projectile)
        {
            // Panggil aura visual tepat saat Yoyo Code 2 aktif di dunia
            if (projectile.type == ProjectileID.Code2 && projectile.owner == Main.myPlayer)
            {
                if (projectile.localAI[1] == 0)
                {
                    projectile.localAI[1] = 1; // Mencegah spawn berulang kali

                    Projectile.NewProjectile(
                        projectile.GetSource_FromAI(),
                        projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<Code2VisualAura>(),
                        0, // Damage 0 (Pure Visual)
                        0f,
                        projectile.owner,
                        ai0: projectile.whoAmI
                    );
                }
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Pemicu Perfect Loop Code2Doom saat hit musuh
            if (projectile.type == ProjectileID.Code2 && projectile.owner == Main.myPlayer)
            {
                if (!HasActiveDoomProjectiles(projectile.owner))
                {
                    int flameDamage = (int)(projectile.damage * 0.50f);

                    for (int i = 0; i < 4; i++)
                    {
                        Projectile.NewProjectile(
                            projectile.GetSource_OnHit(target),
                            projectile.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<Code2Doom>(),
                            flameDamage,
                            projectile.knockBack * 0.3f,
                            projectile.owner,
                            ai0: projectile.whoAmI,
                            ai1: i
                        );
                    }
                }
            }
        }

        private bool HasActiveDoomProjectiles(int owner)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == owner && p.type == ModContent.ProjectileType<Code2Doom>())
                {
                    return true;
                }
            }
            return false;
        }
    }
}