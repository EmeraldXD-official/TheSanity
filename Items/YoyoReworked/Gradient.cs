using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Players;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class GradientGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private bool hasSpawnedBigFlower = false;

        public override void AI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.Gradient)
            {
                // Spawn SunnyFlowerBig tepat di pusat Yoyo
                if (!hasSpawnedBigFlower && projectile.owner == Main.myPlayer)
                {
                    hasSpawnedBigFlower = true;

                    Projectile.NewProjectile(
                        projectile.GetSource_FromAI(),
                        projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<SunnyFlowerBig>(),
                        (int)(projectile.damage * 0.8f),
                        projectile.knockBack * 0.3f,
                        projectile.owner,
                        ai0: projectile.whoAmI
                    );
                }
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // PUKULAN YOYO GRADIENT UTAMA JUGA MENAMBAH STACK
            if (projectile.type == ProjectileID.Gradient)
            {
                Player ownerPlayer = Main.player[projectile.owner];
                var modPlayer = ownerPlayer.GetModPlayer<GradientPlayer>();

                AddStackAndCheckLaunch(ownerPlayer, modPlayer, projectile, target);
            }
        }

        // Helper untuk memproses Stack (4 Hits) & Melepas 4 Bunga Homing
        public static void AddStackAndCheckLaunch(Player player, GradientPlayer modPlayer, Projectile sourceProj, NPC target)
        {
            modPlayer.flowerStack++;

            // MENTOK 4 STACK -> Peluncuran 4 Bunga Homing!
            if (modPlayer.flowerStack >= 4)
            {
                modPlayer.flowerStack = 0; // Reset Stack

                if (sourceProj.owner == Main.myPlayer)
                {
                    SoundEngine.PlaySound(SoundID.Item9, sourceProj.Center);

                    int flowerDamage = (int)(sourceProj.damage * 0.50f); // 50% Damage Yoyo
                    float orbitRadius = 60f;
                    float currentOrbitAngle = (float)Main.timeForVisualEffects * 0.06f;

                    // Meluncurkan 4 Bunga Matahari Kecil tepat dari posisi orbit visual
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = currentOrbitAngle + (i * MathHelper.PiOver2);
                        Vector2 spawnPos = sourceProj.Center + (angle.ToRotationVector2() * orbitRadius);
                        Vector2 launchVel = angle.ToRotationVector2() * 12f;

                        Projectile.NewProjectile(
                            sourceProj.GetSource_OnHit(target),
                            spawnPos,
                            launchVel,
                            ModContent.ProjectileType<SunnyFlowerSmall>(),
                            flowerDamage,
                            sourceProj.knockBack * 0.5f,
                            sourceProj.owner
                        );
                    }
                }
            }
        }
    }
}