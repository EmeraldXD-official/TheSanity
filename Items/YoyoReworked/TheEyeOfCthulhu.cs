using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class TheEyeOfCthulhuGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        public int hitCount = 0;

        public override void PostAI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.TheEyeOfCthulhu && projectile.owner == Main.myPlayer)
            {
                if (projectile.localAI[1] == 0)
                {
                    projectile.localAI[1] = 1;

                    Projectile.NewProjectile(
                        projectile.GetSource_FromAI(),
                        projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<EyeOfCthulhuRing>(),
                        projectile.damage,
                        projectile.knockBack * 0.5f,
                        projectile.owner,
                        ai0: projectile.whoAmI
                    );
                }
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type == ProjectileID.TheEyeOfCthulhu)
            {
                // INFLICT BLEEDING (5 Detik)
                target.AddBuff(BuffID.Bleeding, 300);

                // RESET LIFETIME EYE PHASE 2 KE 5 DETIK
                RefreshEyeLifetime(projectile.owner);

                AddHitStackAndCheck(Main.player[projectile.owner], projectile, target);
            }
        }

        // Fungsi Helper untuk mereset lifetime Eye Phase 2 kembali ke 5 detik (300 ticks)
        public static void RefreshEyeLifetime(int owner)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == owner && p.type == ModContent.ProjectileType<EyeOfCthulhuPhase2>())
                {
                    p.timeLeft = 300; // Reset ke 5 detik
                }
            }
        }

        public static void AddHitStackAndCheck(Player player, Projectile yoyoProj, NPC target)
        {
            if (yoyoProj.GetGlobalProjectile<TheEyeOfCthulhuGlobalProjectile>() is TheEyeOfCthulhuGlobalProjectile globalYoyo)
            {
                globalYoyo.hitCount++;

                if (globalYoyo.hitCount >= 5)
                {
                    globalYoyo.hitCount = 0;

                    if (player.whoAmI == Main.myPlayer && !HasActiveEye(player.whoAmI))
                    {
                        Projectile.NewProjectile(
                            yoyoProj.GetSource_OnHit(target),
                            yoyoProj.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<EyeOfCthulhuPhase2>(),
                            (int)(yoyoProj.damage * 1.25f),
                            yoyoProj.knockBack,
                            player.whoAmI,
                            ai0: target.whoAmI
                        );
                    }
                }
            }
        }

        private static bool HasActiveEye(int owner)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == owner && p.type == ModContent.ProjectileType<EyeOfCthulhuPhase2>())
                {
                    return true;
                }
            }
            return false;
        }
    }
}