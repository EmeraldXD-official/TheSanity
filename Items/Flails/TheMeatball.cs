using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class GlobalTheMeatball : GlobalProjectile
    {
        // Menjaga agar AI Vanilla Flail bergerak normal tanpa terganggu
        public override bool InstancePerEntity => true;

        public int spinTimer = 0;
        public int hitCooldown = 0;

        public override void AI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.TheMeatball)
            {
                // Cooldown On-Hit 1 Detik
                if (hitCooldown > 0)
                {
                    hitCooldown--;
                }

                Player owner = Main.player[projectile.owner];

                if (owner.active && !owner.dead && owner.channel)
                {
                    spinTimer++;

                    // Setiap 0,3 detik (18 tick)
                    if (spinTimer >= 18)
                    {
                        spinTimer = 0;

                        if (Main.myPlayer == projectile.owner)
                        {
                            int miniDamage = (int)(projectile.damage * 0.50f);
                            if (miniDamage < 1) miniDamage = 1;

                            // Cari musuh terdekat
                            NPC target = null;
                            float maxDistance = 500f;
                            float closestDistance = maxDistance;

                            for (int i = 0; i < Main.maxNPCs; i++)
                            {
                                NPC npc = Main.npc[i];
                                if (npc.CanBeChasedBy())
                                {
                                    float dist = Vector2.Distance(projectile.Center, npc.Center);
                                    if (dist < closestDistance)
                                    {
                                        closestDistance = dist;
                                        target = npc;
                                    }
                                }
                            }

                            Vector2 velocity;
                            float launchSpeed = 10.5f;
                            float gravity = 0.35f; // Samakan dengan gravitasi MiniMeatball

                            if (target != null)
                            {
                                // KONTROL BIDIK BALISTIK (Melontar agak ke atas menghitung gravitasi)
                                velocity = GetBallisticVelocity(projectile.Center, target.Center, launchSpeed, gravity);
                            }
                            else
                            {
                                // Jika tidak ada musuh, lontar ke kursor mouse
                                velocity = Vector2.Normalize(Main.MouseWorld - projectile.Center) * launchSpeed;
                            }

                            Projectile.NewProjectile(
                                projectile.GetSource_FromAI(),
                                projectile.Center,
                                velocity,
                                ModContent.ProjectileType<MiniMeatball>(),
                                miniDamage,
                                projectile.knockBack * 0.3f,
                                projectile.owner
                            );
                        }
                    }
                }
                else
                {
                    spinTimer = 0;
                }
            }
        }

        // Kalkulasi Fisika Parabola / Arc Aiming
        private Vector2 GetBallisticVelocity(Vector2 start, Vector2 target, float speed, float gravity)
        {
            Vector2 diff = target - start;
            float dx = diff.X;
            float dy = diff.Y; // Y positif ke arah bawah screen

            float absDx = Math.Abs(dx);
            if (absDx < 0.001f) absDx = 0.001f;

            float v2 = speed * speed;
            float v4 = v2 * v2;

            // Diskriminan rumus lintasan parabola dengan gravitasi
            float inside = v4 - gravity * (gravity * dx * dx - 2f * dy * v2);

            if (inside >= 0)
            {
                float root = (float)Math.Sqrt(inside);
                float signDx = Math.Sign(dx) == 0 ? 1f : Math.Sign(dx);

                // Menghitung sudut tan(theta) untuk lintasan parabola
                float tanTheta = -signDx * (v2 - root) / (gravity * absDx);

                // Konversi kembali ke komponen vektor V_x dan V_y
                float cosTheta = 1f / (float)Math.Sqrt(1f + tanTheta * tanTheta);
                float vx = signDx * speed * cosTheta;
                float vy = vx * tanTheta;

                return new Vector2(vx, vy);
            }

            // Fallback jika jarak musuh terlalu jauh di luar jangkauan tembak
            float t = diff.Length() / speed;
            Vector2 aimPoint = target - new Vector2(0f, 0.5f * gravity * t * t);
            return Vector2.Normalize(aimPoint - start) * speed;
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type == ProjectileID.TheMeatball)
            {
                // Memastikan Cooldown 1 Detik (60 tick) berjalan
                if (hitCooldown <= 0)
                {
                    hitCooldown = 60; // Set cooldown 1 detik

                    if (Main.myPlayer == projectile.owner)
                    {
                        // 1. Spawn 1 Healing Orb
                        Projectile.NewProjectile(
                            projectile.GetSource_OnHit(target),
                            projectile.Center,
                            Main.rand.NextVector2Circular(3f, 3f),
                            ModContent.ProjectileType<MeatballHealingOrb>(),
                            0,
                            0f,
                            projectile.owner
                        );

                        // 2. Spawn 4 Mini Meatball sekaligus
                        int miniDamage = (int)(projectile.damage * 0.50f);
                        if (miniDamage < 1) miniDamage = 1;

                        for (int i = 0; i < 4; i++)
                        {
                            Vector2 burstVelocity = new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-8f, -3f));

                            Projectile.NewProjectile(
                                projectile.GetSource_OnHit(target),
                                projectile.Center,
                                burstVelocity,
                                ModContent.ProjectileType<MiniMeatball>(),
                                miniDamage,
                                projectile.knockBack * 0.3f,
                                projectile.owner
                            );
                        }
                    }
                }
            }
        }
    }
}