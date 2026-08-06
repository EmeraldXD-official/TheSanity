using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Players;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class FormatCGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private int attackTimer = 0;

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type == ProjectileID.FormatC)
            {
                Player ownerPlayer = Main.player[projectile.owner];
                var modPlayer = ownerPlayer.GetModPlayer<FormatCPlayer>();

                // Tambah stack jika belum dalam mode Unleash
                if (!modPlayer.isUnleashing)
                {
                    modPlayer.auraStack++;

                    // MENTOK 20 STACK -> Pemicu mode Unleash
                    if (modPlayer.auraStack >= 20)
                    {
                        modPlayer.auraStack = 20;
                        modPlayer.isUnleashing = true;
                    }
                }
            }
        }

        public override void AI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.FormatC)
            {
                Player ownerPlayer = Main.player[projectile.owner];
                var modPlayer = ownerPlayer.GetModPlayer<FormatCPlayer>();

                float auraRadius = 160f; // Radius 10 Block (160 Pixel)

                // 1. VISUAL AURA DAGING DI SEKELILING YOYO
                if (modPlayer.auraStack > 0)
                {
                    int dustCount = modPlayer.auraStack * 2;
                    for (int i = 0; i < dustCount; i++)
                    {
                        float angle = (float)Main.time * 0.05f + (i * MathHelper.TwoPi / dustCount);
                        Vector2 auraPos = projectile.Center + angle.ToRotationVector2() * auraRadius;

                        Dust d = Dust.NewDustPerfect(auraPos, DustID.GemRuby, Vector2.Zero, 100, default, 1.2f);
                        d.noGravity = true;
                    }
                }

                // 2. TEMBAKAN PROYEKTIIL DARI BORDER RANDOM MENUJU PUSAT MUSUH
                if (modPlayer.isUnleashing && modPlayer.auraStack > 0)
                {
                    attackTimer++;
                    if (attackTimer >= 5) // Tembakan cepat setiap 5 tick (~0.08 detik)
                    {
                        attackTimer = 0;

                        // Cari musuh di dalam radius aura 10 block
                        NPC target = FindEnemyInAura(projectile.Center, auraRadius);

                        if (target != null && projectile.owner == Main.myPlayer)
                        {
                            modPlayer.auraStack--; // Kurangi 1 stack per tembakan

                            int auraDamage = (int)(projectile.damage * 0.50f); // 50% damage Yoyo

                            // 1. POSISI SPAWN: Random 360 Derajat di sepanjang pinggiran/border Ring Aura
                            float randomAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                            Vector2 spawnPos = projectile.Center + randomAngle.ToRotationVector2() * auraRadius;

                            // 2. VELOCITY: Mengarah MASUK dari border tempat spawn menuju ke PUSAT MUSUH
                            Vector2 launchVelocity = (target.Center - spawnPos).SafeNormalize(Vector2.UnitY) * 20f; // Kencang (speed 20)

                            // Spawn Proyektil Daging
                            Projectile.NewProjectile(
                                projectile.GetSource_FromAI(),
                                spawnPos,
                                launchVelocity,
                                ModContent.ProjectileType<FormatCFlesh>(),
                                auraDamage,
                                projectile.knockBack * 0.3f,
                                projectile.owner
                            );

                            // Jika stack habis, matikan mode unleash
                            if (modPlayer.auraStack <= 0)
                            {
                                modPlayer.isUnleashing = false;
                            }
                        }
                    }
                }
            }
        }

        private NPC FindEnemyInAura(Vector2 center, float radius)
        {
            NPC nearest = null;
            float minDistance = radius;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy())
                {
                    float dist = Vector2.Distance(center, npc.Center);
                    if (dist <= minDistance)
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