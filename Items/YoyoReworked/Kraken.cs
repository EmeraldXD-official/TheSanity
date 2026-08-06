using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class KrakenGlobalProjectile : GlobalProjectile
    {
        public override void PostAI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.Kraken && projectile.owner == Main.myPlayer)
            {
                if (projectile.localAI[1] == 0)
                {
                    projectile.localAI[1] = 1;

                    Projectile.NewProjectile(
                        projectile.GetSource_FromAI(),
                        projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<KrakenTyphoonAura>(),
                        (int)(projectile.damage * 0.87f),
                        projectile.knockBack * 0.3f,
                        projectile.owner,
                        ai0: projectile.whoAmI
                    );
                }
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type == ProjectileID.Kraken)
            {
                target.AddBuff(BuffID.Wet, 300);
                target.AddBuff(BuffID.Electrified, 300);

                // 20% Chance & Maksimal 1 Tornado per Player
                if (Main.rand.NextBool(5) && projectile.owner == Main.myPlayer && !HasActiveTornado(projectile.owner))
                {
                    Vector2 spawnPos = target.Center;

                    // Spawn 20 Tumpukan Tornado (Index 0 s/d 19)
                    for (int i = 0; i < 20; i++)
                    {
                        Projectile.NewProjectile(
                            projectile.GetSource_OnHit(target),
                            spawnPos,
                            Vector2.Zero,
                            ModContent.ProjectileType<KrakenCthulunado>(),
                            (int)(projectile.damage * 0.50f), // 50% Damage Yoyo Utama
                            projectile.knockBack * 0.2f,
                            projectile.owner,
                            ai0: spawnPos.X,
                            ai1: spawnPos.Y + 20f,
                            ai2: i // Stack Index
                        );
                    }
                }
            }
        }

        private bool HasActiveTornado(int owner)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == owner && p.type == ModContent.ProjectileType<KrakenCthulunado>())
                {
                    return true;
                }
            }
            return false;
        }
    }
}