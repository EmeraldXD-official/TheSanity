using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class AmazonGlobalProjectile : GlobalProjectile
    {
        // Supaya variabel stingerTimer independen untuk setiap Yoyo yang ada di dunia
        public override bool InstancePerEntity => true;

        private int stingerTimer = 0;

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Main Yoyo (JungleYoyo) -> 40% chance memberikan Poisoned selama 5 detik
            if (projectile.type == ProjectileID.JungleYoyo)
            {
                if (Main.rand.NextFloat() < 0.40f)
                {
                    target.AddBuff(BuffID.Poisoned, 300);
                }
            }
        }

        public override void AI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.JungleYoyo)
            {
                // Timer berjalan setiap frame (60 FPS)
                stingerTimer++;

                // 1,3 detik = 78 ticks (1.3 x 60)
                if (stingerTimer >= 78)
                {
                    stingerTimer = 0; // Reset timer

                    if (projectile.owner == Main.myPlayer)
                    {
                        float stingerSpeed = 12f;
                        int stingerDamage = (int)(projectile.damage * 0.5f); // 50% damage Amazon

                        // CHANCE 5%: Mengeluarkan 8 Stinger ke segala arah (360 derajat)
                        if (Main.rand.NextFloat() < 0.05f)
                        {
                            SoundEngine.PlaySound(SoundID.Item17, projectile.Center);

                            for (int i = 0; i < 8; i++)
                            {
                                float angle = i * MathHelper.PiOver4; // Pembagian 8 arah (setiap 45 derajat)
                                Vector2 velocity = angle.ToRotationVector2() * stingerSpeed;

                                Projectile.NewProjectile(
                                    projectile.GetSource_FromAI(),
                                    projectile.Center,
                                    velocity,
                                    ModContent.ProjectileType<AmazonStinger>(),
                                    stingerDamage,
                                    projectile.knockBack * 0.5f,
                                    projectile.owner
                                );
                            }
                        }
                        // CHANCE 95%: Mengeluarkan 1 Stinger (Auto-target musuh / Random)
                        else
                        {
                            SoundEngine.PlaySound(SoundID.Item17, projectile.Center);

                            Vector2 velocity;
                            NPC target = FindNearestNPC(projectile.Center, 700f); // Cari musuh terdekat dalam jarak 700 pixel

                            if (target != null)
                            {
                                // Arahkan lurus ke musuh
                                velocity = (target.Center - projectile.Center).SafeNormalize(Vector2.UnitX) * stingerSpeed;
                            }
                            else
                            {
                                // Jika tidak ada musuh, tembak ke arah random
                                velocity = Main.rand.NextVector2Unit() * stingerSpeed;
                            }

                            Projectile.NewProjectile(
                                projectile.GetSource_FromAI(),
                                projectile.Center,
                                velocity,
                                ModContent.ProjectileType<AmazonStinger>(),
                                stingerDamage,
                                projectile.knockBack * 0.5f,
                                projectile.owner
                            );
                        }
                    }
                }
            }
        }

        // Helper function untuk mencari musuh terdekat yang bisa diserang
        private NPC FindNearestNPC(Vector2 center, float maxDistance)
        {
            NPC nearest = null;
            float minDistance = maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy())
                {
                    float dist = Vector2.Distance(center, npc.Center);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearest = npc;
                    }
                }
            }

            return nearest;
        }
    }
}